using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Data.Sqlite;
using QRCoder;
using SWC  = System.Windows.Controls;
using SWin = System.Windows;

namespace MenuApp;

public partial class MainWindow
{
    // Вкладка «Составить список» переведена на WPF (dgShopList и панель сервера в MainWindow.xaml).
    private readonly ObservableCollection<ShoppingListRow> shopList = new();
    private ShoppingListServer? _shoppingServer;

    // ══════════════════════════════════════════════════ ИНИЦИАЛИЗАЦИЯ WPF-ВКЛАДКИ

    internal void InitShoppingListTab()
    {
        dgShopList.ItemsSource = shopList;

        btnSlAll.Click       += (_, _) => { foreach (var r in shopList) r.Check = true;  };
        btnSlNone.Click      += (_, _) => { foreach (var r in shopList) r.Check = false; };
        btnSlRefresh.Click   += (_, _) => { shopList.Clear(); PopulateShoppingList(); };
        btnStartServer.Click += BtnStartServer_Click;
        btnStopServer.Click  += BtnStopServer_Click;
    }

    // ══════════════════════════════════════════════════ СОБЫТИЯ

    private void BtnStartServer_Click(object? s, SWin.RoutedEventArgs e)
    {
        StopShoppingServer();
        string html = BuildShoppingHtml();
        if (string.IsNullOrEmpty(html))
        {
            SWin.MessageBox.Show("Не отмечено ни одного продукта.\nПоставьте галочки напротив нужных товаров.",
                "Список пуст", SWin.MessageBoxButton.OK, SWin.MessageBoxImage.Information);
            return;
        }

        int port = FindFreePort(8888);
        _shoppingServer = new ShoppingListServer(html, port);
        _shoppingServer.Start();

        string ip  = GetLocalIp();
        string url = $"http://{ip}:{port}";

        lblServerUrl.Text         = url;
        lblScanHint.Visibility    = SWin.Visibility.Visible;
        lblServerUrl.Visibility   = SWin.Visibility.Visible;
        qrBorder.Visibility       = SWin.Visibility.Visible;
        btnStartServer.Visibility = SWin.Visibility.Collapsed;
        btnStopServer.Visibility  = SWin.Visibility.Visible;

        ShowQr(url);
    }

    private void BtnStopServer_Click(object? s, SWin.RoutedEventArgs e)
    {
        StopShoppingServer();
        lblScanHint.Visibility    = SWin.Visibility.Collapsed;
        lblServerUrl.Visibility   = SWin.Visibility.Collapsed;
        qrBorder.Visibility       = SWin.Visibility.Collapsed;
        btnStopServer.Visibility  = SWin.Visibility.Collapsed;
        btnStartServer.Visibility = SWin.Visibility.Visible;
    }

    // ══════════════════════════════════════════════════ ВСПОМОГАТЕЛЬНЫЕ

    private void PopulateShoppingList()
    {
        if (shopList.Count > 0) return;

        var haItems = LoadSubcategoriesFromHomeAccounting();
        if (haItems.Count > 0)
        {
            foreach (var (name, unit) in haItems)
                shopList.Add(new ShoppingListRow { Name = name, Unit = unit });
            lblListSource.Text       = $"📦 Из HomeAccounting: {haItems.Count} продуктов";
            lblListSource.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(40, 100, 40));
        }
        else
        {
            foreach (var p in prices)
                shopList.Add(new ShoppingListRow { Name = p.Name, Unit = p.Unit });
            lblListSource.Text       = "ℹ Встроенный список (HomeAccounting не найден)";
            lblListSource.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(140, 100, 40));
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
        foreach (var row in shopList)
        {
            if (!row.Check) continue;
            string label = $"{(row.Qty ?? "1").Trim()} {row.Unit}".Trim();
            items.Add((row.Name, label));
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
            byte[] png = new PngByteQRCode(data).GetGraphic(8);

            var img = new System.Windows.Media.Imaging.BitmapImage();
            using (var ms = new MemoryStream(png))
            {
                img.BeginInit();
                img.CacheOption  = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                img.StreamSource = ms;
                img.EndInit();
            }
            img.Freeze();
            imgQr.Source = img;
        }
        catch { qrBorder.Visibility = SWin.Visibility.Collapsed; }
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
