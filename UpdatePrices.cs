using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using ExcelDataReader;

namespace DailyMenuApp
{
    public class UpdatePrices
    {
        private readonly string baseUrl = "https://www.ukrstat.gov.ua/monthly_prices/"; 
        // пример: https://www.ukrstat.gov.ua/monthly_prices/prices_2026_04.xlsx

        public async Task<string> DownloadLatestFile()
        {
            string month = DateTime.Now.ToString("yyyy_MM");
            string fileName = $"prices_{month}.xlsx";
            string url = $"{baseUrl}{fileName}";

            using (HttpClient client = new HttpClient())
            {
                var bytes = await client.GetByteArrayAsync(url);
                File.WriteAllBytes(fileName, bytes);
            }

            return fileName;
        }

        public string ConvertToJson(string excelFile)
        {
            var prices = new List<ProductPrice>();

            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            using (var stream = File.Open(excelFile, FileMode.Open, FileAccess.Read))
            using (var reader = ExcelReaderFactory.CreateReader(stream))
            {
                while (reader.Read())
                {
                    string name = reader.GetString(0);
                    if (decimal.TryParse(reader.GetValue(1)?.ToString(), out decimal price))
                    {
                        prices.Add(new ProductPrice { Name = name, Price = price, Unit = "кг" });
                    }
                }
            }

            var exportData = new { month = DateTime.Now.ToString("yyyy-MM"), prices = prices };
            string jsonFile = $"average_prices_{DateTime.Now:yyyy_MM}.json";
            File.WriteAllText(jsonFile, JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true }));

            return jsonFile;
        }

        public void AutoFillPrices(DataGridView dgvProducts, string jsonFile)
        {
            string json = File.ReadAllText(jsonFile);
            var data = JsonSerializer.Deserialize<Dictionary<string, List<ProductPrice>>>(json);

            var averagePrices = new Dictionary<string, decimal>();
            foreach (var item in data["prices"])
            {
                averagePrices[item.Name] = item.Price;
            }

            foreach (DataGridViewRow row in dgvProducts.Rows)
            {
                if (row.IsNewRow) continue;
                string productName = row.Cells["ProductName"].Value?.ToString();
                if (!string.IsNullOrEmpty(productName) && averagePrices.ContainsKey(productName))
                {
                    row.Cells["Price"].Value = averagePrices[productName];
                }
            }
        }
        private async void btnUpdatePrices_Click(object sender, EventArgs e)
        {
            var updater = new UpdatePrices();
            string excelFile = await updater.DownloadLatestFile();
            string jsonFile = updater.ConvertToJson(excelFile);
            updater.AutoFillPrices(dgvProducts, jsonFile);

            MessageBox.Show("Цены обновлены по данным Госстата!");
        }

        private Dictionary<string, decimal> averagePrices;

        private void LoadAveragePrices(string filePath)
        {
            string json = File.ReadAllText(filePath);
            var data = JsonSerializer.Deserialize<Dictionary<string, List<ProductPrice>>>(json);

            averagePrices = new Dictionary<string, decimal>();
            foreach (var item in data["prices"])
            {
                averagePrices[item.Name] = item.Price;
            }
        }
        private void LoadMonthlyPrices(DataGridView dgvProducts)

        {
            string month = DateTime.Now.ToString("yyyy_MM"); 
            string fileName = $"average_prices_{month}.json";

            if (File.Exists(fileName))
            {
                string json = File.ReadAllText(fileName);
                var data = JsonSerializer.Deserialize<Dictionary<string, List<ProductPrice>>>(json);

                var averagePrices = new Dictionary<string, decimal>();
                foreach (var item in data["prices"])
                {
                    averagePrices[item.Name] = item.Price;
                }
            
                foreach (DataGridViewRow row in dgvProducts.Rows)
                {
                    if (row.IsNewRow) continue;
                    string productName = row.Cells["ProductName"].Value?.ToString();
                    if (!string.IsNullOrEmpty(productName) && averagePrices.ContainsKey(productName))
                    {
                        row.Cells["Price"].Value = averagePrices[productName];
                    }
                }

                MessageBox.Show($"Цены за {month} успешно загружены!");
            }
            else
            {
                MessageBox.Show($"Файл {fileName} не найден. Пожалуйста, добавьте его в папку программы.");
            }
        }
    
        private void btnLoadPrices_Click(object sender, EventArgs e)
        {
            LoadMonthlyPrices(dgvProducts);
        }

        public void UpdatePricesForCurrentMonth(DataGridView dgvProducts)
        {
            string month = DateTime.Now.ToString("yyyy_MM");
            string fileName = $"average_prices_{month}.json";

            if (File.Exists(fileName))
            {
                ApplyPrices(dgvProducts, fileName);
                MessageBox.Show($"Цены за {month} успешно загружены!");
            }
            else
            {
                DialogResult result = MessageBox.Show(
                    $"Файл {fileName} не найден.\nХотите выбрать другой JSON вручную?",
                    "Обновление цен",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    using (OpenFileDialog ofd = new OpenFileDialog())
                    {
                        ofd.Filter = "JSON files (*.json)|*.json";
                        if (ofd.ShowDialog() == DialogResult.OK)
                        {
                            ApplyPrices(dgvProducts, ofd.FileName);
                            MessageBox.Show("Цены успешно загружены из выбранного файла!");
                        }
                    }
                }
            }
        }
        private void ApplyPrices(DataGridView dgvProducts, string jsonFile)
        {
            string json = File.ReadAllText(jsonFile);
            var data = JsonSerializer.Deserialize<Dictionary<string, List<ProductPrice>>>(json);

            var averagePrices = new Dictionary<string, decimal>();
            foreach (var item in data["prices"])
            {
                averagePrices[item.Name] = item.Price;
            }

            foreach (DataGridViewRow row in dgvProducts.Rows)
            {
                if (row.IsNewRow) continue;
                string productName = row.Cells["ProductName"].Value?.ToString();
                if (!string.IsNullOrEmpty(productName) && averagePrices.ContainsKey(productName))
                {
                    row.Cells["Price"].Value = averagePrices[productName];
                }
            }
        }
    }
    
    public class ProductPrice
    {
        public string Name { get; set; }
        public decimal Price { get; set; }
        public string Unit { get; set; }
    }
}

