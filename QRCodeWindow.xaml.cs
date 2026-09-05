using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace Toolbox
{
    public partial class QRCodeWindow : UserControl, IThemeAware
    {
        private BitmapSource _lastBitmap;
        private bool[,] _lastMatrix;
        private string _lastText = "";

        public QRCodeWindow()
        {
            InitializeComponent();
        }

        public void RefreshTheme(bool isDark)
        {
            ThemeIconHelper.SetIcon(LogoImage, "qrcode.png", isDark);
        }

        private void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            string text = ContentInput.Text ?? "";
            if (text.Length == 0)
            {
                StatusText.Text = "请输入要生成二维码的内容。";
                return;
            }
            QrEcc ecc = QrEcc.Medium;
            switch (EccLevel.SelectedIndex)
            {
                case 0: ecc = QrEcc.Low; break;
                case 1: ecc = QrEcc.Medium; break;
                case 2: ecc = QrEcc.Quartile; break;
                case 3: ecc = QrEcc.High; break;
            }

            try
            {
                int version;
                bool[,] matrix = QrEncoder.Generate(text, ecc, out version);
                _lastMatrix = matrix;
                int modules = QrEncoder.GetModuleCount(version);
                _lastBitmap = Render(matrix, 8, 4);
                _lastBitmap.Freeze();
                QrImage.Source = _lastBitmap;
                _lastText = text;
                StatusText.Text = string.Format("版本 {0} · {1}×{1} 模块 · 纠错等级 {2} · 内容 {3} 字节",
                    version, modules, ecc, System.Text.Encoding.UTF8.GetByteCount(text));
                BtnSave.IsEnabled = true;
                BtnCopy.IsEnabled = true;
                BtnCopyText.IsEnabled = true;
            }
            catch (Exception ex)
            {
                QrImage.Source = null;
                _lastBitmap = null;
                BtnSave.IsEnabled = false;
                BtnCopy.IsEnabled = false;
                BtnCopyText.IsEnabled = false;
                StatusText.Text = "生成失败：" + ex.Message;
            }
        }

        private static BitmapSource Render(bool[,] matrix, int scale, int quiet)
        {
            int n = matrix.GetLength(0);
            int dim = n + quiet * 2;
            int px = dim * scale;
            var bmp = new WriteableBitmap(px, px, 96, 96, PixelFormats.Bgra32, null);
            byte[] pixels = new byte[px * px * 4];
            for (int y = 0; y < px; y++)
            {
                int my = y / scale - quiet;
                for (int x = 0; x < px; x++)
                {
                    int mx = x / scale - quiet;
                    bool dark = (my >= 0 && my < n && mx >= 0 && mx < n) && matrix[my, mx];
                    byte v = dark ? (byte)0 : (byte)255;
                    int idx = (y * px + x) * 4;
                    pixels[idx] = v;
                    pixels[idx + 1] = v;
                    pixels[idx + 2] = v;
                    pixels[idx + 3] = 255;
                }
            }
            bmp.WritePixels(new Int32Rect(0, 0, px, px), pixels, px * 4, 0);
            return bmp;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (_lastBitmap == null) return;
            var dlg = new SaveFileDialog
            {
                Filter = "PNG 图片|*.png",
                FileName = "qrcode.png",
                DefaultExt = ".png"
            };
            if (dlg.ShowDialog() == true)
            {
                // 以更高分辨率保存，保证清晰度
                int scale = 12;
                var saveBmp = Render(_lastMatrix, scale, 4);
                saveBmp.Freeze();
                var enc = new PngBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(saveBmp));
                using (var fs = new FileStream(dlg.FileName, FileMode.Create))
                    enc.Save(fs);
                StatusText.Text = "已保存：" + dlg.FileName;
            }
        }

        // 保存时使用最近一次矩阵（_lastMatrix）以保证更高分辨率重渲染

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            if (_lastBitmap == null) return;
            try
            {
                Clipboard.SetImage(_lastBitmap);
                StatusText.Text = "二维码图片已复制到剪贴板。";
            }
            catch { }
        }

        private void BtnCopyText_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_lastText)) return;
            try { Clipboard.SetText(_lastText); StatusText.Text = "内容已复制到剪贴板。"; } catch { }
        }
    }
}
