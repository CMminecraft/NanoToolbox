using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace Toolbox
{
    public partial class App : Application
    {
        public App()
        {
            // 捕获 UI 线程未处理异常：记录日志并阻止程序直接闪退
            DispatcherUnhandledException += (s, e) =>
            {
                LogException(e.Exception, "DispatcherUnhandledException");
                e.Handled = true;
                ShowError(e.Exception);
            };

            // 捕获非 UI 线程未处理异常
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                LogException(e.ExceptionObject as Exception, "UnhandledException");
            };

            // 捕获未观察的 Task 异常
            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                LogException(e.Exception, "UnobservedTaskException");
                e.SetObserved();
            };
        }

        private static void LogException(Exception ex, string type)
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Toolbox_error.log");
                File.AppendAllText(path,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {type}:\r\n{ex?.ToString() ?? "null"}\r\n\r\n");
            }
            catch { }
        }

        private static void ShowError(Exception ex)
        {
            try
            {
                string msg = "纳米工具箱遇到一个错误，但已自动恢复，您可以继续操作。\r\n\r\n";
                if (ex != null)
                    msg += $"错误类型：{ex.GetType().Name}\r\n{ex.Message}";
                MessageBox.Show(msg, "出错了", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch { }
        }
    }
}
