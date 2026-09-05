using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace Toolbox
{
    public partial class UnitConverterWindow : UserControl
    {
        private Dictionary<string, List<string>> units = new Dictionary<string, List<string>>
        {
            {"长度", new List<string> {"米", "千米", "厘米", "毫米", "英寸", "英尺", "码", "英里"}},
            {"重量", new List<string> {"千克", "克", "毫克", "吨", "磅", "盎司"}},
            {"面积", new List<string> {"平方米", "平方千米", "平方厘米", "平方英寸", "平方英尺", "公顷", "亩"}},
            {"温度", new List<string> {"摄氏度", "华氏度", "开尔文"}}
        };

        public UnitConverterWindow()
        {
            InitializeComponent();
            CategoryCombo.SelectedIndex = 0;
        }

        private void CategoryCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string category = CategoryCombo.SelectedItem is ComboBoxItem catItem ? catItem.Content.ToString() : CategoryCombo.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(category) || !units.ContainsKey(category)) return;
            FromUnit.Items.Clear();
            ToUnit.Items.Clear();
            
            foreach (string unit in units[category])
            {
                FromUnit.Items.Add(unit);
                ToUnit.Items.Add(unit);
            }
            
            FromUnit.SelectedIndex = 0;
            ToUnit.SelectedIndex = 1;
        }

        private void BtnConvert_Click(object sender, RoutedEventArgs e)
        {
            if (!double.TryParse(InputValue.Text, out double value))
            {
                MessageBox.Show("请输入有效的数字", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string category = CategoryCombo.SelectedItem is ComboBoxItem catItem ? catItem.Content.ToString() : CategoryCombo.SelectedItem?.ToString();
            string from = FromUnit.SelectedItem?.ToString();
            string to = ToUnit.SelectedItem?.ToString();

            if (string.IsNullOrEmpty(category) || string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to))
                return;

            double result = ConvertUnit(value, category, from, to);
            Result.Text = $"{result:F4} {to}";
        }

        private double ConvertUnit(double value, string category, string from, string to)
        {
            if (from == to) return value;

            switch (category)
            {
                case "长度":
                    return ConvertLength(value, from, to);
                case "重量":
                    return ConvertWeight(value, from, to);
                case "面积":
                    return ConvertArea(value, from, to);
                case "温度":
                    return ConvertTemperature(value, from, to);
                default:
                    return value;
            }
        }

        private double ConvertLength(double value, string from, string to)
        {
            double meters = value;
            switch (from)
            {
                case "千米": meters = value * 1000; break;
                case "厘米": meters = value / 100; break;
                case "毫米": meters = value / 1000; break;
                case "英寸": meters = value * 0.0254; break;
                case "英尺": meters = value * 0.3048; break;
                case "码": meters = value * 0.9144; break;
                case "英里": meters = value * 1609.34; break;
            }

            switch (to)
            {
                case "千米": return meters / 1000;
                case "厘米": return meters * 100;
                case "毫米": return meters * 1000;
                case "英寸": return meters / 0.0254;
                case "英尺": return meters / 0.3048;
                case "码": return meters / 0.9144;
                case "英里": return meters / 1609.34;
                default: return meters;
            }
        }

        private double ConvertWeight(double value, string from, string to)
        {
            double kg = value;
            switch (from)
            {
                case "克": kg = value / 1000; break;
                case "毫克": kg = value / 1000000; break;
                case "吨": kg = value * 1000; break;
                case "磅": kg = value * 0.453592; break;
                case "盎司": kg = value * 0.0283495; break;
            }

            switch (to)
            {
                case "克": return kg * 1000;
                case "毫克": return kg * 1000000;
                case "吨": return kg / 1000;
                case "磅": return kg / 0.453592;
                case "盎司": return kg / 0.0283495;
                default: return kg;
            }
        }

        private double ConvertArea(double value, string from, string to)
        {
            double sqm = value;
            switch (from)
            {
                case "平方千米": sqm = value * 1000000; break;
                case "平方厘米": sqm = value / 10000; break;
                case "平方英寸": sqm = value * 0.00064516; break;
                case "平方英尺": sqm = value * 0.092903; break;
                case "公顷": sqm = value * 10000; break;
                case "亩": sqm = value * 666.6667; break;
            }

            switch (to)
            {
                case "平方千米": return sqm / 1000000;
                case "平方厘米": return sqm * 10000;
                case "平方英寸": return sqm / 0.00064516;
                case "平方英尺": return sqm / 0.092903;
                case "公顷": return sqm / 10000;
                case "亩": return sqm / 666.6667;
                default: return sqm;
            }
        }

        private double ConvertTemperature(double value, string from, string to)
        {
            double celsius = value;
            switch (from)
            {
                case "华氏度": celsius = (value - 32) * 5 / 9; break;
                case "开尔文": celsius = value - 273.15; break;
            }

            switch (to)
            {
                case "华氏度": return celsius * 9 / 5 + 32;
                case "开尔文": return celsius + 273.15;
                default: return celsius;
            }
        }
    }
}
