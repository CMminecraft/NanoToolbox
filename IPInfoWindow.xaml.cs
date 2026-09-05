using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;

namespace Toolbox
{
    public partial class IPInfoWindow : UserControl
    {
        public IPInfoWindow()
        {
            InitializeComponent();
            LoadIPInfo();
        }

        private void LoadIPInfo()
        {
            try
            {
                HostName.Text = Dns.GetHostName();

                var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up);

                var ipv4 = interfaces.SelectMany(n => n.GetIPProperties().UnicastAddresses)
                    .Where(a => a.Address != null && a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    .Select(a => a.Address.ToString());
                IPv4Addresses.Text = string.Join(Environment.NewLine, ipv4);

                var ipv6 = interfaces.SelectMany(n => n.GetIPProperties().UnicastAddresses)
                    .Where(a => a.Address != null && a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                    .Select(a => a.Address.ToString());
                IPv6Addresses.Text = string.Join(Environment.NewLine, ipv6);

                var dns = interfaces.SelectMany(n => n.GetIPProperties().DnsAddresses)
                    .Where(a => a != null)
                    .Select(a => a.ToString());
                DnsServers.Text = string.Join(Environment.NewLine, dns);

                var gateways = interfaces.SelectMany(n => n.GetIPProperties().GatewayAddresses)
                    .Where(g => g.Address != null)
                    .Select(g => g.Address.ToString());
                Gateways.Text = string.Join(Environment.NewLine, gateways);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"获取网络信息失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnGetInfo_Click(object sender, RoutedEventArgs e)
        {
            LoadIPInfo();
        }
    }
}
