using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using SWin   = System.Windows;
using SWC    = System.Windows.Controls;
using SDoc   = System.Windows.Documents;
using WMedia = System.Windows.Media;

namespace MenuApp;

public partial class MainWindow
{
    // ══════════════════════════════════════════════════ СПИСОК ИИ

    private static readonly (string Name, string Url, string ApiId)[] AiList =
    {
        ("ChatGPT",    "https://chat.openai.com",         ""),
        ("Claude",     "https://claude.ai",                ""),
        ("Gemini",     "https://gemini.google.com",        "gemini"),
        ("Copilot",    "https://copilot.microsoft.com",    ""),
        ("Perplexity", "https://www.perplexity.ai",        "perplexity"),
        ("DeepSeek",   "https://chat.deepseek.com",        "deepseek"),
    };

    private static readonly (byte r, byte g, byte b)[] AiColors =
    {
        (16,  163, 127),  // ChatGPT
        (190,  90,  40),  // Claude
        (66,  133, 244),  // Gemini
        (0,   120, 212),  // Copilot
        (20,  100, 180),  // Perplexity
        (50,   80, 200),  // DeepSeek
    };

    // ══════════════════════════════════════════════════ ПОЛЯ

    private readonly ObservableCollection<AiRow> aiRows = new();

    private string _geminiApiKey     = "";
    private string _deepSeekApiKey   = "";
    private string _perplexityApiKey = "";

    private static readonly WMedia.Brush BrushWait    = Frozen(105, 105, 105);  // DimGray
    private static readonly WMedia.Brush BrushBrowser = Frozen(60, 100, 60);
    private static readonly WMedia.Brush BrushError   = Frozen(178, 34, 34);    // Firebrick
    private static readonly WMedia.Brush BrushAnswer  = Frozen(0, 0, 0);

    private static WMedia.Brush Frozen(byte r, byte g, byte b)
    {
        var br = new WMedia.SolidColorBrush(WMedia.Color.FromRgb(r, g, b));
        br.Freeze();
        return br;
    }

    // ══════════════════════════════════════════════════ ИНИЦИАЛИЗАЦИЯ WPF-ВКЛАДКИ

    internal void InitAiQuestionsTab()
    {
        for (int i = 0; i < AiList.Length; i++)
        {
            var (name, url, apiId) = AiList[i];
            aiRows.Add(new AiRow { Name = name, Url = url, ApiId = apiId, HeaderBrush = Frozen(AiColors[i].r, AiColors[i].g, AiColors[i].b) });
        }
        icAiRows.ItemsSource = aiRows;

        btnAiAsk.Click     += async (_, _) => await AskAllAisAsync();
        btnAiSaveAll.Click += (_, _) => SaveAllResponses();
        btnAiApiKeys.Click += (_, _) => ShowApiKeyDialog();
        txAiQuestion.KeyDown += async (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Enter) { e.Handled = true; await AskAllAisAsync(); }
        };
    }

    private void AiOpenButton_Click(object sender, SWin.RoutedEventArgs e)
    {
        if ((sender as SWin.FrameworkElement)?.DataContext is AiRow r)
            Process.Start(new ProcessStartInfo { FileName = r.Url, UseShellExecute = true });
    }

    private void AiSaveButton_Click(object sender, SWin.RoutedEventArgs e)
    {
        if ((sender as SWin.FrameworkElement)?.DataContext is AiRow r)
            SaveSingleResponse(r);
    }

    // ══════════════════════════════════════════════════ ЛОГИКА «СПРОСИТЬ»

    private async Task AskAllAisAsync()
    {
        string question = txAiQuestion.Text.Trim();
        if (string.IsNullOrEmpty(question))
        {
            SWin.MessageBox.Show("Введите вопрос.", "Вопрос пуст",
                SWin.MessageBoxButton.OK, SWin.MessageBoxImage.Information);
            return;
        }

        // Скопировать вопрос в буфер для ИИ, открываемых в браузере
        SWin.Clipboard.SetText(question);

        var tasks         = new List<Task>();
        var browserOpened = new List<string>();

        for (int i = 0; i < aiRows.Count; i++)
        {
            var r = aiRows[i];
            if (!r.Enabled) continue;

            int     idx    = i;
            string? apiKey = ApiKeyFor(r.ApiId);
            bool    hasKey = !string.IsNullOrEmpty(apiKey);

            if (r.ApiId == "gemini" && hasKey)
            {
                r.ResponseBrush = BrushWait; r.Response = "⌛ Запрос к Gemini…";
                tasks.Add(AskGeminiAsync(idx, question, apiKey!));
            }
            else if (r.ApiId == "deepseek" && hasKey)
            {
                r.ResponseBrush = BrushWait; r.Response = "⌛ Запрос к DeepSeek…";
                tasks.Add(AskDeepSeekAsync(idx, question, apiKey!));
            }
            else if (r.ApiId == "perplexity" && hasKey)
            {
                r.ResponseBrush = BrushWait; r.Response = "⌛ Запрос к Perplexity…";
                tasks.Add(AskPerplexityAsync(idx, question, apiKey!));
            }
            else
            {
                string openUrl = BuildBrowserUrl(r.Name, r.Url, question);
                Process.Start(new ProcessStartInfo { FileName = openUrl, UseShellExecute = true });
                r.ResponseBrush = BrushBrowser;
                r.Response = "🌐 Вопрос открыт в браузере.\n" +
                             "Вопрос скопирован в буфер — вставьте его (Ctrl+V) в чат.\n" +
                             "Скопируйте ответ сюда после получения.";
                browserOpened.Add(r.Name);
            }
        }

        if (tasks.Count > 0)
        {
            lblAiStatus.Text       = "⌛ Жду ответы от ИИ…";
            lblAiStatus.Foreground = Frozen(80, 80, 0);
            await Task.WhenAll(tasks);
            lblAiStatus.Text       = "✓ Готово!";
            lblAiStatus.Foreground = Frozen(30, 100, 30);
        }
        else
        {
            lblAiStatus.Text       = browserOpened.Count > 0
                ? $"🌐 Открыт(ы) в браузере: {string.Join(", ", browserOpened)}"
                : "Нет выбранных ИИ.";
            lblAiStatus.Foreground = BrushWait;
        }
    }

    private string? ApiKeyFor(string apiId) => apiId switch
    {
        "gemini"     => string.IsNullOrEmpty(_geminiApiKey)     ? null : _geminiApiKey,
        "deepseek"   => string.IsNullOrEmpty(_deepSeekApiKey)   ? null : _deepSeekApiKey,
        "perplexity" => string.IsNullOrEmpty(_perplexityApiKey) ? null : _perplexityApiKey,
        _            => null
    };

    private static string BuildBrowserUrl(string name, string url, string question)
    {
        string q = Uri.EscapeDataString(question);
        return name switch
        {
            "Perplexity" => $"https://www.perplexity.ai/search?q={q}",
            "Copilot"    => $"https://www.bing.com/search?q={q}&showconv=1",
            _            => url
        };
    }

    // ══════════════════════════════════════════════════ API-ВЫЗОВЫ

    private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

    private async Task AskGeminiAsync(int idx, string question, string key)
    {
        try
        {
            string url  = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={key}";
            string body = $"{{\"contents\":[{{\"parts\":[{{\"text\":{JsonSerializer.Serialize(question)}}}]}}]}}";
            var resp = await _httpClient.PostAsync(url,
                new StringContent(body, Encoding.UTF8, "application/json"));
            string json = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                SetResponse(idx, $"❌ Ошибка Gemini ({(int)resp.StatusCode}): {json}", BrushError);
                return;
            }

            using var doc = JsonDocument.Parse(json);
            string text = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text").GetString() ?? "";
            SetResponse(idx, text, BrushAnswer);
        }
        catch (Exception ex)
        {
            SetResponse(idx, $"❌ Ошибка: {ex.Message}", BrushError);
        }
    }

    private async Task AskDeepSeekAsync(int idx, string question, string key)
    {
        try
        {
            string url  = "https://api.deepseek.com/chat/completions";
            string body = JsonSerializer.Serialize(new
            {
                model    = "deepseek-chat",
                messages = new[] { new { role = "user", content = question } },
                stream   = false
            });

            var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            req.Headers.Add("Authorization", $"Bearer {key}");

            var resp = await _httpClient.SendAsync(req);
            string json = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                SetResponse(idx, $"❌ Ошибка DeepSeek ({(int)resp.StatusCode}): {json}", BrushError);
                return;
            }

            using var doc = JsonDocument.Parse(json);
            string text = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content").GetString() ?? "";
            SetResponse(idx, text, BrushAnswer);
        }
        catch (Exception ex)
        {
            SetResponse(idx, $"❌ Ошибка: {ex.Message}", BrushError);
        }
    }

    private async Task AskPerplexityAsync(int idx, string question, string key)
    {
        try
        {
            string url  = "https://api.perplexity.ai/chat/completions";
            string body = JsonSerializer.Serialize(new
            {
                model    = "llama-3.1-sonar-small-128k-online",
                messages = new[] { new { role = "user", content = question } }
            });

            var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            req.Headers.Add("Authorization", $"Bearer {key}");

            var resp = await _httpClient.SendAsync(req);
            string json = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                SetResponse(idx, $"❌ Ошибка Perplexity ({(int)resp.StatusCode}): {json}", BrushError);
                return;
            }

            using var doc = JsonDocument.Parse(json);
            string text = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content").GetString() ?? "";
            SetResponse(idx, text, BrushAnswer);
        }
        catch (Exception ex)
        {
            SetResponse(idx, $"❌ Ошибка: {ex.Message}", BrushError);
        }
    }

    private void SetResponse(int idx, string text, WMedia.Brush brush)
    {
        // Вызов из фонового потока → через Dispatcher
        Dispatcher.Invoke(() =>
        {
            aiRows[idx].ResponseBrush = brush;
            aiRows[idx].Response      = text;
        });
    }

    // ══════════════════════════════════════════════════ СОХРАНЕНИЕ

    private void SaveAllResponses()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Вопросы ИИ — {DateTime.Now:dd.MM.yyyy HH:mm}");
        sb.AppendLine(new string('═', 60));
        sb.AppendLine($"Вопрос: {txAiQuestion.Text.Trim()}");
        sb.AppendLine();

        bool hasAny = false;
        foreach (var r in aiRows)
        {
            string txt = r.Response.Trim();
            if (string.IsNullOrEmpty(txt)) continue;
            sb.AppendLine(new string('─', 60));
            sb.AppendLine($"■ {r.Name}");
            sb.AppendLine();
            sb.AppendLine(txt);
            sb.AppendLine();
            hasAny = true;
        }

        if (!hasAny)
        {
            SWin.MessageBox.Show("Нет ответов для сохранения.", "Пусто",
                SWin.MessageBoxButton.OK, SWin.MessageBoxImage.Information);
            return;
        }

        SaveToFile(sb.ToString(), $"AI_ответы_{DateTime.Now:yyyyMMdd_HHmm}.txt");
    }

    private void SaveSingleResponse(AiRow r)
    {
        string txt = r.Response.Trim();
        if (string.IsNullOrEmpty(txt))
        {
            SWin.MessageBox.Show("Нет ответа для сохранения.", "Пусто",
                SWin.MessageBoxButton.OK, SWin.MessageBoxImage.Information);
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"{r.Name} — {DateTime.Now:dd.MM.yyyy HH:mm}");
        sb.AppendLine(new string('═', 60));
        sb.AppendLine($"Вопрос: {txAiQuestion.Text.Trim()}");
        sb.AppendLine();
        sb.AppendLine(txt);

        SaveToFile(sb.ToString(), $"{r.Name}_{DateTime.Now:yyyyMMdd_HHmm}.txt");
    }

    private static void SaveToFile(string content, string fileName)
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MenuApp");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, fileName);
        File.WriteAllText(path, content, Encoding.UTF8);
        Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
    }

    // ══════════════════════════════════════════════════ ДИАЛОГ API-КЛЮЧЕЙ (WPF)

    private void ShowApiKeyDialog()
    {
        var dlg = new SWin.Window
        {
            Title  = "API ключи для ИИ",
            Width  = 560, Height = 360,
            WindowStartupLocation = SWin.WindowStartupLocation.CenterOwner,
            Owner  = this,
            ResizeMode = SWin.ResizeMode.NoResize,
            Background = Frozen(242, 248, 242)
        };

        var panel = new SWC.StackPanel { Margin = new SWin.Thickness(16) };

        SWC.TextBox Row(string label, string linkText, string linkUrl, string value)
        {
            panel.Children.Add(new SWC.TextBlock
            {
                Text = label, FontWeight = SWin.FontWeights.Bold, FontSize = 13,
                Margin = new SWin.Thickness(0, 8, 0, 2)
            });
            var tx = new SWC.TextBox { Text = value, FontSize = 13, Height = 30, Padding = new SWin.Thickness(4, 3, 4, 3) };
            panel.Children.Add(tx);

            var link = new SWC.TextBlock { Margin = new SWin.Thickness(0, 3, 0, 4), FontSize = 11 };
            var hl = new SDoc.Hyperlink(new SDoc.Run(linkText)) { NavigateUri = new Uri(linkUrl) };
            hl.RequestNavigate += (_, e) => Process.Start(new ProcessStartInfo { FileName = e.Uri.AbsoluteUri, UseShellExecute = true });
            link.Inlines.Add(hl);
            panel.Children.Add(link);
            return tx;
        }

        var tg = Row("Gemini API ключ:",     "Получить бесплатно на aistudio.google.com", "https://aistudio.google.com/apikey",      _geminiApiKey);
        var td = Row("DeepSeek API ключ:",   "Получить на platform.deepseek.com",         "https://platform.deepseek.com/api_keys",  _deepSeekApiKey);
        var tp = Row("Perplexity API ключ:", "Получить на perplexity.ai/settings/api",    "https://www.perplexity.ai/settings/api",  _perplexityApiKey);

        var btnOk = new SWC.Button
        {
            Content = "Сохранить", Width = 130, Height = 36,
            HorizontalAlignment = SWin.HorizontalAlignment.Right, Margin = new SWin.Thickness(0, 14, 0, 0),
            Background = Frozen(44, 95, 45), Foreground = WMedia.Brushes.White,
            FontWeight = SWin.FontWeights.Bold, IsDefault = true
        };
        btnOk.Click += (_, _) =>
        {
            _geminiApiKey     = tg.Text.Trim();
            _deepSeekApiKey   = td.Text.Trim();
            _perplexityApiKey = tp.Text.Trim();
            SaveAiSettings();
            dlg.DialogResult = true;
        };
        panel.Children.Add(btnOk);

        dlg.Content = panel;
        dlg.ShowDialog();
    }

    // ══════════════════════════════════════════════════ ЗАГРУЗКА / СОХРАНЕНИЕ КЛЮЧЕЙ

    internal void LoadAiSettings()
    {
        string path = Path.Combine(AppDir, "ai_settings.json");
        if (!File.Exists(path)) return;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var root = doc.RootElement;
            if (root.TryGetProperty("GeminiKey",     out var g)) _geminiApiKey     = g.GetString() ?? "";
            if (root.TryGetProperty("DeepSeekKey",   out var d)) _deepSeekApiKey   = d.GetString() ?? "";
            if (root.TryGetProperty("PerplexityKey", out var p)) _perplexityApiKey = p.GetString() ?? "";
        }
        catch { }
    }

    private void SaveAiSettings()
    {
        string path = Path.Combine(AppDir, "ai_settings.json");
        var obj = new { GeminiKey = _geminiApiKey, DeepSeekKey = _deepSeekApiKey, PerplexityKey = _perplexityApiKey };
        File.WriteAllText(path, JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true }));
    }
}
