using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Toolbox
{
    public partial class DownloaderWindow : UserControl, IThemeAware
    {
        private static readonly HttpClient _client = new HttpClient(new HttpClientHandler
        {
            UseCookies = true,             // 让 .NET 自动维护 Cookie（部分 CDN 需要）
            AllowAutoRedirect = true,      // 自动跟随 30x
            AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
        })
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        static DownloaderWindow()
        {
            // .NET Framework 4.8 的 HttpClient 默认不开 TLS 1.2/1.3；
            // 很多 CDN/中国节点会因 TLS 版本/握手指纹（JA3）直接 403 掉非浏览器客户端。
            // 强制 TLS 1.2/1.3，并允许自动重定向与宽松解析（避免部分节点证书链 / SNI 异常）。
            System.Net.ServicePointManager.SecurityProtocol =
                System.Net.SecurityProtocolType.Tls12 | System.Net.SecurityProtocolType.Tls11;
            System.Net.ServicePointManager.DefaultConnectionLimit = 64;
            System.Net.ServicePointManager.Expect100Continue = false;
            System.Net.ServicePointManager.CheckCertificateRevocationList = false;

            // 浏览器 UA 与通用 Accept 头（迅雷直接下载行为一致）
            _client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                "(KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");
            _client.DefaultRequestHeaders.Accept.ParseAdd(
                "text/html,application/xhtml+xml,application/xml;q=0.9," +
                "image/avif,image/webp,*/*;q=0.8");
            _client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("zh-CN,zh;q=0.9,en;q=0.8");
        }

        // 通用浏览器点击行为：访问 https://host/abc.zip 时，Referer 为 https://host/。
        // 迅雷/浏览器在从某页面点击直链下载时都会发 host 级别的 Referer。
        // 这是 Universal 浏览器行为，不是云盘专用逻辑。
        private static void TrySetReferer(HttpRequestMessage req)
        {
            try
            {
                if (req.RequestUri != null)
                {
                    string hostRoot = req.RequestUri.Scheme + "://" + req.RequestUri.Host + "/";
                    req.Headers.Referrer = new Uri(hostRoot);
                    // Origin 头：跨域下载时浏览器会带 Origin: https://host
                    req.Headers.TryAddWithoutValidation("Origin", hostRoot.TrimEnd('/'));
                    // 模拟 Chrome 76+ 的 Sec-Fetch-* 头
                    req.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "same-origin");
                    req.Headers.TryAddWithoutValidation("Sec-Fetch-Mode", "navigate");
                    req.Headers.TryAddWithoutValidation("Sec-Fetch-Dest", "document");
                }
            }
            catch { /* 忽略：失败也不影响主流程 */ }
        }

        // 解析"自定义请求头"文本框：每行 "Name: Value"，# 开头为注释。
        // 用于在 HttpClient 已设置通用头之外，附加用户从浏览器开发者工具复制的请求头
        // （典型场景：CDN 防盗链要求特定 Referer/Cookie/CDN-Sign 等）。
        private List<KeyValuePair<string, string>> ParseCustomHeaders()
        {
            var result = new List<KeyValuePair<string, string>>();
            if (CustomHeadersBox == null) return result;
            string text = CustomHeadersBox.Text;
            if (string.IsNullOrWhiteSpace(text)) return result;
            string[] lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            foreach (string raw in lines)
            {
                string line = raw == null ? "" : raw.Trim();
                if (line.Length == 0) continue;
                if (line[0] == '#') continue;
                int idx = line.IndexOf(':');
                if (idx <= 0) continue; // 必须有 ":" 且 key 非空
                string name = line.Substring(0, idx).Trim();
                string value = line.Substring(idx + 1).Trim();
                if (name.Length == 0 || value.Length == 0) continue;
                // 屏蔽少数会被 HttpClient 自己管理的头，避免触发内部异常
                string nLower = name.ToLowerInvariant();
                if (nLower == "host" || nLower == "content-length" || nLower == "connection")
                    continue;
                result.Add(new KeyValuePair<string, string>(name, value));
            }
            return result;
        }

        // 把自定义请求头附加到 req 上：用 TryAddWithoutValidation 跳过格式校验，
        // 让任意合法字符串都能加（CDN 经常发非标准名）；失败的单个头不抛。
        private static void ApplyCustomHeaders(HttpRequestMessage req, List<KeyValuePair<string, string>> headers)
        {
            if (headers == null || headers.Count == 0 || req == null) return;
            foreach (var kv in headers)
            {
                try
                {
                    bool ok = req.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
                    if (!ok && req.Content != null)
                    {
                        // 落到 Content 头（极少见，但允许）
                        req.Content.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
                    }
                }
                catch
                {
                    // 单个头非法，忽略，不影响其他头
                }
            }
        }
        private CancellationTokenSource _cts;
        private readonly object _writeLock = new object();
        private readonly object _stateLock = new object();

        private string _url;
        private string _outputPath;
        private long _total;
        private long _downloaded;
        private bool _supportsRange;
        private bool _multiThread;
        private bool _paused;

        private List<Segment> _segments = new List<Segment>();
        private DispatcherTimer _speedTimer;
        private long _lastSample;
        private DateTime _lastSampleTime;

        public DownloaderWindow()
        {
            InitializeComponent();
            _speedTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _speedTimer.Tick += SpeedTimer_Tick;
        }

        public void RefreshTheme(bool isDark)
        {
            ThemeIconHelper.SetIcon(LogoImage, "download.png", isDark);
        }

        private class Segment
        {
            public long Start;
            public long Current;
            public long End;
            public bool Done;
            public Border Ui;
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                FileName = SuggestFileName(UrlInput.Text),
                Filter = "所有文件 (*.*)|*.*"
            };
            if (dlg.ShowDialog() == true)
            {
                PathInput.Text = dlg.FileName;
                _outputPath = dlg.FileName;
            }
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (_cts != null && !_cts.IsCancellationRequested && !_paused)
            {
                // 正在下载中，忽略
                return;
            }

            if (_paused)
            {
                ResumeDownload();
                return;
            }

            StartDownload();
        }

        private void BtnPause_Click(object sender, RoutedEventArgs e)
        {
            if (_cts == null) return;
            _paused = true;
            _cts.Cancel();
            _speedTimer.Stop();
            BtnPause.IsEnabled = false;
            BtnPause.Content = "已暂停";
            BtnStart.Content = "继续";
            BtnStart.IsEnabled = true;
            StatusText.Text = "已暂停";
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            if (_cts != null)
            {
                _cts.Cancel();
            }
            _speedTimer.Stop();
            try { if (File.Exists(_outputPath)) File.Delete(_outputPath); } catch { }
            ResetUi("已取消");
        }

        private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_outputPath) && File.Exists(_outputPath))
            {
                try
                {
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{_outputPath}\"");
                }
                catch { }
            }
        }

        private async void StartDownload()
        {
            _url = UrlInput.Text?.Trim();
            if (string.IsNullOrEmpty(_url)) { StatusText.Text = "请输入下载地址"; return; }

            _outputPath = PathInput.Text?.Trim();
            if (string.IsNullOrEmpty(_outputPath))
            {
                string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "downloads");
                try { Directory.CreateDirectory(dir); } catch { dir = AppDomain.CurrentDomain.BaseDirectory; }
                _outputPath = Path.Combine(dir, SuggestFileName(_url));
                PathInput.Text = _outputPath;
            }

            int threads = 4;
            if (ThreadCount.SelectedItem is ComboBoxItem ci && int.TryParse(ci.Content?.ToString(), out int t)) threads = t;

            BtnStart.IsEnabled = false;
            BtnPause.IsEnabled = true;
            BtnCancel.IsEnabled = true;
            BtnBrowse.IsEnabled = false;
            BtnOpenFolder.Visibility = Visibility.Collapsed;
            Progress.Value = 0;
            _downloaded = 0;
            _paused = false;
            LogBox.Clear();
            Log($"开始解析：{_url}");

            _cts = new CancellationTokenSource();
            try
            {
                // 探测文件大小与是否支持 Range
                await ProbeAsync(_cts.Token);

                if (_total <= 0)
                {
                    Log("无法获取文件大小，使用单线程下载。");
                    _supportsRange = false;
                }

                _multiThread = _supportsRange && threads > 1 && _total > 1024 * 1024;
                if (!_multiThread && _supportsRange && threads > 1)
                    Log("文件较小，自动切换为单线程。");

                BuildSegments(_multiThread ? threads : 1);
                BuildSegmentUi();

                _lastSample = 0;
                _lastSampleTime = DateTime.Now;
                _speedTimer.Start();

                if (_multiThread)
                    await RunMultiThreadAsync(_cts.Token);
                else
                    await RunSingleThreadAsync(_cts.Token);

                if (_cts.IsCancellationRequested)
                {
                    if (_paused) return; // 由暂停逻辑接管
                    return;
                }

                _speedTimer.Stop();
                Progress.Value = 100;
                PercentText.Text = "100%";
                SpeedText.Text = "0 B/s";
                EtaText.Text = "完成";
                StatusText.Text = "下载完成";
                BtnPause.IsEnabled = false;
                BtnCancel.IsEnabled = false;
                BtnStart.IsEnabled = true;
                BtnStart.Content = "开始下载";
                BtnBrowse.IsEnabled = true;
                BtnOpenFolder.Visibility = Visibility.Visible;
                Log($"完成：{_outputPath}");
            }
            catch (OperationCanceledException)
            {
                if (_paused) return;
                Log("已取消。");
            }
            catch (Exception ex)
            {
                Log("错误：" + ex.Message);
                StatusText.Text = "出错";
                ResetUi("出错");
            }
        }

        private async void ResumeDownload()
        {
            _paused = false;
            BtnStart.Content = "开始下载";
            BtnStart.IsEnabled = false;
            BtnPause.Content = "暂停";
            BtnPause.IsEnabled = true;
            StatusText.Text = "继续下载中…";

            _cts = new CancellationTokenSource();
            try
            {
                _lastSample = _downloaded;
                _lastSampleTime = DateTime.Now;
                _speedTimer.Start();

                if (_multiThread)
                    await RunMultiThreadAsync(_cts.Token);
                else
                    await RunSingleThreadAsync(_cts.Token, _downloaded);

                if (_cts.IsCancellationRequested && _paused) return;

                _speedTimer.Stop();
                Progress.Value = 100;
                PercentText.Text = "100%";
                SpeedText.Text = "0 B/s";
                EtaText.Text = "完成";
                StatusText.Text = "下载完成";
                BtnPause.IsEnabled = false;
                BtnCancel.IsEnabled = false;
                BtnStart.IsEnabled = true;
                BtnStart.Content = "开始下载";
                BtnBrowse.IsEnabled = true;
                BtnOpenFolder.Visibility = Visibility.Visible;
                Log("完成（续传）。");
            }
            catch (OperationCanceledException)
            {
                if (_paused) return;
            }
            catch (Exception ex)
            {
                Log("错误：" + ex.Message);
                ResetUi("出错");
            }
        }

        private async Task ProbeAsync(CancellationToken ct)
        {
            _supportsRange = false;
            _total = 0;
            // 1) 尝试 HEAD
            try
            {
                var head = new HttpRequestMessage(HttpMethod.Head, _url);
                TrySetReferer(head);
                ApplyCustomHeaders(head, ParseCustomHeaders());
                using var resp = await _client.SendAsync(head, ct);
                if (!resp.IsSuccessStatusCode)
                {
                    Log($"HEAD 失败：{(int)resp.StatusCode} {resp.StatusCode}，改为 GET 探测。");
                    throw new HttpRequestException($"HEAD {(int)resp.StatusCode}");
                }
                if (resp.Content.Headers.ContentLength.HasValue)
                {
                    _total = resp.Content.Headers.ContentLength.Value;
                    _supportsRange = resp.Headers.AcceptRanges.Any(r => r.Equals("bytes", StringComparison.OrdinalIgnoreCase));
                }
                Log($"文件大小：{(_total > 0 ? FormatSize(_total) : "未知")}，支持断点：{_supportsRange}");
            }
            catch (Exception ex)
            {
                Log("探测失败（HEAD）：" + ex.Message + "，尝试 GET 探测。");
                // 2) 退而求其次：发一个 0-0 的 GET 探测
                try
                {
                    var get = new HttpRequestMessage(HttpMethod.Get, _url);
                    TrySetReferer(get);
                    ApplyCustomHeaders(get, ParseCustomHeaders());
                    get.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 0);
                    using var resp = await _client.SendAsync(get, HttpCompletionOption.ResponseHeadersRead, ct);
                    if (!resp.IsSuccessStatusCode)
                    {
                        string body = await TryReadBodyAsync(resp);
                        throw new HttpRequestException($"GET {(int)resp.StatusCode} {resp.StatusCode} — 响应: {Truncate(body, 200)}");
                    }
                    _supportsRange = resp.Headers.AcceptRanges.Any(r => r.Equals("bytes", StringComparison.OrdinalIgnoreCase));
                    _total = (resp.Content.Headers.ContentRange?.Length).GetValueOrDefault(0);
                    Log($"GET 探测：大小 {(_total > 0 ? FormatSize(_total) : "未知")}，支持断点 {_supportsRange}");
                }
                catch (Exception ex2)
                {
                    Log("GET 探测也失败：" + ex2.Message);
                }
            }
        }

        private static async Task<string> TryReadBodyAsync(HttpResponseMessage resp)
        {
            try
            {
                var bytes = await resp.Content.ReadAsByteArrayAsync();
                if (bytes == null || bytes.Length == 0) return "(空)";
                // 简单判断编码：utf8 / gb2312
                string s = System.Text.Encoding.UTF8.GetString(bytes);
                if (s.Contains("�")) s = System.Text.Encoding.GetEncoding("GB18030").GetString(bytes);
                return s;
            }
            catch (Exception ex) { return "(读取失败: " + ex.Message + ")"; }
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Length <= max ? s : s.Substring(0, max) + "...";
        }

        private void BuildSegments(int threadCount)
        {
            _segments.Clear();
            if (_multiThread && _total > 0)
            {
                long seg = _total / threadCount;
                for (int i = 0; i < threadCount; i++)
                {
                    long start = i * seg;
                    long end = (i == threadCount - 1) ? _total - 1 : start + seg - 1;
                    _segments.Add(new Segment { Start = start, Current = start, End = end, Done = false });
                }
            }
            else
            {
                _segments.Add(new Segment { Start = 0, Current = 0, End = _total - 1, Done = false });
            }
        }

        private void BuildSegmentUi()
        {
            SegmentPanel.Children.Clear();
            foreach (var s in _segments)
            {
                var b = new Border
                {
                    Width = 14,
                    Height = 14,
                    Margin = new Thickness(0, 0, 4, 0),
                    CornerRadius = new CornerRadius(3),
                    Background = (Brush)FindResource("BackgroundHoverBrush")
                };
                s.Ui = b;
                SegmentPanel.Children.Add(b);
            }
        }

        private async Task RunMultiThreadAsync(CancellationToken ct)
        {
            using var outStream = new FileStream(_outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous);
            if (_total > 0) outStream.SetLength(_total);

            var tasks = new List<Task>();
            foreach (var seg in _segments)
            {
                tasks.Add(DownloadSegmentAsync(seg, outStream, ct));
            }
            await Task.WhenAll(tasks);
        }

        private async Task DownloadSegmentAsync(Segment seg, FileStream outStream, CancellationToken ct)
        {
            try
            {
                var req = new HttpRequestMessage(HttpMethod.Get, _url);
                TrySetReferer(req);
                ApplyCustomHeaders(req, ParseCustomHeaders());
                req.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(seg.Current, seg.End);
                using var resp = await _client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
                if (!resp.IsSuccessStatusCode)
                {
                    string body = await TryReadBodyAsync(resp);
                    throw new HttpRequestException($"分段 {(int)resp.StatusCode} {resp.StatusCode} — {Truncate(body, 200)}");
                }
                using var stream = await resp.Content.ReadAsStreamAsync();

                byte[] buf = new byte[81920];
                int read;
                while ((read = await stream.ReadAsync(buf, 0, buf.Length, ct)) > 0)
                {
                    lock (_writeLock)
                    {
                        outStream.Seek(seg.Current, SeekOrigin.Begin);
                        outStream.Write(buf, 0, read);
                    }
                    seg.Current += read;
                    Interlocked.Add(ref _downloaded, read);
                    UpdateProgress();
                }
                seg.Done = true;
                if (seg.Ui != null)
                {
                    Dispatcher.Invoke(() => seg.Ui.Background = (Brush)FindResource("PrimaryBrush"));
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                Log($"分段 [{seg.Start}-{seg.End}] 失败：" + ex.Message);
                throw;
            }
        }

        private async Task RunSingleThreadAsync(CancellationToken ct, long resumeFrom = 0)
        {
            using var outStream = new FileStream(_outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous);
            if (_total > 0) outStream.SetLength(_total);

            var req = new HttpRequestMessage(HttpMethod.Get, _url);
            TrySetReferer(req);
            ApplyCustomHeaders(req, ParseCustomHeaders());
            if (resumeFrom > 0 && _supportsRange && _total > 0)
                req.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(resumeFrom, _total - 1);

            using var resp = await _client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!resp.IsSuccessStatusCode)
            {
                string body = await TryReadBodyAsync(resp);
                throw new HttpRequestException($"{(int)resp.StatusCode} {resp.StatusCode} — {Truncate(body, 200)}");
            }
            using var stream = await resp.Content.ReadAsStreamAsync();

            if (resumeFrom > 0 && _supportsRange)
            {
                // 服务器若返回 206 则跳过已下载部分
                if (resp.StatusCode == System.Net.HttpStatusCode.PartialContent)
                {
                    outStream.Seek(resumeFrom, SeekOrigin.Begin);
                }
            }

            byte[] buf = new byte[81920];
            int read;
            long pos = resumeFrom;
            while ((read = await stream.ReadAsync(buf, 0, buf.Length, ct)) > 0)
            {
                lock (_writeLock)
                {
                    outStream.Seek(pos, SeekOrigin.Begin);
                    outStream.Write(buf, 0, read);
                }
                pos += read;
                Interlocked.Add(ref _downloaded, read);
                UpdateProgress();
            }
            if (_segments.Count > 0)
            {
                _segments[0].Done = true;
                if (_segments[0].Ui != null)
                    Dispatcher.Invoke(() => _segments[0].Ui.Background = (Brush)FindResource("PrimaryBrush"));
            }
        }

        private void UpdateProgress()
        {
            Dispatcher.Invoke(() =>
            {
                if (_total > 0)
                {
                    double pct = (double)_downloaded / _total * 100.0;
                    Progress.Value = Math.Min(100, pct);
                    PercentText.Text = $"{pct:0.0}%";
                    SizeText.Text = $"{FormatSize(_downloaded)} / {FormatSize(_total)}";
                }
                else
                {
                    SizeText.Text = FormatSize(_downloaded);
                }
            });
        }

        private void SpeedTimer_Tick(object sender, EventArgs e)
        {
            long now = Interlocked.Read(ref _downloaded);
            var tnow = DateTime.Now;
            double sec = (tnow - _lastSampleTime).TotalSeconds;
            if (sec <= 0) return;
            double speed = (now - _lastSample) / sec;
            _lastSample = now;
            _lastSampleTime = tnow;

            Dispatcher.Invoke(() =>
            {
                SpeedText.Text = FormatSize((long)speed) + "/s";
                if (_total > 0 && speed > 0)
                {
                    long remain = _total - now;
                    double etaSec = remain / speed;
                    EtaText.Text = FormatEta(etaSec);
                }
                else
                {
                    EtaText.Text = "--";
                }
            });
        }

        private void ResetUi(string status)
        {
            Dispatcher.Invoke(() =>
            {
                _speedTimer.Stop();
                BtnStart.IsEnabled = true;
                BtnStart.Content = "开始下载";
                BtnPause.IsEnabled = false;
                BtnPause.Content = "暂停";
                BtnCancel.IsEnabled = false;
                BtnBrowse.IsEnabled = true;
                StatusText.Text = status;
                // 释放旧 CTS，否则 BtnStart_Click 的"正在下载中"守卫会让用户无法重新开始
                if (_cts != null)
                {
                    try { _cts.Cancel(); } catch { }
                    _cts.Dispose();
                    _cts = null;
                }
            });
        }

        private string SuggestFileName(string url)
        {
            try
            {
                var u = new Uri(url);
                string last = Path.GetFileName(u.LocalPath);
                if (string.IsNullOrEmpty(last) || last.EndsWith("/"))
                    last = "download.bin";
                foreach (char c in Path.GetInvalidFileNameChars())
                    last = last.Replace(c, '_');
                return last;
            }
            catch { return "download.bin"; }
        }

        private void Log(string msg)
        {
            Dispatcher.Invoke(() =>
            {
                LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\r\n");
                LogBox.ScrollToEnd();
            });
        }

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:0.0} KB";
            if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):0.0} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):0.0} GB";
        }

        private static string FormatEta(double sec)
        {
            if (sec < 0 || double.IsInfinity(sec) || double.IsNaN(sec)) return "--";
            if (sec < 60) return $"{sec:0}s";
            if (sec < 3600) return $"{(int)(sec / 60)}m {(int)(sec % 60)}s";
            return $"{(int)(sec / 3600)}h {(int)((sec % 3600) / 60)}m";
        }
    }
}
