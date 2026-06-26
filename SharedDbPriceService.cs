using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;

namespace MenuApp
{
    // ── Чтение общей базы «Офиса пенсионера» (SeniorHub) ──────────────
    //
    // Путь по договорённости: %LocalAppData%\SeniorHub\shared.db
    // Контракт: таблица food_purchases, которую наполняет ТОЛЬКО HomeAccounting
    // (одностороннее зеркало своих покупок продуктов). MenuApp — читатель.
    //
    //   CREATE TABLE food_purchases (
    //       id         INTEGER PRIMARY KEY AUTOINCREMENT,
    //       date       TEXT NOT NULL,   -- ISO yyyy-MM-dd
    //       product    TEXT NOT NULL,
    //       qty        REAL NOT NULL,
    //       unit       TEXT,
    //       total      REAL NOT NULL,
    //       source     TEXT,
    //       created_at TEXT DEFAULT (datetime('now'))
    //   );
    //
    // База может отсутствовать (запуск вне «Офиса пенсионера») — тогда
    // возвращаем пустой список, а вызывающий код откатывается на Excel.
    static class SharedDbPriceService
    {
        public static readonly string DbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SeniorHub", "shared.db");

        public static bool DbExists => File.Exists(DbPath);

        // Читает все покупки продуктов из общей базы.
        // Пустой список = базы нет, таблицы нет или данных нет.
        public static List<FoodPurchase> ReadPurchases()
        {
            var result = new List<FoodPurchase>();
            if (!DbExists) return result;

            try
            {
                using var conn = new SqliteConnection(
                    $"Data Source={DbPath};Mode=ReadOnly;Cache=Shared");
                conn.Open();

                using var cmd = conn.CreateCommand();
                cmd.CommandText =
                    "SELECT date, product, qty, unit, total FROM food_purchases";

                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    if (r.IsDBNull(0) || r.IsDBNull(1)) continue;

                    string rawDate = r.GetString(0);
                    if (!TryParseDate(rawDate, out var date)) continue;

                    string name = r.GetString(1).Trim();
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    decimal qty   = r.IsDBNull(2) ? 0m : Convert.ToDecimal(r.GetDouble(2));
                    string  unit  = r.IsDBNull(3) ? "" : r.GetString(3).Trim();
                    decimal total = r.IsDBNull(4) ? 0m : Convert.ToDecimal(r.GetDouble(4));
                    if (qty <= 0 || total <= 0) continue;

                    result.Add(new FoodPurchase(date.Date, name, qty, unit, total));
                }
            }
            catch
            {
                // Таблицы может не быть, или формат базы другой — тихо откатываемся.
                return new List<FoodPurchase>();
            }
            return result;
        }

        private static bool TryParseDate(string s, out DateTime date)
        {
            // Контракт — ISO yyyy-MM-dd, но допускаем дату со временем и локальный формат.
            return DateTime.TryParse(s, CultureInfo.InvariantCulture,
                       DateTimeStyles.None, out date)
                || DateTime.TryParse(s, out date);
        }
    }
}
