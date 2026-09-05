using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;

namespace Toolbox
{
    public partial class NotepadWindow : UserControl
    {
        private string currentFilePath = "";

        public NotepadWindow()
        {
            InitializeComponent();
        }

        private void BtnNew_Click(object sender, RoutedEventArgs e)
        {
            TextEditor.Text = "";
            currentFilePath = "";
        }

        private void BtnOpen_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
                Title = "打开文件"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                TextEditor.Text = System.IO.File.ReadAllText(openFileDialog.FileName);
                currentFilePath = openFileDialog.FileName;
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(currentFilePath))
            {
                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Filter = "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
                    Title = "保存文件"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    currentFilePath = saveFileDialog.FileName;
                }
                else
                {
                    return;
                }
            }

            System.IO.File.WriteAllText(currentFilePath, TextEditor.Text);
            MessageBox.Show("文件已保存", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnUndo_Click(object sender, RoutedEventArgs e)
        {
            if (TextEditor.CanUndo)
                TextEditor.Undo();
        }

        private void BtnRedo_Click(object sender, RoutedEventArgs e)
        {
            if (TextEditor.CanRedo)
                TextEditor.Redo();
        }

        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            TextEditor.SelectAll();
        }
    }
}
