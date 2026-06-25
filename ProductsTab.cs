using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Text.Json;

public class ProductsTab
{
    private DataGridView dgvProducts;
    private TabPage tabPage;

    public ProductsTab(TabPage tabPage)
    {
        this.tabPage = tabPage;
        InitializeProductsTab();
    }

    private void InitializeProductsTab()
    {
        tabPage.Text = "Продукты";

        dgvProducts = new DataGridView();
        dgvProducts.Dock = DockStyle.Fill;
        dgvProducts.AllowUserToAddRows = true;
        dgvProducts.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

        dgvProducts.Columns.Add("ProductName", "Продукты");
        dgvProducts.Columns.Add("Price", "Цена");
        dgvProducts.Columns.Add("Quantity", "Количество");
        dgvProducts.Columns.Add("Need", "Потребность");
        
        int totalIndex = dgvProducts.Columns.Add("Total", "Сумма");
        dgvProducts.Columns[totalIndex].ReadOnly = true;

        dgvProducts.CellValueChanged += dgvProducts_CellValueChanged;
        dgvProducts.EditingControlShowing += dgvProducts_EditingControlShowing;
        dgvProducts.RowPrePaint += dgvProducts_RowPrePaint;

        tabPage.Controls.Add(dgvProducts);

        // Добавляем строку "Итого"
        int index = dgvProducts.Rows.Add();
        dgvProducts.Rows[index].Cells["ProductName"].Value = "Итого:";
        dgvProducts.Rows[index].ReadOnly = true;

        AddButtons();

        // Автоматическая загрузка цен при старте
        var updater = new UpdatePrices();
        updater.UpdatePricesForCurrentMonth(dgvProducts);
    }

    private void AddButtons()
    {
        Button btnExportJson = new Button();
        btnExportJson.Text = "Экспорт в JSON";
        btnExportJson.Dock = DockStyle.Bottom;
        btnExportJson.Height = 40;
        btnExportJson.Click += btnExportJson_Click;

        Button btnImportJson = new Button();
        btnImportJson.Text = "Импорт из JSON";
        btnImportJson.Dock = DockStyle.Bottom;
        btnImportJson.Height = 40;
        btnImportJson.Click += btnImportJson_Click;

        tabPage.Controls.Add(btnImportJson);
        tabPage.Controls.Add(btnExportJson);
    }

    private void dgvProducts_CellValueChanged(object sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < dgvProducts.Rows.Count - 1)
        {
            RecalculateRow(dgvProducts.Rows[e.RowIndex]);
            UpdateGrandTotal();
        }
    }

    private void dgvProducts_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
    {
        if (dgvProducts.CurrentCell.ColumnIndex == dgvProducts.Columns["Price"].Index ||
            dgvProducts.CurrentCell.ColumnIndex == dgvProducts.Columns["Need"].Index)
        {
            if (e.Control is TextBox tb)
            {
                tb.TextChanged -= TextBox_TextChanged;
                tb.TextChanged += TextBox_TextChanged;
            }
        }
    }

    private void TextBox_TextChanged(object sender, EventArgs e)
    {
        if (dgvProducts.CurrentCell != null && dgvProducts.CurrentCell.RowIndex < dgvProducts.Rows.Count - 1)
        {
            int rowIndex = dgvProducts.CurrentCell.RowIndex;
            RecalculateRow(dgvProducts.Rows[rowIndex]);
            UpdateGrandTotal();
        }
    }

    private void RecalculateRow(DataGridViewRow row)
    {
        decimal price = 0;
        decimal need = 0;

        decimal.TryParse(Convert.ToString(row.Cells["Price"].Value), out price);
        decimal.TryParse(Convert.ToString(row.Cells["Need"].Value), out need);

        string unit = Convert.ToString(row.Cells["Quantity"].Value)?.ToLower();
        decimal multiplier = 1;

        if (!string.IsNullOrEmpty(unit))
        {
            if (unit.Contains("г")) multiplier = 0.001m;
            else if (unit.Contains("кг")) multiplier = 1m;
            else if (unit.Contains("мл")) multiplier = 0.001m;
            else if (unit.Contains("л")) multiplier = 1m;
            else if (unit.Contains("шт")) multiplier = 1m;
        }

        if (price > 0 && need > 0)
        {
            decimal total = price * need * multiplier;
            row.Cells["Total"].Value = $"{total:F2} грн";
        }
        else
        {
            row.Cells["Total"].Value = null;
        }
    }

    private void UpdateGrandTotal()
    {
        decimal grandTotal = 0;

        foreach (DataGridViewRow row in dgvProducts.Rows)
        {
            if (row.IsNewRow || row.Cells["ProductName"].Value?.ToString() == "Итого:") continue;

            string totalStr = Convert.ToString(row.Cells["Total"].Value);
            if (!string.IsNullOrEmpty(totalStr) && totalStr.EndsWith("грн"))
            {
                if (decimal.TryParse(totalStr.Replace("грн", "").Trim(), out decimal val))
                {
                    grandTotal += val;
                }
            }
        }

        var totalRow = dgvProducts.Rows[dgvProducts.Rows.Count - 1];
        totalRow.Cells["ProductName"].Value = "Итого:";
        totalRow.Cells["Total"].Value = $"{grandTotal:F2} грн";
    }

    private void dgvProducts_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
    {
        var row = dgvProducts.Rows[e.RowIndex];
        if (row.Cells["ProductName"].Value?.ToString() == "Итого:")
        {
            row.DefaultCellStyle.BackColor = Color.LightYellow;
            row.DefaultCellStyle.Font = new Font(dgvProducts.Font, FontStyle.Bold);
        }
    }

    private void btnExportJson_Click(object sender, EventArgs e)
    {
        SaveFileDialog sfd = new SaveFileDialog();
        sfd.Filter = "JSON файлы (*.json)|*.json|Все файлы (*.*)|*.*";
        sfd.FileName = "products.json";

        if (sfd.ShowDialog() == DialogResult.OK)
        {
            var products = new List<Product>();

            foreach (DataGridViewRow row in dgvProducts.Rows)
            {
                if (row.IsNewRow || row.Cells["ProductName"].Value?.ToString() == "Итого:") continue;

                decimal price = 0;
                decimal need = 0;
                decimal.TryParse(Convert.ToString(row.Cells["Price"].Value), out price);
                decimal.TryParse(Convert.ToString(row.Cells["Need"].Value), out need);

                string totalStr = Convert.ToString(row.Cells["Total"].Value)?.Replace("грн", "").Trim();
                decimal total = 0;
                decimal.TryParse(totalStr, out total);

                products.Add(new Product
                {
                    name = row.Cells["ProductName"].Value?.ToString(),
                    price = price,
                    unit = row.Cells["Quantity"].Value?.ToString(),
                    need = need,
                    total = total
                });
            }

            string grandTotalStr = Convert.ToString(dgvProducts.Rows[dgvProducts.Rows.Count - 1].Cells["Total"].Value)?.Replace("грн", "").Trim();
            decimal grandTotal = 0;
            decimal.TryParse(grandTotalStr, out grandTotal);

            var exportData = new JsonData
            {
                products = products,
                grandTotal = grandTotal
            };

            string json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(sfd.FileName, json);

            MessageBox.Show($"Данные успешно экспортированы в {sfd.FileName}", "Экспорт завершён", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void btnImportJson_Click(object sender, EventArgs e)
    {
        OpenFileDialog ofd = new OpenFileDialog();
        ofd.Filter = "JSON файлы (*.json)|*.json|Все файлы (*.*)|*.*";

        if (ofd.ShowDialog() == DialogResult.OK)
        {
            try
            {
                string json = File.ReadAllText(ofd.FileName);
                var data = JsonSerializer.Deserialize<JsonData>(json);

                if (data == null || data.products == null)
                {
                    MessageBox.Show("Файл повреждён или имеет неверный формат!", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                dgvProducts.Rows.Clear();

                foreach (var p in data.products)
                {
                    int index = dgvProducts.Rows.Add();
                    var row = dgvProducts.Rows[index];

                    bool hasError = false;

                    if (string.IsNullOrWhiteSpace(p.name)) hasError = true;
                    row.Cells["ProductName"].Value = p.name ?? "";

                    if (p.price <= 0) hasError = true;
                    row.Cells["Price"].Value = p.price > 0 ? p.price : null;

                    row.Cells["Quantity"].Value = string.IsNullOrWhiteSpace(p.unit) ? "" : p.unit;

                    if (p.need <= 0) hasError = true;
                    row.Cells["Need"].Value = p.need > 0 ? p.need : null;

                    row.Cells["Total"].Value = p.total > 0 ? $"{p.total:F2} грн" : null;

                    if (hasError)
                    {
                        row.DefaultCellStyle.BackColor = Color.LightCoral;
                    }
                }

                // Добавляем строку "Итого:"
                int lastIndex = dgvProducts.Rows.Add();
                dgvProducts.Rows[lastIndex].Cells["ProductName"].Value = "Итого:";
                dgvProducts.Rows[lastIndex].ReadOnly = true;

                UpdateGrandTotal();
                MessageBox.Show("Данные успешно импортированы!", "Импорт завершён", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при импорте: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}

// Класс для представления продукта
public class Product
{
    public string name { get; set; }
    public decimal price { get; set; }
    public string unit { get; set; }
    public decimal need { get; set; }
    public decimal total { get; set; }
}

// Класс для JSON структуры
public class JsonData
{
    public List<Product> products { get; set; }
    public decimal grandTotal { get; set; }
}