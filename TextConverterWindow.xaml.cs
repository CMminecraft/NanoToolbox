using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace Toolbox
{
    public partial class TextConverterWindow : UserControl
    {
        public TextConverterWindow()
        {
            InitializeComponent();
        }

        private void BtnToUpper_Click(object sender, RoutedEventArgs e)
        {
            InputText.Text = InputText.Text.ToUpper();
        }

        private void BtnToLower_Click(object sender, RoutedEventArgs e)
        {
            InputText.Text = InputText.Text.ToLower();
        }

        private void BtnCapitalize_Click(object sender, RoutedEventArgs e)
        {
            TextInfo ti = CultureInfo.CurrentCulture.TextInfo;
            InputText.Text = ti.ToTitleCase(InputText.Text);
        }

        private void BtnRemoveSpaces_Click(object sender, RoutedEventArgs e)
        {
            InputText.Text = InputText.Text.Replace(" ", "").Replace("\t", "");
        }

        private void BtnReverse_Click(object sender, RoutedEventArgs e)
        {
            char[] arr = InputText.Text.ToCharArray();
            Array.Reverse(arr);
            InputText.Text = new string(arr);
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(InputText.Text);
            MessageBox.Show("已复制到剪贴板", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            InputText.Text = "";
        }
    }
}
