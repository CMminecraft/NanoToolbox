using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Toolbox
{
    public partial class PingToolWindow : UserControl
    {
        private bool isPinging = false;
        private CancellationTokenSource cts;

        public PingToolWindow()
        {
            InitializeComponent();
            this.Unloaded += PingToolWindow_Unloaded;
        }

        private void PingToolWindow_Unloaded(object sender, RoutedEventArgs e)
        {
            isPinging = false;
            cts?.Cancel();
        }

        private async void BtnPing_Click(object sender, RoutedEventArgs e)
        {
            if (isPinging) return;

            isPinging = true;
            ResultBox.Clear();
            List<long> latencies = new List<long>();

            cts = new CancellationTokenSource();

            try
            {
                while (isPinging)
                {
                    using (Ping ping = new Ping())
                    {
                        try
                        {
                            PingReply reply = await ping.SendPingAsync(HostInput.Text, 2000);
                            string result = $"{DateTime.Now:HH:mm:ss} - {reply.Status}";

                            if (reply.Status == IPStatus.Success)
                            {
                                result += $" 延迟: {reply.RoundtripTime}ms";
                                latencies.Add(reply.RoundtripTime);
                                AvgLatency.Text = $"平均: {latencies.Average():F2}ms";
                            }

                            ResultBox.AppendText(result + Environment.NewLine);
                            // 限制行数，避免长时间运行内存无限增长
                            if (ResultBox.LineCount > 500)
                            {
                                int start = ResultBox.GetCharacterIndexFromLineIndex(ResultBox.LineCount - 500);
                                ResultBox.Text = ResultBox.Text.Substring(start);
                            }
                            ResultBox.ScrollToEnd();
                        }
                        catch (Exception ex)
                        {
                            ResultBox.AppendText($"{DateTime.Now:HH:mm:ss} - 错误: {ex.Message}" + Environment.NewLine);
                        }
                    }

                    await Task.Delay(1000, cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // Stopped
            }
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            isPinging = false;
            cts?.Cancel();
        }
    }
}
