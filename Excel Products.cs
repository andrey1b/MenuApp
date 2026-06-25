using System;
using System.IO;
using System.Windows.Forms;
using ExcelDataReader;

namespace DailyMenuApp
{
    public partial class MainForm : Form
    {
        private TabControl tabControl;
        private DataGridView dgvProductsExcel;

        public MainForm()
        {
            InitializeComponent();

            // Создаём TabControl, если его ещё нет
            tabControl = new TabControl { Dock = DockStyle.Fill };
            this.Controls.Add(tabControl);

            // Вкладка "Продукты Эксель"
            TabPage tabExcel = new TabPage("Продукты Эксель");
            dgvProductsExcel = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };

            dgvProductsExcel.Columns.Add("ProductName", "Продукт");
            dgvProductsExcel.Columns.Add("Price", "Цена");

            // Кнопка загрузки Excel
            Button btnLoadExcel = new Button
            {
                Text = "Загрузить Excel",
                Dock = DockStyle.Top
            };
            btnLoadExcel.Click += BtnLoadExcel_Click;

            tabExcel.Controls.Add(dgvProductsExcel);
            tabExcel.Controls.Add(btnLoadExcel);

            tabControl.TabPages.Add(tabExcel);
        }

        private void BtnLoadExcel_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "Excel files (*.xlsx)|*.xlsx";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    LoadExcelFile(ofd.FileName);
                }
            }
        }

        private void LoadExcelFile(string filePath)
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            dgvProductsExcel.Rows.Clear();

            using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read))
            using (var reader = ExcelReaderFactory.CreateReader(stream))
            {
                while (reader.Read())
                {
                    string productName = reader.GetString(0);
                    string price = reader.GetValue(1)?.ToString();

                    if (!string.IsNullOrEmpty(productName))
                    {
                        dgvProductsExcel.Rows.Add(productName, price);
                    }
                }
            }

            MessageBox.Show("Данные из Excel успешно загружены!");
        }
    }
}
