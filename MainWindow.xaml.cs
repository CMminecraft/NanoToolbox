using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Toolbox
{
    public partial class MainWindow : Window
    {
        private bool isDarkTheme = true;
        private bool _heroClosed = false;
        private bool _animationsEnabled = true;
        private string _currentThemeName = "Dark";   // 解析后的完整主题键（如 DarkBlue / LightGreen）
        private string _lastDarkTheme = "Dark";
        private string _customThemePath = null;
        private string _colorKey = "Default";        // 配色：Default/Blue/Green/Teal/Purple/Pink/Orange/Red/Gray
        private string _appearanceMode = "Auto";      // 外观模式：Auto/Dark/Light

        // ====== 彩蛋 ======
        private int _cm666Count = 0;                  // 搜索框输入 CM666 + 回车计数
        private bool _cm666Unlocked = false;          // 是否解锁亚克力
        private bool _acrylicMode = false;            // 是否启用亚克力透明
        private bool _acrylicApplied = false;         // MainWindow_Loaded 里只应用一次亚克力，避免重复
        private int _aboutClickCount = 0;             // 关于图标点击计数
        private bool _neverClickUnlocked = false;     // 是否解锁千万别点
        private int _devtoolCount = 0;                // 搜索框输入 DEVtool + 回车计数
        private bool _devModeUnlocked = false;        // 是否解锁开发者模式
        private Popup _toastPopup = null;             // 彩蛋解锁提示
        private DispatcherTimer _toastTimer = null;
        private Random _prankRandom = new Random();   // 千万别点随机效果
        private Storyboard _prankStoryboard = null;

        // ====== 开发者模式数据 ======
        // 图标覆盖：key=目标标识，value=图标文件名（位于 Icons/White 或 Icons/Black）
        //   目标标识：Window / Nav:Home / Nav:BuiltIn / Nav:App / Nav:Packages / Nav:Settings
        //           / Tool:工具名（如 Tool:记事本）
        //           / Category:分类名（暂未启用，预留）
        private readonly Dictionary<string, string> _devIconOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        // 文本覆盖：key=文本键，value=新文本
        //   文本键：WindowTitle / Nav:Home / Nav:BuiltIn / Nav:App / Nav:Packages / Nav:Settings
        private readonly Dictionary<string, string> _devTextOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        // 颜色覆盖：key=颜色键，value=HEX
        //   颜色键：Primary / Background / Text（深色主题）
        private readonly Dictionary<string, string> _devColorOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private const string SettingsFile = "settings.txt";
        private const string ThemeFile = "theme.txt"; // 旧版主题文件，仅兼容读取
        private const string AppConfigFile = "apps.ini"; // 外部工具统一配置，放在 app/ 目录下
        private const string ThemeConfigFile = "theme.ini"; // 全局分类图标映射（覆盖 apps.ini 内 |图标 语法）

        private FrameworkElement _currentPage;
        private Button _currentNavButton;
        private Dictionary<string, FrameworkElement> _pages;
        private Dictionary<string, Button> _navButtons;

        // APP 工具箱标签页状态
        private List<ToolCategory> _appCategories = new List<ToolCategory>();
        private ToolCategory _selectedAppCategory = null;
        // 分类名 -> 自定义 PNG 图标文件名（来自 ini 的 category=名称|图标 语法）
        private Dictionary<string, string> _categoryIconOverrides =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        // packages.ini 独立图标覆盖字典（与 apps.ini 隔离）
        private Dictionary<string, string> _packagesCategoryIconOverrides =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        // theme.ini 全局分类图标映射（最高优先级，覆盖 apps.ini/packages.ini 内 |图标 语法）
        private Dictionary<string, string> _themeCategoryIconOverrides =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private class ThemeOption
        {
            public string DisplayName { get; set; }
            public string Key { get; set; }
        }

        // 主题选择器只选「配色」
        private readonly List<ThemeOption> _themeOptions = new List<ThemeOption>
        {
            new ThemeOption { DisplayName = "默认蓝", Key = "Default" },
            new ThemeOption { DisplayName = "蓝", Key = "Blue" },
            new ThemeOption { DisplayName = "绿", Key = "Green" },
            new ThemeOption { DisplayName = "青", Key = "Teal" },
            new ThemeOption { DisplayName = "紫", Key = "Purple" },
            new ThemeOption { DisplayName = "粉", Key = "Pink" },
            new ThemeOption { DisplayName = "橙", Key = "Orange" },
            new ThemeOption { DisplayName = "红", Key = "Red" },
            new ThemeOption { DisplayName = "灰", Key = "Gray" }
        };

        // 外观模式：跟随系统 / 深色 / 浅色
        private readonly List<ThemeOption> _modeOptions = new List<ThemeOption>
        {
            new ThemeOption { DisplayName = "跟随系统", Key = "Auto" },
            new ThemeOption { DisplayName = "深色", Key = "Dark" },
            new ThemeOption { DisplayName = "浅色", Key = "Light" }
        };

        private readonly Dictionary<string, string> _builtInThemePaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Dark", "Themes/Dark.xaml" },
            { "Light", "Themes/Light.xaml" },
            { "DarkBlue", "Themes/DarkBlue.xaml" },
            { "DarkGreen", "Themes/DarkGreen.xaml" },
            { "DarkPurple", "Themes/DarkPurple.xaml" },
            { "DarkOrange", "Themes/DarkOrange.xaml" },
            { "DarkTeal", "Themes/DarkTeal.xaml" },
            { "DarkRed", "Themes/DarkRed.xaml" },
            { "DarkPink", "Themes/DarkPink.xaml" },
            { "DarkGray", "Themes/DarkGray.xaml" },
            { "LightBlue", "Themes/LightBlue.xaml" },
            { "LightGreen", "Themes/LightGreen.xaml" },
            { "LightTeal", "Themes/LightTeal.xaml" },
            { "LightPurple", "Themes/LightPurple.xaml" },
            { "LightPink", "Themes/LightPink.xaml" },
            { "LightOrange", "Themes/LightOrange.xaml" },
            { "LightRed", "Themes/LightRed.xaml" },
            { "LightGray", "Themes/LightGray.xaml" }
        };

        public MainWindow()
        {
            InitializeComponent();
            try
            {
                var iconUri = new Uri("pack://application:,,,/Toolbox;component/AppIcon.ico", UriKind.Absolute);
                this.Icon = BitmapFrame.Create(iconUri);
            }
            catch { }
            EnsureDefaultResources();
            LoadSettings();
            UpdateAnimationStyles();
            LoadTheme();
            InitializePages();
            LoadAndBuildTools(animate: _animationsEnabled);
            // PerformanceMonitor 首次 new PerformanceCounter 极慢（5-10s），会卡窗口显示。
            // 推迟到 ApplicationIdle，窗口先出来，性能浮窗后加载。
            Dispatcher.BeginInvoke(new Action(InitializePerfMonitor), DispatcherPriority.ApplicationIdle);
            InitializeEasterEggs();
            // 开发者模式：把已保存的图标/文本覆盖应用到当前 UI
            try
            {
                ApplyDevTextOverrides();
                foreach (var k in _devIconOverrides.Keys.ToList()) ApplyDevIconOverride(k);
                ApplyDevColorOverrides();
            }
            catch { }
            SwitchPage("HomePage", NavHome);
            StartHeroAutoClose();
            // 亚克力加载 bug 修复：窗口构造期间 HWND 未真正显示，SetWindowCompositionAttribute 静默失败，
            // 导致启动时磨砂不生效（需手动关闭再打开才生效）。推迟到 Loaded（窗口显示完成后）再启用。
            this.Loaded += MainWindow_Loaded;
        }

        private void InitializePerfMonitor()
        {
            if (PerfMonitorHost != null)
            {
                PerfMonitorHost.Children.Clear();
                PerfMonitorHost.Children.Add(new PerformanceMonitor());
            }
        }

        private void StartHeroAutoClose()
        {
            var heroTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            heroTimer.Tick += (s, e) =>
            {
                heroTimer.Stop();
                CloseHeroBanner();
            };
            heroTimer.Start();
        }

        private void CloseHeroBanner()
        {
            if (HeroBanner == null || _heroClosed) return;
            _heroClosed = true;
            if (_animationsEnabled)
            {
                var sb = new Storyboard();
                var da = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromMilliseconds(400)));
                Storyboard.SetTarget(da, HeroBanner);
                Storyboard.SetTargetProperty(da, new PropertyPath(Border.OpacityProperty));
                sb.Children.Add(da);
                sb.Completed += (s, e) => { HeroBanner.Visibility = Visibility.Collapsed; };
                sb.Begin();
            }
            else
            {
                HeroBanner.Visibility = Visibility.Collapsed;
            }
        }

        // ============================================================
        // 彩蛋实现
        // ============================================================

        // 窗口加载时根据已保存状态恢复彩蛋
        private void InitializeEasterEggs()
        {
            // 彩蛋1：CM666 解锁「亚克力透明背景」开关
            if (AcrylicEggPanel != null)
                AcrylicEggPanel.Visibility = _cm666Unlocked ? Visibility.Visible : Visibility.Collapsed;
            if (AcrylicToggle != null)
                AcrylicToggle.IsChecked = _acrylicMode;
            // 亚克力实际启用不在构造时做（DWM API 会静默失败），改到 MainWindow_Loaded 里

            // 彩蛋2：关于图标点 5 下 解锁「千万别点」
            if (NeverClickEggPanel != null)
                NeverClickEggPanel.Visibility = _neverClickUnlocked ? Visibility.Visible : Visibility.Collapsed;

            // 彩蛋3：DEVtool 输入 3 次解锁「开发者模式」
            if (DevModePanel != null)
                DevModePanel.Visibility = _devModeUnlocked ? Visibility.Visible : Visibility.Collapsed;
        }

        // 窗口首次显示完成后再启用亚克力（构造时窗口句柄未就绪，SetWindowCompositionAttribute 静默失败）
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (_acrylicApplied) return;
            _acrylicApplied = true;
            if (_cm666Unlocked && _acrylicMode)
                ApplyAcrylicBackground();
        }

        // 搜索框输入 CM666 并回车，累计 3 次解锁亚克力开关
        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            if (SearchBox == null || SearchBox.Text == null) return;

            string text = SearchBox.Text.Trim();

            // 彩蛋 3：DEVtool 输入 3 次解锁开发者模式（独立于 CM666 计数）
            if (!_devModeUnlocked && text.Equals("DEVtool", StringComparison.OrdinalIgnoreCase))
            {
                _devtoolCount++;
                if (_devtoolCount >= 3)
                {
                    _devModeUnlocked = true;
                    if (DevModePanel != null) DevModePanel.Visibility = Visibility.Visible;
                    SaveSettings();
                    ShowToast("🛠 彩蛋解锁：开发者模式");
                    SearchBox.Text = "";
                    try { InitializeDevMode(); } catch { }
                    return;
                }
            }

            if (_cm666Unlocked) return; // 已解锁则不再计数

            if (text.Equals("CM666", StringComparison.OrdinalIgnoreCase))
            {
                _cm666Count++;
                if (_cm666Count >= 3)
                {
                    _cm666Unlocked = true;
                    if (AcrylicEggPanel != null) AcrylicEggPanel.Visibility = Visibility.Visible;
                    if (AcrylicToggle != null) AcrylicToggle.IsChecked = false;
                    SaveSettings();
                    ShowToast("🎉 彩蛋解锁：亚克力透明背景");
                    SearchBox.Text = "";
                }
            }
        }

        // 关于图标点击 5 次解锁「千万别点」
        private void AboutIcon_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_neverClickUnlocked) return;
            _aboutClickCount++;
            if (_aboutClickCount >= 5)
            {
                _neverClickUnlocked = true;
                if (NeverClickEggPanel != null) NeverClickEggPanel.Visibility = Visibility.Visible;
                SaveSettings();
                ShowToast("🎉 彩蛋解锁：千万别点");
            }
        }

        // 亚克力开关：开
        private void AcrylicToggle_Checked(object sender, RoutedEventArgs e)
        {
            _acrylicMode = true;
            ApplyAcrylicBackground();
            SaveSettings();
        }

        // 亚克力开关：关
        private void AcrylicToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            _acrylicMode = false;
            ClearAcrylicBackground();
            SaveSettings();
        }

        // 将 WindowFrame / TitleBar / 主内容区 / 左侧导航背景改为半透明，并启用系统模糊
        private void ApplyAcrylicBackground()
        {
            try
            {
                var baseBrush = FindResource("BackgroundBrush") as SolidColorBrush;
                if (baseBrush != null)
                {
                    var c = baseBrush.Color;
                    // alpha 60：既保留可读性，又能透出后面的模糊桌面
                    var translucent = new SolidColorBrush(Color.FromArgb(60, c.R, c.G, c.B));
                    translucent.Freeze();
                    if (WindowFrame != null) WindowFrame.Background = translucent;
                    if (TitleBar != null) TitleBar.Background = translucent;
                    if (MainContentGrid != null) MainContentGrid.Background = translucent;
                    if (LeftNavBorder != null) LeftNavBorder.Background = translucent;
                }
                EnableAcrylicBlur();
            }
            catch { }
        }

        // 恢复普通背景并关闭模糊
        private void ClearAcrylicBackground()
        {
            try
            {
                DisableAcrylicBlur();
                if (WindowFrame != null)
                    WindowFrame.SetResourceReference(Border.BackgroundProperty, "BackgroundBrush");
                if (TitleBar != null)
                    TitleBar.SetResourceReference(Border.BackgroundProperty, "BackgroundBrush");
                if (MainContentGrid != null)
                    MainContentGrid.SetResourceReference(Grid.BackgroundProperty, "BackgroundBrush");
                if (LeftNavBorder != null)
                    LeftNavBorder.SetResourceReference(Border.BackgroundProperty, "BackgroundSecondaryBrush");
            }
            catch { }
        }

        // 启用 DWM 模糊：Win11 用官方 API，Win10 用 SetWindowCompositionAttribute+BLURBEHIND，最后回退旧 API
        [DllImport("dwmapi.dll")]
        private static extern int DwmEnableBlurBehindWindow(IntPtr hwnd, ref DWM_BLURBEHIND pBlurBehind);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("user32.dll")]
        private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WINDOWCOMPOSITIONATTRIBDATA data);

        private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

        [StructLayout(LayoutKind.Sequential)]
        private struct DWM_BLURBEHIND
        {
            public uint dwFlags;
            public bool fEnable;
            public IntPtr hRgnBlur;
            public bool fTransitionOnMaximized;
        }

        private enum WINDOWCOMPOSITIONATTRIB : uint
        {
            WCA_ACCENT_POLICY = 19
        }

        private enum ACCENT_STATE : uint
        {
            ACCENT_DISABLED = 0,
            ACCENT_ENABLE_GRADIENT = 1,
            ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
            ACCENT_ENABLE_BLURBEHIND = 3,
            ACCENT_ENABLE_ACRYLICBLURBEHIND = 4
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ACCENT_POLICY
        {
            public ACCENT_STATE AccentState;
            public uint AccentFlags;
            public uint GradientColor;
            public uint AnimationId;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WINDOWCOMPOSITIONATTRIBDATA
        {
            public WINDOWCOMPOSITIONATTRIB Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        private void EnableAcrylicBlur()
        {
            try
            {
                IntPtr hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero) return;

                // 关键修复：本窗口是 AllowsTransparency=True 的分层窗口。
                // 分层窗口下 Win11 的 DwmSetWindowAttribute(DWMWA_SYSTEMBACKDROP_TYPE) 会退化为白底（无模糊），
                // 因此必须改用 SetWindowCompositionAttribute + ACCENT_ENABLE_BLURBEHIND（Win10/Win11 均稳定可用）。
                // 仅当窗口“非分层”时才优先使用 Win11 官方 DWMWA_SYSTEMBACKDROP_TYPE（Mica/Acrylic）。
                if (!this.AllowsTransparency)
                {
                    // 1. 先尝试 Win11 22H2+ 官方亚克力/Mica API（仅在非分层窗口下安全）
                    int acrylic = 2; // DWMSBT_TRANSIENTWINDOW
                    if (DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref acrylic, sizeof(int)) == 0)
                        return;
                }

                // 2. 分层窗口 / Win10 用 SetWindowCompositionAttribute + BLURBEHIND，比 DwmEnableBlurBehindWindow 更模糊
                int size = Marshal.SizeOf(typeof(ACCENT_POLICY));
                IntPtr ptr = Marshal.AllocHGlobal(size);
                try
                {
                    var accent = new ACCENT_POLICY
                    {
                        // 回滚：BLURBEHIND(3) 普通模糊在 Win10 上稳定且开销小（用户验证过不卡）；
                        // ACRYLICBLURBEHIND(4) 在这台机器上不生效（变白）且拖动卡。
                        AccentState = ACCENT_STATE.ACCENT_ENABLE_BLURBEHIND,
                        AccentFlags = 2,
                        GradientColor = 0x00FFFFFF, // 透明着色，让系统保留默认模糊强度
                        AnimationId = 0
                    };
                    Marshal.StructureToPtr(accent, ptr, false);
                    var data = new WINDOWCOMPOSITIONATTRIBDATA
                    {
                        Attribute = WINDOWCOMPOSITIONATTRIB.WCA_ACCENT_POLICY,
                        Data = ptr,
                        SizeOfData = size
                    };
                    if (SetWindowCompositionAttribute(hwnd, ref data) == 0)
                        return;
                }
                finally
                {
                    Marshal.FreeHGlobal(ptr);
                }

                // 3. 最后回退到 Win7/10 的 DwmEnableBlurBehindWindow
                var bb = new DWM_BLURBEHIND
                {
                    dwFlags = 1,
                    fEnable = true,
                    hRgnBlur = IntPtr.Zero,
                    fTransitionOnMaximized = false
                };
                DwmEnableBlurBehindWindow(hwnd, ref bb);
            }
            catch { }
        }

        private void DisableAcrylicBlur()
        {
            try
            {
                IntPtr hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero) return;

                // 关闭 Win11 背景类型
                int none = 0; // DWMSBT_NONE
                DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref none, sizeof(int));

                // 关闭 SetWindowCompositionAttribute 模糊
                int size = Marshal.SizeOf(typeof(ACCENT_POLICY));
                IntPtr ptr = Marshal.AllocHGlobal(size);
                try
                {
                    var accent = new ACCENT_POLICY
                    {
                        AccentState = ACCENT_STATE.ACCENT_DISABLED,
                        AccentFlags = 0,
                        GradientColor = 0,
                        AnimationId = 0
                    };
                    Marshal.StructureToPtr(accent, ptr, false);
                    var data = new WINDOWCOMPOSITIONATTRIBDATA
                    {
                        Attribute = WINDOWCOMPOSITIONATTRIB.WCA_ACCENT_POLICY,
                        Data = ptr,
                        SizeOfData = size
                    };
                    SetWindowCompositionAttribute(hwnd, ref data);
                }
                finally
                {
                    Marshal.FreeHGlobal(ptr);
                }

                // 关闭 DwmEnableBlurBehindWindow
                var bb = new DWM_BLURBEHIND
                {
                    dwFlags = 1,
                    fEnable = false,
                    hRgnBlur = IntPtr.Zero,
                    fTransitionOnMaximized = false
                };
                DwmEnableBlurBehindWindow(hwnd, ref bb);
            }
            catch { }
        }

        // 彩蛋解锁后右下角轻提示
        private void ShowToast(string message)
        {
            try
            {
                if (_toastPopup == null)
                {
                    _toastPopup = new Popup
                    {
                        AllowsTransparency = true,
                        StaysOpen = false,
                        PopupAnimation = PopupAnimation.Fade,
                        PlacementTarget = this,
                        Placement = PlacementMode.Relative
                    };
                    _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.4) };
                    _toastTimer.Tick += (s, ev) =>
                    {
                        _toastTimer.Stop();
                        if (_toastPopup != null) _toastPopup.IsOpen = false;
                    };
                }

                var border = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(235, 32, 32, 38)),
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(16, 10, 16, 10),
                    Effect = new DropShadowEffect { BlurRadius = 18, ShadowDepth = 0, Opacity = 0.35 }
                };
                border.Child = new TextBlock
                {
                    Text = message,
                    Foreground = Brushes.White,
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold
                };
                _toastPopup.Child = border;

                double w = this.ActualWidth > 0 ? this.ActualWidth : this.Width;
                double h = this.ActualHeight > 0 ? this.ActualHeight : this.Height;
                _toastPopup.HorizontalOffset = w / 2 - 110;
                _toastPopup.VerticalOffset = h - 90;
                _toastPopup.IsOpen = true;

                _toastTimer.Stop();
                _toastTimer.Start();
            }
            catch { }
        }

        // 彩蛋2「千万别点」：每次点击随机触发一种恶作剧动画
        private void BtnNeverClick_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 按钮文字随机变
                var texts = new[] { "点我", "再点", "别点了", "手滑？", "不听劝" };
                if (BtnNeverClick != null) BtnNeverClick.Content = texts[_prankRandom.Next(texts.Length)];

                // 停止上一次的动画
                StopPrank();

                int effect = _prankRandom.Next(8);
                switch (effect)
                {
                    case 0: PrankShake(); break;
                    case 1: PrankScaleUp(); break;
                    case 2: PrankScaleDown(); break;
                    case 3: PrankRotate(); break;
                    case 4: PrankSkew(); break;
                    case 5: PrankMirror(); break;
                    case 6: PrankMoveAround(); break;
                    case 7: PrankBounce(); break;
                }
            }
            catch { }
        }

        private void StopPrank()
        {
            try
            {
                if (_prankStoryboard != null)
                {
                    _prankStoryboard.Stop();
                    _prankStoryboard = null;
                }
                if (WindowFrame != null)
                {
                    WindowFrame.RenderTransform = null;
                    WindowFrame.RenderTransformOrigin = new Point(0.5, 0.5);
                }
            }
            catch { }
        }

        // 抖动
        private void PrankShake()
        {
            if (WindowFrame == null) return;
            var trans = new TranslateTransform();
            WindowFrame.RenderTransform = trans;
            var sb = new Storyboard();
            var anim = new DoubleAnimationUsingKeyFrames();
            int steps = 12;
            for (int i = 0; i <= steps; i++)
            {
                double v = 0;
                if (i > 0 && i < steps)
                {
                    v = (i % 2 == 1) ? (_prankRandom.Next(10, 18)) : -_prankRandom.Next(10, 18);
                    if (i % 3 == 0) v = (v > 0) ? -v : v; // 随机反相
                }
                anim.KeyFrames.Add(new LinearDoubleKeyFrame(v, KeyTime.FromPercent((double)i / steps)));
            }
            Storyboard.SetTarget(anim, trans);
            Storyboard.SetTargetProperty(anim, new PropertyPath(TranslateTransform.XProperty));
            sb.Children.Add(anim);
            sb.Completed += (s, e) => StopPrank();
            sb.Duration = TimeSpan.FromSeconds(0.75);
            sb.Begin();
            _prankStoryboard = sb;
        }

        // 放大
        private void PrankScaleUp()
        {
            if (WindowFrame == null) return;
            var trans = new ScaleTransform(1, 1);
            WindowFrame.RenderTransform = trans;
            var sb = new Storyboard();
            var animX = new DoubleAnimation(1.0, 1.28, TimeSpan.FromSeconds(0.18))
            {
                AutoReverse = true,
                RepeatBehavior = new RepeatBehavior(2),
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.4 }
            };
            var animY = new DoubleAnimation(1.0, 1.28, TimeSpan.FromSeconds(0.18))
            {
                AutoReverse = true,
                RepeatBehavior = new RepeatBehavior(2),
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.4 }
            };
            Storyboard.SetTarget(animX, trans);
            Storyboard.SetTarget(animY, trans);
            Storyboard.SetTargetProperty(animX, new PropertyPath(ScaleTransform.ScaleXProperty));
            Storyboard.SetTargetProperty(animY, new PropertyPath(ScaleTransform.ScaleYProperty));
            sb.Children.Add(animX);
            sb.Children.Add(animY);
            sb.Completed += (s, e) => StopPrank();
            sb.Begin();
            _prankStoryboard = sb;
        }

        // 缩小
        private void PrankScaleDown()
        {
            if (WindowFrame == null) return;
            var trans = new ScaleTransform(1, 1);
            WindowFrame.RenderTransform = trans;
            var sb = new Storyboard();
            var animX = new DoubleAnimation(1.0, 0.62, TimeSpan.FromSeconds(0.18))
            {
                AutoReverse = true,
                RepeatBehavior = new RepeatBehavior(2),
                EasingFunction = new ElasticEase { EasingMode = EasingMode.EaseOut, Oscillations = 1, Springiness = 6 }
            };
            var animY = new DoubleAnimation(1.0, 0.62, TimeSpan.FromSeconds(0.18))
            {
                AutoReverse = true,
                RepeatBehavior = new RepeatBehavior(2),
                EasingFunction = new ElasticEase { EasingMode = EasingMode.EaseOut, Oscillations = 1, Springiness = 6 }
            };
            Storyboard.SetTarget(animX, trans);
            Storyboard.SetTarget(animY, trans);
            Storyboard.SetTargetProperty(animX, new PropertyPath(ScaleTransform.ScaleXProperty));
            Storyboard.SetTargetProperty(animY, new PropertyPath(ScaleTransform.ScaleYProperty));
            sb.Children.Add(animX);
            sb.Children.Add(animY);
            sb.Completed += (s, e) => StopPrank();
            sb.Begin();
            _prankStoryboard = sb;
        }

        // 旋转
        private void PrankRotate()
        {
            if (WindowFrame == null) return;
            var trans = new RotateTransform(0);
            WindowFrame.RenderTransform = trans;
            var sb = new Storyboard();
            var anim = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(0.9))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTarget(anim, trans);
            Storyboard.SetTargetProperty(anim, new PropertyPath(RotateTransform.AngleProperty));
            sb.Children.Add(anim);
            sb.Completed += (s, e) => StopPrank();
            sb.Begin();
            _prankStoryboard = sb;
        }

        // 变身（倾斜）
        private void PrankSkew()
        {
            if (WindowFrame == null) return;
            var trans = new SkewTransform(0, 0);
            WindowFrame.RenderTransform = trans;
            var sb = new Storyboard();
            var animX = new DoubleAnimation(0, 22, TimeSpan.FromSeconds(0.2))
            {
                AutoReverse = true,
                RepeatBehavior = new RepeatBehavior(3)
            };
            var animY = new DoubleAnimation(0, 12, TimeSpan.FromSeconds(0.2))
            {
                AutoReverse = true,
                RepeatBehavior = new RepeatBehavior(3)
            };
            Storyboard.SetTarget(animX, trans);
            Storyboard.SetTarget(animY, trans);
            Storyboard.SetTargetProperty(animX, new PropertyPath(SkewTransform.AngleXProperty));
            Storyboard.SetTargetProperty(animY, new PropertyPath(SkewTransform.AngleYProperty));
            sb.Children.Add(animX);
            sb.Children.Add(animY);
            sb.Completed += (s, e) => StopPrank();
            sb.Begin();
            _prankStoryboard = sb;
        }

        // 镜像翻转
        private void PrankMirror()
        {
            if (WindowFrame == null) return;
            var trans = new ScaleTransform(1, 1);
            WindowFrame.RenderTransform = trans;
            var sb = new Storyboard();
            var animX = new DoubleAnimation(1, -1, TimeSpan.FromSeconds(0.25))
            {
                AutoReverse = true,
                RepeatBehavior = new RepeatBehavior(2)
            };
            Storyboard.SetTarget(animX, trans);
            Storyboard.SetTargetProperty(animX, new PropertyPath(ScaleTransform.ScaleXProperty));
            sb.Children.Add(animX);
            sb.Completed += (s, e) => StopPrank();
            sb.Begin();
            _prankStoryboard = sb;
        }

        // 乱动：在桌面上随机瞬移几个位置
        private void PrankMoveAround()
        {
            EnsureNormalWindowPosition();
            double startX = this.Left;
            double startY = this.Top;
            var sb = new Storyboard();
            var leftKf = new DoubleAnimationUsingKeyFrames();
            var topKf = new DoubleAnimationUsingKeyFrames();
            int steps = 6;
            var wa = SystemParameters.WorkArea;
            double maxX = Math.Max(0, wa.Width - this.Width);
            double maxY = Math.Max(0, wa.Height - this.Height);
            for (int i = 0; i <= steps; i++)
            {
                double x, y;
                if (i == 0 || i == steps)
                {
                    x = startX;
                    y = startY;
                }
                else
                {
                    x = wa.Left + _prankRandom.NextDouble() * maxX;
                    y = wa.Top + _prankRandom.NextDouble() * maxY;
                }
                leftKf.KeyFrames.Add(new LinearDoubleKeyFrame(x, KeyTime.FromPercent((double)i / steps)));
                topKf.KeyFrames.Add(new LinearDoubleKeyFrame(y, KeyTime.FromPercent((double)i / steps)));
            }
            Storyboard.SetTarget(leftKf, this);
            Storyboard.SetTarget(topKf, this);
            Storyboard.SetTargetProperty(leftKf, new PropertyPath(Window.LeftProperty));
            Storyboard.SetTargetProperty(topKf, new PropertyPath(Window.TopProperty));
            sb.Children.Add(leftKf);
            sb.Children.Add(topKf);
            sb.Duration = TimeSpan.FromSeconds(1.2);
            sb.Completed += (s, e) => _prankStoryboard = null;
            sb.Begin();
            _prankStoryboard = sb;
        }

        // 在桌面上弹来弹去：弹跳轨迹
        private void PrankBounce()
        {
            EnsureNormalWindowPosition();
            double startX = this.Left;
            double startY = this.Top;
            var sb = new Storyboard();
            var leftKf = new DoubleAnimationUsingKeyFrames();
            var topKf = new DoubleAnimationUsingKeyFrames();
            var wa = SystemParameters.WorkArea;
            double maxX = Math.Max(0, wa.Width - this.Width);
            double maxY = Math.Max(0, wa.Height - this.Height);
            // 抛物线弹跳点
            double[] xs = { 0, 0.20, 0.45, 0.65, 0.85, 1.0 };
            double[] ys = { 0, 0.55, 0.15, 0.70, 0.30, 0.0 };
            for (int i = 0; i < xs.Length; i++)
            {
                double x = startX + (xs[i] == 0 || xs[i] == 1.0 ? 0 : (_prankRandom.NextDouble() - 0.5) * 2 * maxX * 0.6);
                double y = startY + (ys[i] == 0 ? 0 : ys[i] * maxY);
                x = Math.Max(wa.Left, Math.Min(wa.Left + maxX, x));
                y = Math.Max(wa.Top, Math.Min(wa.Top + maxY, y));
                leftKf.KeyFrames.Add(new LinearDoubleKeyFrame(x, KeyTime.FromPercent(xs[i])));
                topKf.KeyFrames.Add(new LinearDoubleKeyFrame(y, KeyTime.FromPercent(xs[i])));
            }
            Storyboard.SetTarget(leftKf, this);
            Storyboard.SetTarget(topKf, this);
            Storyboard.SetTargetProperty(leftKf, new PropertyPath(Window.LeftProperty));
            Storyboard.SetTargetProperty(topKf, new PropertyPath(Window.TopProperty));
            sb.Children.Add(leftKf);
            sb.Children.Add(topKf);
            sb.Duration = TimeSpan.FromSeconds(1.6);
            sb.Completed += (s, e) => _prankStoryboard = null;
            sb.Begin();
            _prankStoryboard = sb;
        }

        private void EnsureNormalWindowPosition()
        {
            if (this.WindowState != WindowState.Normal)
                this.WindowState = WindowState.Normal;
            // 确保 Left/Top 有数值，否则从当前位置读取
            if (double.IsNaN(this.Left) || double.IsNaN(this.Top))
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd != IntPtr.Zero)
                {
                    GetWindowRect(hwnd, out RECT rc);
                    this.Left = rc.Left;
                    this.Top = rc.Top;
                }
            }
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private void InitializePages()
        {
            _pages = new Dictionary<string, FrameworkElement>
            {
                { "HomePage", HomePage },
                { "BuiltInPage", BuiltInPage },
                { "AppPage", AppPage },
                { "PackagesPage", PackagesPage },
                { "SettingsPage", SettingsPage }
            };
            _navButtons = new Dictionary<string, Button>
            {
                { "HomePage", NavHome },
                { "BuiltInPage", NavBuiltIn },
                { "AppPage", NavApp },
                { "PackagesPage", NavPackages },
                { "SettingsPage", NavSettings }
            };
        }

        private FrameworkElement _currentToolView;

        // 加载主页（系统总览）
        private void LoadHomePageView()
        {
            if (HomePage == null) return;
            if (HomePage.Content is HomePage)
            {
                return;
            }
            var view = new HomePage(
                isDarkThemeGetter: () => isDarkTheme,
                colorKeyGetter: () => _colorKey,
                appearanceModeGetter: () => _appearanceMode,
                currentThemeNameGetter: () => _currentThemeName,
                customThemePathGetter: () => _customThemePath);
            HomePage.Content = view;
        }

        private void EnsureDefaultResources()
        {
            var res = Application.Current.Resources;
            if (!res.Contains("WindowCornerRadius"))
                res["WindowCornerRadius"] = new CornerRadius(24);
            if (!res.Contains("WindowBorderThickness"))
                res["WindowBorderThickness"] = new Thickness(0);
            if (!res.Contains("WindowBorderBrush"))
                res["WindowBorderBrush"] = new SolidColorBrush(Colors.Transparent);
            if (!res.Contains("ToolTileWidth"))
                res["ToolTileWidth"] = 246.0;
            if (!res.Contains("ToolTileHeight"))
                res["ToolTileHeight"] = 92.0;
            if (!res.Contains("ToolTileCornerRadius"))
                res["ToolTileCornerRadius"] = new CornerRadius(14);
        }

        private void ResetToDefaultResources()
        {
            var res = Application.Current.Resources;
            res["WindowCornerRadius"] = new CornerRadius(24);
            res["WindowBorderThickness"] = new Thickness(0);
            res["WindowBorderBrush"] = new SolidColorBrush(Colors.Transparent);
            res["ToolTileWidth"] = 246.0;
            res["ToolTileHeight"] = 92.0;
            res["ToolTileCornerRadius"] = new CornerRadius(14);
            ClearWallpaper();
        }

        private void SwitchPage(string pageName, Button navButton)
        {
            if (_currentPage != null) _currentPage.Visibility = Visibility.Collapsed;
            if (ToolHost != null) ToolHost.Visibility = Visibility.Collapsed;
            if (_currentNavButton != null) _currentNavButton.Style = (Style)Application.Current.Resources["SideNavItem"];

            // 切回主页时加载 HomePage 内容（懒加载）
            if (pageName == "HomePage") LoadHomePageView();

            // 切回内置工具页时退出工具内嵌
            if (pageName == "BuiltInPage" && ToolHost != null)
            {
                ToolHost.Content = null;
                _currentToolView = null;
            }

            _currentPage = _pages[pageName];
            _currentPage.Visibility = Visibility.Visible;
            AnimatePageIn(_currentPage);

            _currentNavButton = navButton;
            _currentNavButton.Style = (Style)Application.Current.Resources["SideNavItemActive"];

            SearchBoxContainer.Visibility = (pageName == "SettingsPage" || pageName == "HomePage") ? Visibility.Collapsed : Visibility.Visible;

            // 首次进入设置页时懒加载图标库（避免启动开销）
            if (pageName == "SettingsPage")
            {
                LoadIconPackCategories();
                if (_devModeUnlocked) try { InitializeDevMode(); } catch { }
            }

            SearchBox.Text = "";
            SearchPlaceholder.Visibility = Visibility.Visible;
        }

        // 在 ToolHost 中显示内嵌工具
        private void ShowTool(FrameworkElement toolView, string title)
        {
            if (_currentPage != null) _currentPage.Visibility = Visibility.Collapsed;
            if (_currentNavButton != null) _currentNavButton.Style = (Style)Application.Current.Resources["SideNavItem"];

            // 取消激活所有导航按钮（工具内嵌不属于任何导航分类）
            _currentNavButton = null;

            ToolHost.Content = toolView;
            ToolHost.Visibility = Visibility.Visible;
            _currentToolView = toolView;
            // 新打开的工具立即套用当前主题，避免首帧空白图标
            if (toolView is IThemeAware ta) ta.RefreshTheme(isDarkTheme);
            AnimatePageIn(ToolHost);

            SearchBoxContainer.Visibility = Visibility.Collapsed;
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    var lines = File.ReadAllLines(SettingsFile);
                    bool hasColor = false;
                    foreach (var line in lines)
                    {
                        var idx = line.IndexOf('=');
                        if (idx <= 0) continue;
                        var key = line.Substring(0, idx).Trim();
                        var value = line.Substring(idx + 1).Trim();
                        if (key.Equals("ThemeColor", StringComparison.OrdinalIgnoreCase))
                        {
                            _colorKey = value;
                            hasColor = true;
                        }
                        else if (key.Equals("AppearanceMode", StringComparison.OrdinalIgnoreCase))
                            _appearanceMode = value;
                        else if (key.Equals("Animations", StringComparison.OrdinalIgnoreCase))
                            bool.TryParse(value, out _animationsEnabled);
                        else if (key.Equals("LastDarkTheme", StringComparison.OrdinalIgnoreCase))
                            _lastDarkTheme = value;
                        else if (key.Equals("CustomThemePath", StringComparison.OrdinalIgnoreCase))
                            _customThemePath = value;
                        else if (key.Equals("EasterEggCm666", StringComparison.OrdinalIgnoreCase))
                            bool.TryParse(value, out _cm666Unlocked);
                        else if (key.Equals("EasterEggAcrylic", StringComparison.OrdinalIgnoreCase))
                            bool.TryParse(value, out _acrylicMode);
                        else if (key.Equals("EasterEggNeverClick", StringComparison.OrdinalIgnoreCase))
                            bool.TryParse(value, out _neverClickUnlocked);
                        else if (key.Equals("DevModeUnlocked", StringComparison.OrdinalIgnoreCase))
                            bool.TryParse(value, out _devModeUnlocked);
                        else if (key.StartsWith("DevIcon:", StringComparison.OrdinalIgnoreCase))
                            _devIconOverrides[key.Substring(8)] = value;
                        else if (key.StartsWith("DevText:", StringComparison.OrdinalIgnoreCase))
                            _devTextOverrides[key.Substring(8)] = value;
                        else if (key.StartsWith("DevColor:", StringComparison.OrdinalIgnoreCase))
                            _devColorOverrides[key.Substring(9)] = value;
                        else if (key.Equals("Theme", StringComparison.OrdinalIgnoreCase) && !hasColor)
                            MigrateOldTheme(value); // 旧版 Theme=DarkXxx / Light
                    }
                }
                else if (File.Exists(ThemeFile))
                {
                    // 兼容旧版 theme.txt
                    MigrateOldTheme(File.ReadAllText(ThemeFile).Trim());
                }
            }
            catch { }

            if (string.IsNullOrEmpty(_colorKey)) _colorKey = "Default";
            if (string.IsNullOrEmpty(_appearanceMode)) _appearanceMode = "Auto";
            if (string.IsNullOrEmpty(_lastDarkTheme)) _lastDarkTheme = "Dark";
        }

        // 旧版主题键（DarkBlue / LightGreen / Dark / Light）→ 配色 + 模式
        private void MigrateOldTheme(string old)
        {
            old = (old ?? string.Empty).Trim();
            if (old.Equals("Light", StringComparison.OrdinalIgnoreCase)) { _colorKey = "Default"; _appearanceMode = "Light"; }
            else if (old.Equals("Dark", StringComparison.OrdinalIgnoreCase)) { _colorKey = "Default"; _appearanceMode = "Dark"; }
            else if (old.StartsWith("Light", StringComparison.OrdinalIgnoreCase)) { _colorKey = old.Substring(5); _appearanceMode = "Light"; }
            else if (old.StartsWith("Dark", StringComparison.OrdinalIgnoreCase)) { _colorKey = old.Substring(4); _appearanceMode = "Dark"; }
            else { _colorKey = "Default"; _appearanceMode = "Auto"; }
        }

        private void SaveSettings()
        {
            try
            {
                var lines = new List<string>
                {
                    $"ThemeColor={_colorKey}",
                    $"AppearanceMode={_appearanceMode}",
                    $"Animations={_animationsEnabled}",
                    $"LastDarkTheme={_lastDarkTheme}"
                };
                if (!string.IsNullOrEmpty(_customThemePath))
                    lines.Add($"CustomThemePath={_customThemePath}");
                lines.Add($"EasterEggCm666={_cm666Unlocked}");
                lines.Add($"EasterEggAcrylic={_acrylicMode}");
                lines.Add($"EasterEggNeverClick={_neverClickUnlocked}");
                // 开发者模式：解锁状态 + 3 类覆盖
                lines.Add($"DevModeUnlocked={_devModeUnlocked}");
                foreach (var kv in _devIconOverrides)
                    lines.Add($"DevIcon:{kv.Key}={kv.Value}");
                foreach (var kv in _devTextOverrides)
                    lines.Add($"DevText:{kv.Key}={kv.Value}");
                foreach (var kv in _devColorOverrides)
                    lines.Add($"DevColor:{kv.Key}={kv.Value}");
                File.WriteAllLines(SettingsFile, lines);
            }
            catch { }
        }

        private void LoadTheme()
        {
            if (_currentThemeName.Equals("Custom", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(_customThemePath) && File.Exists(_customThemePath))
            {
                string extractDir = System.IO.Path.GetDirectoryName(_customThemePath);
                ApplyCustomTheme(_customThemePath, extractDir, writeFile: false);
            }
            else
            {
                ApplyResolvedTheme(writeFile: false);
            }
        }

        private void BtnTheme_Click(object sender, RoutedEventArgs e)
        {
            // 标题栏按钮：在深色 / 浅色之间显式切换（脱离自动）
            _appearanceMode = isDarkTheme ? "Light" : "Dark";
            ApplyResolvedTheme();
        }

        // 根据「配色 + 外观模式」解析并应用最终主题
        private void ApplyResolvedTheme(bool writeFile = true)
        {
            // 选择内置主题即视为退出自定义主题
            if (!string.IsNullOrEmpty(_customThemePath)) _customThemePath = null;

            isDarkTheme = ResolveIsDark();
            string prefix = isDarkTheme ? "Dark" : "Light";
            string resolved = (string.IsNullOrEmpty(_colorKey) || _colorKey == "Default") ? prefix : (prefix + _colorKey);
            if (!_builtInThemePaths.ContainsKey(resolved))
                resolved = isDarkTheme ? "Dark" : "Light";

            _currentThemeName = resolved;
            if (isDarkTheme) _lastDarkTheme = resolved;

            ChangeTheme(_builtInThemePaths[resolved]);
            UpdateThemeIcons();
            if (writeFile) SaveSettings();
            LoadAndBuildTools();
            ApplySearchFilter();
            UpdateSettingsUI();
            if (_acrylicMode) ApplyAcrylicBackground(); // 切换主题后保持亚克力
        }

        // 自动模式：读取系统「应用使用浅色主题」(0=深色)
        private bool ResolveIsDark()
        {
            if (_appearanceMode.Equals("Dark", StringComparison.OrdinalIgnoreCase)) return true;
            if (_appearanceMode.Equals("Light", StringComparison.OrdinalIgnoreCase)) return false;
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
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

        private void ApplyCustomTheme(string xamlPath, string extractDir = null, bool writeFile = true)
        {
            try
            {
                using (var stream = File.OpenRead(xamlPath))
                {
                    var dict = XamlReader.Load(stream) as ResourceDictionary;
                    if (dict != null)
                    {
                        ApplyResourceDictionary(dict);
                        _customThemePath = xamlPath;
                        _currentThemeName = "Custom";
                        if (!string.IsNullOrEmpty(extractDir))
                            ApplyThemeConfig(extractDir);
                        if (writeFile) SaveSettings();
                        UpdateThemeIcons();
                        LoadAndBuildTools();
                        ApplySearchFilter();
                        UpdateSettingsUI();
                        if (_acrylicMode) ApplyAcrylicBackground(); // 切换主题后保持亚克力
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载自定义主题失败：{ex.Message}", "主题错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ChangeTheme(string themePath)
        {
            ResourceDictionary newTheme;
            if (themePath.StartsWith("Themes/", StringComparison.OrdinalIgnoreCase)
                || themePath.StartsWith("Themes\\", StringComparison.OrdinalIgnoreCase))
            {
                // 内置主题从嵌入资源加载（单文件运行）
                string file = System.IO.Path.GetFileName(themePath);
                var uri = new Uri("pack://application:,,,/Toolbox;component/Themes/" + file, UriKind.Absolute);
                newTheme = new ResourceDictionary { Source = uri };
            }
            else
            {
                // 自定义主题：从磁盘加载
                newTheme = new ResourceDictionary { Source = new Uri(themePath, UriKind.Relative) };
            }
            Application.Current.Resources.MergedDictionaries.RemoveAt(0);
            Application.Current.Resources.MergedDictionaries.Insert(0, newTheme);
            ResetToDefaultResources();
            AnimateThemeTransition();
        }

        private void ApplyResourceDictionary(ResourceDictionary dict)
        {
            Application.Current.Resources.MergedDictionaries.RemoveAt(0);
            Application.Current.Resources.MergedDictionaries.Insert(0, dict);
            ResetToDefaultResources();
            AnimateThemeTransition();
        }

        private void ApplyThemeConfig(string extractDir)
        {
            string jsonPath = System.IO.Path.Combine(extractDir, "Theme.json");
            if (!File.Exists(jsonPath)) return;
            try
            {
                var cfg = ParseSimpleJson(File.ReadAllText(jsonPath));
                var res = Application.Current.Resources;

                if (cfg.ContainsKey("WindowCornerRadius") && double.TryParse(cfg["WindowCornerRadius"], out double wc))
                    res["WindowCornerRadius"] = new CornerRadius(wc);
                if (cfg.ContainsKey("WindowBorderThickness") && double.TryParse(cfg["WindowBorderThickness"], out double wt))
                    res["WindowBorderThickness"] = new Thickness(wt);
                if (cfg.ContainsKey("WindowBorderColor"))
                {
                    var c = ParseColor(cfg["WindowBorderColor"]);
                    res["WindowBorderBrush"] = new SolidColorBrush(c);
                }
                if (cfg.ContainsKey("ButtonWidth") && double.TryParse(cfg["ButtonWidth"], out double bw))
                    res["ToolTileWidth"] = bw;
                if (cfg.ContainsKey("ButtonHeight") && double.TryParse(cfg["ButtonHeight"], out double bh))
                    res["ToolTileHeight"] = bh;
                if (cfg.ContainsKey("ButtonCornerRadius") && double.TryParse(cfg["ButtonCornerRadius"], out double bcr))
                    res["ToolTileCornerRadius"] = new CornerRadius(bcr);

                if (cfg.ContainsKey("HasWallpaper") && cfg["HasWallpaper"].Equals("true", StringComparison.OrdinalIgnoreCase))
                {
                    string wp = FindWallpaperFile(extractDir);
                    if (!string.IsNullOrEmpty(wp)) ApplyWallpaper(wp);
                }
            }
            catch { }
        }

        private string FindWallpaperFile(string extractDir)
        {
            foreach (var ext in new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif" })
            {
                string path = System.IO.Path.Combine(extractDir, "Wallpaper" + ext);
                if (File.Exists(path)) return path;
            }
            return null;
        }

        private Color ParseColor(string hex)
        {
            try
            {
                var s = hex.Trim();
                if (s.StartsWith("#")) s = s.Substring(1);
                if (s.Length == 6)
                {
                    byte r = Convert.ToByte(s.Substring(0, 2), 16);
                    byte g = Convert.ToByte(s.Substring(2, 2), 16);
                    byte b = Convert.ToByte(s.Substring(4, 2), 16);
                    return Color.FromRgb(r, g, b);
                }
                if (s.Length == 8)
                {
                    byte a = Convert.ToByte(s.Substring(0, 2), 16);
                    byte r = Convert.ToByte(s.Substring(2, 2), 16);
                    byte g = Convert.ToByte(s.Substring(4, 2), 16);
                    byte b = Convert.ToByte(s.Substring(6, 2), 16);
                    return Color.FromArgb(a, r, g, b);
                }
            }
            catch { }
            return Colors.Transparent;
        }

        private Dictionary<string, string> ParseSimpleJson(string json)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var lines = json.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var idx = line.IndexOf(':');
                if (idx <= 0) continue;
                var key = line.Substring(0, idx).Trim().Trim('"', ' ', '\t');
                var value = line.Substring(idx + 1).Trim().Trim(',', '"', ' ', '\t');
                if (!string.IsNullOrEmpty(key)) result[key] = value;
            }
            return result;
        }

        private void ApplyWallpaper(string path)
        {
            try
            {
                var brush = new ImageBrush(new BitmapImage(new Uri(path, UriKind.Absolute)))
                {
                    Stretch = Stretch.UniformToFill,
                    Opacity = 0.35
                };
                WindowFrame.Background = brush;
            }
            catch { }
        }

        private void ClearWallpaper()
        {
            if (WindowFrame != null)
                WindowFrame.Background = FindResource("BackgroundBrush") as Brush;
        }

        // 把分类图标覆盖值（如 "9-媒体/芯片"、"9-媒体/芯片.png"、"芯片"）解析为 IconPack svg 相对路径。
        // 找不到对应 svg 时返回 null，调用方会回退到 Icons 内置 PNG。
        private string ResolveIconPackRel(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            string tryRel = name.Trim();
            if (!tryRel.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
            {
                if (tryRel.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    tryRel = tryRel.Substring(0, tryRel.Length - 4) + ".svg";
                else
                    tryRel = tryRel + ".svg";
            }
            foreach (var rel in IconPackManifest.AllFiles)
                if (string.Equals(rel, tryRel, StringComparison.OrdinalIgnoreCase)) return rel;
            // 短名/分类路径不完全匹配时，按文件名匹配（如 "芯片" → "9-媒体/芯片.svg"）
            string file = System.IO.Path.GetFileName(tryRel);
            foreach (var rel in IconPackManifest.AllFiles)
                if (rel.EndsWith("/" + file, StringComparison.OrdinalIgnoreCase)) return rel;
            return null;
        }

        // 把 IconPack SVG 渲染成 ImageSource（单色），默认按当前主题文字色，可传 brush 覆写为白色等
        private ImageSource SvgToImageSource(string rel, Brush brush = null)
        {
            try
            {
                var geo = SvgGeometryLoader.LoadGeometry(rel);
                if (geo == null) return null;
                var b = brush ?? (Brush)FindResource("TextPrimaryBrush");
                var drawing = new GeometryDrawing(b, null, geo);
                return new DrawingImage(drawing);
            }
            catch { return null; }
        }

        private ImageSource GetThemeIcon(string fileName, string forceSet = null)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return LoadEmbeddedIcon(isDarkTheme ? "White" : "Black", "file.png");
            // IconPack 单色 SVG：直接渲染成 ImageSource（不参与选中态变色，用于导航/磁贴/dev 覆盖）
            if (fileName.Contains("/") || fileName.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
            {
                string rel = ResolveIconPackRel(fileName);
                if (rel != null)
                {
                    var svg = SvgToImageSource(rel);
                    if (svg != null) return svg;
                }
            }
            string set = forceSet ?? (isDarkTheme ? "White" : "Black");
            string other = set == "White" ? "Black" : "White";
            if (EmbeddedIconExists(set, fileName)) return LoadEmbeddedIcon(set, fileName);
            if (EmbeddedIconExists(other, fileName)) return LoadEmbeddedIcon(other, fileName);
            return LoadEmbeddedIcon(set, "file.png");
        }

        // 从嵌入资源（Icons/White、Icons/Black）读取 png 图标，实现单文件运行
        private BitmapImage LoadEmbeddedIcon(string set, string fileName)
        {
            var uri = new Uri(string.Format("pack://application:,,,/Toolbox;component/Icons/{0}/{1}", set, fileName), UriKind.Absolute);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = uri;
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            return bmp;
        }

        private bool EmbeddedIconExists(string set, string fileName)
        {
            try
            {
                var uri = new Uri(string.Format("pack://application:,,,/Toolbox;component/Icons/{0}/{1}", set, fileName), UriKind.Absolute);
                var info = Application.GetResourceStream(uri);
                if (info != null) { using (var s = info.Stream) return true; }
                return false;
            }
            catch { return false; }
        }

        private void UpdateThemeIcons()
        {
            if (ThemeIcon != null) ThemeIcon.Source = GetThemeIcon(isDarkTheme ? "theme_moon.png" : "theme_sun.png");
            if (NavHomeIcon != null) NavHomeIcon.Source = GetThemeIcon("home.png");
            if (NavBuiltInIcon != null) NavBuiltInIcon.Source = GetThemeIcon("toolbox.png");
            if (NavAppIcon != null) NavAppIcon.Source = GetThemeIcon("app_grid.png");
            if (NavSettingsIcon != null) NavSettingsIcon.Source = GetThemeIcon("settings.png");
            if (NavPackagesIcon != null) NavPackagesIcon.Source = GetThemeIcon("file.png");
            if (SearchIcon != null) SearchIcon.Source = GetThemeIcon("search.png");
            if (SettingsThemeIcon != null) SettingsThemeIcon.Source = GetThemeIcon("theme_palette.png");
            if (SettingsDarkIcon != null) SettingsDarkIcon.Source = GetThemeIcon(isDarkTheme ? "theme_moon.png" : "theme_sun.png");
            if (SettingsAnimationIcon != null) SettingsAnimationIcon.Source = GetThemeIcon("animation.png");
            if (AboutIcon != null) AboutIcon.Source = GetThemeIcon("toolbox.png");
            if (BannerIcon != null) BannerIcon.Source = GetThemeIcon("toolbox.png", "White");
            UpdateCategoryTabIcons();
            UpdateComboBoxArrow();
            // 当前打开的工具窗口（实现 IThemeAware）也跟随主题刷新图标
            if (_currentToolView is IThemeAware ta) ta.RefreshTheme(isDarkTheme);
            // 首页（实现 IThemeAware）也跟随主题刷新横幅图标
            if (HomePage?.Content is IThemeAware ha) ha.RefreshTheme(isDarkTheme);
            // 开发者模式：覆盖图标（切主题后重新套用）
            try
            {
                foreach (var k in _devIconOverrides.Keys.ToList()) ApplyDevIconOverride(k);
                ApplyDevTextOverrides();
            }
            catch { }
        }

        // 主题切换时同步刷新 APP 分类标签的 PNG 图标
        private void UpdateCategoryTabIcons()
        {
            if (AppTabPanel == null) return;
            foreach (Button btn in AppTabPanel.Children.OfType<Button>())
            {
                var cat = btn.Tag as ToolCategory;
                if (cat == null) continue;
                var grid = btn.Content as Grid;
                var iconElement = grid?.Children.OfType<FrameworkElement>().FirstOrDefault(c => Grid.GetColumn(c) == 0);
                if (iconElement is Border b && b.Child is Image img && !string.IsNullOrEmpty(cat.IconOverride))
                    img.Source = GetThemeIcon(cat.IconOverride);
            }
        }

        private void UpdateComboBoxArrow()
        {
            // 主题配色 & 外观模式 两个下拉框共用 ModernComboBox 样式，都需要刷箭头图标
            if (ThemeSelector != null)
            {
                var arrowDown = ThemeSelector.Template?.FindName("ArrowDown", ThemeSelector) as System.Windows.Controls.Image;
                var arrowUp = ThemeSelector.Template?.FindName("ArrowUp", ThemeSelector) as System.Windows.Controls.Image;
                if (arrowDown != null) arrowDown.Source = GetThemeIcon("combobox_arrow_down.png");
                if (arrowUp != null) arrowUp.Source = GetThemeIcon("combobox_arrow_up.png");
            }
            if (ModeSelector != null)
            {
                var arrowDown = ModeSelector.Template?.FindName("ArrowDown", ModeSelector) as System.Windows.Controls.Image;
                var arrowUp = ModeSelector.Template?.FindName("ArrowUp", ModeSelector) as System.Windows.Controls.Image;
                if (arrowDown != null) arrowDown.Source = GetThemeIcon("combobox_arrow_down.png");
                if (arrowUp != null) arrowUp.Source = GetThemeIcon("combobox_arrow_up.png");
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void BtnMin_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void LeftNav_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string pageName && _pages.ContainsKey(pageName))
            {
                SwitchPage(pageName, btn);
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // 输入框有内容时一定隐藏占位符
            if (!string.IsNullOrEmpty(SearchBox.Text))
                SearchPlaceholder.Visibility = Visibility.Collapsed;
            else if (!SearchBox.IsKeyboardFocused)
                SearchPlaceholder.Visibility = Visibility.Visible;
            ApplySearchFilter();
        }

        // Bug4：聚焦时把占位符"搜索工具或功能"隐藏，避免光标看起来插在占位符中间
        private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            SearchPlaceholder.Visibility = Visibility.Collapsed;
        }

        // 失焦且无内容时，重新显示占位符
        private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (SearchBox != null && string.IsNullOrEmpty(SearchBox.Text))
                SearchPlaceholder.Visibility = Visibility.Visible;
        }

        // ============================================================
        // 数据模型
        // ============================================================
        private class ToolItem
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public string Path { get; set; }
            public string Args { get; set; }
            public string IconFileName { get; set; }
            public string IconKey { get; set; }
            public bool AutoIcon { get; set; } // 未指定 icon 时，自动从 exe 提取图标
            public bool IsBuiltIn { get; set; }
            public Action ClickAction { get; set; }
            public Brush IconColor { get; set; }
            public string CategoryName { get; set; }
            public string Architecture { get; set; } // 当前激活的架构，如 "32"/"64"/"arm64"
            public List<ArchitectureVariant> Variants { get; set; } = new List<ArchitectureVariant>();
            public int CurrentVariantIndex { get; set; } = 0; // Variants[0] 固定保存主项
            public bool IsWebLink { get; set; } // type=网页 时为真，点击用浏览器打开 path 里的网址
            public bool UseFavicon { get; set; } // 网页链接且未指定 icon 时，尝试自动显示网站 favicon
            public ImageSource FaviconImage { get; set; } // 已下载并转好的 favicon 位图（缓存，重建卡片时复用）
            public bool FaviconLoading { get; set; } // 防止对同一工具重复触发下载
        }

        // 同一工具的不同架构变体（右键循环切换）
        private class ArchitectureVariant
        {
            public string Architecture { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public string Path { get; set; }
            public string Args { get; set; }
            public string IconFileName { get; set; }
            public string IconKey { get; set; }
            public bool AutoIcon { get; set; }
        }

        // 把 type 字段/段名后缀规范化为内部 key：arm64 / 64 / 32（统一去掉「位」字、x64→64、x86→32 等）
        private static string NormalizeArch(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var s = raw.Trim().ToLowerInvariant().Replace("位", "").Replace(" ", "");
            if (s == "x64" || s == "amd64") s = "64";
            else if (s == "x86" || s == "x32" || s == "i386") s = "32";
            return s;
        }

        // 右上角标签显示：统一带「位」字，如 64 位 / 32 位 / arm64 位
        private static string ArchDisplay(string arch)
        {
            if (string.IsNullOrEmpty(arch)) return "";
            return arch + "位";
        }

        // 判断 type 字段是否表示「网页链接」（点击用浏览器打开，而非启动 exe）
        private static bool IsWebType(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return false;
            var s = raw.Trim().ToLowerInvariant();
            return s == "网页" || s == "网址" || s == "网站" || s == "web" || s == "url" || s == "link" || s == "链接";
        }

        // 可翻转（多架构）软件置顶：同一分类内，Variants.Count > 1 的排前面，组内保持原相对顺序（稳定排序）。
        private static void SortCategoryToolsPinFlippable(ToolCategory category)
        {
            if (category?.Tools == null || category.Tools.Count <= 1) return;
            category.Tools = category.Tools
                .OrderByDescending(t => t.Variants != null && t.Variants.Count > 1)
                .ToList();
        }

        private class ToolCategory
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public string IconKey { get; set; }
            public string IconOverride { get; set; }   // 自定义分类图标文件名（PNG）
            public bool IsExpanded { get; set; } = true;
            public List<ToolItem> Tools { get; set; } = new List<ToolItem>();
        }

        // ============================================================
        // 加载与生成
        // ============================================================
        private void LoadAndBuildTools(bool animate = false)
        {
            var builtIn = LoadBuiltInCategory();
            if (builtIn != null) ApplyBuiltInIconOverrides(builtIn.Tools);
            _appCategories = LoadAppCategories();

            BuiltInToolsPanel.Children.Clear();
            AppToolsPanel.Children.Clear();
            AppTabPanel.Children.Clear();

            if (builtIn != null && builtIn.Tools.Count > 0)
                BuiltInToolsPanel.Children.Add(BuildCategoryCard(builtIn));

            BuildAppTabs(_appCategories);
            SelectAppCategory(_appCategories.FirstOrDefault());

            LoadPackagesCategories();
            BuildPackagesTabs(_packagesCategories);
            SelectPackagesCategory(_packagesCategories.FirstOrDefault());
            if (animate) PlayEntrance();
        }

        private ToolCategory LoadBuiltInCategory()
        {
            return new ToolCategory
            {
                Name = "全部内置工具",
                Description = "纳米工具箱自带的功能",
                Tools = new List<ToolItem>
                {
                    new ToolItem { Name = "记事本", Description = "轻量级文本编辑工具", IconFileName = "notepad.png", IconColor = (Brush)FindResource("AccentOrangeBrush"), ClickAction = OpenNotepad, IsBuiltIn = true },
                    new ToolItem { Name = "计算器", Description = "支持四则运算与百分比", IconFileName = "calculator.png", IconColor = (Brush)FindResource("PrimaryBrush"), ClickAction = OpenCalculator, IsBuiltIn = true },
                    new ToolItem { Name = "截图工具", Description = "快速截取屏幕并标注", IconFileName = "screenshot.png", IconColor = (Brush)FindResource("AccentPurpleBrush"), ClickAction = OpenScreenshot, IsBuiltIn = true },
                    new ToolItem { Name = "文件哈希", Description = "计算文件的 MD5/SHA256 哈希值", IconFileName = "filehash.png", IconColor = (Brush)FindResource("AccentRedBrush"), ClickAction = OpenFileHash, IsBuiltIn = true },
                    new ToolItem { Name = "进程管理", Description = "查看和管理系统进程", IconFileName = "process.png", IconColor = (Brush)FindResource("AccentTealBrush"), ClickAction = OpenProcessManager, IsBuiltIn = true },
                    new ToolItem { Name = "网络 Ping", Description = "测试网络延迟和连通性", IconFileName = "ping.png", IconColor = (Brush)FindResource("PrimaryBrush"), ClickAction = OpenPingTool, IsBuiltIn = true },
                    new ToolItem { Name = "IP 信息", Description = "查看本机网络配置信息", IconFileName = "ipinfo.png", IconColor = (Brush)FindResource("AccentPurpleBrush"), ClickAction = OpenIPInfo, IsBuiltIn = true },
                    new ToolItem { Name = "文本转换", Description = "大小写转换、编码转换", IconFileName = "textconvert.png", IconColor = (Brush)FindResource("AccentGreenBrush"), ClickAction = OpenTextConverter, IsBuiltIn = true },
                    new ToolItem { Name = "单位转换", Description = "长度、面积、重量等单位转换", IconFileName = "unitconvert.png", IconColor = (Brush)FindResource("PrimaryBrush"), ClickAction = OpenUnitConverter, IsBuiltIn = true },
                    new ToolItem { Name = "多线程下载", Description = "可调线程数的分段并行下载", IconFileName = "download.png", IconColor = (Brush)FindResource("AccentBlueBrush"), ClickAction = OpenDownloader, IsBuiltIn = true },
                    new ToolItem { Name = "批量重命名", Description = "按规则批量重命名文件", IconFileName = "rename.png", IconColor = (Brush)FindResource("AccentOrangeBrush"), ClickAction = OpenBatchRename, IsBuiltIn = true },
                    new ToolItem { Name = "屏幕取色", Description = "取屏幕任意点颜色 HEX/RGB", IconFileName = "colorpicker.png", IconColor = (Brush)FindResource("AccentPurpleBrush"), ClickAction = OpenColorPicker, IsBuiltIn = true },
                    new ToolItem { Name = "二维码生成", Description = "文本/链接生成二维码", IconFileName = "qrcode.png", IconColor = (Brush)FindResource("AccentGreenBrush"), ClickAction = OpenQrCode, IsBuiltIn = true },
                    new ToolItem { Name = "Base64 转换", Description = "文本与文件 Base64 编码 / 解码", IconFileName = "notepad.png", IconColor = (Brush)FindResource("AccentPinkBrush"), ClickAction = OpenBase64, IsBuiltIn = true }
                }
            };
        }

        // 把开发者模式覆盖应用到内置工具列表（在 BuildCategoryCard / CreateToolTile 之前调用）
        private void ApplyBuiltInIconOverrides(List<ToolItem> tools)
        {
            if (tools == null || _builtInIconOverrides == null || _builtInIconOverrides.Count == 0) return;
            foreach (var t in tools)
            {
                if (_builtInIconOverrides.TryGetValue(t.Name, out var icon))
                    t.IconFileName = icon;
            }
        }

        private List<ToolCategory> LoadAppCategories()
        {
            var categories = new List<ToolCategory>();
            string appDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app");
            EnsureAppFolder(appDir);

            string configPath = System.IO.Path.Combine(appDir, AppConfigFile);
            if (!File.Exists(configPath)) return categories;

            // 支持同段名重复（用 type 字段区分架构），也支持段名末尾带架构后缀自动合并
            var sections = ReadAppSections(configPath);
            var catMap = new Dictionary<string, ToolCategory>(StringComparer.OrdinalIgnoreCase);
            _categoryIconOverrides.Clear();
            LoadThemeCategoryIcons(appDir);

            // 第一步：提取每个段的基础名与架构
            var parsed = new List<(string sectionName, string baseName, string arch, Dictionary<string, string> fields)>();
            foreach (var sec in sections)
            {
                string sectionName = sec.Key;
                foreach (var fields in sec.Value)
                {
                    if (fields.Count == 0) continue;
                    string arch = null;
                    string baseName = sectionName;
                    if (fields.ContainsKey("type"))
                    {
                        // type=网页 等表示网页链接，不参与架构合并
                        arch = IsWebType(fields["type"].Trim()) ? null : NormalizeArch(fields["type"]);
                    }
                    else
                    {
                        // 从段名末尾提取架构后缀：arm64 / x64 / x32 / x86 / 64 / 32
                        var m = Regex.Match(sectionName, @"\s*(arm64|x64|x32|x86|64|32)$", RegexOptions.IgnoreCase);
                        if (m.Success)
                        {
                            arch = NormalizeArch(m.Groups[1].Value);
                            baseName = sectionName.Substring(0, sectionName.Length - m.Value.Length).TrimEnd();
                        }
                    }
                    parsed.Add((sectionName, baseName, arch, fields));
                }
            }

            // 第二步：按基础名分组合并为一个多架构工具卡片
            var groups = parsed.GroupBy(p => p.baseName, StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var g in groups)
            {
                var first = g.First();
                var fields = first.fields;
                string sectionName = first.sectionName;

                string rawCategory = fields.ContainsKey("category") ? fields["category"] : "未分类";
                // 支持 category=名称|图标 语法：图标写 png 名即可，可不带 .png 后缀，软件内嵌 White/Black 两套自动匹配
                string categoryName = rawCategory;
                string iniIcon = null;
                int sep = rawCategory.IndexOf('|');
                if (sep >= 0)
                {
                    categoryName = rawCategory.Substring(0, sep).Trim();
                    iniIcon = rawCategory.Substring(sep + 1).Trim();
                }
                // 优先级：theme.ini 全局映射 > ini 内 |图标
                string categoryIcon = null;
                if (_themeCategoryIconOverrides.TryGetValue(categoryName, out var themeIcon) && !string.IsNullOrEmpty(themeIcon))
                    categoryIcon = themeIcon;
                else if (!string.IsNullOrEmpty(iniIcon))
                    categoryIcon = iniIcon;
                // IconPack svg 路径（含 /）自动补 .svg；PNG 或短名保持原样，由渲染层判断 png/svg
                if (!string.IsNullOrEmpty(categoryIcon) && categoryIcon.Contains("/") && !categoryIcon.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                    categoryIcon += ".svg";
                if (!catMap.ContainsKey(categoryName))
                {
                    catMap[categoryName] = new ToolCategory { Name = categoryName, IconOverride = categoryIcon };
                    if (!string.IsNullOrEmpty(categoryIcon)) _categoryIconOverrides[categoryName] = categoryIcon;
                }
                else if (!string.IsNullOrEmpty(categoryIcon) && string.IsNullOrEmpty(catMap[categoryName].IconOverride))
                {
                    catMap[categoryName].IconOverride = categoryIcon;
                    _categoryIconOverrides[categoryName] = categoryIcon;
                }

                var mainTool = LoadToolFromIniSection(sectionName, fields, categoryName, appDir);
                if (mainTool == null) continue;
                mainTool.Architecture = first.arch;

                // 同一基础名的其余条目作为架构变体
                foreach (var variant in g.Skip(1))
                {
                    var vTool = LoadToolFromIniSection(variant.sectionName, variant.fields, categoryName, appDir);
                    if (vTool == null) continue;
                    mainTool.Variants.Add(new ArchitectureVariant
                    {
                        Architecture = variant.arch,
                        Name = vTool.Name,
                        Description = vTool.Description,
                        Path = vTool.Path,
                        Args = vTool.Args,
                        IconFileName = vTool.IconFileName,
                        IconKey = vTool.IconKey,
                        AutoIcon = vTool.AutoIcon
                    });
                }

                // Variants[0] 固定保存主项本身，方便右键循环切换时统一处理
                mainTool.Variants.Insert(0, new ArchitectureVariant
                {
                    Architecture = mainTool.Architecture,
                    Name = mainTool.Name,
                    Description = mainTool.Description,
                    Path = mainTool.Path,
                    Args = mainTool.Args,
                    IconFileName = mainTool.IconFileName,
                    IconKey = mainTool.IconKey,
                    AutoIcon = mainTool.AutoIcon
                });
                mainTool.CurrentVariantIndex = 0;

                catMap[categoryName].Tools.Add(mainTool);
            }

            foreach (var c in catMap.Values)
                SortCategoryToolsPinFlippable(c);
            categories.AddRange(catMap.Values.Where(c => c.Tools.Count > 0));
            return categories;
        }

        // 读取 apps.ini，支持同段名重复出现（用于多架构工具：Hwinfo 32 / Hwinfo 64 / Hwinfo ARM64）
        private Dictionary<string, List<Dictionary<string, string>>> ReadAppSections(string path)
        {
            var result = new Dictionary<string, List<Dictionary<string, string>>>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(path)) return result;

            string section = "";
            foreach (var rawLine in File.ReadAllLines(path))
            {
                var line = rawLine.Trim();
                if (string.IsNullOrEmpty(line)) continue;
                if (line.StartsWith(";") || line.StartsWith("#")) continue;
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    section = line.Substring(1, line.Length - 2);
                    if (!result.ContainsKey(section))
                        result[section] = new List<Dictionary<string, string>>();
                    result[section].Add(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
                    continue;
                }
                int idx = line.IndexOf('=');
                if (idx > 0 && result.ContainsKey(section) && result[section].Count > 0)
                {
                    string key = line.Substring(0, idx).Trim();
                    string value = line.Substring(idx + 1).Trim();
                    result[section][result[section].Count - 1][key] = value;
                }
            }
            return result;
        }

        // 读取一个或多个目录下的 theme.ini（按传入顺序，后者覆盖前者），存为「分类名 -> 图标」映射。
        // 最高优先级，会覆盖 apps.ini / packages.ini 内 category=名称|图标 语法。
        // apps 页只传 app/；packages 页传 app/ + packages/（packages/theme.ini 优先于全局）。
        private void LoadThemeCategoryIcons(params string[] dirs)
        {
            _themeCategoryIconOverrides.Clear();
            try
            {
                foreach (var appDir in dirs)
                {
                    string themePath = System.IO.Path.Combine(appDir, ThemeConfigFile);
                    if (!File.Exists(themePath)) continue;
                    var dict = ReadIni(themePath);
                    foreach (var section in dict)
                    {
                        // 只认 [Categories] 段，其他段忽略（方便以后扩展为字体/颜色配置）
                        if (!string.Equals(section.Key, "Categories", StringComparison.OrdinalIgnoreCase)) continue;
                        foreach (var kv in section.Value)
                        {
                            string cat = kv.Key?.Trim();
                            string icon = kv.Value?.Trim();
                            if (string.IsNullOrEmpty(cat) || string.IsNullOrEmpty(icon)) continue;
                            _themeCategoryIconOverrides[cat] = icon;
                        }
                    }
                }
            }
            catch { }
        }

        // 从单个 ini 段解析一个工具
        private ToolItem LoadToolFromIniSection(string sectionName, Dictionary<string, string> fields, string categoryName, string appDir)
        {
            string toolName = fields.ContainsKey("name") ? fields["name"] : sectionName;
            string description = fields.ContainsKey("description") ? fields["description"] : "";
            string targetPath = fields.ContainsKey("path") ? fields["path"] : "";
            string args = fields.ContainsKey("args") ? fields["args"] : "";
            string iconFileName = null;
            string iconKey = null;
            bool autoIcon = false;
            bool isWeb = fields.ContainsKey("type") && IsWebType(fields["type"].Trim());

            bool useFavicon = false;
            if (isWeb && !fields.ContainsKey("icon"))
            {
                iconFileName = "互联网"; // 网页链接默认先用「互联网」图标兜底，下载到 favicon 后自动替换
                autoIcon = false;
                useFavicon = true;
            }
            else if (fields.ContainsKey("icon"))
            {
                string iconValue = fields["icon"].Trim();
                if (string.IsNullOrEmpty(iconValue))
                    autoIcon = true; // 写了 icon= 但留空，也走自动提取
                else if (iconValue.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    iconFileName = iconValue;
                else
                    iconKey = iconValue;
            }
            else
            {
                autoIcon = true; // 没写 icon 字段，自动从 exe 提取图标
            }

            // 网页链接：path 直接当作网址，不做文件解析 / 存在性检查，直接返回
            if (isWeb)
            {
                string url = targetPath?.Trim() ?? "";
                if (string.IsNullOrEmpty(url)) return null;
                return new ToolItem
                {
                    Name = toolName,
                    Description = string.IsNullOrEmpty(description) ? "网页链接" : description,
                    Path = url,
                    Args = "",
                    IconFileName = iconFileName,
                    IconKey = iconKey,
                    AutoIcon = false,
                    IconColor = (Brush)FindResource("TextSecondaryBrush"),
                    CategoryName = categoryName,
                    IsWebLink = true,
                    UseFavicon = useFavicon
                };
            }

            if (string.IsNullOrEmpty(targetPath))
            {
                // 未写 path：尝试 <段名>.exe 放在 app 目录下
                string guess = System.IO.Path.Combine(appDir, sectionName + ".exe");
                if (File.Exists(guess)) targetPath = guess;
                else return null;
            }
            else
            {
                string originalPath = targetPath;
                targetPath = ResolveToolPath(originalPath, appDir);

                // 若还是找不到，且是相对路径，尝试按分类名/段名目录找（支持 app/分类/程序.exe 结构）
                if ((string.IsNullOrEmpty(targetPath) || !File.Exists(targetPath)) && !System.IO.Path.IsPathRooted(originalPath))
                {
                    if (!string.IsNullOrEmpty(categoryName))
                    {
                        string catPath = System.IO.Path.Combine(appDir, categoryName, originalPath);
                        if (File.Exists(catPath)) targetPath = catPath;
                    }
                    if ((string.IsNullOrEmpty(targetPath) || !File.Exists(targetPath)) && !string.IsNullOrEmpty(sectionName))
                    {
                        string secPath = System.IO.Path.Combine(appDir, sectionName, originalPath);
                        if (File.Exists(secPath)) targetPath = secPath;
                    }
                }
            }

            if (string.IsNullOrEmpty(targetPath) || !File.Exists(targetPath)) return null;

            return new ToolItem
            {
                Name = toolName,
                Description = string.IsNullOrEmpty(description) ? "外部程序" : description,
                Path = targetPath,
                Args = args,
                IconFileName = iconFileName,
                IconKey = iconKey,
                AutoIcon = autoIcon,
                IconColor = (Brush)FindResource("TextSecondaryBrush"),
                CategoryName = categoryName
            };
        }

        // 解析工具路径：绝对路径直接用；相对路径先试 app 目录、再试 exe 目录
        private string ResolveToolPath(string path, string appDir)
        {
            if (System.IO.Path.IsPathRooted(path)) return path;
            string p1 = System.IO.Path.Combine(appDir, path);
            if (File.Exists(p1)) return p1;
            string p2 = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
            if (File.Exists(p2)) return p2;
            return path;
        }

        // 自动创建 app 文件夹；若首次创建（为空），放入一个示例分类 + 使用说明
        private void EnsureAppFolder(string appDir)
        {
            try
            {
                if (!Directory.Exists(appDir))
                    Directory.CreateDirectory(appDir);
                string configPath = System.IO.Path.Combine(appDir, AppConfigFile);
                if (!File.Exists(configPath))
                    CreateSampleApp(appDir, configPath);
                string themePath = System.IO.Path.Combine(appDir, ThemeConfigFile);
                if (!File.Exists(themePath))
                    CreateSampleTheme(appDir, themePath);
            }
            catch { }
        }

        // 在 app/ 下创建 theme.ini 示例（仅当文件不存在），所有项默认被注释掉，避免污染用户配置
        private void CreateSampleTheme(string appDir, string themePath)
        {
            try
            {
                File.WriteAllText(themePath,
                    "; 全局分类图标配置（app/theme.ini）\r\n" +
                    "; ---------------------------------------------------------------------------\r\n" +
                    "; 作用：把分类名映射到图标，让 apps.ini / packages.ini 只管工具配置本身，\r\n" +
                    ";       图标集中在一个地方管理，避免每个 ini 里都写 category=名称|图标 这种混写。\r\n" +
                    "; 优先级（高 → 低）：本文件 > apps.ini / packages.ini 内的 category=名称|图标\r\n" +
                    ";\r\n" +
                    "; 图标写法（与 category=名称|图标 一致）：\r\n" +
                    ";   · 短名（如 芯片）→ 自动在 IconPack 里查找同名 svg（如 9-媒体/芯片.svg）\r\n" +
                    ";   · 完整相对路径（如 9-媒体/芯片）→ 加载 IconPack/9-媒体/芯片.svg\r\n" +
                    ";   · 不写后缀会自动补 .svg；找不到回退默认图标，不崩。\r\n" +
                    ";\r\n" +
                    "; 用法：把下方示例行开头的「;」删掉，改成你自己的分类名与图标。\r\n" +
                    ";   例：[硬件检测]  下加  芯片 = 9-媒体/芯片\r\n" +
                    "; ---------------------------------------------------------------------------\r\n\r\n" +
                    "[Categories]\r\n" +
                    ";硬件检测=9-媒体/芯片\r\n" +
                    ";系统工具=sysinfo.png\r\n" +
                    ";常用工具=toolbox.png\r\n" +
                    ";常用软件=2-物品/工具箱\r\n" +
                    ";游戏平台=1-游戏/游戏手柄\r\n");
            }
            catch { }
        }

        private void CreateSampleApp(string appDir, string configPath)
        {
            File.WriteAllText(configPath,
                "; 所有外部工具都写在这个文件里，每个工具一个 [段名]\r\n" +
                "; category 字段用于分组；不写则归到「未分类」\r\n" +
                "; category 支持 名称|图标 语法，给整个分类指定一个 PNG 图标（如 系统工具|sysinfo.png）\r\n" +
                ";   图标文件名必须是 Icons/White 或 Icons/Black 里已有的 png；不写则按分类名自动匹配\r\n" +
                "; path 留空时，会自动找 app 目录下与段名同名的 exe\r\n" +
                ";\r\n" +
                "; 多架构合并（右键切换）：同一基础名的多个段会自动合成一张卡片\r\n" +
                ";   · 写法一：段名带架构后缀，如 [Hwinfo 32]、[Hwinfo 64]、[Hwinfo ARM64]\r\n" +
                ";   · 写法二：同段名重复，用 type=32 / type=64 / type=arm64 区分\r\n" +
                ";   · 合并后右键卡片会循环切换，并播放翻转动画\r\n\r\n" +
                "[记事本]\r\n" +
                "category=常用工具\r\n" +
                "description=Windows 自带记事本（示例）\r\n" +
                "path=C:\\Windows\\System32\\notepad.exe\r\n" +
                "icon=notepad.png\r\n\r\n" +
                "[计算器]\r\n" +
                "category=常用工具\r\n" +
                "path=C:\\Windows\\System32\\calc.exe\r\n\r\n" +
                "[任务管理器]\r\n" +
                "category=系统工具|sysinfo.png\r\n" +
                "path=C:\\Windows\\System32\\taskmgr.exe\r\n" +
                "icon=process.png\r\n\r\n" +
                "[Hwinfo 32]\r\n" +
                "category=硬件检测\r\n" +
                "description=详尽硬件传感器监控\r\n" +
                "path=硬件检测\\Hwinfo_32.exe\r\n\r\n" +
                "[Hwinfo 64]\r\n" +
                "category=硬件检测\r\n" +
                "description=详尽硬件传感器监控\r\n" +
                "path=硬件检测\\Hwinfo_64.exe\r\n\r\n" +
                "[Hwinfo ARM64]\r\n" +
                "category=硬件检测\r\n" +
                "description=详尽硬件传感器监控\r\n" +
                "path=硬件检测\\Hwinfo_ARM64.exe\r\n\r\n" +
                "[百度]\r\n" +
                "category=常用工具\r\n" +
                "description=搜索引擎（网页链接示例）\r\n" +
                "path=baidu.com\r\n" +
                "type=网页\r\n");

            File.WriteAllText(System.IO.Path.Combine(appDir, "使用说明.txt"),
                "所有工具都写在 app/apps.ini 这一个文件里，格式如下：\r\n\r\n" +
                "  [工具显示名]            ← 方括号里是工具名\r\n" +
                "  category=分组名      ← 用哪一类（可选，不写归到「未分类」）\r\n" +
                "  category=分组名|图标.png  ← 同时给整个分类指定一个 PNG 图标（可选）\r\n" +
                "  description=介绍     ← 工具卡片副标题，显示在名称下方（可选，建议一句话简介）\r\n" +
                "  path=程序路径        ← .exe 或绝对路径；留空则自动找 app 目录下同名 exe\r\n" +
                "  args=启动参数        ← 可选\r\n" +
                "  icon=图标文件.png    ← 工具卡片图标，放在 Icons/White 或 Icons/Black（可选，默认 file.png）\r\n" +
                "  type=架构            ← 多架构写法用，如 32 / 64 / arm64（可选）\r\n" +
                "  type=网页            ← 网页链接写法：点击用默认浏览器打开 path 里的网址（需要配合下一行）\r\n" +
                "  path=网址            ← 网页链接时直接写网址，如 baidu.com（会自动补 https://，也可写完整 https://...）\r\n\r\n" +
                "多架构合并（右键切换）：\r\n" +
                "  同一基础名的工具会自动合成一张卡片，右键卡片循环切换架构，带翻转动画。\r\n" +
                "  · 写法一（推荐）：段名带架构后缀\r\n" +
                "      [Hwinfo 32] / [Hwinfo 64] / [Hwinfo ARM64]\r\n" +
                "  · 写法二：同段名重复，用 type 区分\r\n" +
                "      [Hwinfo] type=32 path=...\r\n" +
                "      [Hwinfo] type=64 path=...\r\n" +
                "      [Hwinfo] type=arm64 path=...\r\n\r\n" +
                "网页链接（点击用浏览器打开）：\r\n" +
                "  type=网页 且 path 写网址即可，例如\r\n" +
                "      [百度]\r\n" +
                "      category=常用工具\r\n" +
                "      path=baidu.com\r\n" +
                "      type=网页\r\n" +
                "  不写 icon 时默认用「互联网」图标；也可自己写 icon=（PNG 或 IconPack SVG 路径）。\r\n\r\n" +
                "分类标签图标规则（应用页顶部）：\r\n" +
                "  · 写了 category=名称|图标.png → 用该 PNG（图标需在 Icons 里存在）\r\n" +
                "  · 没写 → 按分类名关键字自动匹配：系统→sysinfo / 网络→ipinfo / 文件→file /\r\n" +
                "    媒体→screenshot / 工具→toolbox / 硬件→process / 优化→settings / 游戏→app_grid\r\n" +
                "  · 都没有匹配 → 兜底 app_grid.png\r\n\r\n" +
                "示例：\r\n" +
                "  [截图工具]\r\n" +
                "  category=我的工具|camera.png\r\n" +
                "  path=D:\\Tools\\snipaste.exe\r\n" +
                "  icon=camera.png\r\n");
        }

        private Dictionary<string, Dictionary<string, string>> ReadIni(string path)
        {
            var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(path)) return result;

            string section = "";
            foreach (var rawLine in File.ReadAllLines(path))
            {
                var line = rawLine.Trim();
                if (string.IsNullOrEmpty(line)) continue;
                if (line.StartsWith(";") || line.StartsWith("#")) continue;
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    section = line.Substring(1, line.Length - 2);
                    if (!result.ContainsKey(section))
                        result[section] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    continue;
                }
                int idx = line.IndexOf('=');
                if (idx > 0)
                {
                    string key = line.Substring(0, idx).Trim();
                    string value = line.Substring(idx + 1).Trim();
                    if (!result.ContainsKey(section))
                        result[section] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    result[section][key] = value;
                }
            }
            return result;
        }

        private Border BuildCategoryCard(ToolCategory category)
        {
            var card = new Border
            {
                Style = FindResource("CardBorder") as Style,
                Margin = new Thickness(0, 0, 0, 22),
                Tag = category
            };

            var stack = new StackPanel();

            var toggle = new Button
            {
                Style = FindResource("SectionHeader") as Style,
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };
            var titleGrid = new Grid();
            titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var titleText = new TextBlock
            {
                Text = category.Name,
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextPrimaryBrush")
            };
            var arrow = new System.Windows.Shapes.Path
            {
                Data = (Geometry)FindResource("IconArrowDown"),
                Width = 24,
                Height = 24,
                Stretch = Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Center,
                Fill = (Brush)FindResource("TextSecondaryBrush"),
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new RotateTransform(0)
            };
            Grid.SetColumn(titleText, 0);
            Grid.SetColumn(arrow, 1);
            titleGrid.Children.Add(titleText);
            titleGrid.Children.Add(arrow);
            toggle.Content = titleGrid;

            var wrap = new WrapPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 16, 0, 4)
            };

            foreach (var tool in category.Tools)
            {
                wrap.Children.Add(CreateToolTile(tool));
            }

            toggle.Click += (s, e) =>
            {
                bool expand = wrap.Visibility == Visibility.Collapsed;
                PlayCategoryToggle(wrap, arrow, expand);
            };

            stack.Children.Add(toggle);
            stack.Children.Add(wrap);
            card.Child = stack;
            return card;
        }

        // ============================================================
        // APP 工具箱顶部标签栏
        // ============================================================
        private void BuildAppTabs(List<ToolCategory> categories)
        {
            if (AppTabPanel == null) return;
            AppTabPanel.Children.Clear();

            foreach (var category in categories)
            {
                try
                {
                    var btn = BuildAppTabButton(category);
                    if (btn != null) AppTabPanel.Children.Add(btn);
                }
                catch { }
            }

            // 兜底：至少显示一个标签，避免标签栏完全空白
            if (AppTabPanel.Children.Count == 0)
            {
                AppTabPanel.Children.Add(new Button
                {
                    Style = FindResource("AppTabButton") as Style,
                    Content = new TextBlock { Text = "全部工具", FontSize = 14, FontWeight = FontWeights.SemiBold },
                    Margin = new Thickness(0, 0, 8, 0),
                    IsEnabled = false
                });
            }

            Dispatcher.BeginInvoke(new Action(UpdateAppTabArrowVisibility), DispatcherPriority.Render);
        }

        private Button BuildAppTabButton(ToolCategory category)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            FrameworkElement iconElement;
            if (!string.IsNullOrEmpty(category.IconOverride))
            {
                // 优先尝试 IconPack SVG（单色，可随主题 Fill 染色，彻底解决之前 PNG 选中变白/发灰的问题）
                string rel = ResolveIconPackRel(category.IconOverride);
                var svgGeo = rel != null ? SvgGeometryLoader.LoadGeometry(rel) : null;
                if (svgGeo != null)
                {
                    iconElement = new System.Windows.Shapes.Path
                    {
                        Data = svgGeo,
                        Width = 24, Height = 24, Stretch = Stretch.Uniform,
                        Fill = (Brush)FindResource("TextPrimaryBrush"),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                }
                else
                {
                    // 回退到 Icons 内置 PNG（不可染色，用 Image 显示）
                    var iconImg = new Image
                    {
                        Width = 24, Height = 24, Stretch = Stretch.Uniform,
                        Source = GetThemeIcon(category.IconOverride),
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                    RenderOptions.SetBitmapScalingMode(iconImg, BitmapScalingMode.HighQuality);
                    iconElement = iconImg;
                }
            }
            else
            {
                // 默认单色矢量图标（内置几何资源）
                iconElement = new System.Windows.Shapes.Path
                {
                    Data = (Geometry)FindResource(GetCategoryIconKey(category.Name)),
                    Width = 24, Height = 24, Stretch = Stretch.Uniform,
                    Fill = (Brush)FindResource("TextPrimaryBrush"),
                    VerticalAlignment = VerticalAlignment.Center
                };
            }

            var text = new TextBlock
            {
                Text = category.Name,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextSecondaryBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0)
            };

            Grid.SetColumn(iconElement, 0);
            Grid.SetColumn(text, 1);
            grid.Children.Add(iconElement);
            grid.Children.Add(text);

            var btn = new Button
            {
                Style = FindResource("AppTabButton") as Style,
                Content = grid,
                Margin = new Thickness(0, 0, 4, 0),
                Tag = category
            };

            btn.Click += (s, e) => SelectAppCategory(category);

            // 悬停：文字与矢量图标提亮；PNG 自定义图标容器也跟随变浅
            Brush hoverBrush = (Brush)FindResource("PrimaryBrush");
            Brush idleBrush = (Brush)FindResource("TextSecondaryBrush");

            btn.MouseEnter += (s, e) =>
            {
                if (text != null) text.Foreground = hoverBrush;
                if (iconElement is System.Windows.Shapes.Path p) p.Fill = hoverBrush;
            };
            btn.MouseLeave += (s, e) =>
            {
                if (btn.Tag == _selectedAppCategory) return; // 选中状态由 SelectAppCategory 维护
                if (text != null) text.Foreground = idleBrush;
                if (iconElement is System.Windows.Shapes.Path p) p.Fill = (Brush)FindResource("TextPrimaryBrush");
            };

            return btn;
        }

        private void SelectAppCategory(ToolCategory category)
        {
            if (category == null || AppToolsPanel == null) return;

            _selectedAppCategory = category;

            // 高亮当前标签：底部主色条 + 文字加粗/高亮
            foreach (Button btn in AppTabPanel.Children.OfType<Button>())
            {
                bool selected = btn.Tag == category;

                var border = btn.Template?.FindName("Bd", btn) as Border;
                if (border != null) border.Background = Brushes.Transparent;

                var indicator = btn.Template?.FindName("SelectedIndicator", btn) as Border;
                if (indicator != null) indicator.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;

                var grid = btn.Content as Grid;
                if (grid != null)
                {
                    var text = grid.Children.OfType<TextBlock>().FirstOrDefault();
                    if (text != null)
                    {
                        text.FontWeight = selected ? FontWeights.Bold : FontWeights.SemiBold;
                        text.Foreground = selected
                            ? (Brush)FindResource("PrimaryBrush")
                            : (Brush)FindResource("TextSecondaryBrush");
                    }

                    var iconElement = grid.Children.OfType<FrameworkElement>().FirstOrDefault(c => Grid.GetColumn(c) == 0);
                    if (iconElement is System.Windows.Shapes.Path p)
                    {
                        // 矢量/SVG 图标：选中主色蓝，未选中主文字色（深/浅自动）；SVG 可染色，无需光晕
                        p.Fill = selected ? (Brush)FindResource("PrimaryBrush") : (Brush)FindResource("TextPrimaryBrush");
                        p.Opacity = 1.0;
                    }
                    else if (iconElement is Image img)
                    {
                        // 内置 PNG（不可染色）：选中不透明，未选中略淡
                        img.Opacity = selected ? 1.0 : 0.6;
                        img.Effect = null;
                    }
                }
            }

            // 清空并填充工具
            AppToolsPanel.Children.Clear();
            foreach (var tool in category.Tools)
            {
                AppToolsPanel.Children.Add(CreateToolTile(tool));
            }

            // 入场动画：先淡出旧的，再淡入 + 上移
            if (_animationsEnabled)
            {
                AnimateTabContentIn(AppToolsPanel);
            }
        }

        // 标签内容入场动画
        private void AnimateTabContentIn(UIElement target)
        {
            if (target == null) return;

            // 准备 RenderTransform
            if (!(target.RenderTransform is TranslateTransform tt))
            {
                tt = new TranslateTransform();
                target.RenderTransform = tt;
            }

            target.Opacity = 0;
            tt.Y = 12;

            var fadeIn = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(240),
                EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
            };
            var slideIn = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 12,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(240),
                EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
            };

            target.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            tt.BeginAnimation(TranslateTransform.YProperty, slideIn);
        }

        // 分类标签默认矢量图标（不带彩色底，Tab 不会看起来像卡片）
        private string GetCategoryIconKey(string categoryName)
        {
            string c = (categoryName ?? "").ToLowerInvariant();
            if (c.Contains("系统")) return "IconSettings";
            if (c.Contains("网络") || c.Contains("远程") || c.Contains("互联网")) return "IconInternet";
            if (c.Contains("文件")) return "IconFolder";
            if (c.Contains("媒体") || c.Contains("图片") || c.Contains("视频")) return "IconImage";
            if (c.Contains("办公") || c.Contains("实用") || c.Contains("工具")) return "IconToolbox";
            if (c.Contains("硬件")) return "IconInfo";
            if (c.Contains("优化")) return "IconRefresh";
            if (c.Contains("游戏")) return "IconLike";
            return "IconGrid";
        }

        // 分类标签图标：优先用自定义覆盖，其次按名称关键字匹配 PNG，最后兜底
        private string GetCategoryIconFile(string categoryName)
        {
            if (!string.IsNullOrEmpty(categoryName)
                && _categoryIconOverrides.TryGetValue(categoryName, out string ov)
                && !string.IsNullOrEmpty(ov))
                return ov;

            string c = (categoryName ?? "").ToLowerInvariant();
            if (c.Contains("系统")) return "sysinfo.png";
            if (c.Contains("网络") || c.Contains("远程") || c.Contains("互联网")) return "ipinfo.png";
            if (c.Contains("文件")) return "file.png";
            if (c.Contains("媒体") || c.Contains("图片") || c.Contains("视频")) return "screenshot.png";
            if (c.Contains("办公") || c.Contains("实用") || c.Contains("工具")) return "toolbox.png";
            if (c.Contains("硬件")) return "process.png";
            if (c.Contains("优化")) return "settings.png";
            if (c.Contains("游戏")) return "app_grid.png";
            return "app_grid.png";
        }

        private void AppTabScrollLeft_Click(object sender, RoutedEventArgs e)
        {
            if (AppTabScrollViewer == null) return;
            AppTabScrollViewer.LineLeft();
            AppTabScrollViewer.LineLeft();
            AppTabScrollViewer.LineLeft();
            UpdateAppTabArrowVisibility();
        }

        private void AppTabScrollRight_Click(object sender, RoutedEventArgs e)
        {
            if (AppTabScrollViewer == null) return;
            AppTabScrollViewer.LineRight();
            AppTabScrollViewer.LineRight();
            AppTabScrollViewer.LineRight();
            UpdateAppTabArrowVisibility();
        }

        private void AppTabScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateAppTabArrowVisibility();
        }

        private void UpdateAppTabArrowVisibility()
        {
            if (AppTabScrollViewer == null || AppTabScrollLeft == null || AppTabScrollRight == null) return;
            bool canScroll = AppTabScrollViewer.ExtentWidth > AppTabScrollViewer.ViewportWidth + 1;
            AppTabScrollLeft.Visibility = canScroll ? Visibility.Visible : Visibility.Collapsed;
            AppTabScrollRight.Visibility = canScroll ? Visibility.Visible : Visibility.Collapsed;
            AppTabScrollLeft.IsEnabled = AppTabScrollViewer.HorizontalOffset > 0;
            AppTabScrollRight.IsEnabled = AppTabScrollViewer.HorizontalOffset + AppTabScrollViewer.ViewportWidth < AppTabScrollViewer.ExtentWidth - 1;
        }

        private Button CreateToolTile(ToolItem tool)
        {
            var btn = new Button
            {
                Style = FindResource("ToolTile") as Style,
                Tag = tool
            };
            btn.Click += (s, e) => LaunchTool(tool);

            var grid = new Grid { Margin = new Thickness(16) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.RenderTransformOrigin = new Point(0.5, 0.5);

            // 图标
            var iconBorder = new Border
            {
                Width = 44,
                Height = 44,
                CornerRadius = new CornerRadius(11),
                Background = (Brush)FindResource("BackgroundHoverBrush"),
                VerticalAlignment = VerticalAlignment.Center
            };
            RefreshToolTileIcon(tool, iconBorder);
            TryLoadFaviconAsync(tool, iconBorder);

            // 文字
            var textStack = new StackPanel
            {
                Margin = new Thickness(14, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            const double titleMaxWidth = 140;
            var title = new TextBlock
            {
                Text = tool.Name,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextPrimaryBrush"),
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextAlignment = TextAlignment.Left,
                HorizontalAlignment = HorizontalAlignment.Left,
                MaxWidth = titleMaxWidth
            };
            // 长名字自动缩小字号适配卡片宽度，保持左对齐
            AutoShrinkFont(title, titleMaxWidth);
            var desc = new TextBlock
            {
                Text = tool.Description,
                FontSize = 12,
                Margin = new Thickness(0, 3, 0, 0),
                Foreground = (Brush)FindResource("TextSecondaryBrush"),
                TextWrapping = TextWrapping.Wrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 140,
                MaxHeight = 34 // 约 2 行（12pt × 1.4 行距 × 2）
            };
            textStack.Children.Add(title);
            textStack.Children.Add(desc);

            Grid.SetColumn(iconBorder, 0);
            Grid.SetColumn(textStack, 1);
            grid.Children.Add(iconBorder);
            grid.Children.Add(textStack);

            // 外层容器：内容网格 + 右上角架构标签（多架构时）
            var root = new Grid();
            root.Children.Add(grid);

            TextBlock archLabel = null;
            // 多架构工具：右上角显示当前架构，右键切换变体，带 Y 轴翻转动画
            if (tool.Variants.Count > 1)
            {
                archLabel = new TextBlock
                {
                    Text = ArchDisplay(tool.Architecture),
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = (Brush)FindResource("TextSecondaryBrush"),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, 4, 8, 0)
                };
                root.Children.Add(archLabel);

                btn.MouseRightButtonDown += (s, e) =>
                {
                    e.Handled = true;
                    SwitchToolVariant(tool, iconBorder, title, desc, grid, archLabel);
                };
            }

            btn.Content = root;

            return btn;
        }

        /// <summary>
        /// 根据可用宽度自动缩小 TextBlock 的字号，使其保持左对齐且不超出容器。
        /// </summary>
        private void AutoShrinkFont(TextBlock tb, double maxWidth)
        {
            if (string.IsNullOrEmpty(tb.Text)) return;
            var typeface = new Typeface(tb.FontFamily, tb.FontStyle, tb.FontWeight, tb.FontStretch);
            double size = tb.FontSize;
            while (size > 8)
            {
                var ft = new FormattedText(
                    tb.Text,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    size,
                    System.Windows.Media.Brushes.Black,
                    1.0);
                if (ft.Width <= maxWidth) break;
                size -= 0.5;
            }
            tb.FontSize = size;
        }

        // 根据 tool 当前属性生成图标并放入 iconBorder
        private void RefreshToolTileIcon(ToolItem tool, Border iconBorder)
        {
            var iconElement = BuildToolTileIcon(tool, out Brush tileBg);
            iconBorder.Background = tileBg;
            iconBorder.Child = iconElement;
        }

        // 网页链接：异步下载网站 favicon，转 png 后缓存到 exe 旁边 favicon/ 目录，成功后刷新卡片图标
        private void TryLoadFaviconAsync(ToolItem tool, Border iconBorder)
        {
            if (tool == null || !tool.UseFavicon || tool.FaviconLoading) return;
            if (tool.FaviconImage != null) return; // 已加载过：重建卡片时直接复用
            tool.FaviconLoading = true;

            string host = null;
            try
            {
                string url = tool.Path?.Trim() ?? "";
                if (!System.Text.RegularExpressions.Regex.IsMatch(url, @"^[a-zA-Z][a-zA-Z0-9+.\-]*://"))
                    url = "https://" + url;
                host = new Uri(url).Host;
            }
            catch { return; }
            if (string.IsNullOrEmpty(host)) return;

            // 缓存文件名按 host 生成，去掉可能出现的非法字符
            char[] invalid = System.IO.Path.GetInvalidFileNameChars();
            string safeHost = new string(host.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
            // favicon 缓存放在 Windows 临时目录（%TEMP%\ToolboxFavicon），不随 exe 旁生成 favicon/ 文件夹，
            // 这样打包成单文件 exe 时无需额外排除，且多份工具箱可共享同一份缓存。
            string cacheDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ToolboxFavicon");
            string cachePath = System.IO.Path.Combine(cacheDir, safeHost + ".png");

            // 命中缓存：直接显示
            if (File.Exists(cachePath))
            {
                ApplyFaviconToTile(tool, iconBorder, cachePath);
                return;
            }

            Task.Run(() =>
            {
                try
                {
                    byte[] data = DownloadFavicon(host);
                    if (data == null || data.Length == 0) return;
                    try { System.IO.Directory.CreateDirectory(cacheDir); } catch { }
                    string tmpPath = cachePath + ".tmp";
                    // favicon.ico 是 ico 格式，WPF 不能直接显示，转成 png 再存缓存
                    using (var ms = new MemoryStream(data))
                    using (var icon = new System.Drawing.Icon(ms))
                    using (var bmp = icon.ToBitmap())
                    {
                        bmp.Save(tmpPath, System.Drawing.Imaging.ImageFormat.Png);
                    }
                    File.Copy(tmpPath, cachePath, true);
                    try { File.Delete(tmpPath); } catch { }
                    Dispatcher.BeginInvoke(new Action(() => ApplyFaviconToTile(tool, iconBorder, cachePath)));
                }
                catch { }
            });
        }

        // 把缓存好的 favicon png 应用到卡片（必须在 UI 线程调用）
        private void ApplyFaviconToTile(ToolItem tool, Border iconBorder, string pngPath)
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(pngPath, UriKind.Absolute);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                tool.FaviconImage = bmp;
                if (iconBorder != null)
                {
                    iconBorder.Background = (Brush)FindResource("BackgroundHoverBrush");
                    var img = new Image
                    {
                        Width = 32,
                        Height = 32,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Source = bmp
                    };
                    RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                    iconBorder.Child = img;
                }
            }
            catch { }
        }

        // 下载网站 favicon：先试 https，失败再试 http（5 秒超时）
        private byte[] DownloadFavicon(string host)
        {
            string[] urls = { "https://" + host + "/favicon.ico", "http://" + host + "/favicon.ico" };
            foreach (var url in urls)
            {
                try
                {
                    var req = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(url);
                    req.Timeout = 5000;
                    req.ReadWriteTimeout = 5000;
                    req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Toolbox/1.0";
                    req.AllowAutoRedirect = true;
                    using (var resp = (System.Net.HttpWebResponse)req.GetResponse())
                    {
                        if (resp.StatusCode != System.Net.HttpStatusCode.OK) continue;
                        using (var s = resp.GetResponseStream())
                        using (var ms = new MemoryStream())
                        {
                            s.CopyTo(ms);
                            if (ms.Length > 0) return ms.ToArray();
                        }
                    }
                }
                catch { }
            }
            return null;
        }

        // 构建工具卡片图标：优先从 exe 提取，其次指定 PNG/矢量，最后兜底问号
        private UIElement BuildToolTileIcon(ToolItem tool, out Brush tileBg)
        {
            // 已下载好的网站 favicon：优先显示（网页链接且未指定 icon 时）
            if (tool.UseFavicon && tool.FaviconImage != null)
            {
                tileBg = (Brush)FindResource("BackgroundHoverBrush");
                var img = new Image
                {
                    Width = 32,
                    Height = 32,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Source = tool.FaviconImage
                };
                RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                return img;
            }
            if (tool.AutoIcon && !string.IsNullOrEmpty(tool.Path))
            {
                var extracted = TryExtractIcon(tool.Path);
                if (extracted != null)
                {
                    var img = new Image
                    {
                        Width = 32,
                        Height = 32,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Source = extracted
                    };
                    RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                    // 真实 exe 图标自带颜色，用中性背景更清爽
                    tileBg = (Brush)FindResource("BackgroundHoverBrush");
                    return img;
                }
                tileBg = CreateLightBackground(tool.IconColor);
                return BuildPngIcon("file.png");
            }
            if (!string.IsNullOrEmpty(tool.IconFileName))
            {
                // 支持 IconPack SVG（含 / 或 .svg，或短名能解析到 svg）：以传入 brush（默认白色）渲染在彩色底上，与其它磁贴一致
                if (tool.IconFileName.Contains("/") || tool.IconFileName.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) || ResolveIconPackRel(tool.IconFileName) != null)
                {
                    string rel = ResolveIconPackRel(tool.IconFileName);
                    if (rel != null)
                    {
                        var svgSrc = SvgToImageSource(rel, Brushes.White);
                        if (svgSrc != null)
                        {
                            tileBg = CreateLightBackground(tool.IconColor);
                            return new Image
                            {
                                Width = 28,
                                Height = 28,
                                HorizontalAlignment = HorizontalAlignment.Center,
                                VerticalAlignment = VerticalAlignment.Center,
                                Source = svgSrc
                            };
                        }
                    }
                }
                tileBg = CreateLightBackground(tool.IconColor);
                return BuildPngIcon(tool.IconFileName);
            }
            if (!string.IsNullOrEmpty(tool.IconKey))
            {
                tileBg = CreateLightBackground(tool.IconColor);
                return new System.Windows.Shapes.Path
                {
                    Data = (Geometry)FindResource(tool.IconKey),
                    Width = 24,
                    Height = 24,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Fill = Brushes.Transparent,
                    Stroke = Brushes.White,
                    StrokeThickness = 1.5,
                    StrokeLineJoin = PenLineJoin.Round,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round
                };
            }
            tileBg = CreateLightBackground(tool.IconColor);
            return new TextBlock { Text = "?", FontSize = 20, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        }

        // 右键循环切换工具的架构变体，并用 Y 轴翻转动画刷新显示
        private void SwitchToolVariant(ToolItem tool, Border iconBorder, TextBlock title, TextBlock desc, Grid grid, TextBlock archLabel)
        {
            if (tool.Variants.Count <= 1) return;
            if (grid.Tag is bool flipping && flipping) return; // 动画中忽略重复点击
            grid.Tag = true;

            int nextIndex = (tool.CurrentVariantIndex + 1) % tool.Variants.Count;
            var variant = tool.Variants[nextIndex];

            var scale = grid.RenderTransform as ScaleTransform;
            if (scale == null)
            {
                scale = new ScaleTransform(1, 1, 0.5, 0.5);
                grid.RenderTransform = scale;
            }

            var flipOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(140))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            flipOut.Completed += (s2, e2) =>
            {
                // 翻到背面时更新数据与 UI
                tool.CurrentVariantIndex = nextIndex;
                tool.Architecture = variant.Architecture;
                tool.Name = variant.Name;
                tool.Description = variant.Description;
                tool.Path = variant.Path;
                tool.Args = variant.Args;
                tool.IconFileName = variant.IconFileName;
                tool.IconKey = variant.IconKey;
                tool.AutoIcon = variant.AutoIcon;

                title.Text = tool.Name;
                desc.Text = tool.Description;
                if (archLabel != null) archLabel.Text = ArchDisplay(tool.Architecture);
                RefreshToolTileIcon(tool, iconBorder);

                var flipIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                flipIn.Completed += (s3, e3) => { grid.Tag = false; };
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, flipIn);
            };
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, flipOut);
        }

        private string GetIconPath(string fileName)
        {
            // 优先从 exe 所在目录查找，这样 exe 被复制到别处也能找到图标
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            try
            {
                string exePath = Process.GetCurrentProcess().MainModule.FileName;
                if (!string.IsNullOrEmpty(exePath))
                    baseDir = System.IO.Path.GetDirectoryName(exePath);
            }
            catch { }

            // 磁贴统一使用白色图标，放在彩色背景上更醒目（亮/暗主题都一致）
            string iconsDir = System.IO.Path.Combine(baseDir, "Icons", "White");
            if (!string.IsNullOrEmpty(fileName))
            {
                string path = System.IO.Path.Combine(iconsDir, fileName);
                if (File.Exists(path)) return path;
            }
            // 回退：黑色图标
            string fallbackDir = System.IO.Path.Combine(baseDir, "Icons", "Black");
            if (!string.IsNullOrEmpty(fileName))
            {
                string fallback = System.IO.Path.Combine(fallbackDir, fileName);
                if (File.Exists(fallback)) return fallback;
            }
            return System.IO.Path.Combine(iconsDir, "file.png");
        }

        // 从 Icons 目录或嵌入资源读取 png 图标
        private UIElement BuildPngIcon(string fileName)
        {
            // IconPack 单色 SVG：渲染成随主题色的图标（用于 dev 覆盖 / app 磁贴）
            if (!string.IsNullOrEmpty(fileName) && (fileName.Contains("/") || fileName.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)))
            {
                string rel = ResolveIconPackRel(fileName);
                var svg = rel != null ? SvgToImageSource(rel) : null;
                if (svg != null)
                {
                    var svgImage = new Image
                    {
                        Width = 28, Height = 28,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Source = svg
                    };
                    RenderOptions.SetBitmapScalingMode(svgImage, BitmapScalingMode.HighQuality);
                    return svgImage;
                }
            }
            // 优先磁盘（支持用户自定义图标），否则从嵌入资源加载
            string diskPath = null;
            if (!string.IsNullOrEmpty(fileName))
            {
                string p = GetIconPath(fileName);
                if (File.Exists(p)) diskPath = p;
            }

            BitmapImage src;
            if (diskPath != null)
            {
                src = new BitmapImage(new Uri(diskPath, UriKind.Absolute));
            }
            else
            {
                string set = "White";
                if (!EmbeddedIconExists(set, fileName)) set = "Black";
                if (!EmbeddedIconExists(set, fileName)) fileName = "file.png";
                src = LoadEmbeddedIcon(set, fileName);
            }

            var image = new Image
            {
                Width = 28,
                Height = 28,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Source = src
            };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
            return image;
        }

        // 从 exe/lnk 文件中提取其自带的关联图标（Windows 资源管理器里看到的就是这个）
        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteObject(IntPtr hObject);

        private ImageSource TryExtractIcon(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return null;
            try
            {
                using (var icon = System.Drawing.Icon.ExtractAssociatedIcon(filePath))
                {
                    if (icon == null) return null;
                    using (var bmp = icon.ToBitmap())
                    {
                        IntPtr hbm = bmp.GetHbitmap();
                        try
                        {
                            return Imaging.CreateBitmapSourceFromHBitmap(
                                hbm, IntPtr.Zero, Int32Rect.Empty,
                                BitmapSizeOptions.FromEmptyOptions());
                        }
                        finally
                        {
                            DeleteObject(hbm);
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        // ============================================================
        // 安装包页（与 APP 工具箱同构：ini 驱动 + 分类标签 + 卡片网格）
        // ============================================================
        private List<ToolCategory> _packagesCategories = new List<ToolCategory>();
        private ToolCategory _selectedPackagesCategory = null;
        private const string PackagesConfigDir = "packages"; // 与 app/ 平级，独立存放 packages.ini
        private const string PackagesConfigFile = "packages.ini";

        private void LoadPackagesCategories()
        {
            _packagesCategories = new List<ToolCategory>();
            _packagesCategoryIconOverrides.Clear();
            // packages.ini 放在独立的 packages/ 目录，与 app/ 平级，互不干扰
            string packagesDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, PackagesConfigDir);
            try { System.IO.Directory.CreateDirectory(packagesDir); } catch { }
            string appDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app");
            // packages 页同时读全局 app/theme.ini 与专属 packages/theme.ini（后者优先）
            LoadThemeCategoryIcons(appDir, packagesDir);
            string configPath = System.IO.Path.Combine(packagesDir, PackagesConfigFile);

            // 安装包页专属 theme.ini（与 packages.ini 同目录，自包含）；仅当不存在时生成示例
            string packagesThemePath = System.IO.Path.Combine(packagesDir, ThemeConfigFile);
            if (!File.Exists(packagesThemePath))
                CreateSamplePackagesTheme(packagesDir, packagesThemePath);

            // 兼容迁移：旧版本 packages.ini 在 app/ 下，首次运行自动搬到 packages/
            if (!File.Exists(configPath))
            {
                string legacy = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app", PackagesConfigFile);
                if (File.Exists(legacy)) { try { System.IO.File.Copy(legacy, configPath, true); } catch { } }
            }

            if (!File.Exists(configPath))
                CreateSamplePackages(packagesDir, configPath);
            if (!File.Exists(configPath)) return;

            // 安装包页支持同段名重复（[微信] 出现 64/32 两次），必须用 ReadAppSections 读取；
            // 普通 ReadIni 会把同段名覆盖，导致多架构无法合并。
            var sections = ReadAppSections(configPath);

            // 第一步：提取每个段的基础名与架构（支持 type= 字段或段名末尾架构后缀）
            var parsed = new List<(string sectionName, string baseName, string arch, Dictionary<string, string> fields)>();
            foreach (var sec in sections)
            {
                string sectionName = sec.Key;
                foreach (var fields in sec.Value)
                {
                    if (fields.Count == 0) continue;
                    string arch = null;
                    string baseName = sectionName;
                    if (fields.ContainsKey("type"))
                    {
                        // type=网页 等表示网页链接，不参与架构合并；否则兼容 64位/32位/arm64位/x64/x86 等
                        arch = IsWebType(fields["type"].Trim()) ? null : NormalizeArch(fields["type"]);
                    }
                    else
                    {
                        // 从段名末尾提取架构后缀：arm64 / x64 / x32 / x86 / 64 / 32
                        var m = Regex.Match(sectionName, @"\s*(arm64|x64|x32|x86|64|32)$", RegexOptions.IgnoreCase);
                        if (m.Success)
                        {
                            arch = NormalizeArch(m.Groups[1].Value);
                            baseName = sectionName.Substring(0, sectionName.Length - m.Value.Length).TrimEnd();
                        }
                    }
                    parsed.Add((sectionName, baseName, arch, fields));
                }
            }

            // 第二步：按基础名分组合并为一个多架构工具卡片（右上角显示 type，右键翻转切换）
            var catMap = new Dictionary<string, ToolCategory>(StringComparer.OrdinalIgnoreCase);
            var groups = parsed.GroupBy(p => p.baseName, StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var g in groups)
            {
                var first = g.First();
                var fields = first.fields;
                string sectionName = first.sectionName;

                string rawCategory = fields.ContainsKey("category") ? fields["category"] : "未分类";
                // 支持 category=名称|图标 语法：图标写 png 名即可，可不带 .png 后缀，软件内嵌 White/Black 两套自动匹配
                string categoryName = rawCategory;
                string iniIcon = null;
                int sep = rawCategory.IndexOf('|');
                if (sep >= 0)
                {
                    categoryName = rawCategory.Substring(0, sep).Trim();
                    iniIcon = rawCategory.Substring(sep + 1).Trim();
                }
                // 优先级：theme.ini 全局映射 > ini 内 |图标
                string categoryIcon = null;
                if (_themeCategoryIconOverrides.TryGetValue(categoryName, out var themeIcon) && !string.IsNullOrEmpty(themeIcon))
                    categoryIcon = themeIcon;
                else if (!string.IsNullOrEmpty(iniIcon))
                    categoryIcon = iniIcon;
                // IconPack svg 路径（含 /）自动补 .svg；PNG 或短名保持原样，由渲染层判断 png/svg
                if (!string.IsNullOrEmpty(categoryIcon) && categoryIcon.Contains("/") && !categoryIcon.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                    categoryIcon += ".svg";
                if (!catMap.ContainsKey(categoryName))
                {
                    catMap[categoryName] = new ToolCategory { Name = categoryName, IconOverride = categoryIcon };
                    if (!string.IsNullOrEmpty(categoryIcon)) _packagesCategoryIconOverrides[categoryName] = categoryIcon;
                }
                else if (!string.IsNullOrEmpty(categoryIcon) && string.IsNullOrEmpty(catMap[categoryName].IconOverride))
                {
                    catMap[categoryName].IconOverride = categoryIcon;
                    _packagesCategoryIconOverrides[categoryName] = categoryIcon;
                }

                var mainTool = LoadToolFromIniSection(sectionName, fields, categoryName, packagesDir);
                if (mainTool == null) continue;
                mainTool.Architecture = first.arch;

                // 同一基础名的其余条目作为架构变体
                foreach (var variant in g.Skip(1))
                {
                    var vTool = LoadToolFromIniSection(variant.sectionName, variant.fields, categoryName, packagesDir);
                    if (vTool == null) continue;
                    mainTool.Variants.Add(new ArchitectureVariant
                    {
                        Architecture = variant.arch,
                        Name = vTool.Name,
                        Description = vTool.Description,
                        Path = vTool.Path,
                        Args = vTool.Args,
                        IconFileName = vTool.IconFileName,
                        IconKey = vTool.IconKey,
                        AutoIcon = vTool.AutoIcon
                    });
                }

                // Variants[0] 固定保存主项本身，方便右键循环切换时统一处理
                mainTool.Variants.Insert(0, new ArchitectureVariant
                {
                    Architecture = mainTool.Architecture,
                    Name = mainTool.Name,
                    Description = mainTool.Description,
                    Path = mainTool.Path,
                    Args = mainTool.Args,
                    IconFileName = mainTool.IconFileName,
                    IconKey = mainTool.IconKey,
                    AutoIcon = mainTool.AutoIcon
                });
                mainTool.CurrentVariantIndex = 0;

                catMap[categoryName].Tools.Add(mainTool);
            }

            foreach (var c in catMap.Values)
                SortCategoryToolsPinFlippable(c);
            _packagesCategories.AddRange(catMap.Values.Where(c => c.Tools.Count > 0));
        }

        private void CreateSamplePackages(string appDir, string configPath)
        {
            try
            {
                File.WriteAllText(configPath,
                    "; 安装包页：每个 [段名] 是一个安装包 / 软件\r\n" +
                    "; path 指向 exe 或 msi；不写 icon 时自动提取该 exe 的真实图标\r\n" +
                    "; category 用于分组；不写则归到「未分类」\r\n" +
                    "; category 支持 名称|图标 语法；图标可是 Icons 内置 png 名，或 IconPack svg 相对路径（如 9-媒体/芯片），深浅自动切换\r\n" +
                    "; 找不到的程序会提示，按你电脑上的真实路径修改即可\r\n\r\n" +
                    "[微信]\r\n" +
                    "category=常用软件\r\n" +
                    "path=C:\\Program Files (x86)\\Tencent\\WeChat\\WeChat.exe\r\n\r\n" +
                    "[Steam]\r\n" +
                    "category=游戏平台\r\n" +
                    "path=C:\\Program Files (x86)\\Steam\\steam.exe\r\n\r\n" +
                    "[谷歌浏览器]\r\n" +
                    "category=常用软件\r\n" +
                    "path=C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe\r\n\r\n" +
                    "[7-Zip]\r\n" +
                    "category=系统工具\r\n" +
                    "path=C:\\Program Files\\7-Zip\\7zFM.exe\r\n\r\n" +
                    "; ── 多架构写法（可选）────────────────────────────\r\n" +
                    "; 同一软件的不同架构（32/64/ARM64）会合并成一张卡片，\r\n" +
                    "; 右键卡片可在架构间翻转动画切换，右上角显示当前 type。\r\n" +
                    "; 两种写法二选一：\r\n" +
                    ";   ① 段名带架构后缀（直接保留现有习惯）：\r\n" +
                    ";[微信 64]\r\n" +
                    ";category=常用软件\r\n" +
                    ";path=C:\\Program Files\\Tencent\\WeChat\\WeChat.exe\r\n" +
                    ";[微信 32]\r\n" +
                    ";category=常用软件\r\n" +
                    ";path=C:\\Program Files (x86)\\Tencent\\WeChat\\WeChat.exe\r\n" +
                    ";   ② 同段名重复 + type 字段（更规整）：\r\n" +
                    ";[微信]\r\n" +
                    ";category=常用软件\r\n" +
                    ";type=64\r\n" +
                    ";path=C:\\Program Files\\Tencent\\WeChat\\WeChat.exe\r\n" +
                    ";[微信]\r\n" +
                    ";category=常用软件\r\n" +
                    ";type=32\r\n" +
                    ";path=C:\\Program Files (x86)\\Tencent\\WeChat\\WeChat.exe\r\n");
            }
            catch { }
        }

        // 在 packages/ 下创建 theme.ini 示例（仅当文件不存在），写法与 app/theme.ini 一致。
        // 本文件优先于全局 app/theme.ini，让安装包分类图标自包含、与 packages.ini 同目录管理。
        private void CreateSamplePackagesTheme(string dir, string themePath)
        {
            try
            {
                File.WriteAllText(themePath,
                    "; 安装包页专属分类图标配置（packages/theme.ini）\r\n" +
                    "; 与 app/theme.ini 写法一致，本文件优先于全局 app/theme.ini。\r\n" +
                    "; 作用：把分类名映射到图标，packages.ini 只管软件本身，图标集中在这里管理。\r\n" +
                    ";\r\n" +
                    "; 图标写法：\r\n" +
                    ";   · 短名（如 工具箱）→ 自动在 IconPack 里查找同名 svg（如 2-物品/工具箱.svg）\r\n" +
                    ";   · 完整相对路径（如 1-游戏/游戏手柄）→ 加载 IconPack/1-游戏/游戏手柄.svg\r\n" +
                    ";   · 不写后缀自动补 .svg；找不到回退默认图标，不崩。\r\n" +
                    ";\r\n" +
                    "; 用法：把下方示例行开头的「;」删掉，改成你自己的分类名与图标。\r\n" +
                    "[Categories]\r\n" +
                    ";常用软件=2-物品/工具箱\r\n" +
                    ";游戏平台=1-游戏/游戏手柄\r\n" +
                    ";系统工具=sysinfo.png\r\n");
            }
            catch { }
        }

        private void BuildPackagesTabs(List<ToolCategory> categories)
        {
            if (PackagesTabPanel == null) return;
            PackagesTabPanel.Children.Clear();

            foreach (var category in categories)
            {
                try
                {
                    var btn = BuildPackagesTabButton(category);
                    if (btn != null) PackagesTabPanel.Children.Add(btn);
                }
                catch { }
            }

            if (PackagesTabPanel.Children.Count == 0)
            {
                PackagesTabPanel.Children.Add(new Button
                {
                    Style = FindResource("AppTabButton") as Style,
                    Content = new TextBlock { Text = "全部安装包", FontSize = 14, FontWeight = FontWeights.SemiBold },
                    Margin = new Thickness(0, 0, 8, 0),
                    IsEnabled = false
                });
            }

            Dispatcher.BeginInvoke(new Action(UpdatePackagesTabArrowVisibility), DispatcherPriority.Render);
        }

        private Button BuildPackagesTabButton(ToolCategory category)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            FrameworkElement iconElement;
            if (!string.IsNullOrEmpty(category.IconOverride))
            {
                string rel = ResolveIconPackRel(category.IconOverride);
                var svgGeo = rel != null ? SvgGeometryLoader.LoadGeometry(rel) : null;
                if (svgGeo != null)
                {
                    iconElement = new System.Windows.Shapes.Path
                    {
                        Data = svgGeo,
                        Width = 24, Height = 24, Stretch = Stretch.Uniform,
                        Fill = (Brush)FindResource("TextPrimaryBrush"),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                }
                else
                {
                    var iconImg = new Image
                    {
                        Width = 24, Height = 24, Stretch = Stretch.Uniform,
                        Source = GetThemeIcon(category.IconOverride),
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                    RenderOptions.SetBitmapScalingMode(iconImg, BitmapScalingMode.HighQuality);
                    iconElement = iconImg;
                }
            }
            else
            {
                iconElement = new System.Windows.Shapes.Path
                {
                    Data = (Geometry)FindResource(GetCategoryIconKey(category.Name)),
                    Width = 24, Height = 24, Stretch = Stretch.Uniform,
                    Fill = (Brush)FindResource("TextPrimaryBrush"),
                    VerticalAlignment = VerticalAlignment.Center
                };
            }

            var text = new TextBlock
            {
                Text = category.Name, FontSize = 14, FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextSecondaryBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0)
            };

            Grid.SetColumn(iconElement, 0);
            Grid.SetColumn(text, 1);
            grid.Children.Add(iconElement);
            grid.Children.Add(text);

            var btn = new Button
            {
                Style = FindResource("AppTabButton") as Style,
                Content = grid, Margin = new Thickness(0, 0, 4, 0), Tag = category
            };
            btn.Click += (s, e) => SelectPackagesCategory(category);

            Brush hoverBrush = (Brush)FindResource("PrimaryBrush");
            Brush idleBrush = (Brush)FindResource("TextSecondaryBrush");

            btn.MouseEnter += (s, e) =>
            {
                if (text != null) text.Foreground = hoverBrush;
                if (iconElement is System.Windows.Shapes.Path p) p.Fill = hoverBrush;
            };
            btn.MouseLeave += (s, e) =>
            {
                if (btn.Tag == _selectedPackagesCategory) return;
                if (text != null) text.Foreground = idleBrush;
                if (iconElement is System.Windows.Shapes.Path p) p.Fill = (Brush)FindResource("TextPrimaryBrush");
            };

            return btn;
        }

        private void SelectPackagesCategory(ToolCategory category)
        {
            if (category == null || PackagesToolsPanel == null) return;
            _selectedPackagesCategory = category;

            foreach (Button btn in PackagesTabPanel.Children.OfType<Button>())
            {
                bool selected = btn.Tag == category;
                var border = btn.Template?.FindName("Bd", btn) as Border;
                if (border != null) border.Background = Brushes.Transparent;
                var indicator = btn.Template?.FindName("SelectedIndicator", btn) as Border;
                if (indicator != null) indicator.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;

                var grid = btn.Content as Grid;
                if (grid != null)
                {
                    var text = grid.Children.OfType<TextBlock>().FirstOrDefault();
                    if (text != null)
                    {
                        text.FontWeight = selected ? FontWeights.Bold : FontWeights.SemiBold;
                        text.Foreground = selected ? (Brush)FindResource("PrimaryBrush") : (Brush)FindResource("TextSecondaryBrush");
                    }
                    var iconElement = grid.Children.OfType<FrameworkElement>().FirstOrDefault(c => Grid.GetColumn(c) == 0);
                    if (iconElement is System.Windows.Shapes.Path p)
                    {
                        // 矢量/SVG 图标：选中主色蓝，未选中主文字色（深/浅自动）
                        p.Fill = selected ? (Brush)FindResource("PrimaryBrush") : (Brush)FindResource("TextPrimaryBrush");
                        p.Opacity = 1.0;
                    }
                    else if (iconElement is Image img)
                    {
                        img.Opacity = selected ? 1.0 : 0.6;
                        img.Effect = null;
                    }
                }
            }

            PackagesToolsPanel.Children.Clear();
            foreach (var tool in category.Tools)
                PackagesToolsPanel.Children.Add(CreateToolTile(tool));

            if (_animationsEnabled) AnimateTabContentIn(PackagesToolsPanel);
        }

        private void PackagesTabScrollLeft_Click(object sender, RoutedEventArgs e)
        {
            if (PackagesTabScrollViewer == null) return;
            PackagesTabScrollViewer.LineLeft();
            PackagesTabScrollViewer.LineLeft();
            PackagesTabScrollViewer.LineLeft();
            UpdatePackagesTabArrowVisibility();
        }

        private void PackagesTabScrollRight_Click(object sender, RoutedEventArgs e)
        {
            if (PackagesTabScrollViewer == null) return;
            PackagesTabScrollViewer.LineRight();
            PackagesTabScrollViewer.LineRight();
            PackagesTabScrollViewer.LineRight();
            UpdatePackagesTabArrowVisibility();
        }

        private void PackagesTabScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdatePackagesTabArrowVisibility();
        }

        private void UpdatePackagesTabArrowVisibility()
        {
            if (PackagesTabScrollViewer == null || PackagesTabScrollLeft == null || PackagesTabScrollRight == null) return;
            bool canScroll = PackagesTabScrollViewer.ExtentWidth > PackagesTabScrollViewer.ViewportWidth + 1;
            PackagesTabScrollLeft.Visibility = canScroll ? Visibility.Visible : Visibility.Collapsed;
            PackagesTabScrollRight.Visibility = canScroll ? Visibility.Visible : Visibility.Collapsed;
            PackagesTabScrollLeft.IsEnabled = PackagesTabScrollViewer.HorizontalOffset > 0;
            PackagesTabScrollRight.IsEnabled = PackagesTabScrollViewer.HorizontalOffset < PackagesTabScrollViewer.ExtentWidth - PackagesTabScrollViewer.ViewportWidth - 1;
        }

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            double v = bytes;
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            int i = 0;
            while (v >= 1024 && i < units.Length - 1) { v /= 1024; i++; }
            return v.ToString("0.##") + " " + units[i];
        }

        private Brush CreateLightBackground(Brush baseBrush)
        {
            if (baseBrush is SolidColorBrush solid)
            {
                var color = solid.Color;
                color.A = 80; // 提高到约 31% 不透明度，亮主题下也能看出颜色
                return new SolidColorBrush(color);
            }
            return (Brush)FindResource("BackgroundHoverBrush");
        }

        private void LaunchTool(ToolItem tool)
        {
            if (tool.IsBuiltIn && tool.ClickAction != null)
            {
                tool.ClickAction();
                return;
            }

            // 网页链接：用默认浏览器打开 path 里的网址（自动补 https://）
            if (tool.IsWebLink)
            {
                if (string.IsNullOrEmpty(tool.Path)) return;
                try
                {
                    string url = tool.Path.Trim();
                    if (!System.Text.RegularExpressions.Regex.IsMatch(url, @"^[a-zA-Z][a-zA-Z0-9+.\-]*://"))
                        url = "https://" + url;
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "打开网页失败", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                return;
            }

            if (string.IsNullOrEmpty(tool.Path)) return;

            if (!File.Exists(tool.Path))
            {
                MessageBox.Show($"找不到程序：{tool.Path}", "启动失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(tool.Path, tool.Args)
                {
                    WorkingDirectory = System.IO.Path.GetDirectoryName(tool.Path)
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "启动失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ============================================================
        // 搜索过滤
        // ============================================================
        private void ApplySearchFilter()
        {
            string keyword = SearchBox.Text?.Trim().ToLowerInvariant() ?? "";

            if (_currentPage == BuiltInPage)
            {
                var panel = BuiltInToolsPanel;
                if (panel != null)
                {
                    foreach (Border card in panel.Children.OfType<Border>())
                    {
                        if (!(card.Tag is ToolCategory category)) continue;
                        var stack = card.Child as StackPanel;
                        if (stack == null) continue;
                        var wrap = stack.Children.Count > 1 ? stack.Children[1] as WrapPanel : null;
                        if (wrap == null) continue;

                        if (string.IsNullOrEmpty(keyword))
                        {
                            card.Visibility = Visibility.Visible;
                            wrap.Visibility = Visibility.Visible;
                            foreach (Button btn in wrap.Children.OfType<Button>())
                                btn.Visibility = Visibility.Visible;
                        }
                        else
                        {
                            bool categoryMatch = category.Name.ToLowerInvariant().Contains(keyword);
                            bool anyToolMatch = false;
                            foreach (Button btn in wrap.Children.OfType<Button>())
                            {
                                if (btn.Tag is ToolItem tool)
                                {
                                    bool match = tool.Name.ToLowerInvariant().Contains(keyword) ||
                                                 (tool.Description ?? "").ToLowerInvariant().Contains(keyword);
                                    btn.Visibility = match ? Visibility.Visible : Visibility.Collapsed;
                                    if (match) anyToolMatch = true;
                                }
                            }
                            card.Visibility = (categoryMatch || anyToolMatch) ? Visibility.Visible : Visibility.Collapsed;
                            wrap.Visibility = (categoryMatch || anyToolMatch) ? Visibility.Visible : Visibility.Collapsed;
                        }
                    }
                }
            }
            else if (_currentPage == AppPage)
            {
                if (AppToolsPanel == null) { AnimateSearchRefresh(); return; }

                if (string.IsNullOrEmpty(keyword))
                {
                    // 清空搜索：恢复当前选中的分类
                    SelectAppCategory(_selectedAppCategory);
                }
                else
                {
                    // 搜索态：平铺所有分类中匹配的工具
                    AppToolsPanel.Children.Clear();
                    foreach (var cat in _appCategories)
                    {
                        foreach (var tool in cat.Tools)
                        {
                            bool match = tool.Name.ToLowerInvariant().Contains(keyword) ||
                                         (tool.Description ?? "").ToLowerInvariant().Contains(keyword);
                            if (match)
                                AppToolsPanel.Children.Add(CreateToolTile(tool));
                        }
                    }
                    // 搜索态取消标签高亮
                    foreach (Button tab in AppTabPanel.Children.OfType<Button>())
                    {
                        tab.Background = Brushes.Transparent;
                        var grid = tab.Content as Grid;
                        if (grid != null)
                        {
                            var text = grid.Children.OfType<TextBlock>().FirstOrDefault();
                            if (text != null) text.FontWeight = FontWeights.SemiBold;
                        }
                    }
                }
            }

            else if (_currentPage == PackagesPage)
            {
                if (PackagesToolsPanel == null) { AnimateSearchRefresh(); return; }

                if (string.IsNullOrEmpty(keyword))
                {
                    // 清空搜索：恢复当前选中的分类
                    SelectPackagesCategory(_selectedPackagesCategory);
                }
                else
                {
                    // 搜索态：平铺所有分类中匹配的工具
                    PackagesToolsPanel.Children.Clear();
                    foreach (var cat in _packagesCategories)
                    {
                        foreach (var tool in cat.Tools)
                        {
                            bool match = tool.Name.ToLowerInvariant().Contains(keyword) ||
                                         (tool.Description ?? "").ToLowerInvariant().Contains(keyword);
                            if (match)
                                PackagesToolsPanel.Children.Add(CreateToolTile(tool));
                        }
                    }
                    // 搜索态取消标签高亮
                    foreach (Button tab in PackagesTabPanel.Children.OfType<Button>())
                    {
                        tab.Background = Brushes.Transparent;
                        var grid = tab.Content as Grid;
                        if (grid != null)
                        {
                            var text = grid.Children.OfType<TextBlock>().FirstOrDefault();
                            if (text != null) text.FontWeight = FontWeights.SemiBold;
                        }
                    }
                }
            }

            AnimateSearchRefresh();
        }

        // ============================================================
        // 开发者模式（彩蛋3：搜索框输入 DEVtool 回车 3 次解锁）
        // ============================================================

        // 内置工具的图标覆盖：key=工具名，value=图标文件名
        private Dictionary<string, string> _builtInIconOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // 在窗体加载后 / 解锁后 / 切到设置页时调用一次
        private void InitializeDevMode()
        {
            // 1) 应用文本覆盖（窗口标题、左侧导航等）
            ApplyDevTextOverrides();

            // 2) 应用图标覆盖：初始化目标下拉框
            if (DevIconTargetCombo != null && DevIconTargetCombo.Items.Count == 0)
            {
                DevIconTargetCombo.Items.Add(new DevTargetItem { Key = "Window", Display = "窗口标题栏图标" });
                DevIconTargetCombo.Items.Add(new DevTargetItem { Key = "Nav:Home", Display = "导航：主页" });
                DevIconTargetCombo.Items.Add(new DevTargetItem { Key = "Nav:BuiltIn", Display = "导航：内置工具" });
                DevIconTargetCombo.Items.Add(new DevTargetItem { Key = "Nav:App", Display = "导航：应用" });
                DevIconTargetCombo.Items.Add(new DevTargetItem { Key = "Nav:Packages", Display = "导航：安装包" });
                DevIconTargetCombo.Items.Add(new DevTargetItem { Key = "Nav:Settings", Display = "导航：设置" });
                // 动态加入所有内置工具
                var builtIn = LoadBuiltInCategory();
                if (builtIn != null)
                {
                    foreach (var t in builtIn.Tools)
                        DevIconTargetCombo.Items.Add(new DevTargetItem { Key = "Tool:" + t.Name, Display = "内置工具：" + t.Name });
                }
                DevIconTargetCombo.DisplayMemberPath = "Display";
                DevIconTargetCombo.SelectedValuePath = "Key";
                DevIconTargetCombo.SelectedIndex = 0;
            }

            // 3) 文本编辑器：填入当前值
            RefreshDevTextEditor();

            // 4) 颜色编辑器：填入当前主题色
            if (DevColorPrimary != null) DevColorPrimary.Text = GetCurrentColor("Primary");
            if (DevColorBackground != null) DevColorBackground.Text = GetCurrentColor("Background");
            if (DevColorText != null) DevColorText.Text = GetCurrentColor("TextPrimary");
            UpdateDevColorPreview();

            // 5) 应用已有的颜色覆盖（如果重启前改了色没保存，重启后也要继续生效）
            ApplyDevColorOverrides();

            // 6) 应用已有的图标覆盖（导航、窗口）
            foreach (var k in _devIconOverrides.Keys.ToList()) ApplyDevIconOverride(k);
        }

        // 颜色键 → 当前已应用的颜色（HEX）
        private string GetCurrentColor(string key)
        {
            try
            {
                if (_devColorOverrides.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v))
                    return v.StartsWith("#") ? v : "#" + v;
                var res = Application.Current.Resources[key];
                if (res is Color c)
                    return "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");
            }
            catch { }
            return "#000000";
        }

        private void UpdateDevColorPreview()
        {
            try
            {
                if (DevColorPrimaryPreview != null) DevColorPrimaryPreview.Background = new SolidColorBrush(ParseHexColor(DevColorPrimary?.Text));
                if (DevColorBackgroundPreview != null) DevColorBackgroundPreview.Background = new SolidColorBrush(ParseHexColor(DevColorBackground?.Text));
                if (DevColorTextPreview != null) DevColorTextPreview.Background = new SolidColorBrush(ParseHexColor(DevColorText?.Text));
            }
            catch { }
        }

        private static Color ParseHexColor(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return Colors.Transparent;
            string s = hex.Trim();
            if (s.StartsWith("#")) s = s.Substring(1);
            if (s.Length == 6)
            {
                try
                {
                    return Color.FromRgb(
                        Convert.ToByte(s.Substring(0, 2), 16),
                        Convert.ToByte(s.Substring(2, 2), 16),
                        Convert.ToByte(s.Substring(4, 2), 16));
                }
                catch { }
            }
            return Colors.Transparent;
        }

        // ---- 图标快捷设置 ----
        private void DevIconApply_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_iconPackCurrentRel))
            {
                if (DevIconStatus != null) DevIconStatus.Text = "请先在上方「图标库」选中一个图标（点一下就行）。";
                return;
            }
            if (DevIconTargetCombo == null || DevIconTargetCombo.SelectedItem == null)
            {
                if (DevIconStatus != null) DevIconStatus.Text = "请先选择应用目标。";
                return;
            }
            var target = DevIconTargetCombo.SelectedItem as DevTargetItem;
            if (target == null) return;

            // 提取文件名（IconPack 现为单色 SVG，两层路径：分类/文件名.svg）
            var parts = _iconPackCurrentRel.Split('/');
            string fileName = parts.Length >= 2 ? parts[1] : _iconPackCurrentRel;

            _devIconOverrides[target.Key] = fileName;
            SaveSettings();
            ApplyDevIconOverride(target.Key);
            if (DevIconStatus != null) DevIconStatus.Text = "已应用：" + target.Display + " ← " + fileName;
        }

        // 把某个覆盖应用到具体的 UI 元素
        private void ApplyDevIconOverride(string targetKey)
        {
            if (!_devIconOverrides.TryGetValue(targetKey, out var fileName)) return;
            try
            {
                if (targetKey.Equals("Window", StringComparison.OrdinalIgnoreCase))
                {
                    // 窗口图标需要位图，SVG 单色图标不支持作窗口图标，仅接受 Icons 内置 PNG
                    if (!fileName.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                    {
                        var uri = new Uri($"pack://application:,,,/Toolbox;component/Icons/{(isDarkTheme ? "White" : "Black")}/{fileName}", UriKind.Absolute);
                        this.Icon = BitmapFrame.Create(uri);
                    }
                }
                else if (targetKey.Equals("Nav:Home", StringComparison.OrdinalIgnoreCase)) { if (NavHomeIcon != null) NavHomeIcon.Source = GetThemeIcon(fileName); }
                else if (targetKey.Equals("Nav:BuiltIn", StringComparison.OrdinalIgnoreCase)) { if (NavBuiltInIcon != null) NavBuiltInIcon.Source = GetThemeIcon(fileName); }
                else if (targetKey.Equals("Nav:App", StringComparison.OrdinalIgnoreCase)) { if (NavAppIcon != null) NavAppIcon.Source = GetThemeIcon(fileName); }
                else if (targetKey.Equals("Nav:Packages", StringComparison.OrdinalIgnoreCase)) { if (NavPackagesIcon != null) NavPackagesIcon.Source = GetThemeIcon(fileName); }
                else if (targetKey.Equals("Nav:Settings", StringComparison.OrdinalIgnoreCase)) { if (NavSettingsIcon != null) NavSettingsIcon.Source = GetThemeIcon(fileName); }
                else if (targetKey.StartsWith("Tool:", StringComparison.OrdinalIgnoreCase))
                {
                    string toolName = targetKey.Substring(5);
                    _builtInIconOverrides[toolName] = fileName;
                    LoadAndBuildTools(animate: _animationsEnabled);
                }
            }
            catch (Exception ex)
            {
                if (DevIconStatus != null) DevIconStatus.Text = "应用失败：" + ex.Message;
            }
        }

        // ---- 文本标题编辑 ----
        private void DevTextApply_Click(object sender, RoutedEventArgs e)
        {
            if (DevTextEditor == null) return;
            _devTextOverrides.Clear();
            int applied = 0;
            foreach (var raw in DevTextEditor.Text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int idx = raw.IndexOf('=');
                if (idx <= 0) continue;
                string k = raw.Substring(0, idx).Trim();
                string v = raw.Substring(idx + 1).Trim();
                if (string.IsNullOrEmpty(k) || string.IsNullOrEmpty(v)) continue;
                _devTextOverrides[k] = v;
                applied++;
            }
            SaveSettings();
            ApplyDevTextOverrides();
            if (DevTextStatus != null) DevTextStatus.Text = $"已应用 {applied} 条";
        }

        private void DevTextReset_Click(object sender, RoutedEventArgs e)
        {
            _devTextOverrides.Clear();
            SaveSettings();
            ApplyDevTextOverrides();
            RefreshDevTextEditor();
            if (DevTextStatus != null) DevTextStatus.Text = "已恢复默认";
        }

        // 拿默认值填编辑器
        private void RefreshDevTextEditor()
        {
            if (DevTextEditor == null) return;
            var sb = new System.Text.StringBuilder();
            string[] keys = { "WindowTitle", "Nav:Home", "Nav:BuiltIn", "Nav:App", "Nav:Packages", "Nav:Settings" };
            foreach (var k in keys)
            {
                string v = _devTextOverrides.TryGetValue(k, out var vv) ? vv : GetDefaultText(k);
                sb.Append(k).Append('=').Append(v).Append('\n');
            }
            DevTextEditor.Text = sb.ToString();
        }

        private string GetDefaultText(string key)
        {
            if (key == "WindowTitle") return "纳米工具箱";
            if (key == "Nav:Home") return "主页";
            if (key == "Nav:BuiltIn") return "内置工具";
            if (key == "Nav:App") return "应用";
            if (key == "Nav:Packages") return "安装包";
            if (key == "Nav:Settings") return "设置";
            return "";
        }

        // 把所有文本覆盖应用到 UI
        private void ApplyDevTextOverrides()
        {
            if (_devTextOverrides.TryGetValue("WindowTitle", out var title))
                this.Title = title;
            if (_devTextOverrides.TryGetValue("Nav:Home", out var t1))
                SetNavButtonText(NavHome, t1);
            if (_devTextOverrides.TryGetValue("Nav:BuiltIn", out var t2))
                SetNavButtonText(NavBuiltIn, t2);
            if (_devTextOverrides.TryGetValue("Nav:App", out var t3))
                SetNavButtonText(NavApp, t3);
            if (_devTextOverrides.TryGetValue("Nav:Packages", out var t4))
                SetNavButtonText(NavPackages, t4);
            if (_devTextOverrides.TryGetValue("Nav:Settings", out var t5))
                SetNavButtonText(NavSettings, t5);
        }

        // 导航按钮 Content 是 StackPanel(Image+TextBlock)，改 TextBlock 即可
        private void SetNavButtonText(Button btn, string text)
        {
            if (btn?.Content is StackPanel sp && sp.Children.Count >= 2 && sp.Children[1] is TextBlock tb)
                tb.Text = text;
        }

        // ---- 主题快速编辑 ----
        private void DevColorApply_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _devColorOverrides["Primary"] = (DevColorPrimary?.Text ?? "").Trim();
                _devColorOverrides["Background"] = (DevColorBackground?.Text ?? "").Trim();
                _devColorOverrides["TextPrimary"] = (DevColorText?.Text ?? "").Trim();
                SaveSettings();
                ApplyDevColorOverrides();
                if (DevColorStatus != null) DevColorStatus.Text = "已应用并保存";
            }
            catch (Exception ex)
            {
                if (DevColorStatus != null) DevColorStatus.Text = "失败：" + ex.Message;
            }
        }

        private void DevColorReset_Click(object sender, RoutedEventArgs e)
        {
            _devColorOverrides.Clear();
            SaveSettings();
            ApplyResolvedTheme();
            if (DevColorPrimary != null) DevColorPrimary.Text = GetCurrentColor("Primary");
            if (DevColorBackground != null) DevColorBackground.Text = GetCurrentColor("Background");
            if (DevColorText != null) DevColorText.Text = GetCurrentColor("TextPrimary");
            UpdateDevColorPreview();
            if (DevColorStatus != null) DevColorStatus.Text = "已恢复当前主题";
        }

        // 把 3 个颜色应用到 Application.Resources，立即生效
        private void ApplyDevColorOverrides()
        {
            try
            {
                var res = Application.Current.Resources;
                if (_devColorOverrides.TryGetValue("Primary", out var p) && !string.IsNullOrWhiteSpace(p))
                {
                    var c = ParseHexColor(p);
                    if (c != Colors.Transparent)
                    {
                        res["Primary"] = c;
                        res["PrimaryBrush"] = new SolidColorBrush(c);
                        var h = Darken(c, 0.12);
                        var pp = Darken(c, 0.22);
                        res["PrimaryHover"] = h;
                        res["PrimaryHoverBrush"] = new SolidColorBrush(h);
                        res["PrimaryPressed"] = pp;
                        res["PrimaryPressedBrush"] = new SolidColorBrush(pp);
                    }
                }
                if (_devColorOverrides.TryGetValue("Background", out var b) && !string.IsNullOrWhiteSpace(b))
                {
                    var c = ParseHexColor(b);
                    if (c != Colors.Transparent)
                    {
                        res["Background"] = c;
                        res["BackgroundBrush"] = new SolidColorBrush(c);
                        res["BackgroundSecondary"] = Darken(c, 0.05);
                        res["BackgroundSecondaryBrush"] = new SolidColorBrush((Color)res["BackgroundSecondary"]);
                        res["BackgroundCard"] = Darken(c, 0.10);
                        res["BackgroundCardBrush"] = new SolidColorBrush((Color)res["BackgroundCard"]);
                        res["BackgroundHover"] = Darken(c, 0.18);
                        res["BackgroundHoverBrush"] = new SolidColorBrush((Color)res["BackgroundHover"]);
                        res["BackgroundActive"] = Darken(c, 0.28);
                        res["BackgroundActiveBrush"] = new SolidColorBrush((Color)res["BackgroundActive"]);
                    }
                }
                if (_devColorOverrides.TryGetValue("TextPrimary", out var t) && !string.IsNullOrWhiteSpace(t))
                {
                    var c = ParseHexColor(t);
                    if (c != Colors.Transparent)
                    {
                        res["TextPrimary"] = c;
                        res["TextPrimaryBrush"] = new SolidColorBrush(c);
                        res["TextSecondary"] = Darken(c, 0.30);
                        res["TextSecondaryBrush"] = new SolidColorBrush((Color)res["TextSecondary"]);
                        res["TextTertiary"] = Darken(c, 0.55);
                        res["TextTertiaryBrush"] = new SolidColorBrush((Color)res["TextTertiary"]);
                    }
                }
                UpdateThemeIcons();
            }
            catch (Exception ex)
            {
                if (DevColorStatus != null) DevColorStatus.Text = "应用失败：" + ex.Message;
            }
        }

        // 把颜色按比例调暗
        private static Color Darken(Color c, double ratio)
        {
            byte r = (byte)Math.Max(0, c.R * (1 - ratio));
            byte g = (byte)Math.Max(0, c.G * (1 - ratio));
            byte b = (byte)Math.Max(0, c.B * (1 - ratio));
            return Color.FromRgb(r, g, b);
        }

        // 三个颜色输入框的实时预览
        private void DevColor_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateDevColorPreview();
        }

        // 内部类：图标应用目标项
        private class DevTargetItem
        {
            public string Key { get; set; }
            public string Display { get; set; }
        }

        // ============================================================
        // 图标库（内置图标包，随 exe 嵌入）
        // ============================================================
        private string _iconPackCurrentRel = "";

        private void LoadIconPackCategories()
        {
            if (IconPackCategories == null) return;
            IconPackCategories.Children.Clear();
            var cats = new List<string>();
            foreach (var rel in IconPackManifest.AllFiles)
            {
                var parts = rel.Split('/');
                if (parts.Length >= 2 && !cats.Contains(parts[0]))
                    cats.Add(parts[0]);
            }
            foreach (var cat in cats)
            {
                var btn = new Button
                {
                    Content = cat,
                    Style = (Style)FindResource("BtnSecondary"),
                    Margin = new Thickness(0, 0, 8, 8),
                    Padding = new Thickness(12, 6, 12, 6)
                };
                string c = cat;
                btn.Click += (s, e) => SelectIconPackCategory(c, btn);
                IconPackCategories.Children.Add(btn);
            }
            if (cats.Count > 0)
            {
                var first = IconPackCategories.Children.OfType<Button>().FirstOrDefault();
                SelectIconPackCategory(cats[0], first);
            }
        }

        private void SelectIconPackCategory(string cat, Button btn)
        {
            foreach (Button b in IconPackCategories.Children.OfType<Button>())
                b.Style = (Style)FindResource("BtnSecondary");
            if (btn != null) btn.Style = (Style)FindResource("PrimaryButton");

            if (IconPackGrid == null) return;
            IconPackGrid.Children.Clear();
            Brush idleBg = (Brush)FindResource("BackgroundHoverBrush");
            Brush hoverBg = (Brush)FindResource("BackgroundActiveBrush");
            Brush idleBorder = (Brush)FindResource("BorderBrush");
            Brush hoverBorder = (Brush)FindResource("PrimaryBrush");
            Brush iconBrush = (Brush)FindResource("TextPrimaryBrush");
            foreach (var rel in IconPackManifest.AllFiles)
            {
                var parts = rel.Split('/');
                if (parts.Length >= 2 && parts[0] == cat)
                {
                    var tile = new Border
                    {
                        Width = 46, Height = 46, Margin = new Thickness(0, 0, 8, 8),
                        CornerRadius = new CornerRadius(8),
                        Background = idleBg,
                        BorderBrush = idleBorder,
                        BorderThickness = new Thickness(1),
                        Cursor = System.Windows.Input.Cursors.Hand,
                        ToolTip = System.IO.Path.GetFileNameWithoutExtension(parts[1])
                    };
                    tile.MouseEnter += (s, e) => { tile.BorderBrush = hoverBorder; tile.Background = hoverBg; };
                    tile.MouseLeave += (s, e) => { tile.BorderBrush = idleBorder; tile.Background = idleBg; };
                    // 单色 SVG：直接用 Path 渲染，随主题色（深/浅自动），不再依赖白/黑两套 PNG
                    var geo = SvgGeometryLoader.LoadGeometry(rel);
                    var path = new System.Windows.Shapes.Path
                    {
                        Data = geo, Fill = iconBrush, Width = 32, Height = 32,
                        Stretch = Stretch.Uniform,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    tile.Child = path;
                    string r = rel;
                    tile.MouseLeftButtonDown += (s, e) => ShowIconPackPreview(r);
                    IconPackGrid.Children.Add(tile);
                }
            }
        }

        private void IconSetWhite_Click(object sender, RoutedEventArgs e)
        {
            // IconPack 已改为单色 SVG，"白色/黑色"按钮改为切换预览背景（亮/暗），方便查看图标在不同背景上的效果
            IconSetWhite.Style = (Style)FindResource("PrimaryButton");
            IconSetBlack.Style = (Style)FindResource("BtnSecondary");
            if (IconPackPreviewBox != null) IconPackPreviewBox.Background = Brushes.White;
        }

        private void IconSetBlack_Click(object sender, RoutedEventArgs e)
        {
            IconSetBlack.Style = (Style)FindResource("PrimaryButton");
            IconSetWhite.Style = (Style)FindResource("BtnSecondary");
            if (IconPackPreviewBox != null) IconPackPreviewBox.Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x30));
        }

        private void ShowIconPackPreview(string rel)
        {
            _iconPackCurrentRel = rel;
            if (IconPackPreview != null) IconPackPreview.Visibility = Visibility.Visible;
            if (IconPackPreviewBox != null)
            {
                var geo = SvgGeometryLoader.LoadGeometry(rel);
                var path = new System.Windows.Shapes.Path
                {
                    Data = geo, Fill = (Brush)FindResource("TextPrimaryBrush"),
                    Width = 56, Height = 56, Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                IconPackPreviewBox.Child = path;
            }
            var parts = rel.Split('/');
            string fileName = parts.Length >= 2 ? parts[1] : rel;
            if (IconPackPreviewName != null) IconPackPreviewName.Text = System.IO.Path.GetFileNameWithoutExtension(fileName);
            if (IconPackPreviewPath != null) IconPackPreviewPath.Text = rel;
        }

        private void IconPackExportBtn_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_iconPackCurrentRel)) return;
            var parts = _iconPackCurrentRel.Split('/');
            string fileName = parts.Length >= 2 ? parts[1] : System.IO.Path.GetFileName(_iconPackCurrentRel);
            string targetDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Icons");
            try
            {
                System.IO.Directory.CreateDirectory(targetDir);
                string dest = System.IO.Path.Combine(targetDir, fileName);
                if (System.IO.File.Exists(dest))
                {
                    System.Windows.MessageBox.Show("已存在：\n" + dest + "\n（未覆盖）", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                var encRel = string.Join("/", _iconPackCurrentRel.Split('/').Select(p => Uri.EscapeDataString(p)));
                var resUri = new Uri("pack://application:,,,/Toolbox;component/IconPack/" + encRel, UriKind.Absolute);
                var stream = Application.GetResourceStream(resUri);
                if (stream == null) { System.Windows.MessageBox.Show("资源未找到。", "错误", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
                using (var fs = System.IO.File.Create(dest))
                    stream.Stream.CopyTo(fs);
                System.Windows.MessageBox.Show("已导出到：\n" + dest, "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (System.Exception ex)
            {
                System.Windows.MessageBox.Show("导出失败：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void IconPackCopyBtn_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_iconPackCurrentRel)) return;
            try { System.Windows.Clipboard.SetText(_iconPackCurrentRel); } catch { }
            System.Windows.MessageBox.Show("已复制路径：\n" + _iconPackCurrentRel, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ============================================================
        // 动画
        // ============================================================
        private void AnimatePageIn(FrameworkElement page)
        {
            if (page == null) return;
            if (!_animationsEnabled)
            {
                page.Opacity = 1;
                var tt = page.RenderTransform as TranslateTransform;
                if (tt != null) tt.Y = 0;
                else page.RenderTransform = new TranslateTransform();
                return;
            }
            page.Opacity = 0;
            if (page.RenderTransform == null) page.RenderTransform = new TranslateTransform();
            var ttAnim = page.RenderTransform as TranslateTransform;
            if (ttAnim != null) ttAnim.Y = 14;
            var sb = new Storyboard();
            var op = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.28))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(op, page);
            Storyboard.SetTargetProperty(op, new PropertyPath(UIElement.OpacityProperty));
            var tr = new DoubleAnimation(14, 0, TimeSpan.FromSeconds(0.32))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(tr, page);
            Storyboard.SetTargetProperty(tr, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));
            sb.Children.Add(op);
            sb.Children.Add(tr);
            page.BeginStoryboard(sb);
        }

        private void PlayCategoryToggle(WrapPanel wrap, System.Windows.Shapes.Path arrow, bool expand)
        {
            if (wrap == null || arrow == null) return;

            // 确保箭头旋转变换始终存在
            if (arrow.RenderTransform == null)
            {
                arrow.RenderTransform = new RotateTransform(0);
                arrow.RenderTransformOrigin = new Point(0.5, 0.5);
            }
            var arrowRot = arrow.RenderTransform as RotateTransform;
            if (arrowRot == null)
            {
                arrow.RenderTransform = arrowRot = new RotateTransform(0);
                arrow.RenderTransformOrigin = new Point(0.5, 0.5);
            }
            double fromAngle = arrowRot.Angle;
            double toAngle = expand ? 0 : -90;

            if (!_animationsEnabled)
            {
                arrowRot.Angle = toAngle;
                wrap.Visibility = expand ? Visibility.Visible : Visibility.Collapsed;
                wrap.Opacity = expand ? 1 : 0;
                if (wrap.RenderTransform == null) wrap.RenderTransform = new ScaleTransform();
                var scale = wrap.RenderTransform as ScaleTransform;
                if (scale != null) scale.ScaleY = expand ? 1 : 0;
                return;
            }

            var arrowSb = new Storyboard();
            var rot = new DoubleAnimation(fromAngle, toAngle, TimeSpan.FromSeconds(0.25))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(rot, arrow);
            Storyboard.SetTargetProperty(rot, new PropertyPath("(UIElement.RenderTransform).(RotateTransform.Angle)"));
            arrowSb.Children.Add(rot);
            arrow.BeginStoryboard(arrowSb);

            // 内容缩放 + 淡入/淡出
            if (wrap.RenderTransform == null) wrap.RenderTransform = new ScaleTransform();
            wrap.RenderTransformOrigin = new Point(0.5, 0);
            double dur = expand ? 0.26 : 0.2;
            var sb = new Storyboard();
            var sy = new DoubleAnimation(expand ? 0 : 1, expand ? 1 : 0, TimeSpan.FromSeconds(dur))
            {
                EasingFunction = new CubicEase { EasingMode = expand ? EasingMode.EaseOut : EasingMode.EaseIn }
            };
            Storyboard.SetTarget(sy, wrap);
            Storyboard.SetTargetProperty(sy, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleY)"));
            var op = new DoubleAnimation(expand ? 0 : 1, expand ? 1 : 0, TimeSpan.FromSeconds(dur));
            Storyboard.SetTarget(op, wrap);
            Storyboard.SetTargetProperty(op, new PropertyPath(UIElement.OpacityProperty));
            sb.Children.Add(sy);
            sb.Children.Add(op);

            if (expand)
            {
                wrap.Visibility = Visibility.Visible;
                wrap.BeginStoryboard(sb);
            }
            else
            {
                sb.Completed += (s, e) => { wrap.Visibility = Visibility.Collapsed; };
                wrap.BeginStoryboard(sb);
            }
        }

        private void PlayEntrance()
        {
            int index = 0;
            foreach (var panel in new Panel[] { BuiltInToolsPanel, AppToolsPanel })
            {
                if (panel == null) continue;
                // 分类卡片里的工具（内置页 / APP 旧卡片模式）
                foreach (Border card in panel.Children.OfType<Border>())
                {
                    if (!(card.Child is StackPanel stack) || stack.Children.Count < 2) continue;
                    if (!(stack.Children[1] is WrapPanel wrap)) continue;
                    foreach (Button btn in wrap.Children.OfType<Button>())
                        AnimateToolButton(btn, ref index);
                }
                // 直接平铺的工具（APP 页标签模式）
                foreach (Button btn in panel.Children.OfType<Button>())
                    AnimateToolButton(btn, ref index);
            }
        }

        private void AnimateToolButton(Button btn, ref int index)
        {
            if (btn == null) return;
            if (!_animationsEnabled)
            {
                btn.Opacity = 1;
                if (btn.RenderTransform == null) btn.RenderTransform = new TranslateTransform();
                var tt = btn.RenderTransform as TranslateTransform;
                if (tt != null) tt.Y = 0;
                return;
            }
            double delay = index * 0.035;
            index++;
            btn.Opacity = 0;
            if (btn.RenderTransform == null) btn.RenderTransform = new TranslateTransform();
            var sb = new Storyboard();
            var op = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.4))
            {
                BeginTime = TimeSpan.FromSeconds(delay),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(op, btn);
            Storyboard.SetTargetProperty(op, new PropertyPath(UIElement.OpacityProperty));
            var tr = new DoubleAnimation(14, 0, TimeSpan.FromSeconds(0.45))
            {
                BeginTime = TimeSpan.FromSeconds(delay),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(tr, btn);
            Storyboard.SetTargetProperty(tr, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));
            sb.Children.Add(op);
            sb.Children.Add(tr);
            btn.BeginStoryboard(sb);
        }

        private void AnimateThemeTransition()
        {
            var target = MainContent;
            if (target == null) return;
            if (!_animationsEnabled) return;
            var sb = new Storyboard();
            var op = new DoubleAnimationUsingKeyFrames();
            op.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            op.KeyFrames.Add(new LinearDoubleKeyFrame(0.55, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.12))));
            op.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.28))));
            Storyboard.SetTarget(op, target);
            Storyboard.SetTargetProperty(op, new PropertyPath(UIElement.OpacityProperty));
            sb.Children.Add(op);
            target.BeginStoryboard(sb);
        }

        private void AnimateSearchRefresh()
        {
            var target = (_currentPage == BuiltInPage) ? (FrameworkElement)BuiltInPage : AppPage;
            if (target == null) return;
            if (!_animationsEnabled)
            {
                target.Opacity = 1;
                return;
            }
            var sb = new Storyboard();
            var op = new DoubleAnimation(0.9, 1, TimeSpan.FromSeconds(0.12))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(op, target);
            Storyboard.SetTargetProperty(op, new PropertyPath(UIElement.OpacityProperty));
            sb.Children.Add(op);
            target.BeginStoryboard(sb);
        }

        // ============================================================
        // 设置页主题切换
        // ============================================================
        private void ThemeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ThemeSelector == null || ThemeSelector.SelectedValue == null) return;
            string key = ThemeSelector.SelectedValue as string;
            if (string.IsNullOrEmpty(key)) return;
            if (!key.Equals(_colorKey, StringComparison.OrdinalIgnoreCase))
            {
                _colorKey = key;
                ApplyResolvedTheme();
            }
        }

        private void ModeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ModeSelector == null || ModeSelector.SelectedValue == null) return;
            string key = ModeSelector.SelectedValue as string;
            if (string.IsNullOrEmpty(key)) return;
            if (!key.Equals(_appearanceMode, StringComparison.OrdinalIgnoreCase))
            {
                _appearanceMode = key;
                ApplyResolvedTheme();
            }
        }

        private void ThemeSelector_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateComboBoxArrow();
        }

        // Bug3 修复：ModeSelector 也要在加载时刷箭头（之前只刷 ThemeSelector，导致外观模式没有右侧小箭头）
        private void ModeSelector_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateComboBoxArrow();
        }

        private readonly Dictionary<string, Style> _animatedStyles = new Dictionary<string, Style>();

        private void AnimationToggle_Checked(object sender, RoutedEventArgs e)
        {
            _animationsEnabled = true;
            SaveSettings();
            UpdateAnimationStyles();
            LoadAndBuildTools(animate: false);
        }

        private void AnimationToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            _animationsEnabled = false;
            SaveSettings();
            UpdateAnimationStyles();
            LoadAndBuildTools(animate: false);
        }

        private Style GetOriginalStyle(string key)
        {
            if (!_animatedStyles.ContainsKey(key))
            {
                _animatedStyles[key] = FindResource(key) as Style;
            }
            return _animatedStyles[key];
        }

        private void UpdateAnimationStyles()
        {
            // 先保存原始动画样式（必须在覆盖资源键之前）
            var originalKeys = new[] { "ToolTile", "SideNavItem", "SideNavItemActive", "TitleBarButton", "TitleBarCloseButton", "ModernToggleSwitch" };
            foreach (var key in originalKeys)
            {
                GetOriginalStyle(key);
            }

            try
            {
                var appRes = Application.Current.Resources;
                if (_animationsEnabled)
                {
                    appRes["ToolTile"] = GetOriginalStyle("ToolTile");
                    appRes["SideNavItem"] = GetOriginalStyle("SideNavItem");
                    appRes["SideNavItemActive"] = GetOriginalStyle("SideNavItemActive");
                    appRes["TitleBarButton"] = GetOriginalStyle("TitleBarButton");
                    appRes["TitleBarCloseButton"] = GetOriginalStyle("TitleBarCloseButton");
                    appRes["ModernToggleSwitch"] = GetOriginalStyle("ModernToggleSwitch");
                }
                else
                {
                    appRes["ToolTile"] = FindResource("ToolTileStatic") as Style;
                    appRes["SideNavItem"] = FindResource("SideNavItemStatic") as Style;
                    appRes["SideNavItemActive"] = FindResource("SideNavItemActiveStatic") as Style;
                    appRes["TitleBarButton"] = FindResource("TitleBarButtonStatic") as Style;
                    appRes["TitleBarCloseButton"] = FindResource("TitleBarCloseButtonStatic") as Style;
                    appRes["ModernToggleSwitch"] = FindResource("ModernToggleSwitchStatic") as Style;
                }
            }
            catch { }

            // 刷新当前导航按钮样式（DynamicResource 会跟随资源键更新，但选中态需要显式重设）
            if (_currentNavButton != null)
            {
                _currentNavButton.Style = (Style)Application.Current.Resources["SideNavItemActive"];
            }
        }

        private void BtnLoadZipTheme_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.OpenFileDialog
            {
                Filter = "Zip 主题包 (*.zip)|*.zip",
                Title = "选择主题包"
            };
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            string zipPath = dialog.FileName;
            LoadZipThemeInternal(zipPath);
        }

        private void LoadZipThemeInternal(string zipPath)
        {
            string extractDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "themes", "custom");
            try
            {
                if (Directory.Exists(extractDir))
                    Directory.Delete(extractDir, true);
                Directory.CreateDirectory(extractDir);
                ZipFile.ExtractToDirectory(zipPath, extractDir);

                // 小白式：先找 Theme.xaml，没有就找任意 .xaml
                string xamlPath = System.IO.Path.Combine(extractDir, "Theme.xaml");
                if (!File.Exists(xamlPath))
                {
                    var xamls = Directory.GetFiles(extractDir, "*.xaml", SearchOption.AllDirectories);
                    if (xamls.Length > 0) xamlPath = xamls[0];
                }
                if (File.Exists(xamlPath))
                {
                    ApplyCustomTheme(xamlPath, extractDir);
                    MessageBox.Show("自定义主题已加载。\n\n提示：主题包只需要放一个 .xaml 文件（推荐命名为 Theme.xaml），系统会自动读取。", "主题导入成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("压缩包内没有找到主题文件。\n\n请确保 zip 里至少有一个 .xaml 文件，建议命名为 Theme.xaml。", "主题导入失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导入失败：{ex.Message}", "主题导入失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CreateSampleZipTheme_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string saveDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "themes");
                if (!Directory.Exists(saveDir)) Directory.CreateDirectory(saveDir);
                string savePath = System.IO.Path.Combine(saveDir, "示例主题包.zip");
                if (File.Exists(savePath)) File.Delete(savePath);

                using (var zip = ZipFile.Open(savePath, ZipArchiveMode.Create))
                {
                    var themeEntry = zip.CreateEntry("Theme.xaml");
                    using (var stream = themeEntry.Open())
                    using (var writer = new StreamWriter(stream))
                    {
                        writer.Write(GetSampleThemeXaml());
                    }
                    var readmeEntry = zip.CreateEntry("README.txt");
                    using (var stream = readmeEntry.Open())
                    using (var writer = new StreamWriter(stream))
                    {
                        writer.Write(GetThemeReadmeText());
                    }
                }
                MessageBox.Show($"示例主题包已生成：\n{savePath}\n\n你可以解压后修改颜色，再压缩成 zip 导入。", "示例主题包", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"生成失败：{ex.Message}", "示例主题包", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetSampleThemeXaml()
        {
            return @"<ResourceDictionary xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"" xmlns:system=""clr-namespace:System;assembly=mscorlib"">
  <!-- 主色调 -->
  <Color x:Key=""Primary"">#FF6B6B</Color>
  <Color x:Key=""PrimaryHover"">#EE5253</Color>
  <Color x:Key=""PrimaryPressed"">#D63031</Color>
  <!-- 背景色 -->
  <Color x:Key=""Background"">#1A1A2E</Color>
  <Color x:Key=""BackgroundSecondary"">#16213E</Color>
  <Color x:Key=""BackgroundCard"">#0F3460</Color>
  <Color x:Key=""BackgroundHover"">#533483</Color>
  <Color x:Key=""BackgroundActive"">#E94560</Color>
  <!-- 边框色 -->
  <Color x:Key=""Border"">#533483</Color>
  <Color x:Key=""BorderLight"">#16213E</Color>
  <!-- 文字色 -->
  <Color x:Key=""TextPrimary"">#FFFFFF</Color>
  <Color x:Key=""TextSecondary"">#BFCFE0</Color>
  <Color x:Key=""TextTertiary"">#6B7280</Color>
  <Color x:Key=""TextWhite"">#FFFFFF</Color>
  <!-- 强调色 -->
  <Color x:Key=""AccentBlue"">#60A5FA</Color>
  <Color x:Key=""AccentGreen"">#4ADE80</Color>
  <Color x:Key=""AccentOrange"">#FB923C</Color>
  <Color x:Key=""AccentRed"">#F87171</Color>
  <Color x:Key=""AccentPurple"">#C084FC</Color>
  <Color x:Key=""AccentTeal"">#2DD4BF</Color>
  <Color x:Key=""AccentPink"">#FB7185</Color>
  <!-- 图标色 -->
  <Color x:Key=""IconPrimary"">#FFFFFF</Color>
  <Color x:Key=""IconSecondary"">#BFCFE0</Color>
  <Color x:Key=""IconWhite"">#FFFFFF</Color>
  <!-- 阴影 -->
  <Color x:Key=""Shadow"">#FFFFFF</Color>
  <!-- 扩展尺寸配置（可选） -->
  <system:Double x:Key=""WindowCornerRadius"">18</system:Double>
  <system:Double x:Key=""WindowBorderThickness"">1</system:Double>
  <system:Double x:Key=""ButtonWidth"">246</system:Double>
  <system:Double x:Key=""ButtonHeight"">92</system:Double>
  <system:Double x:Key=""ButtonCornerRadius"">14</system:Double>
  <!-- 以下保持与上方颜色一一对应，不要删 -->
  <SolidColorBrush x:Key=""PrimaryBrush"" Color=""{StaticResource Primary}""/>
  <SolidColorBrush x:Key=""PrimaryHoverBrush"" Color=""{StaticResource PrimaryHover}""/>
  <SolidColorBrush x:Key=""PrimaryPressedBrush"" Color=""{StaticResource PrimaryPressed}""/>
  <SolidColorBrush x:Key=""BackgroundBrush"" Color=""{StaticResource Background}""/>
  <SolidColorBrush x:Key=""BackgroundSecondaryBrush"" Color=""{StaticResource BackgroundSecondary}""/>
  <SolidColorBrush x:Key=""BackgroundCardBrush"" Color=""{StaticResource BackgroundCard}""/>
  <SolidColorBrush x:Key=""BackgroundHoverBrush"" Color=""{StaticResource BackgroundHover}""/>
  <SolidColorBrush x:Key=""BackgroundActiveBrush"" Color=""{StaticResource BackgroundActive}""/>
  <SolidColorBrush x:Key=""BorderBrush"" Color=""{StaticResource Border}""/>
  <SolidColorBrush x:Key=""BorderLightBrush"" Color=""{StaticResource BorderLight}""/>
  <SolidColorBrush x:Key=""TextPrimaryBrush"" Color=""{StaticResource TextPrimary}""/>
  <SolidColorBrush x:Key=""TextSecondaryBrush"" Color=""{StaticResource TextSecondary}""/>
  <SolidColorBrush x:Key=""TextTertiaryBrush"" Color=""{StaticResource TextTertiary}""/>
  <SolidColorBrush x:Key=""TextWhiteBrush"" Color=""{StaticResource TextWhite}""/>
  <SolidColorBrush x:Key=""AccentBlueBrush"" Color=""{StaticResource AccentBlue}""/>
  <SolidColorBrush x:Key=""AccentGreenBrush"" Color=""{StaticResource AccentGreen}""/>
  <SolidColorBrush x:Key=""AccentOrangeBrush"" Color=""{StaticResource AccentOrange}""/>
  <SolidColorBrush x:Key=""AccentRedBrush"" Color=""{StaticResource AccentRed}""/>
  <SolidColorBrush x:Key=""AccentPurpleBrush"" Color=""{StaticResource AccentPurple}""/>
  <SolidColorBrush x:Key=""AccentTealBrush"" Color=""{StaticResource AccentTeal}""/>
  <SolidColorBrush x:Key=""AccentPinkBrush"" Color=""{StaticResource AccentPink}""/>
  <SolidColorBrush x:Key=""IconPrimaryBrush"" Color=""{StaticResource IconPrimary}""/>
  <SolidColorBrush x:Key=""IconSecondaryBrush"" Color=""{StaticResource IconSecondary}""/>
  <SolidColorBrush x:Key=""IconWhiteBrush"" Color=""{StaticResource IconWhite}""/>
  <SolidColorBrush x:Key=""ShadowBrush"" Color=""{StaticResource Shadow}""/>
</ResourceDictionary>";
        }

        private string GetThemeReadmeText()
        {
            return @"主题包制作说明（小白式）
==================
1. 复制本文件夹里的 Theme.xaml。
2. 用记事本打开 Theme.xaml，修改里面的颜色值（比如 #FF6B6B）。
   只改颜色，不要改 x:Key 名字，也不要删 SolidColorBrush 行。
3. 把 Theme.xaml 压缩成 zip（也可以改名，但不能是中文特殊符号）。
4. 在纳米工具箱设置页点击「选择 zip 文件」，选中压缩包即可应用。

高级配置（可选）：
   本文件夹里的 Theme.json 包含窗口圆角、边框粗细、按钮尺寸等设置，
   可用记事本修改。也可以把 Wallpaper.png/jpg 放到 zip 根目录作为背景。

提示：zip 里只放 Theme.xaml 一个文件就够了；
      如果有多个 .xaml，纳米工具箱会读取找到的第一个。";
        }

        private void UpdateSettingsUI()
        {
            if (ThemeSelector != null)
            {
                ThemeSelector.ItemsSource = _themeOptions;
                ThemeSelector.DisplayMemberPath = "DisplayName";
                ThemeSelector.SelectedValuePath = "Key";
                ThemeSelector.SelectedValue = _colorKey;
            }
            if (ModeSelector != null)
            {
                ModeSelector.ItemsSource = _modeOptions;
                ModeSelector.DisplayMemberPath = "DisplayName";
                ModeSelector.SelectedValuePath = "Key";
                ModeSelector.SelectedValue = _appearanceMode;
            }
            if (AnimationToggle != null) AnimationToggle.IsChecked = _animationsEnabled;
            if (CurrentThemeNameText != null)
            {
                string colorName = _colorKey;
                var cOpt = _themeOptions.FirstOrDefault(o => o.Key.Equals(_colorKey, StringComparison.OrdinalIgnoreCase));
                if (cOpt != null) colorName = cOpt.DisplayName;
                string modeLabel = _appearanceMode.Equals("Dark", StringComparison.OrdinalIgnoreCase) ? "深色"
                              : _appearanceMode.Equals("Light", StringComparison.OrdinalIgnoreCase) ? "浅色" : "自动";
                if (_currentThemeName.Equals("Custom", StringComparison.OrdinalIgnoreCase))
                    CurrentThemeNameText.Text = "当前：自定义主题包";
                else
                    CurrentThemeNameText.Text = $"当前：{colorName} · {modeLabel}";
            }
            UpdateThemeIcons();
        }

        // ============================================================
        // 内置工具 - 全部内嵌到主窗口右侧
        // ============================================================
        private void OpenNotepad() { ShowTool(new NotepadWindow(), "记事本"); }
        private void OpenCalculator() { ShowTool(new CalculatorWindow(), "计算器"); }
        private void OpenScreenshot() { ShowTool(new ScreenshotWindow(), "截图工具"); }
        private void OpenFileHash() { ShowTool(new FileHashWindow(), "文件哈希"); }
        private void OpenProcessManager() { ShowTool(new ProcessManagerWindow(), "进程管理"); }
        private void OpenPingTool() { ShowTool(new PingToolWindow(), "网络 Ping"); }
        private void OpenIPInfo() { ShowTool(new IPInfoWindow(), "IP 信息"); }
        private void OpenTextConverter() { ShowTool(new TextConverterWindow(), "文本转换"); }
        private void OpenUnitConverter() { ShowTool(new UnitConverterWindow(), "单位转换"); }
        private void OpenDownloader() { ShowTool(new DownloaderWindow(), "多线程下载"); }
        private void OpenBatchRename() { ShowTool(new BatchRenameWindow(), "批量重命名"); }
        private void OpenColorPicker() { ShowTool(new ColorPickerWindow(), "屏幕取色"); }
        private void OpenQrCode() { ShowTool(new QRCodeWindow(), "二维码生成"); }
        private void OpenBase64() { ShowTool(new Base64Window(), "Base64 转换"); }

        // 嵌套滚动冒泡：子滚动容器（图标网格、文本框等）滚到头时，把滚轮事件交给外层 ScrollViewer，
        // 解决“某些区域滚轮滚不动”的问题（否则子 ScrollViewer 会吞掉滚轮事件，外层页面无法滚动）。
        private void BubbleScroll_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            ScrollViewer sv = sender as ScrollViewer;
            if (sv == null && sender is TextBox tb)
                sv = tb.Template.FindName("PART_ContentHost", tb) as ScrollViewer;
            if (sv == null) return;

            bool scrollingUp = e.Delta > 0;
            bool atTop = sv.VerticalOffset <= 0.5;
            bool atBottom = sv.VerticalOffset >= sv.ScrollableHeight - 0.5;

            // 子容器仍可继续滚动时，交给它自己处理
            if (sv.ScrollableHeight > 0.5 && ((scrollingUp && !atTop) || (!scrollingUp && !atBottom)))
                return;

            // 子容器已到边界：交给最近的祖先 ScrollViewer 滚动
            ScrollViewer parent = FindVisualParent<ScrollViewer>(sv);
            while (parent != null)
            {
                bool pAtTop = parent.VerticalOffset <= 0.5;
                bool pAtBottom = parent.VerticalOffset >= parent.ScrollableHeight - 0.5;
                if ((scrollingUp && !pAtTop) || (!scrollingUp && !pAtBottom))
                {
                    parent.ScrollToVerticalOffset(parent.VerticalOffset - e.Delta);
                    e.Handled = true;
                    return;
                }
                parent = FindVisualParent<ScrollViewer>(parent);
            }
        }

        private static T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parent = VisualTreeHelper.GetParent(child);
            while (parent != null)
            {
                if (parent is T t) return t;
                parent = VisualTreeHelper.GetParent(parent);
            }
            return null;
        }

        // 保留旧的事件签名（XAML 中不再引用，但防止意外引用时报错）
        private void OpenNotepad_Click(object sender, RoutedEventArgs e) { OpenNotepad(); }
        private void OpenCalculator_Click(object sender, RoutedEventArgs e) { OpenCalculator(); }
        private void OpenScreenshot_Click(object sender, RoutedEventArgs e) { OpenScreenshot(); }
        private void OpenFileHash_Click(object sender, RoutedEventArgs e) { OpenFileHash(); }
        private void OpenProcessManager_Click(object sender, RoutedEventArgs e) { OpenProcessManager(); }
        private void OpenPingTool_Click(object sender, RoutedEventArgs e) { OpenPingTool(); }
        private void OpenIPInfo_Click(object sender, RoutedEventArgs e) { OpenIPInfo(); }
        private void OpenTextConverter_Click(object sender, RoutedEventArgs e) { OpenTextConverter(); }
        private void OpenUnitConverter_Click(object sender, RoutedEventArgs e) { OpenUnitConverter(); }
    }
}
