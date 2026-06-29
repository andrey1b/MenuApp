using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using SWF = System.Windows.Forms;
using SD  = System.Drawing;

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

    private static readonly SD.Color[] AiButtonColors =
    {
        SD.Color.FromArgb(16,  163, 127),  // ChatGPT
        SD.Color.FromArgb(190,  90,  40),  // Claude
        SD.Color.FromArgb(66,  133, 244),  // Gemini
        SD.Color.FromArgb(0,   120, 212),  // Copilot
        SD.Color.FromArgb(20,  100, 180),  // Perplexity
        SD.Color.FromArgb(50,   80, 200),  // DeepSeek
    };

    // ══════════════════════════════════════════════════ ПОЛЯ

    private SWF.TextBox      txAiQuestion  = null!;
    private SWF.Label        lblAiStatus   = null!;
    private SWF.RichTextBox[] txAiResponse = null!;
    private SWF.CheckBox[]   cbAiEnabled   = null!;

    private string _geminiApiKey    = "";
    private string _deepSeekApiKey  = "";
    private string _perplexityApiKey = "";

    // ══════════════════════════════════════════════════ СОЗДАНИЕ ВКЛАДКИ

    internal SWF.Panel CreateAiQuestionsPanel()
    {
        txAiResponse = new SWF.RichTextBox[AiList.Length];
        cbAiEnabled  = new SWF.CheckBox[AiList.Length];

        var outer = new SWF.Panel { Dock = SWF.DockStyle.Fill };
        outer.Controls.Add(BuildAiScrollArea());
        outer.Controls.Add(BuildAiTopBar());
        return outer;
    }

    // ── Верхняя панель ───────────────────────────────

    private SWF.Panel BuildAiTopBar()
    {
        var top = new SWF.Panel
        {
            Dock = SWF.DockStyle.Top, Height = 74,
            BackColor = SD.Color.FromArgb(238, 248, 238),
            Padding = new SWF.Padding(10, 8, 10, 6)
        };

        var lblQ = new SWF.Label
        {
            Text = "Вопрос:", Left = 10, Top = 20, Width = 75, Height = 36,
            TextAlign = SD.ContentAlignment.MiddleRight,
            Font = new SD.Font("Segoe UI", 13)
        };

        txAiQuestion = new SWF.TextBox
        {
            Left = 90, Top = 16, Height = 40,
            Font = new SD.Font("Segoe UI", 13),
            BorderStyle = SWF.BorderStyle.FixedSingle
        };

        var btnAsk = new SWF.Button
        {
            Text = "▶  Спросить", Top = 14, Width = 155, Height = 44,
            Font = new SD.Font("Segoe UI", 13, SD.FontStyle.Bold),
            BackColor = SD.Color.FromArgb(44, 95, 45), ForeColor = SD.Color.White,
            FlatStyle = SWF.FlatStyle.Flat
        };
        btnAsk.FlatAppearance.BorderSize = 0;

        var btnSaveAll = new SWF.Button
        {
            Text = "💾  Сохранить все", Top = 14, Width = 180, Height = 44,
            Font = new SD.Font("Segoe UI", 12),
            BackColor = SD.Color.FromArgb(200, 228, 200), ForeColor = SD.Color.FromArgb(30, 70, 30),
            FlatStyle = SWF.FlatStyle.Flat
        };
        btnSaveAll.FlatAppearance.BorderSize = 1;

        var btnApiKeys = new SWF.Button
        {
            Text = "⚙  API ключи", Top = 14, Width = 140, Height = 44,
            Font = new SD.Font("Segoe UI", 12),
            BackColor = SD.Color.FromArgb(220, 224, 240), ForeColor = SD.Color.FromArgb(40, 50, 100),
            FlatStyle = SWF.FlatStyle.Flat
        };
        btnApiKeys.FlatAppearance.BorderSize = 1;

        lblAiStatus = new SWF.Label
        {
            Left = 90, Top = 58, Height = 18, Width = 800, AutoSize = false,
            Font = new SD.Font("Segoe UI", 10), ForeColor = SD.Color.DimGray
        };

        void LayoutTop(object? s, EventArgs e)
        {
            int w = top.ClientSize.Width;
            btnApiKeys.Left  = w - 10 - 140;
            btnSaveAll.Left  = btnApiKeys.Left - 8 - 180;
            btnAsk.Left      = btnSaveAll.Left - 8 - 155;
            txAiQuestion.Width = btnAsk.Left - 90 - 8;
            lblAiStatus.Width  = btnAsk.Left - 90 - 8;
        }
        top.Resize        += LayoutTop;
        top.HandleCreated += LayoutTop;

        btnAsk.Click     += async (_, _) => await AskAllAisAsync();
        btnSaveAll.Click += (_, _) => SaveAllResponses();
        btnApiKeys.Click += (_, _) => ShowApiKeyDialog();

        txAiQuestion.KeyDown += (_, e) =>
        {
            if (e.KeyCode == SWF.Keys.Enter) { e.SuppressKeyPress = true; btnAsk.PerformClick(); }
        };

        top.Controls.AddRange(new SWF.Control[] { lblQ, txAiQuestion, btnAsk, btnSaveAll, btnApiKeys, lblAiStatus });
        return top;
    }

    // ── Прокручиваемая область с рядами ИИ ───────────

    private SWF.Panel BuildAiScrollArea()
    {
        const int RowH = 130;

        var scroll = new SWF.Panel
        {
            Dock = SWF.DockStyle.Fill,
            AutoScroll = true,
            BackColor = SD.Color.FromArgb(245, 250, 245)
        };

        var table = new SWF.TableLayoutPanel
        {
            ColumnCount = 3, RowCount = AiList.Length,
            BackColor   = SD.Color.Transparent,
            Padding     = new SWF.Padding(6),
            Left = 0, Top = 0,
            Height = AiList.Length * RowH + 12
        };
        // 20% кнопка ИИ | 60% текстбокс | 20% чекбокс+сохранить
        table.ColumnStyles.Add(new SWF.ColumnStyle(SWF.SizeType.Percent, 20));
        table.ColumnStyles.Add(new SWF.ColumnStyle(SWF.SizeType.Percent, 60));
        table.ColumnStyles.Add(new SWF.ColumnStyle(SWF.SizeType.Percent, 20));
        for (int i = 0; i < AiList.Length; i++)
            table.RowStyles.Add(new SWF.RowStyle(SWF.SizeType.Absolute, RowH));

        for (int i = 0; i < AiList.Length; i++)
        {
            int   idx        = i;
            var  (name, url, _) = AiList[i];

            // Кнопка с названием ИИ (20%)
            var btnAi = new SWF.Button
            {
                Text      = name,
                Dock      = SWF.DockStyle.Fill,
                Margin    = new SWF.Padding(6, 6, 4, 6),
                Font      = new SD.Font("Segoe UI", 15, SD.FontStyle.Bold),
                BackColor = AiButtonColors[i], ForeColor = SD.Color.White,
                FlatStyle = SWF.FlatStyle.Flat,
                Cursor    = SWF.Cursors.Hand
            };
            btnAi.FlatAppearance.BorderSize = 0;
            btnAi.Click += (_, _) =>
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });

            // Текстбокс ответа (60%)
            txAiResponse[i] = new SWF.RichTextBox
            {
                Dock        = SWF.DockStyle.Fill,
                Margin      = new SWF.Padding(4, 6, 4, 6),
                Font        = new SD.Font("Segoe UI", 12),
                ScrollBars  = SWF.RichTextBoxScrollBars.Both,
                WordWrap    = true,
                BackColor   = SD.Color.White,
                ReadOnly    = false,
                BorderStyle = SWF.BorderStyle.FixedSingle
            };

            // Правая панель 20%: чекбокс + кнопка «Сохранить»
            var right = new SWF.Panel { Dock = SWF.DockStyle.Fill, Margin = new SWF.Padding(4, 6, 6, 6) };

            cbAiEnabled[i] = new SWF.CheckBox
            {
                Text = "Включить", Left = 6, Top = 10, Height = 30, AutoSize = false,
                Font = new SD.Font("Segoe UI", 12), Checked = true,
                ForeColor = SD.Color.FromArgb(30, 70, 30)
            };

            var btnSave = new SWF.Button
            {
                Text = "💾  Сохранить", Left = 4, Top = 48, Height = 40,
                Font = new SD.Font("Segoe UI", 11),
                BackColor = SD.Color.FromArgb(200, 228, 200), ForeColor = SD.Color.FromArgb(30, 70, 30),
                FlatStyle = SWF.FlatStyle.Flat
            };
            btnSave.FlatAppearance.BorderSize = 1;
            btnSave.Click += (_, _) => SaveSingleResponse(idx);

            // Ширина кнопки и чекбокса — по ширине правой панели
            right.Resize += (_, _) =>
            {
                cbAiEnabled[idx].Width = right.ClientSize.Width - 10;
                btnSave.Width          = right.ClientSize.Width - 8;
            };

            right.Controls.AddRange(new SWF.Control[] { cbAiEnabled[i], btnSave });

            table.Controls.Add(btnAi,           0, i);
            table.Controls.Add(txAiResponse[i], 1, i);
            table.Controls.Add(right,            2, i);
        }

        // Растягиваем таблицу по ширине scroll-панели
        scroll.Resize += (_, _) => table.Width = scroll.ClientSize.Width;
        scroll.Controls.Add(table);

        // Первоначальный размер после создания хэндла
        scroll.HandleCreated += (_, _) => table.Width = scroll.ClientSize.Width;

        return scroll;
    }

    // ══════════════════════════════════════════════════ ЛОГИКА «СПРОСИТЬ»

    private async Task AskAllAisAsync()
    {
        string question = txAiQuestion.Text.Trim();
        if (string.IsNullOrEmpty(question))
        {
            SWF.MessageBox.Show("Введите вопрос.", "Вопрос пуст",
                SWF.MessageBoxButtons.OK, SWF.MessageBoxIcon.Information);
            return;
        }

        // Скопировать вопрос в буфер для ИИ, открываемых в браузере
        SWF.Clipboard.SetText(question);

        var tasks       = new List<Task>();
        var browserOpened = new List<string>();

        for (int i = 0; i < AiList.Length; i++)
        {
            if (!cbAiEnabled[i].Checked) continue;

            int    idx            = i;
            var   (name, url, apiId) = AiList[i];
            string? apiKey        = ApiKeyFor(apiId);
            bool   hasKey         = !string.IsNullOrEmpty(apiKey);

            if (apiId == "gemini" && hasKey)
            {
                txAiResponse[idx].ForeColor = SD.Color.DimGray;
                txAiResponse[idx].Text      = "⌛ Запрос к Gemini…";
                tasks.Add(AskGeminiAsync(idx, question, apiKey!));
            }
            else if (apiId == "deepseek" && hasKey)
            {
                txAiResponse[idx].ForeColor = SD.Color.DimGray;
                txAiResponse[idx].Text      = "⌛ Запрос к DeepSeek…";
                tasks.Add(AskDeepSeekAsync(idx, question, apiKey!));
            }
            else if (apiId == "perplexity" && hasKey)
            {
                txAiResponse[idx].ForeColor = SD.Color.DimGray;
                txAiResponse[idx].Text      = "⌛ Запрос к Perplexity…";
                tasks.Add(AskPerplexityAsync(idx, question, apiKey!));
            }
            else
            {
                // Открыть в браузере
                string openUrl = BuildBrowserUrl(name, url, question);
                Process.Start(new ProcessStartInfo { FileName = openUrl, UseShellExecute = true });
                txAiResponse[idx].ForeColor = SD.Color.FromArgb(60, 100, 60);
                txAiResponse[idx].Text = $"🌐 Вопрос открыт в браузере.\n" +
                                          "Вопрос скопирован в буфер — вставьте его (Ctrl+V) в чат.\n" +
                                          "Скопируйте ответ сюда после получения.";
                browserOpened.Add(name);
            }
        }

        if (tasks.Count > 0)
        {
            lblAiStatus.Text      = "⌛ Жду ответы от ИИ…";
            lblAiStatus.ForeColor = SD.Color.FromArgb(80, 80, 0);
        }

        if (tasks.Count > 0)
        {
            await Task.WhenAll(tasks);
            lblAiStatus.Text      = "✓ Готово!";
            lblAiStatus.ForeColor = SD.Color.FromArgb(30, 100, 30);
        }
        else
        {
            lblAiStatus.Text      = browserOpened.Count > 0
                ? $"🌐 Открыт(ы) в браузере: {string.Join(", ", browserOpened)}"
                : "Нет выбранных ИИ.";
            lblAiStatus.ForeColor = SD.Color.DimGray;
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
                SetResponse(idx, $"❌ Ошибка Gemini ({(int)resp.StatusCode}): {json}", SD.Color.Firebrick);
                return;
            }

            using var doc = JsonDocument.Parse(json);
            string text = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text").GetString() ?? "";
            SetResponse(idx, text, SD.Color.Black);
        }
        catch (Exception ex)
        {
            SetResponse(idx, $"❌ Ошибка: {ex.Message}", SD.Color.Firebrick);
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
                SetResponse(idx, $"❌ Ошибка DeepSeek ({(int)resp.StatusCode}): {json}", SD.Color.Firebrick);
                return;
            }

            using var doc = JsonDocument.Parse(json);
            string text = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content").GetString() ?? "";
            SetResponse(idx, text, SD.Color.Black);
        }
        catch (Exception ex)
        {
            SetResponse(idx, $"❌ Ошибка: {ex.Message}", SD.Color.Firebrick);
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
                SetResponse(idx, $"❌ Ошибка Perplexity ({(int)resp.StatusCode}): {json}", SD.Color.Firebrick);
                return;
            }

            using var doc = JsonDocument.Parse(json);
            string text = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content").GetString() ?? "";
            SetResponse(idx, text, SD.Color.Black);
        }
        catch (Exception ex)
        {
            SetResponse(idx, $"❌ Ошибка: {ex.Message}", SD.Color.Firebrick);
        }
    }

    private void SetResponse(int idx, string text, SD.Color color)
    {
        // Вызов из фонового потока → через Dispatcher
        Dispatcher.Invoke(() =>
        {
            txAiResponse[idx].ForeColor = color;
            txAiResponse[idx].Text      = text;
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
        for (int i = 0; i < AiList.Length; i++)
        {
            string txt = txAiResponse[i].Text.Trim();
            if (string.IsNullOrEmpty(txt)) continue;
            sb.AppendLine(new string('─', 60));
            sb.AppendLine($"■ {AiList[i].Name}");
            sb.AppendLine();
            sb.AppendLine(txt);
            sb.AppendLine();
            hasAny = true;
        }

        if (!hasAny)
        {
            SWF.MessageBox.Show("Нет ответов для сохранения.", "Пусто",
                SWF.MessageBoxButtons.OK, SWF.MessageBoxIcon.Information);
            return;
        }

        SaveToFile(sb.ToString(), $"AI_ответы_{DateTime.Now:yyyyMMdd_HHmm}.txt");
    }

    private void SaveSingleResponse(int idx)
    {
        string txt = txAiResponse[idx].Text.Trim();
        if (string.IsNullOrEmpty(txt))
        {
            SWF.MessageBox.Show("Нет ответа для сохранения.", "Пусто",
                SWF.MessageBoxButtons.OK, SWF.MessageBoxIcon.Information);
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"{AiList[idx].Name} — {DateTime.Now:dd.MM.yyyy HH:mm}");
        sb.AppendLine(new string('═', 60));
        sb.AppendLine($"Вопрос: {txAiQuestion.Text.Trim()}");
        sb.AppendLine();
        sb.AppendLine(txt);

        SaveToFile(sb.ToString(), $"{AiList[idx].Name}_{DateTime.Now:yyyyMMdd_HHmm}.txt");
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

    // ══════════════════════════════════════════════════ ДИАЛОГ API-КЛЮЧЕЙ

    private void ShowApiKeyDialog()
    {
        var dlg = new SWF.Form
        {
            Text = "API ключи для ИИ",
            Width = 520, Height = 340,
            StartPosition = SWF.FormStartPosition.CenterParent,
            FormBorderStyle = SWF.FormBorderStyle.FixedDialog,
            MaximizeBox = false, MinimizeBox = false,
            BackColor = SD.Color.FromArgb(242, 248, 242)
        };

        SWF.Control[] MakeRow(string label, string linkText, string linkUrl, string value, int top)
        {
            var lbl  = new SWF.Label { Text = label, Left = 16, Top = top, Width = 460, Height = 24, Font = new SD.Font("Segoe UI", 11, SD.FontStyle.Bold) };
            var tx   = new SWF.TextBox { Left = 16, Top = top + 26, Width = 460, Height = 32, Font = new SD.Font("Segoe UI", 11), Text = value, BorderStyle = SWF.BorderStyle.FixedSingle };
            var lnk  = new SWF.LinkLabel { Text = linkText, Left = 16, Top = top + 62, Width = 460, Height = 22, Font = new SD.Font("Segoe UI", 10), ForeColor = SD.Color.FromArgb(30, 80, 160) };
            lnk.LinkClicked += (_, _) => Process.Start(new ProcessStartInfo { FileName = linkUrl, UseShellExecute = true });
            return new SWF.Control[] { lbl, tx, lnk };
        }

        var rowG = MakeRow("Gemini API ключ:", "Получить бесплатно на aistudio.google.com", "https://aistudio.google.com/apikey", _geminiApiKey, 12);
        var rowD = MakeRow("DeepSeek API ключ:", "Получить на platform.deepseek.com", "https://platform.deepseek.com/api_keys", _deepSeekApiKey, 102);
        var rowP = MakeRow("Perplexity API ключ:", "Получить на perplexity.ai/settings/api", "https://www.perplexity.ai/settings/api", _perplexityApiKey, 192);

        var btnOk = new SWF.Button
        {
            Text = "Сохранить", Left = 360, Top = 272, Width = 130, Height = 38,
            Font = new SD.Font("Segoe UI", 12, SD.FontStyle.Bold),
            BackColor = SD.Color.FromArgb(44, 95, 45), ForeColor = SD.Color.White,
            FlatStyle = SWF.FlatStyle.Flat, DialogResult = SWF.DialogResult.OK
        };
        btnOk.FlatAppearance.BorderSize = 0;

        dlg.Controls.AddRange(rowG);
        dlg.Controls.AddRange(rowD);
        dlg.Controls.AddRange(rowP);
        dlg.Controls.Add(btnOk);
        dlg.AcceptButton = btnOk;

        if (dlg.ShowDialog() == SWF.DialogResult.OK)
        {
            _geminiApiKey     = ((SWF.TextBox)rowG[1]).Text.Trim();
            _deepSeekApiKey   = ((SWF.TextBox)rowD[1]).Text.Trim();
            _perplexityApiKey = ((SWF.TextBox)rowP[1]).Text.Trim();
            SaveAiSettings();
            UpdateApiKeyHints();
        }
    }

    private void UpdateApiKeyHints()
    {
        for (int i = 0; i < AiList.Length; i++)
        {
            string apiId = AiList[i].ApiId;
            if (string.IsNullOrEmpty(apiId)) continue;
            string? key = ApiKeyFor(apiId);
            bool has = !string.IsNullOrEmpty(key);
            cbAiEnabled[i].Text = has ? "Включить (API ✓)" : "Включить";
            cbAiEnabled[i].ForeColor = has ? SD.Color.FromArgb(20, 100, 20) : SD.Color.FromArgb(30, 70, 30);
        }
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
