using System;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace Toolbox
{
    /// <summary>
    /// 工具窗口用的图标加载工具：从嵌入资源 Icons/White|Black 加载 PNG，
    /// 自动根据 isDark 选对应套。失败时静默忽略（避免破坏工具本身功能）。
    /// </summary>
    public static class ThemeIconHelper
    {
        public static void SetIcon(Image img, string fileName, bool isDark)
        {
            if (img == null || string.IsNullOrEmpty(fileName)) return;
            string set = isDark ? "White" : "Black";
            try
            {
                var uri = new Uri(string.Format(
                    "pack://application:,,,/Toolbox;component/Icons/{0}/{1}", set, fileName),
                    UriKind.Absolute);
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = uri;
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                img.Source = bmp;
            }
            catch { /* 图标缺失不影响工具主流程 */ }
        }
    }
}
