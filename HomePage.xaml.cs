using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Toolbox
{
    public partial class HomePage : UserControl, IThemeAware
    {
        private readonly DispatcherTimer _timer;
        private readonly Func<bool> _isDarkThemeGetter;
        private readonly Func<string> _colorKeyGetter;
        private readonly Func<string> _appearanceModeGetter;
        private readonly Func<string> _currentThemeNameGetter;
        private readonly Func<string> _customThemePathGetter;

        private PerformanceCounter _cpuCounter; // 首次 new 极慢（5-10s），在 InitBackend 后台线程初始化
        private ulong _totalMemory = 0;
        private readonly string _systemDrive;
        private bool _backendReady; // 后台硬件信息采集完成标志

        public HomePage(Func<bool> isDarkThemeGetter,
                        Func<string> colorKeyGetter,
                        Func<string> appearanceModeGetter,
                        Func<string> currentThemeNameGetter,
                        Func<string> customThemePathGetter)
        {
            InitializeComponent();
            _isDarkThemeGetter = isDarkThemeGetter;
            _colorKeyGetter = colorKeyGetter;
            _appearanceModeGetter = appearanceModeGetter;
            _currentThemeNameGetter = currentThemeNameGetter;
            _customThemePathGetter = customThemePathGetter;

            try { _systemDrive = Path.GetPathRoot(Environment.SystemDirectory); } catch { }
            _systemDrive = _systemDrive?.TrimEnd('\\') ?? "C:";

            // Timer 先准备好但 Start 延后，等后台初始化完成
            _timer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _timer.Tick += Timer_Tick;

            // 立即刷新不依赖硬件的部分：欢迎语、配色 / 模式 / 主题文字、首页横幅图标
            UpdateCfgText();
            UpdateBannerIcon(_isDarkThemeGetter());

            // 后台线程跑：WMI 查总内存 + PerformanceCounter 首次构造 + 10+ 个硬件 WMI 查询
            // 这些每次启动 5-10s，全部放到后台，UI 线程立即返回，窗口先显示出来
            System.Threading.Tasks.Task.Run((Action)InitBackend);
        }

        // 后台线程：采集总内存、CPU 性能计数器、全部硬件信息到本地变量；
        // 完成后回 UI 线程一次性填充到控件，避免 WPF 跨线程访问抛异常。
        private void InitBackend()
        {
            try { _totalMemory = GetTotalMemoryBytes(); } catch { }
            if (_totalMemory == 0)
            {
                try
                {
                    var memStatus = new MEMORYSTATUSEX();
                    if (GlobalMemoryStatusEx(memStatus))
                        _totalMemory = memStatus.ullTotalPhys;
                }
                catch { }
            }

            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total", true);
                _cpuCounter.NextValue();
            }
            catch { }

            // 采集全部硬件信息到本地变量
            string osName, osArch, cpuName, cpuFreq, cores, cpuArch, gpuName, mb, screen, dpi, nic, machine;
            try { osName = GetOsCaption(); } catch { osName = Environment.OSVersion.VersionString; }
            try { osArch = Environment.Is64BitOperatingSystem ? "64 位 (x64)" : "32 位 (x86)"; } catch { osArch = "--"; }
            try { cpuName = GetCpuName(); } catch { cpuName = "--"; }
            try { cpuFreq = GetCpuMaxFreq(); } catch { cpuFreq = "--"; }
            try { cores = $"{Environment.ProcessorCount} 核 / {GetLogicalProcessorCount()} 线程"; } catch { cores = Environment.ProcessorCount + " 核"; }
            try { cpuArch = GetCpuArch(); } catch { cpuArch = "--"; }
            try { gpuName = GetGpuName(); } catch { gpuName = "--"; }
            try { mb = GetMotherboard(); } catch { mb = "--"; }
            try { screen = GetScreenInfo(); } catch { screen = "--"; }
            try { dpi = GetDpiScale(); } catch { dpi = "--"; }
            try { nic = GetActiveNic(); } catch { nic = "--"; }
            try { machine = $"{Environment.MachineName}  ·  {Environment.UserName}"; } catch { machine = "--"; }

            _backendReady = true;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!_backendReady) return;
                OsName.Text = osName;
                OsArch.Text = osArch;
                CpuName.Text = cpuName;
                CpuFreq.Text = cpuFreq;
                CoreCount.Text = cores;
                CpuArch.Text = cpuArch;
                if (_totalMemory > 0) TotalMemory.Text = FormatBytes(_totalMemory);
                GpuName.Text = gpuName;
                Motherboard.Text = mb;
                ScreenInfo.Text = screen;
                DpiScale.Text = dpi;
                NetAdapter.Text = nic;
                MachineUser.Text = machine;

                Timer_Tick(null, null);
                _timer.Start();
            }));
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (!_backendReady) return; // 后台 WMI 还在跑，Tick 直接跳过，等就绪后回 UI 时已自动跑一次
            try { UpdateCpu(); } catch { }
            try { UpdateMemory(); } catch { }
            try { UpdateDisk(); } catch { }
            try { UpdateCfgText(); } catch { }
        }

        private void UpdateCfgText()
        {
            try
            {
                string colorName = _colorKeyGetter?.Invoke() ?? "默认";
                if (colorName.Equals("Default", StringComparison.OrdinalIgnoreCase)) colorName = "默认蓝";
                else colorName += "色";

                string mode = _appearanceModeGetter?.Invoke();
                string modeLabel = mode?.Equals("Dark", StringComparison.OrdinalIgnoreCase) == true ? "深色"
                                 : mode?.Equals("Light", StringComparison.OrdinalIgnoreCase) == true ? "浅色" : "自动";

                string themeName = _currentThemeNameGetter?.Invoke();
                string customPath = _customThemePathGetter?.Invoke();
                string themeLabel = !string.IsNullOrEmpty(customPath) ? "自定义" : (themeName ?? "Dark");

                CfgColor.Text = colorName;
                CfgMode.Text = modeLabel;
                CfgTheme.Text = !string.IsNullOrEmpty(customPath)
                    ? $"自定义主题包: {Path.GetFileName(customPath)}"
                    : $"内置主题 · {themeLabel}";

                string modePrefix = modeLabel == "深色" ? "夜色" : modeLabel == "浅色" ? "白昼" : "智能";
                WelcomeSub.Text = $"当前为 {colorName} · {modePrefix}模式，所有工具已就绪。";
            }
            catch { }
        }

        private void UpdateCpu()
        {
            float v = 0;
            try { v = _cpuCounter?.NextValue() ?? 0; } catch { }
            v = Math.Max(0, Math.Min(100, v));
            HomeCpuValue.Text = $"{v:0.0}%";
            HomeCpuBar.Value = v;
        }

        private void UpdateMemory()
        {
            if (_totalMemory == 0) return;
            ulong avail = 0;
            ulong used = 0;
            try
            {
                var ms = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(ms))
                {
                    avail = ms.ullAvailPhys;
                    used = _totalMemory - avail;
                }
            }
            catch { }
            float percent = _totalMemory > 0 ? (float)(used * 100.0 / _totalMemory) : 0;
            HomeMemValue.Text = $"{FormatBytes(used)} / {FormatBytes(_totalMemory)}";
            HomeMemBar.Value = percent;
            AvailMemory.Text = FormatBytes(avail);
        }

        private void UpdateDisk()
        {
            try
            {
                var drive = new DriveInfo(_systemDrive);
                if (!drive.IsReady) return;
                long total = drive.TotalSize;
                long free = drive.AvailableFreeSpace;
                long used = total - free;
                float percent = total > 0 ? (float)(used * 100.0 / total) : 0;
                HomeDiskValue.Text = $"{FormatBytes((ulong)used)} / {FormatBytes((ulong)total)}";
                HomeDiskBar.Value = percent;
                HomeDiskLabel.Text = $"磁盘 ({drive.Name.TrimEnd('\\')})";
            }
            catch { }
        }

        // LoadSystemInfo 已被 InitBackend 替代（后台采集 + UI 线程填充），保留各 Get* 查询方法
        private string GetOsCaption()
        {
            try
            {
                using (var s = new ManagementObjectSearcher("SELECT Caption, Version, BuildNumber FROM Win32_OperatingSystem"))
                using (var c = s.Get())
                {
                    foreach (ManagementObject o in c)
                    {
                        var cap = o["Caption"]?.ToString()?.Trim() ?? "";
                        var ver = o["Version"]?.ToString() ?? "";
                        var bld = o["BuildNumber"]?.ToString() ?? "";
                        if (!string.IsNullOrEmpty(cap))
                        {
                            if (!string.IsNullOrEmpty(bld))
                                return $"{cap} · {ver} ({bld})";
                            return $"{cap} · {ver}";
                        }
                    }
                }
            }
            catch { }
            return Environment.OSVersion.VersionString;
        }

        private string GetCpuName()
        {
            try
            {
                using (var s = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor"))
                using (var c = s.Get())
                {
                    foreach (ManagementObject o in c)
                    {
                        return (o["Name"]?.ToString() ?? "").Trim();
                    }
                }
            }
            catch { }
            return "Unknown";
        }

        private string GetCpuMaxFreq()
        {
            try
            {
                using (var s = new ManagementObjectSearcher("SELECT MaxClockSpeed FROM Win32_Processor"))
                using (var c = s.Get())
                {
                    foreach (ManagementObject o in c)
                    {
                        if (uint.TryParse(o["MaxClockSpeed"]?.ToString(), out uint mhz))
                            return $"{mhz / 1000.0:0.##} GHz";
                    }
                }
            }
            catch { }
            return "--";
        }

        private int GetLogicalProcessorCount()
        {
            try
            {
                using (var s = new ManagementObjectSearcher("SELECT NumberOfLogicalProcessors FROM Win32_Processor"))
                using (var c = s.Get())
                {
                    int total = 0;
                    foreach (ManagementObject o in c)
                    {
                        if (int.TryParse(o["NumberOfLogicalProcessors"]?.ToString(), out int n))
                            total += n;
                    }
                    if (total > 0) return total;
                }
            }
            catch { }
            return Environment.ProcessorCount;
        }

        private string GetCpuArch()
        {
            try
            {
                using (var s = new ManagementObjectSearcher("SELECT Architecture FROM Win32_Processor"))
                using (var c = s.Get())
                {
                    foreach (ManagementObject o in c)
                    {
                        if (ushort.TryParse(o["Architecture"]?.ToString(), out ushort a))
                        {
                            if (a == 0) return "x86";
                            if (a == 6) return "x64 (AMD64)";
                            if (a == 9) return "x64 (EM64T)";
                            if (a == 12) return "ARM64";
                            return $"架构 {a}";
                        }
                    }
                }
            }
            catch { }
            return "--";
        }

        private string GetGpuName()
        {
            try
            {
                using (var s = new ManagementObjectSearcher("SELECT Name, AdapterRAM FROM Win32_VideoController"))
                using (var c = s.Get())
                {
                    foreach (ManagementObject o in c)
                    {
                        var name = o["Name"]?.ToString() ?? "";
                        if (string.IsNullOrEmpty(name)) continue;
                        if (name.IndexOf("Basic", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                        ulong ram = 0;
                        ulong.TryParse(o["AdapterRAM"]?.ToString(), out ram);
                        if (ram > 0)
                            return $"{name} · {FormatBytes(ram)}";
                        return name;
                    }
                }
            }
            catch { }
            return "--";
        }

        private string GetMotherboard()
        {
            try
            {
                string product = "", manufacturer = "";
                using (var s = new ManagementObjectSearcher("SELECT Product, Manufacturer FROM Win32_BaseBoard"))
                using (var c = s.Get())
                {
                    foreach (ManagementObject o in c)
                    {
                        product = o["Product"]?.ToString() ?? "";
                        manufacturer = o["Manufacturer"]?.ToString() ?? "";
                        break;
                    }
                }
                if (string.IsNullOrEmpty(product) && string.IsNullOrEmpty(manufacturer))
                {
                    using (var s = new ManagementObjectSearcher("SELECT Model, Manufacturer FROM Win32_ComputerSystem"))
                    using (var c = s.Get())
                    {
                        foreach (ManagementObject o in c)
                        {
                            product = o["Model"]?.ToString() ?? "";
                            manufacturer = o["Manufacturer"]?.ToString() ?? "";
                            break;
                        }
                    }
                }
                string s2 = $"{manufacturer} {product}".Trim();
                return string.IsNullOrEmpty(s2) ? "--" : s2;
            }
            catch { }
            return "--";
        }

        private string GetScreenInfo()
        {
            try
            {
                var sb = System.Windows.Forms.Screen.PrimaryScreen?.Bounds ?? new System.Drawing.Rectangle(0, 0, 0, 0);
                return $"{sb.Width} × {sb.Height}";
            }
            catch { }
            return "--";
        }

        private string GetDpiScale()
        {
            try
            {
                using (var bmp = new System.Drawing.Bitmap(1, 1))
                using (var g = System.Drawing.Graphics.FromImage(bmp))
                {
                    float scale = g.DpiX / 96f;
                    return $"{scale:0.##}× ({g.DpiX:0} DPI)";
                }
            }
            catch { }
            return "--";
        }

        private string GetActiveNic()
        {
            try
            {
                var nic = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up
                                && n.NetworkInterfaceType != NetworkInterfaceType.Loopback
                                && n.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                    .OrderByDescending(n => { try { return n.GetIPv4Statistics().BytesReceived; } catch { return 0; } })
                    .FirstOrDefault();
                if (nic == null) return "无活动网络";
                var props = nic.GetIPProperties();
                var ip = props.UnicastAddresses
                    .Where(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    .Select(a => a.Address.ToString())
                    .FirstOrDefault();
                return string.IsNullOrEmpty(ip)
                    ? nic.Name
                    : $"{nic.Name} · {ip}";
            }
            catch { }
            return "--";
        }

        private static ulong GetTotalMemoryBytes()
        {
            try
            {
                using (var s = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"))
                using (var c = s.Get())
                {
                    foreach (ManagementObject o in c)
                    {
                        if (ulong.TryParse(o["TotalPhysicalMemory"]?.ToString(), out ulong v))
                            return v;
                    }
                }
            }
            catch { }
            return 0;
        }

        private static string FormatBytes(ulong bytes)
        {
            double b = bytes;
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            int i = 0;
            while (b >= 1024 && i < units.Length - 1) { b /= 1024; i++; }
            return $"{b:0.##} {units[i]}";
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
            public MEMORYSTATUSEX() { dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX)); }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

        // IThemeAware：主题切换时由 MainWindow 通知刷新
        public void RefreshTheme(bool isDark)
        {
            UpdateBannerIcon(isDark);
            UpdateCfgText();
        }

        private void UpdateBannerIcon(bool isDark)
        {
            try
            {
                // 横幅背景为 Primary 蓝色渐变，白色工具箱图标对比清晰；统一使用 White 套
                var uri = new Uri("pack://application:,,,/Toolbox;component/Icons/White/toolbox.png", UriKind.Absolute);
                HomeBannerIcon.Source = new BitmapImage(uri);
            }
            catch { }
        }
    }
}
