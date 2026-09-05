using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Threading;

namespace Toolbox
{
    public partial class ScreenshotWindow : System.Windows.Controls.UserControl
    {
        private const int WH_MOUSE_LL = 14;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const uint GA_ROOT = 2;

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
        private LowLevelMouseProc _mouseProc;
        private IntPtr _hookId = IntPtr.Zero;

        public ScreenshotWindow()
        {
            InitializeComponent();
        }

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(POINT pt);

        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private void BtnFullScreen_Click(object sender, RoutedEventArgs e)
        {
            var owner = Window.GetWindow(this);
            if (owner != null) owner.Hide();
            Thread.Sleep(300);

            try
            {
                using (Bitmap bmp = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height))
                {
                    using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(bmp))
                    {
                        g.CopyFromScreen(0, 0, 0, 0, bmp.Size);
                    }
                    System.Windows.Forms.Clipboard.SetImage(bmp);
                }
                System.Windows.MessageBox.Show("全屏截图已复制到剪贴板", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"截图失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (owner != null) owner.Show();
            }
        }

        private void BtnRegion_Click(object sender, RoutedEventArgs e)
        {
            var owner = Window.GetWindow(this);
            if (owner != null) owner.Hide();
            Thread.Sleep(300);

            try
            {
                using (Bitmap bmp = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height))
                {
                    using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(bmp))
                    {
                        g.CopyFromScreen(0, 0, 0, 0, bmp.Size);
                    }

                    using (System.Windows.Forms.Form form = new System.Windows.Forms.Form())
                    {
                        form.WindowState = System.Windows.Forms.FormWindowState.Maximized;
                        form.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
                        form.Opacity = 0.5;
                        form.BackColor = Color.Black;
                        form.TopMost = true;

                        bool dragging = false;
                        System.Drawing.Point startPoint = System.Drawing.Point.Empty;
                        Rectangle selection = Rectangle.Empty;

                        form.MouseDown += (s, ea) =>
                        {
                            dragging = true;
                            startPoint = ea.Location;
                        };

                        form.MouseMove += (s, ea) =>
                        {
                            if (dragging)
                            {
                                selection = new Rectangle(
                                    Math.Min(startPoint.X, ea.X),
                                    Math.Min(startPoint.Y, ea.Y),
                                    Math.Abs(ea.X - startPoint.X),
                                    Math.Abs(ea.Y - startPoint.Y)
                                );
                                form.Invalidate();
                            }
                        };

                        form.MouseUp += (s, ea) =>
                        {
                            dragging = false;
                            if (selection.Width > 0 && selection.Height > 0)
                            {
                                using (Bitmap cropped = bmp.Clone(selection, bmp.PixelFormat))
                                {
                                    System.Windows.Forms.Clipboard.SetImage(cropped);
                                }
                            }
                            form.Close();
                        };

                        form.Paint += (s, ea) =>
                        {
                            using (Brush brush = new SolidBrush(Color.FromArgb(100, 0, 0, 0)))
                            {
                                ea.Graphics.FillRectangle(brush, new Rectangle(0, 0, form.Width, form.Height));
                            }

                            if (selection.Width > 0 && selection.Height > 0)
                            {
                                ea.Graphics.DrawImage(bmp, selection, selection, GraphicsUnit.Pixel);
                                using (Pen pen = new Pen(Color.Red, 2))
                                {
                                    ea.Graphics.DrawRectangle(pen, selection);
                                }
                            }
                        };

                        form.ShowDialog();
                    }
                }
                System.Windows.MessageBox.Show("区域截图已复制到剪贴板", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"截图失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (owner != null) owner.Show();
            }
        }

        private void BtnWindow_Click(object sender, RoutedEventArgs e)
        {
            var owner = Window.GetWindow(this);
            if (owner != null) owner.Hide();
            Thread.Sleep(300);

            try
            {
                using (ToolTipForm tip = new ToolTipForm("请点击要截图的窗口"))
                {
                    tip.Show();
                    System.Windows.Forms.Application.DoEvents();

                    _mouseProc = new LowLevelMouseProc(HookCallback);
                    _hookId = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, GetModuleHandle(null), 0);
                }
            }
            catch (Exception ex)
            {
                if (owner != null) owner.Show();
                System.Windows.MessageBox.Show($"截图失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_LBUTTONDOWN)
            {
                MSLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;

                Dispatcher.BeginInvoke(new Action(() => CaptureWindowFromPoint(hookStruct.pt)), DispatcherPriority.Normal);
                return (IntPtr)1;
            }
            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        private void CaptureWindowFromPoint(POINT pt)
        {
            var owner = Window.GetWindow(this);
            try
            {
                IntPtr hWnd = WindowFromPoint(pt);
                if (hWnd == IntPtr.Zero)
                {
                    System.Windows.MessageBox.Show("未找到窗口", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                hWnd = GetAncestor(hWnd, GA_ROOT);
                if (hWnd == IntPtr.Zero)
                {
                    System.Windows.MessageBox.Show("未找到窗口", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                RECT rect;
                if (!GetWindowRect(hWnd, out rect))
                {
                    System.Windows.MessageBox.Show("无法获取窗口位置", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int width = rect.Right - rect.Left;
                int height = rect.Bottom - rect.Top;
                if (width <= 0 || height <= 0)
                {
                    System.Windows.MessageBox.Show("窗口尺寸无效", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                SetForegroundWindow(hWnd);
                Thread.Sleep(200);

                using (Bitmap bmp = new Bitmap(width, height))
                {
                    using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(bmp))
                    {
                        g.CopyFromScreen(rect.Left, rect.Top, 0, 0, new System.Drawing.Size(width, height));
                    }
                    System.Windows.Forms.Clipboard.SetImage(bmp);
                }

                System.Windows.MessageBox.Show("窗口截图已复制到剪贴板", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"截图失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (owner != null) owner.Show();
            }
        }

        private class ToolTipForm : System.Windows.Forms.Form
        {
            public ToolTipForm(string text)
            {
                FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
                StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
                Size = new System.Drawing.Size(280, 60);
                BackColor = Color.FromArgb(45, 45, 48);
                ForeColor = Color.White;
                Font = new System.Drawing.Font("Microsoft YaHei", 12F);
                TopMost = true;
                ShowInTaskbar = false;
                Opacity = 0.95;

                var label = new System.Windows.Forms.Label
                {
                    Text = text,
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                    ForeColor = Color.White
                };
                Controls.Add(label);
            }
        }
    }
}
