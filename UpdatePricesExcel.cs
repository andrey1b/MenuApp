using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using ExcelDataReader;

namespace DailyMenuApp
{
    public class UpdatePricesExcel
    {
        public void UpdatePricesForCurrentMonth(DataGridView dgvProducts)
        {
            string month = DateTime.Now.ToString("yyyy_MM");
            string fileName = $"average_prices_{month}.xlsx";

            if (File.Exists(fileName))
            {
                ApplyPrices(dgvProducts, fileName);
                MessageBox.Show($"Цены за {month} успешно загружены из Excel!");
            }
            else
            {
                DialogResult result = MessageBox.Show(
                    $"Файл {fileName} не найден.\nХотите выбрать Excel вручную?",
                    "Обновление цен",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    using (OpenFileDialog ofd = new OpenFileDialog())
                    {
                        ofd.Filter = "Excel files (*.xlsx)|*.xlsx";
                        if (ofd.ShowDialog() == DialogResult.OK)
                        {
                            ApplyPrices(dgvProducts, ofd.FileName);
                            MessageBox.Show("Цены успешно загружены из выбранного Excel!");
                        }
                    }
                }
            }
        }

        private void ApplyPrices(DataGridView dgvProducts, string excelFile)
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            var averagePrices = new Dictionary<string, decimal>();

            using (var stream = File.Open(excelFile, FileMode.Open, FileAccess.Read))
            using (var reader = ExcelReaderFactory.CreateReader(stream))
            {
                while (reader.Read())
                {
                    string name = reader.GetString(0);
                    if (decimal.TryParse(reader.GetValue(1)?.ToString(), out decimal price))
                    {
                        averagePrices[name] = price;
                    }
                }
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
}
