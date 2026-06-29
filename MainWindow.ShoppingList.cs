using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Data.Sqlite;
using QRCoder;
using SWF  = System.Windows.Forms;
using SD   = System.Drawing;

namespace MenuApp;

public partial class MainWindow
{
    private SWF.DataGridView  dgvShoppingList = null!;
    private SWF.Label         lblServerUrl    = null!;
    private SWF.Label         lblScanHint     = null!;
    private SWF.Label         lblListSource   = null!;
    private SWF.PictureBox    pbQr            = null!;
    private SWF.Button        btnStartServer  = null!;
    private SWF.Button        btnStopServer   = null!;
    private ShoppingListServer? _shoppingServer;

    // ══════════════════════════════════════════════════ ПАНЕЛЬ

    internal SWF.Panel CreateShoppingListPanel()
    {
        var table = new SWF.TableLayoutPanel
        {
            Dock = SWF.DockStyle.Fill,
            ColumnCount = 2, RowCount = 1
        };
        table.ColumnStyles.Add(new SWF.ColumnStyle(SWF.SizeType.Percent, 66f));
        table.ColumnStyles.Add(new SWF.ColumnStyle(SWF.SizeType.Percent, 34f));
        table.RowStyles.Add(new SWF.RowStyle(SWF.SizeType.Percent, 100f));

        table.Controls.Add(BuildListPanel(), 0, 0);
        table.Controls.Add(BuildServerPanel(), 1, 0);

        var outer = new SWF.Panel { Dock = SWF.DockStyle.Fill };
        outer.Controls.Add(table);
        outer.VisibleChanged += (_, _) => { if (outer.Visible) PopulateShoppingList(); };
        return outer;
    }

    // ── Левая панель: таблица продуктов ─────────────────────

    private SWF.Panel BuildListPanel()
    {
        var panel = new SWF.Panel { Dock = SWF.DockStyle.Fill };

        // Верхняя полоска с кнопками
        var top = new SWF.Panel { Dock = SWF.DockStyle.Top, Height = 92, BackColor = SD.Color.WhiteSmoke, Padding = new SWF.Padding(8, 8, 8, 4) };
        var btnAll     = MakeSmallBtn("Выбрать все",  4,  8, 140, SD.Color.FromArgb(195, 230, 195));
        var btnNone    = MakeSmallBtn("Снять все",   152, 8, 115, SD.Color.FromArgb(230, 200, 200));
        var btnRefresh = MakeSmallBtn("🔄 Обновить", 275, 8, 130, SD.Color.FromArgb(210, 230, 255));
        var lblHint = new SWF.Label
        {
            Text = "Отметьте продукты, укажите количество, затем нажмите «Открыть на телефоне» →",
            Left = 415, Top = 10, Width = 450, Height = 36,
            TextAlign = SD.ContentAlignment.MiddleLeft,
            Font = new SD.Font("Segoe UI", 11), ForeColor = SD.Color.DimGray, AutoSize = false
        };
        lblListSource = new SWF.Label
        {
            Text = "", Left = 4, Top = 52, Width = 860, Height = 32,
            TextAlign = SD.ContentAlignment.MiddleLeft,
            Font = new SD.Font("Segoe UI", 10), ForeColor = SD.Color.FromArgb(80, 120, 80), AutoSize = false
        };
        btnRefresh.Click += (_, _) => { dgvShoppingList.Rows.Clear(); PopulateShoppingList(); };
        top.Controls.AddRange(new SWF.Control[] { btnAll, btnNone, btnRefresh, lblHint, lblListSource });

        // DataGridView
        dgvShoppingList = new SWF.DataGridView
        {
            Dock = SWF.DockStyle.Fill,
            AllowUserToAddRows = false, AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false, RowHeadersVisible = false,
            SelectionMode = SWF.DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = SWF.DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = SD.Color.White, BorderStyle = SWF.BorderStyle.None,
            Font = new SD.Font("Segoe UI", 13),
            GridColor = SD.Color.FromArgb(168, 213, 169),
            ColumnHeadersDefaultCellStyle = new SWF.DataGridViewCellStyle
            {
                BackColor = SD.Color.FromArgb(44, 95, 45), ForeColor = SD.Color.White,
                Font = new SD.Font("Segoe UI", 13, SD.FontStyle.Bold),
                Alignment = SWF.DataGridViewContentAlignment.MiddleCenter
            },
            AlternatingRowsDefaultCellStyle = new SWF.DataGridViewCellStyle { BackColor = SD.Color.FromArgb(240, 248, 240) },
            RowTemplate = { Height = 40 }
        };
        dgvShoppingList.ColumnHeadersHeightSizeMode = SWF.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        dgvShoppingList.ColumnHeadersHeight = 42;
        dgvShoppingList.EnableHeadersVisualStyles = false;

        dgvShoppingList.Columns.Add(new SWF.DataGridViewCheckBoxColumn { Name = "SlCheck", HeaderText = "✓",         FillWeight = 5  });
        dgvShoppingList.Columns.Add(new SWF.DataGridViewTextBoxColumn  { Name = "SlName",  HeaderText = "Продукт",   FillWeight = 52, ReadOnly = true });
        dgvShoppingList.Columns.Add(new SWF.DataGridViewTextBoxColumn  { Name = "SlQty",   HeaderText = "Количество",FillWeight = 26 });
        dgvShoppingList.Columns.Add(new SWF.DataGridViewTextBoxColumn  { Name = "SlUnit",  HeaderText = "Ед.",       FillWeight = 17, ReadOnly = true,
            DefaultCellStyle = new SWF.DataGridViewCellStyle { Alignment = SWF.DataGridViewContentAlignment.MiddleCenter } });

        foreach (SWF.DataGridViewColumn col in dgvShoppingList.Columns) col.SortMode = SWF.DataGridViewColumnSortMode.NotSortable;
        dgvShoppingList.CurrentCellDirtyStateChanged += (s, e) =>
        {
            if (dgvShoppingList.IsCurrentCellDirty) dgvShoppingList.CommitEdit(SWF.DataGridViewDataErrorContexts.Commit);
        };

        btnAll.Click  += (_, _) => { foreach (SWF.DataGridViewRow r in dgvShoppingList.Rows) r.Cells["SlCheck"].Value = true;  };
        btnNone.Click += (_, _) => { foreach (SWF.DataGridViewRow r in dgvShoppingList.Rows) r.Cells["SlCheck"].Value = false; };

        panel.Controls.Add(dgvShoppingList);
        panel.Controls.Add(top);
        return panel;
    }

    // ── Правая панель: сервер + QR ──────────────────────────

    private SWF.Panel BuildServerPanel()
    {
        var panel = new SWF.Panel { Dock = SWF.DockStyle.Fill, BackColor = SD.Color.FromArgb(238, 248, 238), Padding = new SWF.Padding(16) };

        var lblTitle = new SWF.Label
        {
            Text = "Отправить на телефон",
            Font = new SD.Font("Segoe UI", 14, SD.FontStyle.Bold),
            ForeColor = SD.Color.FromArgb(44, 95, 45),
            AutoSize = true, Left = 16, Top = 14
        };

        btnStartServer = new SWF.Button
        {
            Text = "📱  Открыть на телефоне",
            Left = 16, Top = 54, Width = 260, Height = 46,
            Font = new SD.Font("Segoe UI", 13, SD.FontStyle.Bold),
            BackColor = SD.Color.FromArgb(44, 95, 45), ForeColor = SD.Color.White,
            FlatStyle = SWF.FlatStyle.Flat, Cursor = SWF.Cursors.Hand
        };
        btnStartServer.FlatAppearance.BorderSize = 0;

        btnStopServer = new SWF.Button
        {
            Text = "■  Остановить сервер",
            Left = 16, Top = 54, Width = 260, Height = 46,
            Font = new SD.Font("Segoe UI", 13, SD.FontStyle.Bold),
            BackColor = SD.Color.FromArgb(190, 50, 50), ForeColor = SD.Color.White,
            FlatStyle = SWF.FlatStyle.Flat, Cursor = SWF.Cursors.Hand, Visible = false
        };
        btnStopServer.FlatAppearance.BorderSize = 0;

        lblScanHint = new SWF.Label
        {
            Text = "Отсканируйте QR-код или откройте\nадрес в браузере телефона:",
            Left = 16, Top = 114, Width = 300, Height = 46,
            Font = new SD.Font("Segoe UI", 11), ForeColor = SD.Color.DimGray, Visible = false
        };

        lblServerUrl = new SWF.Label
        {
            Text = "", Left = 16, Top = 166, Width = 340, Height = 28,
            Font = new SD.Font("Courier New", 12, SD.FontStyle.Bold),
            ForeColor = SD.Color.FromArgb(0, 70, 150), Visible = false
        };

        pbQr = new SWF.PictureBox
        {
            Left = 16, Top = 202, Width = 250, Height = 250,
            SizeMode = SWF.PictureBoxSizeMode.Zoom,
            BackColor = SD.Color.White,
            BorderStyle = SWF.BorderStyle.FixedSingle,
            Visible = false
        };

        btnStartServer.Click += BtnStartServer_Click;
        btnStopServer.Click  += BtnStopServer_Click;

        panel.Controls.AddRange(new SWF.Control[] { lblTitle, btnStartServer, btnStopServer, lblScanHint, lblServerUrl, pbQr });
        return panel;
    }

    // ══════════════════════════════════════════════════ СОБЫТИЯ

    private void BtnStartServer_Click(object? s, EventArgs e)
    {
        StopShoppingServer();
        string html = BuildShoppingHtml();
        if (string.IsNullOrEmpty(html))
        {
            SWF.MessageBox.Show("Не отмечено ни одного продукта.\nПоставьте галочки напротив нужных товаров.",
                "Список пуст", SWF.MessageBoxButtons.OK, SWF.MessageBoxIcon.Information);
            return;
        }

        int port = FindFreePort(8888);
        _shoppingServer = new ShoppingListServer(html, port);
        _shoppingServer.Start();

        string ip  = GetLocalIp();
        string url = $"http://{ip}:{port}";

        lblServerUrl.Text     = url;
        lblScanHint.Visible   = true;
        lblServerUrl.Visible  = true;
        pbQr.Visible          = true;
        btnStartServer.Visible = false;
        btnStopServer.Visible  = true;

        ShowQr(url);
    }

    private void BtnStopServer_Click(object? s, EventArgs e)
    {
        StopShoppingServer();
        lblScanHint.Visible    = false;
        lblServerUrl.Visible   = false;
        pbQr.Visible           = false;
        btnStopServer.Visible  = false;
        btnStartServer.Visible = true;
    }

    // ══════════════════════════════════════════════════ ВСПОМОГАТЕЛЬНЫЕ

    private void PopulateShoppingList()
    {
        if (dgvShoppingList.Rows.Count > 0) return;

        var haItems = LoadSubcategoriesFromHomeAccounting();
        if (haItems.Count > 0)
        {
            foreach (var (name, unit) in haItems)
                dgvShoppingList.Rows.Add(false, name, "1", unit);
            lblListSource.Text = $"📦 Из HomeAccounting: {haItems.Count} продуктов";
            lblListSource.ForeColor = SD.Color.FromArgb(40, 100, 40);
        }
        else
        {
            foreach (var p in prices)
                dgvShoppingList.Rows.Add(false, p.Name, "1", p.Unit);
            lblListSource.Text = "ℹ Встроенный список (HomeAccounting не найден)";
            lblListSource.ForeColor = SD.Color.FromArgb(140, 100, 40);
        }
    }

    private List<(string name, string unit)> LoadSubcategoriesFromHomeAccounting()
    {
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HomeAccounting", "homeaccounting.db");
        if (!File.Exists(dbPath)) return new List<(string, string)>();

        var result = new List<(string, string)>();
        try
        {
            using var conn = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly;Pooling=False");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                WITH food_subs AS (
                    SELECT DISTINCT sc.id, sc.name
                    FROM subcategories sc
                    JOIN categories c ON sc.category_id = c.id
                    WHERE c.type = 'expense'
                      AND (c.name LIKE '%родукт%' OR c.name LIKE '%итани%')
                ),
                unit_counts AS (
                    SELECT fs.name, u.name AS unit_name,
                           ROW_NUMBER() OVER (PARTITION BY fs.name ORDER BY COUNT(*) DESC) AS rn
                    FROM expenses e
                    JOIN food_subs fs ON e.subcategory_id = fs.id
                    JOIN units u ON e.unit_id = u.id
                    WHERE e.unit_id IS NOT NULL
                    GROUP BY fs.name, u.name
                )
                SELECT DISTINCT fs.name, COALESCE(uc.unit_name, 'шт.') AS unit
                FROM food_subs fs
                LEFT JOIN unit_counts uc ON uc.name = fs.name AND uc.rn = 1
                ORDER BY fs.name";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                result.Add((reader.GetString(0), reader.GetString(1)));
        }
        catch { }
        return result;
    }

    private string BuildShoppingHtml()
    {
        var items = new List<(string name, string qty)>();
        foreach (SWF.DataGridViewRow row in dgvShoppingList.Rows)
        {
            if (row.IsNewRow) continue;
            if (row.Cells["SlCheck"].Value is not true) continue;
            string name = row.Cells["SlName"].Value?.ToString() ?? "";
            string qty  = (row.Cells["SlQty"].Value?.ToString() ?? "1").Trim();
            string unit = row.Cells["SlUnit"].Value?.ToString() ?? "";
            string label = $"{qty} {unit}".Trim();
            items.Add((name, label));
        }
        if (items.Count == 0) return "";

        var json = new StringBuilder("[");
        foreach (var (name, qty) in items)
            json.Append($"{{\"n\":\"{EscJson(name)}\",\"q\":\"{EscJson(qty)}\"}},");
        json.Length--;
        json.Append("]");

        return ShoppingHtml.Replace("ITEMS_PLACEHOLDER", json.ToString());
    }

    private void ShowQr(string url)
    {
        try
        {
            var gen  = new QRCodeGenerator();
            var data = gen.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
            var code = new QRCode(data);
            pbQr.Image = code.GetGraphic(8, SD.Color.Black, SD.Color.White, true);
        }
        catch { pbQr.Visible = false; }
    }

    private void StopShoppingServer()
    {
        _shoppingServer?.Dispose();
        _shoppingServer = null;
    }

    private static string EscJson(string s) =>
        s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string GetLocalIp()
    {
        try
        {
            using var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.IP);
            s.Connect("8.8.8.8", 65530);
            return ((IPEndPoint)s.LocalEndPoint!).Address.ToString();
        }
        catch { return "localhost"; }
    }

    private static int FindFreePort(int start)
    {
        for (int p = start; p < start + 50; p++)
        {
            try { var l = new TcpListener(IPAddress.Any, p); l.Start(); l.Stop(); return p; }
            catch { }
        }
        return start;
    }

    private static SWF.Button MakeSmallBtn(string text, int left, int top, int w, SD.Color bg) =>
        new SWF.Button
        {
            Text = text, Left = left, Top = top, Width = w, Height = 36,
            Font = new SD.Font("Segoe UI", 11),
            BackColor = bg, FlatStyle = SWF.FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1 }
        };

    // ══════════════════════════════════════════════════ HTML-ШАБЛОН

    private const string ShoppingHtml = """
<!DOCTYPE html>
<html lang="ru">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1,user-scalable=no">
<title>Список покупок</title>
<style>
*{box-sizing:border-box;-webkit-tap-highlight-color:transparent}
body{margin:0;background:#eef6ee;font-family:'Segoe UI',Arial,sans-serif}
header{background:#2C5F2D;color:#fff;padding:16px 16px 10px;position:sticky;top:0;z-index:10;box-shadow:0 2px 6px rgba(0,0,0,.25)}
h1{margin:0 0 5px;font-size:21px}
#ctr{font-size:13px;opacity:.82}
.list{padding:10px 12px 90px}
.row{display:flex;align-items:center;background:#fff;border-radius:10px;margin-bottom:9px;padding:14px;box-shadow:0 1px 4px rgba(0,0,0,.08);cursor:pointer;user-select:none;-webkit-user-select:none;transition:background .12s}
.row.done{background:#dff0df}
.cb{width:32px;height:32px;border:2.5px solid #3E8741;border-radius:7px;flex-shrink:0;display:flex;align-items:center;justify-content:center;margin-right:14px;font-size:20px;color:transparent;background:#fff;transition:.12s}
.row.done .cb{background:#3E8741;border-color:#2C5F2D;color:#fff}
.name{flex:1;font-size:18px;line-height:1.3}
.row.done .name{text-decoration:line-through;color:#888}
.qty{font-size:14px;color:#666;white-space:nowrap;margin-left:10px;min-width:54px;text-align:right}
.row.done .qty{color:#aaa;text-decoration:line-through}
.empty{text-align:center;padding:52px 20px;color:#2C5F2D;font-size:19px}
footer{position:fixed;bottom:0;left:0;right:0;background:#fff;border-top:1px solid #ddd;padding:10px 14px;display:flex;gap:10px}
footer button{flex:1;padding:14px;border:none;border-radius:9px;font-size:16px;font-weight:600;cursor:pointer}
#bReset{background:#f0f0f0;color:#555}
#bToggle{background:#3E8741;color:#fff}
</style>
</head>
<body>
<header><h1>🛒 Список покупок</h1><div id="ctr"></div></header>
<div class="list" id="list"></div>
<footer>
  <button id="bReset"  onclick="resetAll()">Сбросить</button>
  <button id="bToggle" onclick="toggleView()">Показать купленные</button>
</footer>
<script>
const items=ITEMS_PLACEHOLDER;
const K='sl_v2';
let done=JSON.parse(localStorage.getItem(K)||'{}');
let showAll=false;
function save(){localStorage.setItem(K,JSON.stringify(done))}
function upd(){
  var b=items.filter(function(_,i){return done[i]}).length;
  document.getElementById('ctr').textContent='Куплено: '+b+' из '+items.length;
}
function esc(s){return s.replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;')}
function render(){
  var el=document.getElementById('list');
  var vis=showAll?items:items.filter(function(_,i){return !done[i]});
  if(vis.length===0&&!showAll){
    el.innerHTML='<div class="empty">✅ Всё куплено!<br><small style="color:#666;font-size:15px">Нажмите «Показать купленные»<br>чтобы увидеть список</small></div>';
  }else{
    el.innerHTML=vis.map(function(item){
      var i=items.indexOf(item),d=done[i];
      return '<div class="row'+(d?' done':'')+'" onclick="tog('+i+')">'+
        '<div class="cb">'+(d?'✓':'')+'</div>'+
        '<span class="name">'+esc(item.n)+'</span>'+
        '<span class="qty">'+esc(item.q)+'</span></div>';
    }).join('');
  }
  upd();
}
function tog(i){done[i]=!done[i];save();render()}
function resetAll(){if(confirm('Сбросить все отметки?')){done={};save();render()}}
function toggleView(){
  showAll=!showAll;
  document.getElementById('bToggle').textContent=showAll?'Скрыть купленные':'Показать купленные';
  render();
}
render();
</script>
</body>
</html>
""";
}
