using System;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace Toolbox
{
    public partial class ColorPickerWindow : UserControl, IThemeAware
    {
        private readonly ObservableCollection<Color> _history = new ObservableCollection<Color>();

        public ColorPickerWindow()
        {
            InitializeComponent();
            HistoryList.ItemsSource = _history;
            SetColor(Colors.White);
        }

        public void RefreshTheme(bool isDark)
        {
            ThemeIconHelper.SetIcon(LogoImage, "colorpicker.png", isDark);
        }

        private void BtnPick_Click(object sender, RoutedEventArgs e)
        {
            var overlay = new PickerOverlayWindow();
            if (overlay.ShowDialog() == true && overlay.PickedColor.HasValue)
            {
                var c = overlay.PickedColor.Value;
                SetColor(c);
                _history.Insert(0, c);
                if (_history.Count > 24) _history.RemoveAt(_history.Count - 1);
            }
        }

        private void SetColor(Color c)
        {
            SolidColorBg.Background = new SolidColorBrush(c);
            HexBox.Text = "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");
            RgbBox.Text = "rgb(" + c.R + ", " + c.G + ", " + c.B + ")";
        }

        private void BtnCopyHex_Click(object sender, RoutedEventArgs e)
        {
            Copy(HexBox.Text);
        }

        private void BtnCopyRgb_Click(object sender, RoutedEventArgs e)
        {
            Copy(RgbBox.Text);
        }

        private void HistorySwatch_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border b && b.Tag is Color c)
            {
                SetColor(c);
            }
        }

        private void Copy(string s)
        {
            try { Clipboard.SetText(s ?? ""); } catch { }
        }
    }

    // 全屏透明取色覆盖窗口
    public class PickerOverlayWindow : Window
    {
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern uint GetPixel(IntPtr hDC, int x, int y);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        public Color? PickedColor { get; private set; }

        private TextBlock _info;
        private DispatcherTimer _timer;
        private POINT _lastPos;
        private Color _lastColor = Colors.White;

        public PickerOverlayWindow()
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = new SolidColorBrush(Color.FromArgb(2, 0, 0, 0)); // alpha=2 即可命中事件，又近乎透明
            Topmost = true;
            WindowState = WindowState.Maximized;
            Cursor = Cursors.Cross;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;
            Focusable = true;

            // 顶部信息
            _info = new TextBlock
            {
                FontSize = 14,
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                Padding = new Thickness(10, 6, 10, 6),
                Text = "将鼠标移至目标位置后点击 (Esc 取消)"
            };
            var border = new Border
            {
                Child = _info,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(20, 20, 0, 0)
            };
            Content = border;

            MouseMove += (s, e) => UpdateSample();
            MouseLeftButtonDown += (s, e) => Capture();
            MouseRightButtonDown += (s, e) => { DialogResult = false; Close(); };
            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape) { DialogResult = false; Close(); }
            };

            Loaded += (s, e) =>
            {
                this.Focus();
                _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
                _timer.Tick += (ss, ee) => UpdateSample();
                _timer.Start();
            };
            Closed += (s, e) => { if (_timer != null) _timer.Stop(); };
        }

        private void UpdateSample()
        {
            POINT p;
            if (!GetCursorPos(out p)) return;
            _lastPos = p;
            IntPtr hdc = GetDC(IntPtr.Zero);
            try
            {
                uint c = GetPixel(hdc, p.X, p.Y);
                if (c == 0xFFFFFFFF) return; // 屏幕保护或越界
                byte r = (byte)(c & 0xFF);
                byte g = (byte)((c >> 8) & 0xFF);
                byte b = (byte)((c >> 16) & 0xFF);
                _lastColor = Color.FromRgb(r, g, b);
                _info.Text = string.Format("X:{0} Y:{1}  #{2:X2}{3:X2}{4:X2}  rgb({5},{6},{7})",
                    p.X, p.Y, r, g, b, r, g, b);
            }
            finally
            {
                ReleaseDC(IntPtr.Zero, hdc);
            }
        }

        private void Capture()
        {
            PickedColor = _lastColor;
            DialogResult = true;
            Close();
        }
    }
}
