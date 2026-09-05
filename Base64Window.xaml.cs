using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace Toolbox
{
    public partial class Base64Window : UserControl, IThemeAware
    {
        public Base64Window()
        {
            InitializeComponent();
            // 主题感知：图标随主题自动切换
            RefreshTheme(ThemeManager.IsDark);
        }

        // 主题切换时由 MainWindow 通知，刷新 HeaderIcon 白/黑图标
        public void RefreshTheme(bool isDark)
        {
            try
            {
                ThemeIconHelper.SetIcon(HeaderIcon, "notepad.png", isDark);
            }
            catch { }
        }

        // 编码：输入文本 → 输出 Base64
        private void BtnEncode_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(InputText.Text))
                {
                    SetStatus("输入为空，无需编码。", false);
                    return;
                }
                byte[] bytes = Encoding.UTF8.GetBytes(InputText.Text);
                string b64 = Convert.ToBase64String(bytes);
                if (UrlSafeCheck.IsChecked == true) b64 = ToUrlSafe(b64);
                OutputText.Text = b64;
                SetStatus($"已编码（{bytes.Length} 字节 → {b64.Length} 字符）。", true);
            }
            catch (Exception ex)
            {
                SetStatus("编码失败：" + ex.Message, false);
            }
        }

        // 解码：输入 Base64 → 输出文本
        private void BtnDecode_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(InputText.Text))
                {
                    SetStatus("输入为空，无需解码。", false);
                    return;
                }
                string b64 = InputText.Text.Trim();
                if (UrlSafeCheck.IsChecked == true) b64 = FromUrlSafe(b64);
                if (IgnoreInvalidCheck.IsChecked == true) b64 = StripNonBase64(b64);
                try
                {
                    byte[] bytes = Convert.FromBase64String(b64);
                    OutputText.Text = Encoding.UTF8.GetString(bytes);
                    SetStatus($"已解码（{b64.Length} 字符 → {bytes.Length} 字节）。", true);
                }
                catch (FormatException)
                {
                    // 自动尝试当作十六进制 / Latin1 失败时给个提示
                    SetStatus("Base64 格式不正确。请检查输入或勾选「忽略非 Base64 字符」", false);
                }
            }
            catch (Exception ex)
            {
                SetStatus("解码失败：" + ex.Message, false);
            }
        }

        // 文件 → Base64（输出文本框）
        private void BtnFileToBase64_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "选择要编码的文件",
                Filter = "所有文件 (*.*)|*.*"
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                byte[] bytes = File.ReadAllBytes(dlg.FileName);
                string b64 = Convert.ToBase64String(bytes);
                if (UrlSafeCheck.IsChecked == true) b64 = ToUrlSafe(b64);
                InputText.Text = $"[文件] {dlg.SafeFileName} ({bytes.Length} 字节)";
                OutputText.Text = b64;
                SetStatus($"已编码文件：{dlg.FileName}（{bytes.Length} 字节 → {b64.Length} 字符）。", true);
            }
            catch (Exception ex)
            {
                SetStatus("文件编码失败：" + ex.Message, false);
            }
        }

        // Base64 → 文件（输入文本框）
        private void BtnBase64ToFile_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(InputText.Text))
            {
                SetStatus("请先把 Base64 文本贴到上方输入区。", false);
                return;
            }
            var dlg = new SaveFileDialog
            {
                Title = "选择保存位置",
                Filter = "所有文件 (*.*)|*.*",
                FileName = "decoded.bin"
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                string b64 = InputText.Text.Trim();
                if (UrlSafeCheck.IsChecked == true) b64 = FromUrlSafe(b64);
                if (IgnoreInvalidCheck.IsChecked == true) b64 = StripNonBase64(b64);
                byte[] bytes = Convert.FromBase64String(b64);
                File.WriteAllBytes(dlg.FileName, bytes);
                SetStatus($"已解码并保存到：{dlg.FileName}（{b64.Length} 字符 → {bytes.Length} 字节）。", true);
                InputText.Text = $"[Base64] 来自上方输入";
                OutputText.Text = $"已保存到：{dlg.FileName}\n大小：{bytes.Length} 字节";
            }
            catch (FormatException)
            {
                SetStatus("Base64 格式不正确，无法解码。", false);
            }
            catch (Exception ex)
            {
                SetStatus("文件解码失败：" + ex.Message, false);
            }
        }

        private void BtnCopyResult_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(OutputText.Text))
            {
                SetStatus("输出为空。", false);
                return;
            }
            try
            {
                Clipboard.SetText(OutputText.Text);
                SetStatus("已复制到剪贴板。", true);
            }
            catch (Exception ex)
            {
                SetStatus("复制失败：" + ex.Message, false);
            }
        }

        private void BtnCopyInputToOutput_Click(object sender, RoutedEventArgs e)
        {
            OutputText.Text = InputText.Text;
            SetStatus("已将输入复制到输出。", true);
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            InputText.Text = "";
            OutputText.Text = "";
            SetStatus("已清空。", true);
        }

        private void SetStatus(string text, bool ok)
        {
            if (StatusText != null) StatusText.Text = text;
            if (InfoText != null) InfoText.Text = text;
        }

        // ====== 工具方法 ======

        // 标准 Base64 → URL-Safe：把 + 换 -，/ 换 _，去掉末尾 =
        private static string ToUrlSafe(string b64)
        {
            return b64.Replace('+', '-').Replace('/', '_').TrimEnd('=');
        }

        // URL-Safe → 标准 Base64：把 - 换 +，_ 换 /，补 = 到 4 倍数
        private static string FromUrlSafe(string b64)
        {
            string s = b64.Replace('-', '+').Replace('_', '/');
            int pad = s.Length % 4;
            if (pad == 2) s += "==";
            else if (pad == 3) s += "=";
            else if (pad == 1) throw new FormatException("URL-Safe Base64 长度不合法");
            return s;
        }

        private static string StripNonBase64(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') ||
                    (c >= '0' && c <= '9') || c == '+' || c == '/' || c == '=')
                    sb.Append(c);
            }
            return sb.ToString();
        }
    }

    // 简单主题判断工具（避免每个窗口都引用 MainWindow）
    internal static class ThemeManager
    {
        public static bool IsDark
        {
            get
            {
                try
                {
                    using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                    {
                        if (key != null)
                        {
                            var val = key.GetValue("AppsUseLightTheme");
                            if (val is int i) return i == 0;
                        }
                    }
                }
                catch { }
                return true;
            }
        }
    }
}
