using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;

namespace DailyMenuApp
{
    public partial class ProductsForm : Form
    {
        public ProductsForm()
        {
            InitializeComponent();

            // Настройка таблицы
            dgvProducts.Columns.Add("ProductName", "Продукт");
            dgvProducts.Columns.Add("Price", "Цена");
            dgvProducts.Columns.Add("Quantity", "Количество");
            dgvProducts.Columns.Add("Need", "Потребность");
            dgvProducts.Columns.Add("Sum", "Сумма");

            // Кнопки
            Button btnImportJson = new Button { Text = "Импорт из JSON", Dock = DockStyle.Top };
            Button btnExportJson = new Button { Text = "Экспорт в JSON", Dock = DockStyle.Top };

            btnImportJson.Click += BtnImportJson_Click;
            btnExportJson.Click += BtnExportJson_Click;

            Controls.Add(btnExportJson);
            Controls.Add(btnImportJson);
            Controls.Add(dgvProducts);

            // Обработчик пересчёта суммы
            dgvProducts.CellValueChanged += DgvProducts_CellValueChanged;
        }

        private void DgvProducts_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                var row = dgvProducts.Rows[e.RowIndex];

                if (decimal.TryParse(row.Cells["Price"].Value?.ToString(), out decimal price) &&
                    decimal.TryParse(row.Cells["Quantity"].Value?.ToString(), out decimal quantity) &&
                    decimal.TryParse(row.Cells["Need"].Value?.ToString(), out decimal need))
                {
                    row.Cells["Sum"].Value = price * quantity * need;
                }
                else
                {
                    row.Cells["Sum"].Value = "";
                }
            }
        }

        private void BtnImportJson_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "JSON files (*.json)|*.json";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    string json = File.ReadAllText(ofd.FileName);
                    var data = JsonSerializer.Deserialize<Dictionary<string, List<Product>>>(json);

                    dgvProducts.Rows.Clear();
                    foreach (var product in data["products"])
                    {
                        dgvProducts.Rows.Add(product.Name, "", "", "", "");
                    }
                }
            }
        }

        private void BtnExportJson_Click(object sender, EventArgs e)
        {
            var products = new List<Product>();

            foreach (DataGridViewRow row in dgvProducts.Rows)
            {
                if (row.IsNewRow) continue;
                products.Add(new Product { Name = row.Cells["ProductName"].Value?.ToString() });
            }

            var exportData = new { products = products };

            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "JSON files (*.json)|*.json";
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    string json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(sfd.FileName, json);
                    MessageBox.Show("Список успешно сохранён в JSON");
                }
            }
        }
    }

    public class Product
    {
        public string Name { get; set; }
    }
}
