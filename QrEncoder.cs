using System;
using System.Collections.Generic;
using System.Text;

namespace Toolbox
{
    // 二维码纠错等级（顺序与数据表索引一致：0=L,1=M,2=Q,3=H）
    public enum QrEcc
    {
        Low = 0,
        Medium = 1,
        Quartile = 2,
        High = 3
    }

    /// <summary>
    /// 自包含二维码编码器（字节模式，版本 1-10，纠错等级 L/M/Q/H）。
    /// 无第三方依赖：GF(256) 里德-所罗门、格式/版本信息 BCH、掩码与罚分均按 QR 标准实现。
    /// </summary>
    public static class QrEncoder
    {
        // 各版本、各纠错等级的「每块纠错码字数」
        // 索引：[version-1][eccIndex]，eccIndex: 0=L,1=M,2=Q,3=H
        private static readonly int[][] EccPerBlock = new int[10][]
        {
            new int[] { 7, 10, 13, 17 },     // v1
            new int[] { 10, 16, 22, 28 },    // v2
            new int[] { 15, 26, 18, 22 },    // v3
            new int[] { 20, 18, 26, 16 },    // v4
            new int[] { 26, 24, 18, 22 },    // v5
            new int[] { 18, 16, 24, 28 },    // v6
            new int[] { 20, 18, 18, 26 },    // v7
            new int[] { 24, 22, 22, 26 },    // v8
            new int[] { 30, 22, 20, 24 },    // v9
            new int[] { 18, 26, 24, 28 },    // v10
        };

        // 各版本、各纠错等级的「每块数据码字数」（分块结构，用于 RS 与交织）
        private static readonly int[][][] DataBlocks = new int[10][][]
        {
            new int[][] { new[] { 19 },              new[] { 16 },              new[] { 13 },              new[] { 9 } },
            new int[][] { new[] { 34 },              new[] { 28 },              new[] { 22 },              new[] { 16 } },
            new int[][] { new[] { 55 },              new[] { 44 },              new[] { 17, 17 },          new[] { 13, 13 } },
            new int[][] { new[] { 80 },              new[] { 32, 32 },          new[] { 24, 24 },          new[] { 9, 9, 9, 9 } },
            new int[][] { new[] { 108 },             new[] { 43, 43 },          new[] { 15, 15, 16, 16 },  new[] { 11, 11, 12, 12 } },
            new int[][] { new[] { 68, 68 },          new[] { 27, 27, 27, 27 },  new[] { 19, 19, 19, 19 },  new[] { 15, 15, 15, 15 } },
            new int[][] { new[] { 78, 78 },          new[] { 31, 31, 31, 31 },  new[] { 14, 14, 15, 15, 15, 15 }, new[] { 13, 13, 13, 13, 14 } },
            new int[][] { new[] { 97, 97 },          new[] { 38, 38, 39, 39 },  new[] { 18, 18, 18, 18, 19, 19 }, new[] { 14, 14, 14, 14, 15, 15 } },
            new int[][] { new[] { 116, 116 },        new[] { 36, 36, 36, 37, 37 }, new[] { 16, 16, 16, 16, 17, 17, 17, 17 }, new[] { 12, 12, 12, 12, 13, 13, 13, 13 } },
            new int[][] { new[] { 68, 68, 69, 69 },  new[] { 43, 43, 43, 43, 44 }, new[] { 19, 19, 19, 19, 19, 19, 20, 20 }, new[] { 15, 15, 15, 15, 15, 15, 16, 16 } },
        };

        // 格式信息中纠错等级的 2 位指示：M=00, L=01, H=10, Q=11
        private static readonly int[] FormatBits = { 1, 0, 3, 2 };

        private const int MaxVersion = 10;

        // ---- GF(256) 表 ----
        private static readonly byte[] GfExp = new byte[256];
        private static readonly byte[] GfLog = new byte[256];

        static QrEncoder()
        {
            int x = 1;
            for (int i = 0; i < 255; i++)
            {
                GfExp[i] = (byte)x;
                GfLog[x] = (byte)i;
                x <<= 1;
                if (x >= 256) x ^= 0x11D;
            }
            GfExp[255] = GfExp[0];
        }

        private static byte GfMul(byte a, byte b)
        {
            if (a == 0 || b == 0) return 0;
            return GfExp[(GfLog[a] + GfLog[b]) % 255];
        }

        // ---- 里德-所罗门 ----
        private static byte[] RsDivisor(int degree)
        {
            byte[] result = new byte[degree];
            result[degree - 1] = 1;
            int root = 1;
            for (int i = 0; i < degree; i++)
            {
                for (int j = 0; j < degree; j++)
                {
                    result[j] = (byte)((result[j] == 0) ? 0 : GfMul(result[j], (byte)root));
                    if (j + 1 < degree) result[j] ^= result[j + 1];
                }
                root = GfMul((byte)root, 2);
            }
            return result;
        }

        private static byte[] RsRemainder(byte[] data, byte[] divisor)
        {
            byte[] result = new byte[divisor.Length];
            foreach (byte b in data)
            {
                byte factor = (byte)(b ^ result[0]);
                Array.Copy(result, 1, result, 0, result.Length - 1);
                result[result.Length - 1] = 0;
                for (int i = 0; i < result.Length; i++)
                    result[i] ^= GfMul(divisor[i], factor);
            }
            return result;
        }

        // ---- 比特缓冲 ----
        private sealed class BitBuffer
        {
            private readonly List<byte> _bytes = new List<byte>();
            private int _bitLen;

            public int Length { get { return _bitLen; } }

            public void AppendBits(int val, int len)
            {
                for (int i = len - 1; i >= 0; i--)
                    AppendBit(((val >> i) & 1) != 0);
            }

            public void AppendBit(bool b)
            {
                int idx = _bitLen >> 3;
                if (idx >= _bytes.Count) _bytes.Add(0);
                if (b) _bytes[idx] |= (byte)(1 << (7 - (_bitLen & 7)));
                _bitLen++;
            }

            public byte[] GetBytes() { return _bytes.ToArray(); }
        }

        private static int SumData(int ver, int eccIndex)
        {
            int s = 0;
            foreach (int d in DataBlocks[ver - 1][eccIndex]) s += d;
            return s;
        }

        private static byte[] EncodeMessage(string text, int ver, int eccIndex)
        {
            byte[] dataBytes = Encoding.UTF8.GetBytes(text);
            int ccBits = (ver <= 9) ? 8 : 16;
            int capacity = SumData(ver, eccIndex) * 8;
            BitBuffer bb = new BitBuffer();
            bb.AppendBits(0x4, 4);                  // 字节模式指示
            bb.AppendBits(dataBytes.Length, ccBits); // 字符计数
            foreach (byte b in dataBytes) bb.AppendBits(b, 8);
            // 终止符（最多 4 个 0）
            int term = Math.Min(4, capacity - bb.Length);
            bb.AppendBits(0, term);
            // 位填充至字节边界
            bb.AppendBits(0, (8 - bb.Length % 8) % 8);
            // 字节填充
            byte[] pad = { 0xEC, 0x11 };
            int p = 0;
            while (bb.Length < capacity)
            {
                bb.AppendBits(pad[p], 8);
                p ^= 1;
            }
            return bb.GetBytes();
        }

        private static byte[] AddEccAndInterleave(byte[] msg, int ver, int eccIndex)
        {
            int[] blocks = DataBlocks[ver - 1][eccIndex];
            int ecPerBlock = EccPerBlock[ver - 1][eccIndex];
            byte[] divisor = RsDivisor(ecPerBlock);
            byte[][] enc = new byte[blocks.Length][];
            int k = 0;
            for (int i = 0; i < blocks.Length; i++)
            {
                int dl = blocks[i];
                byte[] dat = new byte[dl];
                Array.Copy(msg, k, dat, 0, dl);
                k += dl;
                byte[] ecc = RsRemainder(dat, divisor);
                enc[i] = new byte[dl + ecPerBlock];
                Array.Copy(dat, 0, enc[i], 0, dl);
                Array.Copy(ecc, 0, enc[i], dl, ecPerBlock);
            }
            int total = SumData(ver, eccIndex) + blocks.Length * ecPerBlock;
            byte[] result = new byte[total];
            int pos = 0;
            int maxData = 0;
            foreach (int d in blocks) if (d > maxData) maxData = d;
            for (int j = 0; j < maxData; j++)
                for (int i = 0; i < blocks.Length; i++)
                    if (j < blocks[i]) result[pos++] = enc[i][j];
            for (int j = 0; j < ecPerBlock; j++)
                for (int i = 0; i < blocks.Length; i++)
                    result[pos++] = enc[i][blocks[i] + j];
            return result;
        }

        // ---- 矩阵构建 ----
        private sealed class Builder
        {
            public readonly int Size;
            public readonly int Version;
            public readonly int EccIndex;
            public readonly bool[,] Mod;
            public readonly bool[,] Func;

            public Builder(int version, int eccIndex)
            {
                Version = version;
                EccIndex = eccIndex;
                Size = version * 4 + 17;
                Mod = new bool[Size, Size];
                Func = new bool[Size, Size];
            }

            private void SetFn(int x, int y, bool dark)
            {
                Mod[y, x] = dark;
                Func[y, x] = true;
            }

            public void DrawFunctionPatterns()
            {
                // 先画定时图案，再画定位图案（定位图案会覆盖定时线位于其区域内的部分）
                for (int i = 0; i < Size; i++)
                {
                    SetFn(6, i, i % 2 == 0);
                    SetFn(i, 6, i % 2 == 0);
                }
                DrawFinder(3, 3);
                DrawFinder(Size - 4, 3);
                DrawFinder(3, Size - 4);

                // 校正图案
                List<int> pos = AlignmentPositions(Version);
                int n = pos.Count;
                for (int i = 0; i < n; i++)
                    for (int j = 0; j < n; j++)
                    {
                        int x = pos[i], y = pos[j];
                        if ((x == 6 && y == 6) || (x == Size - 7 && y == 6) || (x == 6 && y == Size - 7))
                            continue;
                        DrawAlignment(x, y);
                    }

                // 固定黑模块
                SetFn(8, Size - 8, true);
            }

            private void DrawFinder(int x, int y)
            {
                for (int dy = -4; dy <= 4; dy++)
                    for (int dx = -4; dx <= 4; dx++)
                    {
                        int dist = Math.Max(Math.Abs(dx), Math.Abs(dy));
                        int xx = x + dx, yy = y + dy;
                        if (xx >= 0 && xx < Size && yy >= 0 && yy < Size)
                            SetFn(xx, yy, dist != 2 && dist != 4);
                    }
            }

            private void DrawAlignment(int x, int y)
            {
                for (int dy = -2; dy <= 2; dy++)
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        int dist = Math.Max(Math.Abs(dx), Math.Abs(dy));
                        SetFn(x + dx, y + dy, dist != 1);
                    }
            }

            public void ReserveFormatAndVersion()
            {
                // 格式信息：列8 / 行8 周围（这些单元稍后由 DrawFormatBits 写入，这里仅标记为功能模块）
                for (int i = 0; i <= 5; i++) SetFn(8, i, false);
                SetFn(8, 7, false);
                SetFn(8, 8, false);
                SetFn(7, 8, false);
                for (int i = 9; i < 15; i++) SetFn(14 - i, 8, false);
                for (int i = 0; i < 8; i++) SetFn(Size - 1 - i, 8, false);
                for (int i = 8; i < 15; i++) SetFn(8, Size - 15 + i, false);
                SetFn(8, Size - 8, false);
                if (Version >= 7)
                {
                    for (int i = 0; i < 18; i++)
                    {
                        int off = i % 3;
                        int r = i / 3;
                        SetFn(Size - 11 + off, r, false);
                        SetFn(r, Size - 11 + off, false);
                    }
                }
            }

            public void DrawVersion()
            {
                if (Version < 7) return;
                int rem = Version;
                for (int i = 0; i < 12; i++) rem = (rem << 1) ^ ((rem >> 11) * 0x1F25);
                int bits = (Version << 12) | rem; // 18 位，MSB 在前
                for (int i = 0; i < 18; i++)
                {
                    bool bit = ((bits >> (17 - i)) & 1) != 0;
                    int off = i % 3;
                    int r = i / 3;
                    SetFn(Size - 11 + off, r, bit);
                    SetFn(r, Size - 11 + off, bit);
                }
            }

            public void DrawFormatBits(int mask)
            {
                int data = (FormatBits[EccIndex] << 3) | mask;
                int rem = data;
                for (int i = 0; i < 10; i++)
                    rem = ((rem << 1) ^ ((rem >> 9) * 0x537)) & 0x3FF;
                int bits = ((data << 10) | rem) ^ 0x5412;

                // 第一份：左上角周围（SetFn 参数为 col,row）
                for (int i = 0; i <= 5; i++) SetFn(8, i, ((bits >> i) & 1) != 0);
                SetFn(8, 7, ((bits >> 6) & 1) != 0);
                SetFn(8, 8, ((bits >> 7) & 1) != 0);
                SetFn(7, 8, ((bits >> 8) & 1) != 0);
                for (int i = 9; i < 15; i++) SetFn(14 - i, 8, ((bits >> i) & 1) != 0);

                // 第二份：右上 + 左下
                for (int i = 0; i < 8; i++) SetFn(Size - 1 - i, 8, ((bits >> i) & 1) != 0);
                for (int i = 8; i < 15; i++) SetFn(8, Size - 15 + i, ((bits >> i) & 1) != 0);

                // 固定黑模块
                SetFn(8, Size - 8, true);
            }

            public void DrawCodewords(byte[] data)
            {
                int i = 0;
                int total = data.Length * 8;
                for (int right = Size - 1; right >= 1; right -= 2)
                {
                    if (right == 6) right = 5;
                    for (int vert = 0; vert < Size; vert++)
                    {
                        for (int j = 0; j < 2; j++)
                        {
                            int x = right - j;
                            bool upward = ((right + 1) & 2) == 0;
                            int y = upward ? Size - 1 - vert : vert;
                            if (!Func[y, x] && i < total)
                            {
                                Mod[y, x] = ((data[i >> 3] >> (7 - (i & 7))) & 1) != 0;
                                i++;
                            }
                        }
                    }
                }
            }

            public void ApplyMask(int mask)
            {
                for (int y = 0; y < Size; y++)
                    for (int x = 0; x < Size; x++)
                    {
                        if (Func[y, x]) continue;
                        bool invert;
                        switch (mask)
                        {
                            case 0: invert = (x + y) % 2 == 0; break;
                            case 1: invert = y % 2 == 0; break;
                            case 2: invert = x % 3 == 0; break;
                            case 3: invert = (x + y) % 3 == 0; break;
                            case 4: invert = (x / 3 + y / 2) % 2 == 0; break;
                            case 5: invert = (x * y) % 2 + (x * y) % 3 == 0; break;
                            case 6: invert = ((x * y) % 2 + (x * y) % 3) % 2 == 0; break;
                            default: invert = ((x + y) % 2 + (x * y) % 3) % 2 == 0; break;
                        }
                        if (invert) Mod[y, x] = !Mod[y, x];
                    }
            }

            public int Penalty()
            {
                int result = 0;
                // 规则 1：行/列中连续同色
                for (int y = 0; y < Size; y++)
                {
                    bool? cur = null;
                    int run = 0;
                    for (int x = 0; x < Size; x++)
                    {
                        if (cur == null || Mod[y, x] == cur.Value) { run++; if (run == 5) result += 3; else if (run > 5) result++; }
                        else { cur = Mod[y, x]; run = 1; }
                    }
                }
                for (int x = 0; x < Size; x++)
                {
                    bool? cur = null;
                    int run = 0;
                    for (int y = 0; y < Size; y++)
                    {
                        if (cur == null || Mod[y, x] == cur.Value) { run++; if (run == 5) result += 3; else if (run > 5) result++; }
                        else { cur = Mod[y, x]; run = 1; }
                    }
                }
                // 规则 2：2x2 同色块
                for (int y = 0; y < Size - 1; y++)
                    for (int x = 0; x < Size - 1; x++)
                    {
                        bool c = Mod[y, x];
                        if (c == Mod[y, x + 1] && c == Mod[y + 1, x] && c == Mod[y + 1, x + 1])
                            result += 3;
                    }
                // 规则 3：类似定位图案的 1:1:3:1:1:4:1
                int[] pats = { 0b10111010000, 0b00001011101 };
                for (int y = 0; y < Size; y++)
                {
                    for (int x = 0; x + 11 <= Size; x++)
                    {
                        int v = 0;
                        for (int k = 0; k < 11; k++) v = (v << 1) | (Mod[y, x + k] ? 1 : 0);
                        if (v == pats[0] || v == pats[1]) result += 40;
                    }
                }
                for (int x = 0; x < Size; x++)
                {
                    for (int y = 0; y + 11 <= Size; y++)
                    {
                        int v = 0;
                        for (int k = 0; k < 11; k++) v = (v << 1) | (Mod[y + k, x] ? 1 : 0);
                        if (v == pats[0] || v == pats[1]) result += 40;
                    }
                }
                // 规则 4：明暗比例
                int dark = 0;
                for (int y = 0; y < Size; y++)
                    for (int x = 0; x < Size; x++)
                        if (Mod[y, x]) dark++;
                int total = Size * Size;
                int pct = dark * 100 / total;
                int dev = Math.Abs(pct - 50);
                result += (dev / 5) * 10;
                return result;
            }
        }

        private static List<int> AlignmentPositions(int ver)
        {
            if (ver == 1) return new List<int> { 6 };
            int numAlign = ver / 7 + 2;
            int step = (ver == 32) ? 26 : (int)Math.Ceiling((ver * 4 + 4) / (double)(numAlign * 2 - 2)) * 2;
            List<int> res = new List<int> { 6 };
            for (int pos = ver * 4 + 10; pos > 6; pos -= step) res.Add(pos);
            res.Add(6);
            res.Reverse();
            return res;
        }

        public static int GetModuleCount(int version) { return version * 4 + 17; }

        /// <summary>
        /// 生成二维码矩阵（true=深色）。自动选择能容纳内容的最小版本。
        /// </summary>
        public static bool[,] Generate(string text, QrEcc ecc, out int version)
        {
            int dummy = -1;
            return Generate(text, ecc, out version, dummy);
        }

        public static bool[,] Generate(string text, QrEcc ecc, out int version, int forceMask)
        {
            byte[] dataBytes = Encoding.UTF8.GetBytes(text);
            int eccIndex = (int)ecc;
            int ver = -1;
            for (int v = 1; v <= MaxVersion; v++)
            {
                int cc = (v <= 9) ? 8 : 16;
                int need = 4 + cc + 8 * dataBytes.Length;
                if (SumData(v, eccIndex) * 8 >= need) { ver = v; break; }
            }
            if (ver == -1)
                throw new ArgumentException("内容过长，无法在版本 1-" + MaxVersion + " 内编码，请缩短文本或链接。");
            version = ver;

            byte[] msg = EncodeMessage(text, ver, eccIndex);
            byte[] full = AddEccAndInterleave(msg, ver, eccIndex);

            Builder b = new Builder(ver, eccIndex);
            b.DrawFunctionPatterns();
            b.ReserveFormatAndVersion();
            b.DrawVersion();
            b.DrawCodewords(full);

            int bestMask;
            if (forceMask >= 0 && forceMask <= 7)
            {
                bestMask = forceMask;
            }
            else
            {
                bestMask = 0;
                int minPenalty = int.MaxValue;
                for (int m = 0; m < 8; m++)
                {
                    b.ApplyMask(m);
                    b.DrawFormatBits(m);
                    int p = b.Penalty();
                    if (p < minPenalty) { minPenalty = p; bestMask = m; }
                    b.ApplyMask(m); // 还原
                }
            }
            b.ApplyMask(bestMask);
            b.DrawFormatBits(bestMask);
            return b.Mod;
        }
    }
}
