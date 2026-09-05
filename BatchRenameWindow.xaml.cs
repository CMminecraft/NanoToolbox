using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace Toolbox
{
    public partial class BatchRenameWindow : UserControl, IThemeAware
    {
        private List<RenameItem> _items = new List<RenameItem>();
        private bool _loading;

        public BatchRenameWindow()
        {
            // 必须在 InitializeComponent 之前置 true：
            // XAML 里的 <ComboBoxItem IsSelected="True"/> 在解析期间会同步触发 SelectionChanged，
            // 届时其它命名控件（如 FileCountText、PreviewList）尚未被 XAML 解析器初始化，
            // 任何在处理器里访问它们的代码都会 NRE。_loading 守卫可让初始化期间的回调直接返回。
            _loading = true;
            InitializeComponent();
            _loading = false;
        }

        public void RefreshTheme(bool isDark)
        {
            ThemeIconHelper.SetIcon(LogoImage, "rename.png", isDark);
        }

        private void BtnBrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            string folder = PickFolder();
            if (!string.IsNullOrEmpty(folder))
            {
                FolderInput.Text = folder;
                LoadFiles();
            }
        }

        private string PickFolder()
        {
            try
            {
                using var fbd = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "选择要重命名的文件夹",
                    ShowNewFolderButton = false
                };
                if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    return fbd.SelectedPath;
            }
            catch (Exception ex)
            {
                MessageBox.Show("无法打开文件夹选择：\n" + ex.Message);
            }
            return null;
        }

        private void FileTypeFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_loading) return;
            LoadFiles();
        }

        private void LoadFiles()
        {
            string folder = FolderInput.Text?.Trim();
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                _items.Clear();
                PreviewList.ItemsSource = null;
                FileCountText.Text = "共 0 个文件";
                return;
            }

            string[] patterns = GetPatterns();
            var files = new List<string>();
            try
            {
                foreach (var pat in patterns)
                {
                    files.AddRange(Directory.GetFiles(folder, pat, SearchOption.TopDirectoryOnly));
                }
                files = files.Distinct().OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
            }
            catch (Exception ex)
            {
                MessageBox.Show("读取目录失败：\n" + ex.Message);
                return;
            }

            _items = files.Select(f => new RenameItem
            {
                FullPath = f,
                OriginalName = Path.GetFileName(f),
                BaseName = Path.GetFileNameWithoutExtension(f),
                Ext = Path.GetExtension(f)
            }).ToList();

            FileCountText.Text = "共 " + _items.Count + " 个文件";
            Rebuild();
        }

        private string[] GetPatterns()
        {
            string custom = ExtFilter?.Text?.Trim();
            if (!string.IsNullOrEmpty(custom))
            {
                return custom.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.StartsWith(".") ? "*" + p : "*." + p)
                    .ToArray();
            }

            string sel = (FileTypeFilter.SelectedItem as ComboBoxItem)?.Content?.ToString();
            switch (sel)
            {
                case "图片": return new[] { "*.jpg", "*.jpeg", "*.png", "*.gif", "*.bmp", "*.webp" };
                case "文档": return new[] { "*.txt", "*.doc", "*.docx", "*.pdf", "*.md", "*.rtf" };
                case "视频": return new[] { "*.mp4", "*.mkv", "*.avi", "*.mov", "*.flv", "*.wmv" };
                case "音频": return new[] { "*.mp3", "*.wav", "*.flac", "*.aac", "*.ogg", "*.m4a" };
                case "代码": return new[] { "*.cs", "*.java", "*.py", "*.js", "*.ts", "*.cpp", "*.c", "*.h", "*.go", "*.rs" };
                default: return new[] { "*.*" };
            }
        }

        private void OnRuleChanged(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            Rebuild();
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadFiles();
        }

        private void Rebuild()
        {
            if (_items == null || _items.Count == 0)
            {
                PreviewList.ItemsSource = null;
                return;
            }

            int start = ParseInt(NumStart.Text, 1);
            int step = ParseInt(NumStep.Text, 1);
            int width = Math.Max(1, ParseInt(NumWidth.Text, 2));
            bool numOn = NumberingEnabled.IsChecked == true;
            bool numFront = (NumPosition.SelectedItem as ComboBoxItem)?.Content?.ToString() == "前面";
            string prefix = PrefixInput.Text ?? "";
            string suffix = SuffixInput.Text ?? "";
            string find = FindText.Text ?? "";
            string replace = ReplaceText.Text ?? "";
            bool regex = RegexEnabled.IsChecked == true;
            bool onlyName = MatchExtEnabled.IsChecked == true;

            for (int i = 0; i < _items.Count; i++)
            {
                var it = _items[i];
                string baseName = it.BaseName;

                if (!string.IsNullOrEmpty(find))
                {
                    try
                    {
                        if (regex)
                            baseName = Regex.Replace(baseName, find, replace);
                        else
                            baseName = baseName.Replace(find, replace);
                    }
                    catch { }
                }

                string numStr = "";
                if (numOn)
                {
                    int n = start + i * step;
                    numStr = n.ToString("D" + width);
                }

                string finalBase;
                if (numOn && numFront)
                    finalBase = prefix + numStr + baseName + suffix;
                else
                    finalBase = prefix + baseName + (numOn ? numStr : "") + suffix;

                string newName = finalBase + it.Ext;
                it.NewName = newName;
                it.NewFullPath = Path.Combine(Path.GetDirectoryName(it.FullPath), newName);
                it.Collision = it.NewName.Equals(it.OriginalName, StringComparison.OrdinalIgnoreCase) ? RenameStatus.Same : RenameStatus.Pending;
            }

            var nameCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var it in _items)
            {
                if (!nameCount.ContainsKey(it.NewName)) nameCount[it.NewName] = 0;
                nameCount[it.NewName]++;
            }
            var existingInDir = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(FolderInput.Text) && Directory.Exists(FolderInput.Text))
            {
                foreach (var f in Directory.GetFiles(FolderInput.Text))
                {
                    var name = Path.GetFileName(f);
                    var origNames = new HashSet<string>(_items.Select(x => x.OriginalName), StringComparer.OrdinalIgnoreCase);
                    if (!origNames.Contains(name)) existingInDir.Add(name);
                }
            }
            foreach (var it in _items)
            {
                if (nameCount[it.NewName] > 1)
                    it.Collision = RenameStatus.Duplicate;
                else if (existingInDir.Contains(it.NewName))
                    it.Collision = RenameStatus.Exists;
            }

            PreviewList.ItemsSource = null;
            PreviewList.ItemsSource = _items;
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            if (_items == null || _items.Count == 0) return;

            int conflict = _items.Count(x => x.Collision == RenameStatus.Duplicate || x.Collision == RenameStatus.Exists);
            if (conflict > 0)
            {
                var r = MessageBox.Show("有 " + conflict + " 个目标文件名与已有文件或互相冲突，仍要继续？", "存在冲突",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (r != MessageBoxResult.Yes) return;
            }

            int ok = 0, fail = 0;
            var tmps = new List<TempMove>();
            try
            {
                int idx = 0;
                foreach (var it in _items)
                {
                    if (it.Collision == RenameStatus.Same) continue;
                    string tmp = it.FullPath + ".__tmp_" + idx++;
                    File.Move(it.FullPath, tmp);
                    tmps.Add(new TempMove { Item = it, Temp = tmp });
                }
                foreach (var m in tmps)
                {
                    try
                    {
                        if (File.Exists(m.Item.NewFullPath))
                        {
                            string dir = Path.GetDirectoryName(m.Item.NewFullPath);
                            string baseN = Path.GetFileNameWithoutExtension(m.Item.NewName);
                            string ext = Path.GetExtension(m.Item.NewName);
                            int n = 1;
                            string candidate;
                            do
                            {
                                candidate = Path.Combine(dir, baseN + "_" + n + ext);
                                n++;
                            } while (File.Exists(candidate));
                            File.Move(m.Temp, candidate);
                        }
                        else
                        {
                            File.Move(m.Temp, m.Item.NewFullPath);
                        }
                        ok++;
                    }
                    catch
                    {
                        try { File.Move(m.Temp, m.Item.FullPath); } catch { }
                        fail++;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("重命名过程中出错：\n" + ex.Message);
            }

            ApplyStatus.Text = "完成：成功 " + ok + "，失败 " + fail;
            LoadFiles();
        }

        private static int ParseInt(string s, int def)
        {
            return int.TryParse(s, out int v) ? v : def;
        }

        private class TempMove
        {
            public RenameItem Item;
            public string Temp;
        }

        public class RenameItem
        {
            public string FullPath { get; set; }
            public string OriginalName { get; set; }
            public string BaseName { get; set; }
            public string Ext { get; set; }
            public string NewName { get; set; }
            public string NewFullPath { get; set; }
            public RenameStatus Collision { get; set; }
            public string StatusText
            {
                get
                {
                    switch (Collision)
                    {
                        case RenameStatus.Pending: return "待重命名";
                        case RenameStatus.Same: return "无变化";
                        case RenameStatus.Duplicate: return "重复";
                        case RenameStatus.Exists: return "目标已存在";
                        case RenameStatus.Done: return "已重命名";
                        default: return "";
                    }
                }
            }
        }

        public enum RenameStatus { Pending, Same, Duplicate, Exists, Done }
    }
}
