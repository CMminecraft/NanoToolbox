using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Toolbox
{
    public class ProcessInfo
    {
        public string Name { get; set; }
        public int Id { get; set; }
        public double Memory { get; set; }
        public double Cpu { get; set; }
    }

    public class ProcessCpuSnapshot
    {
        public TimeSpan TotalProcessorTime { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public partial class ProcessManagerWindow : UserControl
    {
        private readonly Dictionary<int, ProcessCpuSnapshot> _lastSnapshot = new Dictionary<int, ProcessCpuSnapshot>();
        private DispatcherTimer _cpuTimer;
        private readonly int _processorCount;

        public ProcessManagerWindow()
        {
            InitializeComponent();
            _processorCount = Environment.ProcessorCount;
            LoadProcesses();

            _cpuTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _cpuTimer.Tick += (s, e) => LoadProcesses();
            _cpuTimer.Start();
            this.Unloaded += (s, e) => { _cpuTimer?.Stop(); };
        }

        private void LoadProcesses()
        {
            var now = DateTime.UtcNow;
            var processes = new List<ProcessInfo>();
            var newSnapshot = new Dictionary<int, ProcessCpuSnapshot>();
            var snapshotAvailable = _lastSnapshot.Count > 0;

            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    TimeSpan totalCpu;
                    try { totalCpu = p.TotalProcessorTime; }
                    catch { totalCpu = TimeSpan.Zero; }

                    double cpuPercent = 0;
                    if (snapshotAvailable && _lastSnapshot.TryGetValue(p.Id, out var last))
                    {
                        var elapsed = (now - last.Timestamp).TotalMilliseconds;
                        if (elapsed > 0)
                        {
                            var usedMs = (totalCpu - last.TotalProcessorTime).TotalMilliseconds;
                            cpuPercent = usedMs / elapsed / _processorCount * 100.0;
                            if (cpuPercent < 0) cpuPercent = 0;
                            if (cpuPercent > 100 * _processorCount) cpuPercent = 100 * _processorCount;
                        }
                    }

                    newSnapshot[p.Id] = new ProcessCpuSnapshot
                    {
                        TotalProcessorTime = totalCpu,
                        Timestamp = now
                    };

                    processes.Add(new ProcessInfo
                    {
                        Name = p.ProcessName,
                        Id = p.Id,
                        Memory = p.WorkingSet64 / 1024.0 / 1024.0,
                        Cpu = cpuPercent
                    });
                }
                catch { }
                finally
                {
                    try { p.Dispose(); } catch { }
                }
            }

            _lastSnapshot.Clear();
            foreach (var kvp in newSnapshot)
                _lastSnapshot[kvp.Key] = kvp.Value;

            ProcessGrid.ItemsSource = processes.OrderByDescending(p => p.Memory);
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadProcesses();
        }

        private void BtnKill_Click(object sender, RoutedEventArgs e)
        {
            if (ProcessGrid.SelectedItem is ProcessInfo info)
            {
                if (MessageBox.Show($"确定要结束进程 {info.Name} 吗？", "确认",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    try
                    {
                        using (var proc = Process.GetProcessById(info.Id))
                        {
                            proc.Kill();
                        }
                        LoadProcesses();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"无法结束进程: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (ProcessGrid.SelectedItem is ProcessInfo info)
            {
                try
                {
                    string path;
                    using (var proc = Process.GetProcessById(info.Id))
                    {
                        path = System.IO.Path.GetDirectoryName(proc.MainModule.FileName);
                    }
                    Process.Start("explorer.exe", path);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"无法打开文件夹: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
