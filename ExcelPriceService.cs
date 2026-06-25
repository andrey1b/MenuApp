using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ClosedXML.Excel;

namespace MenuApp
{
    class PriceMapping
    {
        public string  AppProduct { get; set; } = "";
        public string  ExcelName  { get; set; } = "";
        public decimal Multiplier { get; set; } = 1.0m;
    }

    class RealPriceResult
    {
        public decimal  LastPrice { get; set; }   // цена за 1 Excel-единицу (последняя покупка)
        public DateTime LastDate  { get; set; }
        public string   LastUnit  { get; set; } = "";
        public decimal  Avg30d    { get; set; }   // средняя за 30 дн., per Excel-единица
        public decimal  Avg90d    { get; set; }   // средняя за 90 дн., per Excel-единица
        public int      Count30d  { get; set; }
        public int      Count90d  { get; set; }
    }

    record FoodPurchase(DateTime Date, string Name, decimal Qty, string Unit, decimal Total);

    static class ExcelPriceService
    {
        public static readonly string ExcelFilePath =
            @"C:\Users\User\Opus 4.6\HomeB\Excel\Расходы_0.XLSX";

        public static string DataDirectory { get; set; } =
            AppDomain.CurrentDomain.BaseDirectory;

        private static string MappingsPath =>
            Path.Combine(DataDirectory, "price_mappings.json");

        // ── Persistence ───────────────────────────────────────────

        public static List<PriceMapping> LoadMappings()
        {
            if (!File.Exists(MappingsPath)) return new();
            try
            {
                var json = File.ReadAllText(MappingsPath, System.Text.Encoding.UTF8);
                return JsonSerializer.Deserialize<List<PriceMapping>>(json) ?? new();
            }
            catch { return new(); }
        }

        public static void SaveMappings(List<PriceMapping> mappings)
        {
            try
            {
                var json = JsonSerializer.Serialize(mappings, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });
                File.WriteAllText(MappingsPath, json, System.Text.Encoding.UTF8);
            }
            catch { }
        }

        // ── Excel reading ─────────────────────────────────────────

        // Returns all "Продукты питания" rows from Sheet1.
        public static List<FoodPurchase> ReadPurchases()
        {
            var result = new List<FoodPurchase>();
            if (!File.Exists(ExcelFilePath)) return result;
            try
            {
                using var wb = new XLWorkbook(ExcelFilePath);
                var ws = wb.Worksheet(1);
                bool isHeader = true;
                foreach (var row in ws.RowsUsed())
                {
                    if (isHeader) { isHeader = false; continue; }
                    try
                    {
                        string cat = row.Cell(3).GetString();
                        if (!cat.Contains("Продукты", StringComparison.OrdinalIgnoreCase)) continue;

                        if (!row.Cell(1).TryGetValue(out DateTime date)) continue;
                        string name = row.Cell(4).GetString().Trim();
                        string unit = row.Cell(6).GetString().Trim();
                        if (!row.Cell(5).TryGetValue(out decimal qty)   || qty   <= 0) continue;
                        if (!row.Cell(7).TryGetValue(out decimal total) || total <= 0) continue;
                        if (string.IsNullOrWhiteSpace(name)) continue;

                        result.Add(new FoodPurchase(date.Date, name, qty, unit, total));
                    }
                    catch { }
                }
            }
            catch { }
            return result;
        }

        public static List<string> GetDistinctNames(List<FoodPurchase> purchases) =>
            purchases.Select(p => p.Name)
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(n => n)
                     .ToList();

        // ── Price computation ─────────────────────────────────────

        // Prices are stored PER EXCEL UNIT (no multiplier division).
        // Use mapping.Multiplier in the UI when converting to app-units.
        public static Dictionary<string, RealPriceResult> ComputeRealPrices(
            List<PriceMapping> mappings, List<FoodPurchase> purchases)
        {
            var result = new Dictionary<string, RealPriceResult>(StringComparer.OrdinalIgnoreCase);
            var today  = DateTime.Today;

            foreach (var m in mappings)
            {
                if (string.IsNullOrWhiteSpace(m.ExcelName)) continue;

                var hits = purchases
                    .Where(p => p.Name.Equals(m.ExcelName, StringComparison.OrdinalIgnoreCase)
                             && p.Qty > 0)
                    .Select(p => (date: p.Date, price: p.Total / p.Qty, unit: p.Unit))
                    .OrderByDescending(x => x.date)
                    .ToList();

                if (!hits.Any()) continue;

                var d30 = hits.Where(x => (today - x.date).TotalDays <= 30).ToList();
                var d90 = hits.Where(x => (today - x.date).TotalDays <= 90).ToList();

                result[m.AppProduct] = new RealPriceResult
                {
                    LastPrice = Math.Round(hits.First().price, 2),
                    LastDate  = hits.First().date,
                    LastUnit  = hits.First().unit,
                    Avg30d    = d30.Any() ? Math.Round(d30.Average(x => x.price), 2) : 0,
                    Avg90d    = d90.Any() ? Math.Round(d90.Average(x => x.price), 2) : 0,
                    Count30d  = d30.Count,
                    Count90d  = d90.Count,
                };
            }
            return result;
        }

        // ── Auto-matching ─────────────────────────────────────────

        // Known aliases: our product name → possible Excel subcategory names (in priority order)
        private static readonly Dictionary<string, string[]> KnownAliases =
            new(StringComparer.OrdinalIgnoreCase)
        {
            ["Свинина"]           = new[] { "Мясо", "Шейка", "Котлеты", "Корейка", "Свинина" },
            ["Говядина"]          = new[] { "Говядина", "Вырезка", "Мясо" },
            ["Филе куриное"]      = new[] { "Филе курицы", "Филе" },
            ["Рыба мороженая"]    = new[] { "Рыба", "Минтай", "Хек" },
            ["Масло подсолнечное"]= new[] { "Подсолнечное масло", "Масло подсолнечное", "Масло" },
        };

        // Best-effort name match: aliases → exact → first-word prefix → contains
        public static string? AutoMatch(string appProduct, List<string> excelNames)
        {
            // Check known aliases first
            if (KnownAliases.TryGetValue(appProduct, out var aliases))
                foreach (var alias in aliases)
                {
                    var hit = excelNames.FirstOrDefault(
                        n => n.Equals(alias, StringComparison.OrdinalIgnoreCase));
                    if (hit != null) return hit;
                }

            var exact = excelNames.FirstOrDefault(
                n => n.Equals(appProduct, StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact;

            string firstWord = appProduct.Split(' ')[0];
            return excelNames.FirstOrDefault(n =>
                n.StartsWith(firstWord, StringComparison.OrdinalIgnoreCase) ||
                n.Contains(firstWord,   StringComparison.OrdinalIgnoreCase));
        }

        // Heuristic multiplier: how many app-units are in one Excel unit
        public static decimal DefaultMultiplier(string excelUnit, string appUnit)
        {
            string eu = excelUnit.ToLowerInvariant().TrimEnd('.');
            string au = appUnit.ToLowerInvariant();
            if (eu is "кг" && au == "кг")           return 1.0m;
            if (eu is "л"  && au == "л")            return 1.0m;
            if (eu is "дес" or "десяток" && au == "десяток") return 1.0m;
            if (eu is "пакет" or "пачка" && au == "кг") return 1.0m;   // 1 кг-пачка
            if (eu == "батон"   && au == "кг")      return 0.6m;  // батон ≈ 600г
            if (eu is "пачка"   or "упаковка" && au == "200 г") return 1.0m;
            if (eu is "упаковка"              && au == "500 г") return 1.0m;
            return 1.0m;
        }
    }
}
