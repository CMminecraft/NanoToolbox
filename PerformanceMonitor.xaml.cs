using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Toolbox
{
    public partial class PerformanceMonitor : UserControl
    {
        private readonly DispatcherTimer _timer;
        private readonly string _systemDrive;

        private PerformanceCounter _cpuCounter;     // 首次 new 极慢（5-10s），必须放后台线程
        private NetworkInterface _primaryNic;
        private bool _backendReady;                 // 后台初始化完成标志
        private ulong _totalMemory = 0;
        private long _lastBytesReceived = 0;
        private long _lastBytesSent = 0;
        private DateTime _lastSample = DateTime.UtcNow;

        public PerformanceMonitor()
        {
            InitializeComponent();

            _systemDrive = (Path.GetPathRoot(Environment.SystemDirectory) ?? "C:").TrimEnd('\\');

            // Timer 先准备好，但等后台初始化完成后再启动，避免 Tick 读未就绪的字段
            _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(2) };
            _timer.Tick += Timer_Tick;

            // 耗时操作（PerformanceCounter 首次构造 + WMI + 网卡枚举）放后台线程，
            // 不阻塞 UI，浮窗会显示"--"，等后台就绪后自动开始更新。
            Task.Run((Action)InitBackend);
        }

        // 后台线程：执行首次 PerformanceCounter 采样、WMI 查总内存、枚举活动网卡
        private void InitBackend()
        {
            try { _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total", true); _cpuCounter.NextValue(); } catch { }
            try { _totalMemory = QueryTotalMemory(); } catch { }
            if (_totalMemory == 0)
            {
                try
                {
                    var ms = new MEMORYSTATUSEX();
                    if (GlobalMemoryStatusEx(ms)) _totalMemory = ms.ullTotalPhys;
                }
                catch { }
            }

            try
            {
                _primaryNic = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up
                                && n.NetworkInterfaceType != NetworkInterfaceType.Loopback
                                && n.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                    .OrderByDescending(n => { try { return n.GetIPv4Statistics().BytesReceived; } catch { return 0; } })
                    .FirstOrDefault();
                if (_primaryNic != null)
                {
                    var s = _primaryNic.GetIPv4Statistics();
                    _lastBytesReceived = s.BytesReceived;
                    _lastBytesSent = s.BytesSent;
                }
            }
            catch { }

            _backendReady = true;

            // 回 UI 线程启动定时器，并立即跑一次刷新
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!_backendReady) return;
                Timer_Tick(null, null);
                _timer.Start();
            }));
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (!_backendReady) return; // 后台未就绪，Tick 直接跳过，不抛异常
            try { UpdateCpu(); } catch { }
            try { UpdateMem(); } catch { }
            try { UpdateDisk(); } catch { }
            try { UpdateNet(); } catch { }
        }

        private void UpdateCpu()
        {
            float v = 0;
            try { v = _cpuCounter?.NextValue() ?? 0; } catch { }
            v = Math.Max(0, Math.Min(100, v));
            CpuValue.Text = $"{v:0.0}%";
            CpuBar.Value = v;
        }

        private void UpdateMem()
        {
            if (_totalMemory == 0) return;
            ulong used = 0;
            try
            {
                var ms = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(ms)) used = _totalMemory - ms.ullAvailPhys;
            }
            catch { }
            float percent = _totalMemory > 0 ? (float)(used * 100.0 / _totalMemory) : 0;
            MemValue.Text = $"{FormatBytes(used)} / {FormatBytes(_totalMemory)}";
            MemBar.Value = percent;
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
                DiskValue.Text = $"{FormatBytes(used)} / {FormatBytes(total)}";
                DiskBar.Value = percent;
            }
            catch { }
        }

        private void UpdateNet()
        {
            if (_primaryNic == null) return;
            try
            {
                var s = _primaryNic.GetIPv4Statistics();
                long rx = s.BytesReceived;
                long tx = s.BytesSent;
                DateTime now = DateTime.UtcNow;
                double sec = (now - _lastSample).TotalSeconds;
                if (sec <= 0) { _lastSample = now; return; }
                double rxSpeed = Math.Max(0, (rx - _lastBytesReceived) / sec);
                double txSpeed = Math.Max(0, (tx - _lastBytesSent) / sec);
                _lastBytesReceived = rx;
                _lastBytesSent = tx;
                _lastSample = now;
                NetValue.Text = $"↓ {FormatRate(rxSpeed)}  ↑ {FormatRate(txSpeed)}";
            }
            catch { }
        }

        private static string FormatBytes(ulong b)
        {
            double v = b;
            string[] u = { "B", "KB", "MB", "GB", "TB" };
            int i = 0;
            while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
            return $"{v:0.##} {u[i]}";
        }

        private static string FormatBytes(long b)
        {
            if (b < 0) b = 0;
            return FormatBytes((ulong)b);
        }

        private static string FormatRate(double b)
        {
            if (b < 0) b = 0;
            string[] u = { "B/s", "KB/s", "MB/s", "GB/s" };
            int i = 0;
            while (b >= 1024 && i < u.Length - 1) { b /= 1024; i++; }
            return $"{b:0.##} {u[i]}";
        }

        private static ulong QueryTotalMemory()
        {
            try
            {
                using (var s = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"))
                using (var c = s.Get())
                {
                    foreach (ManagementObject o in c)
                    {
                        if (ulong.TryParse(o["TotalPhysicalMemory"]?.ToString(), out ulong v)) return v;
                    }
                }
            }
            catch { }
            return 0;
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
    }
}
