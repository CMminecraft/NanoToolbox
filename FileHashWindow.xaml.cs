using Microsoft.Win32;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace Toolbox
{
    public partial class FileHashWindow : UserControl
    {
        public FileHashWindow()
        {
            InitializeComponent();
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Title = "选择文件"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                FilePath.Text = openFileDialog.FileName;
            }
        }

        private void BtnCalculate_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(FilePath.Text) || !File.Exists(FilePath.Text))
            {
                MessageBox.Show("请选择有效的文件", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            using (var md5 = MD5.Create())
            using (var sha256 = SHA256.Create())
            using (var sha1 = SHA1.Create())
            using (var stream = File.OpenRead(FilePath.Text))
            {
                MD5Result.Text = BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "").ToLower();
                stream.Position = 0;
                SHA256Result.Text = BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", "").ToLower();
                stream.Position = 0;
                SHA1Result.Text = BitConverter.ToString(sha1.ComputeHash(stream)).Replace("-", "").ToLower();
            }
        }

        private void BtnCopyMD5_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(MD5Result.Text))
            {
                Clipboard.SetText(MD5Result.Text);
                MessageBox.Show("MD5 已复制", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnCopySHA1_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(SHA1Result.Text))
            {
                Clipboard.SetText(SHA1Result.Text);
                MessageBox.Show("SHA1 已复制", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnCopySHA256_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(SHA256Result.Text))
            {
                Clipboard.SetText(SHA256Result.Text);
                MessageBox.Show("SHA256 已复制", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
