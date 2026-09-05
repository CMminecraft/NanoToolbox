using System.Windows;
using System.Windows.Controls;

namespace Toolbox
{
    public partial class CalculatorWindow : UserControl
    {
        private double firstNumber = 0;
        private double secondNumber = 0;
        private string operation = "";
        private bool isNewNumber = true;

        public CalculatorWindow()
        {
            InitializeComponent();
            Display.Text = "0";
        }

        private void BtnNumber_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            string number = btn.Content.ToString();

            if (isNewNumber)
            {
                Display.Text = number;
                isNewNumber = false;
            }
            else
            {
                Display.Text += number;
            }
        }

        private void BtnZero_Click(object sender, RoutedEventArgs e)
        {
            if (!isNewNumber && Display.Text != "0")
            {
                Display.Text += "0";
            }
        }

        private void BtnDot_Click(object sender, RoutedEventArgs e)
        {
            if (!Display.Text.Contains("."))
            {
                Display.Text += ".";
                isNewNumber = false;
            }
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            Display.Text = "0";
            firstNumber = 0;
            secondNumber = 0;
            operation = "";
            isNewNumber = true;
        }

        private void BtnNegate_Click(object sender, RoutedEventArgs e)
        {
            if (Display.Text != "0")
            {
                if (Display.Text.StartsWith("-"))
                {
                    Display.Text = Display.Text.Substring(1);
                }
                else
                {
                    Display.Text = "-" + Display.Text;
                }
            }
        }

        private void BtnPercent_Click(object sender, RoutedEventArgs e)
        {
            double num = double.Parse(Display.Text);
            Display.Text = (num / 100).ToString();
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            firstNumber = double.Parse(Display.Text);
            operation = "+";
            isNewNumber = true;
        }

        private void BtnSubtract_Click(object sender, RoutedEventArgs e)
        {
            firstNumber = double.Parse(Display.Text);
            operation = "-";
            isNewNumber = true;
        }

        private void BtnMultiply_Click(object sender, RoutedEventArgs e)
        {
            firstNumber = double.Parse(Display.Text);
            operation = "*";
            isNewNumber = true;
        }

        private void BtnDivide_Click(object sender, RoutedEventArgs e)
        {
            firstNumber = double.Parse(Display.Text);
            operation = "/";
            isNewNumber = true;
        }

        private void BtnEquals_Click(object sender, RoutedEventArgs e)
        {
            secondNumber = double.Parse(Display.Text);
            double result = 0;

            switch (operation)
            {
                case "+":
                    result = firstNumber + secondNumber;
                    break;
                case "-":
                    result = firstNumber - secondNumber;
                    break;
                case "*":
                    result = firstNumber * secondNumber;
                    break;
                case "/":
                    if (secondNumber != 0)
                        result = firstNumber / secondNumber;
                    else
                        Display.Text = "错误";
                    break;
            }

            if (operation != "" && secondNumber != 0)
            {
                Display.Text = result.ToString();
            }
            operation = "";
            isNewNumber = true;
        }
    }
}
