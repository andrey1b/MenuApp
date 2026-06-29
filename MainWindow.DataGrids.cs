using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using SWF   = System.Windows.Forms;
using SWFI  = System.Windows.Forms.Integration;
using SWC   = System.Windows.Controls;
using SD    = System.Drawing;

namespace MenuApp;

// Частичный класс: WinForms-хосты, DataGridView, вся бизнес-логика
public partial class MainWindow
{
    // ══════════════════════════════════════════════════ ПОЛЯ ДАННЫХ

    private SWF.DataGridView dgvMenu          = null!;
    private SWF.DataGridView dgvProducts      = null!;
    private SWF.Label        lblBudgetStatus  = null!;
    private SWF.DataGridView dgvShoppingToday    = null!;
    private SWF.DataGridView dgvShoppingTomorrow = null!;
    private SWF.Label        lblTodayTitle    = null!;
    private SWF.Label        lblTomorrowTitle = null!;
    private SWF.DataGridView dgvShoppingWeekly  = null!;
    private SWF.DataGridView dgvShoppingMonthly = null!;
    private SWF.Label        lblWeeklyTitle = null!;
    private SWF.Label        lblMonthlyTitle = null!;
    private SWF.Label        lblWeeklyInfo  = null!;
    private SWF.Label        lblMonthlyInfo = null!;
    private SWF.DataGridView dgvRealPrices   = null!;
    private SWF.Label        lblRealStatus   = null!;
    private SWF.DataGridViewComboBoxColumn colExcelName = null!;

    private List<MealDay>   mealPlan = new();
    private List<PriceItem> prices   = new();

    private Dictionary<string, Dictionary<string, Dictionary<string, decimal>>> paidData = new();

    private List<PriceMapping>  priceMappings  = new();
    private List<FoodPurchase>  excelPurchases = new();
    private List<string>        excelNames     = new();
    private Dictionary<string, RealPriceResult> realPriceData = new();

    private static string AppDir =>
        Path.GetDirectoryName(Process.GetCurrentProcess().MainModule?.FileName)
        ?? AppDomain.CurrentDomain.BaseDirectory;

    // ══════════════════════════════════════════════════ WINFORMS-ХОСТЫ

    internal void InitWinFormsHosts()
    {
        void AddHost(SWC.Grid grid, SWF.Control ctrl)
        {
            var host = new SWFI.WindowsFormsHost { Child = ctrl };
            grid.Children.Add(host);
        }

        AddHost(MenuHost,       CreateMenuTabPanel());
        AddHost(ProductsHost,   CreateProductsTabPanel());
        AddHost(ShoppingHost,   CreateShoppingTabPanel());
        AddHost(WeeklyHost,     CreateWeeklyTabPanel());
        AddHost(MonthlyHost,    CreateMonthlyTabPanel());
        AddHost(RealPricesHost,   CreateRealPricesTabPanel());
        AddHost(ShoppingListHost, CreateShoppingListPanel());
        AddHost(AiQuestionsHost,  CreateAiQuestionsPanel());
    }

    // ══════════════════════════════════════════════════ МЕНЮ

    private SWF.Panel CreateMenuTabPanel()
    {
        dgvMenu = new SWF.DataGridView
        {
            Dock = SWF.DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToResizeRows = false,
            SelectionMode = SWF.DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = SWF.DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = SD.Color.White,
            BorderStyle = SWF.BorderStyle.None,
            Font = new SD.Font("Segoe UI", 13),
            GridColor = SD.Color.FromArgb(168, 213, 169),
            ColumnHeadersDefaultCellStyle = new SWF.DataGridViewCellStyle
            {
                BackColor = SD.Color.FromArgb(44, 95, 45),
                ForeColor = SD.Color.White,
                Font = new SD.Font("Segoe UI", 13, SD.FontStyle.Bold),
                Alignment = SWF.DataGridViewContentAlignment.MiddleCenter
            },
            AlternatingRowsDefaultCellStyle = new SWF.DataGridViewCellStyle { BackColor = SD.Color.AliceBlue },
            RowTemplate = { Height = 40 }
        };
        dgvMenu.ColumnHeadersHeightSizeMode = SWF.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        dgvMenu.ColumnHeadersHeight = 42;
        dgvMenu.EnableHeadersVisualStyles = false;

        dgvMenu.Columns.Add(new SWF.DataGridViewTextBoxColumn
        { Name = "Date",     HeaderText = "День",      Width = 155, AutoSizeMode = SWF.DataGridViewAutoSizeColumnMode.None });
        dgvMenu.Columns.Add(new SWF.DataGridViewTextBoxColumn { Name = "Breakfast", HeaderText = "Завтрак",  FillWeight = 23 });
        dgvMenu.Columns.Add(new SWF.DataGridViewTextBoxColumn { Name = "Lunch",     HeaderText = "Обед",     FillWeight = 30 });
        dgvMenu.Columns.Add(new SWF.DataGridViewTextBoxColumn { Name = "Snack",     HeaderText = "Полдник",  FillWeight = 14 });
        dgvMenu.Columns.Add(new SWF.DataGridViewTextBoxColumn { Name = "Dinner",    HeaderText = "Ужин",     FillWeight = 28 });

        var calStyle = new SWF.DataGridViewCellStyle
        { Alignment = SWF.DataGridViewContentAlignment.MiddleRight, ForeColor = SD.Color.DimGray };
        dgvMenu.Columns.Add(new SWF.DataGridViewTextBoxColumn { Name = "CalBf",  HeaderText = "Ккал завтрак", Width = 88, AutoSizeMode = SWF.DataGridViewAutoSizeColumnMode.None, ReadOnly = true, DefaultCellStyle = calStyle });
        dgvMenu.Columns.Add(new SWF.DataGridViewTextBoxColumn { Name = "CalLn",  HeaderText = "Ккал обед",    Width = 80, AutoSizeMode = SWF.DataGridViewAutoSizeColumnMode.None, ReadOnly = true, DefaultCellStyle = calStyle });
        dgvMenu.Columns.Add(new SWF.DataGridViewTextBoxColumn { Name = "CalSn",  HeaderText = "Ккал полдник", Width = 80, AutoSizeMode = SWF.DataGridViewAutoSizeColumnMode.None, ReadOnly = true, DefaultCellStyle = calStyle });
        dgvMenu.Columns.Add(new SWF.DataGridViewTextBoxColumn { Name = "CalDn",  HeaderText = "Ккал ужин",    Width = 80, AutoSizeMode = SWF.DataGridViewAutoSizeColumnMode.None, ReadOnly = true, DefaultCellStyle = calStyle });
        dgvMenu.Columns.Add(new SWF.DataGridViewTextBoxColumn { Name = "CalDay", HeaderText = "Ккал/день", Width = 82, AutoSizeMode = SWF.DataGridViewAutoSizeColumnMode.None, ReadOnly = true,
            DefaultCellStyle = new SWF.DataGridViewCellStyle { Alignment = SWF.DataGridViewContentAlignment.MiddleRight, Font = new SD.Font("Segoe UI", 13, SD.FontStyle.Bold) } });
        dgvMenu.Columns.Add(new SWF.DataGridViewTextBoxColumn { Name = "CalNorm", HeaderText = "Норма ккал", Width = 85, AutoSizeMode = SWF.DataGridViewAutoSizeColumnMode.None, ReadOnly = true,
            DefaultCellStyle = new SWF.DataGridViewCellStyle { Alignment = SWF.DataGridViewContentAlignment.MiddleRight, ForeColor = SD.Color.FromArgb(44, 95, 45) } });
        dgvMenu.Columns.Add(new SWF.DataGridViewTextBoxColumn { Name = "DayCost", HeaderText = "~Стоим. грн", Width = 90, AutoSizeMode = SWF.DataGridViewAutoSizeColumnMode.None, ReadOnly = true,
            DefaultCellStyle = new SWF.DataGridViewCellStyle { Alignment = SWF.DataGridViewContentAlignment.MiddleRight, ForeColor = SD.Color.DarkGreen } });

        foreach (SWF.DataGridViewColumn col in dgvMenu.Columns) col.SortMode = SWF.DataGridViewColumnSortMode.NotSortable;
        dgvMenu.DefaultCellStyle.WrapMode = SWF.DataGridViewTriState.True;
        dgvMenu.AutoSizeRowsMode = SWF.DataGridViewAutoSizeRowsMode.AllCells;

        var panel = new SWF.Panel { Dock = SWF.DockStyle.Fill };
        panel.Controls.Add(dgvMenu);
        return panel;
    }

    // ══════════════════════════════════════════════════ ПРОДУКТЫ

    private SWF.Panel CreateProductsTabPanel()
    {
        dgvProducts = new SWF.DataGridView
        {
            Dock = SWF.DockStyle.Fill,
            AllowUserToAddRows = true,
            AllowUserToDeleteRows = true,
            AllowUserToResizeRows = false,
            SelectionMode = SWF.DataGridViewSelectionMode.FullRowSelect,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = SWF.DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = SD.Color.White,
            BorderStyle = SWF.BorderStyle.None,
            Font = new SD.Font("Segoe UI", 13),
            GridColor = SD.Color.FromArgb(168, 213, 169),
            ColumnHeadersDefaultCellStyle = new SWF.DataGridViewCellStyle
            {
                BackColor = SD.Color.FromArgb(44, 95, 45), ForeColor = SD.Color.White,
                Font = new SD.Font("Segoe UI", 13, SD.FontStyle.Bold),
                Alignment = SWF.DataGridViewContentAlignment.MiddleCenter
            },
            AlternatingRowsDefaultCellStyle = new SWF.DataGridViewCellStyle { BackColor = SD.Color.AliceBlue },
            RowTemplate = { Height = 38 }
        };
        dgvProducts.ColumnHeadersHeightSizeMode = SWF.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        dgvProducts.ColumnHeadersHeight = 42;
        dgvProducts.EnableHeadersVisualStyles = false;

        // Все столбцы используют FillWeight — растягиваются пропорционально ширине окна
        dgvProducts.Columns.Add(new SWF.DataGridViewTextBoxColumn { Name = "ProductName", HeaderText = "Продукт",      FillWeight = 26 });
        dgvProducts.Columns.Add(new SWF.DataGridViewTextBoxColumn { Name = "Tier",        HeaderText = "Уровень",      FillWeight = 7,  ReadOnly = true,
            DefaultCellStyle = new SWF.DataGridViewCellStyle { Alignment = SWF.DataGridViewContentAlignment.MiddleCenter, Font = new SD.Font("Segoe UI", 12) } });

        var colFreq = new SWF.DataGridViewComboBoxColumn
        {
            Name = "Frequency", HeaderText = "Частота", FillWeight = 12,
            FlatStyle = SWF.FlatStyle.Flat,
            DefaultCellStyle = new SWF.DataGridViewCellStyle { Alignment = SWF.DataGridViewContentAlignment.MiddleCenter, Font = new SD.Font("Segoe UI", 12) }
        };
        colFreq.Items.AddRange("ежедневно", "еженедельно", "ежемесячно");
        dgvProducts.Columns.Add(colFreq);

        dgvProducts.Columns.Add(new SWF.DataGridViewTextBoxColumn { Name = "Unit",      HeaderText = "Ед.",        FillWeight = 5 });
        dgvProducts.Columns.Add(new SWF.DataGridViewTextBoxColumn { Name = "Price",     HeaderText = "Цена (грн)", FillWeight = 9 });
        dgvProducts.Columns.Add(new SWF.DataGridViewTextBoxColumn { Name = "RealPrice", HeaderText = "Реал. цена", FillWeight = 9,  ReadOnly = true,
            DefaultCellStyle = new SWF.DataGridViewCellStyle { Alignment = SWF.DataGridViewContentAlignment.MiddleRight, Font = new SD.Font("Segoe UI", 12) } });
        dgvProducts.Columns.Add(new SWF.DataGridViewTextBoxColumn { Name = "Qty",       HeaderText = "Кол-во",     FillWeight = 7 });
        dgvProducts.Columns.Add(new SWF.DataGridViewTextBoxColumn { Name = "PackInfo",  HeaderText = "Упаковок",   FillWeight = 11, ReadOnly = true,
            DefaultCellStyle = new SWF.DataGridViewCellStyle { ForeColor = SD.Color.MidnightBlue, Alignment = SWF.DataGridViewContentAlignment.MiddleCenter, Font = new SD.Font("Segoe UI", 12) } });
        dgvProducts.Columns.Add(new SWF.DataGridViewTextBoxColumn { Name = "Sum",       HeaderText = "Сумма (грн)", FillWeight = 10, ReadOnly = true,
            DefaultCellStyle = new SWF.DataGridViewCellStyle { ForeColor = SD.Color.DarkGreen, Alignment = SWF.DataGridViewContentAlignment.MiddleRight } });
        dgvProducts.Columns.Add(new SWF.DataGridViewTextBoxColumn { Name = "Kcal",      HeaderText = "Ккал/пер.",  FillWeight = 9,  ReadOnly = true,
            DefaultCellStyle = new SWF.DataGridViewCellStyle { ForeColor = SD.Color.DarkBlue, Alignment = SWF.DataGridViewContentAlignment.MiddleRight } });

        if (dgvProducts.Columns["Price"] != null) dgvProducts.Columns["Price"]!.DefaultCellStyle.Alignment = SWF.DataGridViewContentAlignment.MiddleRight;
        if (dgvProducts.Columns["Qty"]   != null) dgvProducts.Columns["Qty"]!.DefaultCellStyle.Alignment   = SWF.DataGridViewContentAlignment.MiddleRight;
        foreach (SWF.DataGridViewColumn col in dgvProducts.Columns) col.SortMode = SWF.DataGridViewColumnSortMode.NotSortable;

        dgvProducts.CellValueChanged  += DgvProducts_CellValueChanged;
        dgvProducts.RowsRemoved       += (_, _) => UpdateProductsTotal();
        dgvProducts.UserDeletingRow   += (s, e) => { if (e.Row?.Tag?.ToString() == "total") e.Cancel = true; };
        dgvProducts.CurrentCellDirtyStateChanged += (s, e) =>
        {
            if (dgvProducts.IsCurrentCellDirty) dgvProducts.CommitEdit(SWF.DataGridViewDataErrorContexts.Commit);
        };
        dgvProducts.DataError += (s, e) => e.Cancel = true;

        lblBudgetStatus = new SWF.Label
        {
            Dock = SWF.DockStyle.Bottom, Height = 34,
            TextAlign = System.Drawing.ContentAlignment.MiddleRight,
            Font = new SD.Font("Segoe UI", 11, SD.FontStyle.Bold),
            Padding = new System.Windows.Forms.Padding(0, 0, 12, 0),
            BackColor = SD.Color.White,
            BorderStyle = SWF.BorderStyle.FixedSingle
        };

        var panel = new SWF.Panel { Dock = SWF.DockStyle.Fill };
        panel.Controls.Add(dgvProducts);
        panel.Controls.Add(lblBudgetStatus);
        return panel;
    }

    // ══════════════════════════════════════════════════ ПОКУПКИ

    private SWF.Panel CreateShoppingTabPanel()
    {
        var table = new SWF.TableLayoutPanel
        {
            Dock = SWF.DockStyle.Fill,
            ColumnCount = 2, RowCount = 1,
            BackColor = SD.Color.WhiteSmoke
        };
        table.ColumnStyles.Add(new SWF.ColumnStyle(SWF.SizeType.Percent, 50f));
        table.ColumnStyles.Add(new SWF.ColumnStyle(SWF.SizeType.Percent, 50f));
        table.RowStyles.Add(new SWF.RowStyle(SWF.SizeType.Percent, 100f));

        var (panelToday,    gridToday,    titleToday)    = BuildShoppingPanel(SD.Color.FromArgb(255, 251, 214));
        var (panelTomorrow, gridTomorrow, titleTomorrow) = BuildShoppingPanel(SD.Color.FromArgb(214, 241, 214));

        dgvShoppingToday    = gridToday;
        dgvShoppingTomorrow = gridTomorrow;
        lblTodayTitle       = titleToday;
        lblTomorrowTitle    = titleTomorrow;

        table.Controls.Add(panelToday,    0, 0);
        table.Controls.Add(panelTomorrow, 1, 0);

        var panel = new SWF.Panel { Dock = SWF.DockStyle.Fill };
        panel.Controls.Add(table);
        return panel;
    }

    private (SWF.Panel panel, SWF.DataGridView dgv, SWF.Label title) BuildShoppingPanel(Color titleColor)
    {
        var title = new SWF.Label
        {
            Dock = SWF.DockStyle.Top, Height = 52,
            TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
            Font = new SD.Font("Segoe UI", 13, SD.FontStyle.Bold),
            BackColor = titleColor,
            ForeColor = SD.Color.DarkSlateGray
        };

        var dgv = new SWF.DataGridView
        {
            Dock = SWF.DockStyle.Fill,
            AllowUserToAddRows = false,
            SelectionMode = SWF.DataGridViewSelectionMode.FullRowSelect,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = SWF.DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = SD.Color.White,
            BorderStyle = SWF.BorderStyle.None,
            Font = new SD.Font("Segoe UI", 13),
            GridColor = SD.Color.FromArgb(168, 213, 169),
            ColumnHeadersDefaultCellStyle = new SWF.DataGridViewCellStyle
            {
                BackColor = SD.Color.FromArgb(62, 135, 65), ForeColor = SD.Color.White,
                Font = new SD.Font("Segoe UI", 13, SD.FontStyle.Bold),
                Alignment = SWF.DataGridViewContentAlignment.MiddleCenter
            },
            AlternatingRowsDefaultCellStyle = new SWF.DataGridViewCellStyle { BackColor = SD.Color.FromArgb(240, 248, 240) },
            RowTemplate = { Height = 38 }
        };
        dgv.ColumnHeadersHeightSizeMode = SWF.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        dgv.ColumnHeadersHeight = 42;
        dgv.EnableHeadersVisualStyles = false;

        dgv.Columns.Add(new SWF.DataGridViewCheckBoxColumn { Name = "Done", HeaderText = "✓", Width = 40, AutoSizeMode = SWF.DataGridViewAutoSizeColumnMode.None });
        dgv.Columns.Add(new SWF.DataGridViewTextBoxColumn  { Name = "Product",  HeaderText = "Продукт",   FillWeight = 50, ReadOnly = true });
        dgv.Columns.Add(new SWF.DataGridViewTextBoxColumn  { Name = "Quantity", HeaderText = "Количество", FillWeight = 28, ReadOnly = true });
        dgv.Columns.Add(new SWF.DataGridViewTextBoxColumn  { Name = "Price",    HeaderText = "~Цена (грн)", FillWeight = 22, ReadOnly = true,
            DefaultCellStyle = new SWF.DataGridViewCellStyle { Alignment = SWF.DataGridViewContentAlignment.MiddleRight } });
        dgv.Columns.Add(new SWF.DataGridViewTextBoxColumn  { Name = "Paid",     HeaderText = "Заплачено", FillWeight = 22, ReadOnly = false,
            DefaultCellStyle = new SWF.DataGridViewCellStyle { Alignment = SWF.DataGridViewContentAlignment.MiddleRight, BackColor = SD.Color.FromArgb(255, 255, 230) } });

        foreach (SWF.DataGridViewColumn col in dgv.Columns) col.SortMode = SWF.DataGridViewColumnSortMode.NotSortable;

        dgv.CurrentCellDirtyStateChanged += (s, e) =>
        {
            if (dgv.IsCurrentCellDirty && dgv.CurrentCell is SWF.DataGridViewCheckBoxCell)
                dgv.CommitEdit(SWF.DataGridViewDataErrorContexts.Commit);
        };
        dgv.CellValueChanged += (s, e) =>
        {
            if (e.RowIndex < 0) return;
            int doneIdx = dgv.Columns["Done"]?.Index ?? -1;
            int paidIdx = dgv.Columns["Paid"]?.Index ?? -1;
            if (e.ColumnIndex == doneIdx)
            {
                bool done  = dgv.Rows[e.RowIndex].Cells["Done"].Value is true;
                var style  = dgv.Rows[e.RowIndex].DefaultCellStyle;
                style.ForeColor = done ? SD.Color.Gray : SD.Color.Empty;
                style.Font      = done ? new SD.Font("Segoe UI", 13, SD.FontStyle.Strikeout) : null;
                dgv.InvalidateRow(e.RowIndex);
            }
            else if (e.ColumnIndex == paidIdx) UpdateShoppingPaidTotal(dgv);
        };

        var btnCopy = new SWF.Button
        {
            Text = "📋", Width = 30, Height = 30,
            Anchor = SWF.AnchorStyles.Top | SWF.AnchorStyles.Right,
            FlatStyle = SWF.FlatStyle.Flat, BackColor = SD.Color.Transparent,
            ForeColor = SD.Color.DarkSlateGray, Font = new SD.Font("Segoe UI", 12),
            Cursor = SWF.Cursors.Hand
        };
        btnCopy.FlatAppearance.BorderSize = 0;
        btnCopy.Click += (_, _) => CopyShoppingListToClipboard(dgv, title.Text);
        title.Controls.Add(btnCopy);
        title.Resize += (_, _) => btnCopy.Location = new System.Drawing.Point(title.Width - 34, 2);

        var panel = new SWF.Panel { Dock = SWF.DockStyle.Fill, Padding = new SWF.Padding(4) };
        panel.Controls.Add(dgv);
        panel.Controls.Add(title);
        return (panel, dgv, title);
    }

    // ══════════════════════════════════════════════════ ПОКУПКИ НА НЕДЕЛЮ

    private SWF.Panel CreateWeeklyTabPanel()
    {
        lblWeeklyTitle = new SWF.Label
        {
            Dock = SWF.DockStyle.Top, Height = 50,
            TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
            Font = new SD.Font("Segoe UI", 13, SD.FontStyle.Bold),
            BackColor = SD.Color.FromArgb(200, 228, 200),
            ForeColor = SD.Color.DarkSlateGray
        };
        lblWeeklyInfo = new SWF.Label
        {
            Dock = SWF.DockStyle.Bottom, Height = 42,
            TextAlign = System.Drawing.ContentAlignment.MiddleRight,
            Font = new SD.Font("Segoe UI", 13, SD.FontStyle.Bold),
            Padding = new SWF.Padding(0, 0, 12, 0),
            BackColor = SD.Color.FromArgb(210, 236, 210),
            BorderStyle = SWF.BorderStyle.FixedSingle
        };

        dgvShoppingWeekly = BuildPeriodicShoppingGrid();

        var btnCopy = new SWF.Button
        {
            Text = "📋", Width = 30, Height = 30, Anchor = SWF.AnchorStyles.Top | SWF.AnchorStyles.Right,
            FlatStyle = SWF.FlatStyle.Flat, BackColor = SD.Color.Transparent, ForeColor = SD.Color.DarkSlateGray,
            Font = new SD.Font("Segoe UI", 12), Cursor = SWF.Cursors.Hand
        };
        btnCopy.FlatAppearance.BorderSize = 0;
        btnCopy.Click += (_, _) => CopyShoppingListToClipboard(dgvShoppingWeekly, lblWeeklyTitle.Text);
        lblWeeklyTitle.Controls.Add(btnCopy);
        lblWeeklyTitle.Resize += (_, _) => btnCopy.Location = new System.Drawing.Point(lblWeeklyTitle.Width - 34, 3);

        var panel = new SWF.Panel { Dock = SWF.DockStyle.Fill };
        panel.Controls.Add(dgvShoppingWeekly);
        panel.Controls.Add(lblWeeklyTitle);
        panel.Controls.Add(lblWeeklyInfo);
        return panel;
    }

    // ══════════════════════════════════════════════════ ПОКУПКИ НА МЕСЯЦ

    private SWF.Panel CreateMonthlyTabPanel()
    {
        lblMonthlyTitle = new SWF.Label
        {
            Dock = SWF.DockStyle.Top, Height = 50,
            TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
            Font = new SD.Font("Segoe UI", 13, SD.FontStyle.Bold),
            BackColor = SD.Color.FromArgb(214, 245, 225),
            ForeColor = SD.Color.DarkSlateGray
        };
        lblMonthlyInfo = new SWF.Label
        {
            Dock = SWF.DockStyle.Bottom, Height = 42,
            TextAlign = System.Drawing.ContentAlignment.MiddleRight,
            Font = new SD.Font("Segoe UI", 13, SD.FontStyle.Bold),
            Padding = new SWF.Padding(0, 0, 12, 0),
            BackColor = SD.Color.FromArgb(220, 255, 235),
            BorderStyle = SWF.BorderStyle.FixedSingle
        };

        dgvShoppingMonthly = BuildPeriodicShoppingGrid();

        var btnCopy = new SWF.Button
        {
            Text = "📋", Width = 30, Height = 30, Anchor = SWF.AnchorStyles.Top | SWF.AnchorStyles.Right,
            FlatStyle = SWF.FlatStyle.Flat, BackColor = SD.Color.Transparent, ForeColor = SD.Color.DarkSlateGray,
            Font = new SD.Font("Segoe UI", 12), Cursor = SWF.Cursors.Hand
        };
        btnCopy.FlatAppearance.BorderSize = 0;
        btnCopy.Click += (_, _) => CopyShoppingListToClipboard(dgvShoppingMonthly, lblMonthlyTitle.Text);
        lblMonthlyTitle.Controls.Add(btnCopy);
        lblMonthlyTitle.Resize += (_, _) => btnCopy.Location = new System.Drawing.Point(lblMonthlyTitle.Width - 34, 3);

        var panel = new SWF.Panel { Dock = SWF.DockStyle.Fill };
        panel.Controls.Add(dgvShoppingMonthly);
        panel.Controls.Add(lblMonthlyTitle);
        panel.Controls.Add(lblMonthlyInfo);
        return panel;
    }

    private SWF.DataGridView BuildPeriodicShoppingGrid()
    {
        var dgv = new SWF.DataGridView
        {
            Dock = SWF.DockStyle.Fill,
            AllowUserToAddRows = false,
            SelectionMode = SWF.DataGridViewSelectionMode.FullRowSelect,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = SWF.DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = SD.Color.White,
            BorderStyle = SWF.BorderStyle.None,
            Font = new SD.Font("Segoe UI", 13),
            GridColor = SD.Color.FromArgb(168, 213, 169),
            ColumnHeadersDefaultCellStyle = new SWF.DataGridViewCellStyle
            {
                BackColor = SD.Color.FromArgb(62, 135, 65), ForeColor = SD.Color.White,
                Font = new SD.Font("Segoe UI", 13, SD.FontStyle.Bold),
                Alignment = SWF.DataGridViewContentAlignment.MiddleCenter
            },
            AlternatingRowsDefaultCellStyle = new SWF.DataGridViewCellStyle { BackColor = SD.Color.FromArgb(240, 248, 240) },
            RowTemplate = { Height = 40 }
        };
        dgv.ColumnHeadersHeightSizeMode = SWF.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        dgv.ColumnHeadersHeight = 42;
        dgv.EnableHeadersVisualStyles = false;

        dgv.Columns.Add(new SWF.DataGridViewCheckBoxColumn { Name = "Done", HeaderText = "✓", Width = 40, AutoSizeMode = SWF.DataGridViewAutoSizeColumnMode.None });
        dgv.Columns.Add(new SWF.DataGridViewTextBoxColumn { Name = "Product",  HeaderText = "Продукт",    FillWeight = 40, ReadOnly = true });
        dgv.Columns.Add(new SWF.DataGridViewTextBoxColumn { Name = "Quantity", HeaderText = "Количество",  FillWeight = 28, ReadOnly = true,
            DefaultCellStyle = new SWF.DataGridViewCellStyle { Alignment = SWF.DataGridViewContentAlignment.MiddleCenter } });
        dgv.Columns.Add(new SWF.DataGridViewTextBoxColumn { Name = "Price", HeaderText = "~Цена (грн)", FillWeight = 20, ReadOnly = true,
            DefaultCellStyle = new SWF.DataGridViewCellStyle { Alignment = SWF.DataGridViewContentAlignment.MiddleRight } });
        dgv.Columns.Add(new SWF.DataGridViewTextBoxColumn { Name = "Paid", HeaderText = "Заплачено", FillWeight = 20, ReadOnly = false,
            DefaultCellStyle = new SWF.DataGridViewCellStyle { Alignment = SWF.DataGridViewContentAlignment.MiddleRight, BackColor = SD.Color.FromArgb(255, 255, 230) } });

        foreach (SWF.DataGridViewColumn col in dgv.Columns) col.SortMode = SWF.DataGridViewColumnSortMode.NotSortable;

        dgv.CurrentCellDirtyStateChanged += (s, e) =>
        {
            if (dgv.IsCurrentCellDirty && dgv.CurrentCell is SWF.DataGridViewCheckBoxCell)
                dgv.CommitEdit(SWF.DataGridViewDataErrorContexts.Commit);
        };
        dgv.CellValueChanged += (s, e) =>
        {
            if (e.RowIndex < 0) return;
            int doneIdx = dgv.Columns["Done"]?.Index ?? -1;
            int paidIdx = dgv.Columns["Paid"]?.Index ?? -1;
            if (e.ColumnIndex == doneIdx)
            {
                bool done = dgv.Rows[e.RowIndex].Cells["Done"].Value is true;
                var style = dgv.Rows[e.RowIndex].DefaultCellStyle;
                style.ForeColor = done ? SD.Color.Gray : SD.Color.Empty;
                style.Font      = done ? new SD.Font("Segoe UI", 13, SD.FontStyle.Strikeout) : null;
                dgv.InvalidateRow(e.RowIndex);
            }
            else if (e.ColumnIndex == paidIdx) UpdateShoppingPaidTotal(dgv);
        };

        return dgv;
    }

    // ══════════════════════════════════════════════════ РЕАЛЬНЫЕ ЦЕНЫ

    private SWF.Panel CreateRealPricesTabPanel()
    {
        var toolbar = new SWF.Panel
        {
            Dock = SWF.DockStyle.Top, Height = 42,
            BackColor = SD.Color.FromArgb(228, 244, 228),
            Padding = new SWF.Padding(6, 6, 6, 0)
        };

        var lblFile = new SWF.Label
        {
            Text = "Файл расходов: " + ExcelPriceService.ExcelFilePath,
            AutoSize = true, Location = new System.Drawing.Point(8, 12),
            Font = new SD.Font("Segoe UI", 12), ForeColor = SD.Color.DimGray
        };

        var btnRefresh = new SWF.Button
        {
            Text = "⟳ Обновить из файла",
            Location = new System.Drawing.Point(toolbar.Width - 180, 6),
            Width = 168, Height = 30,
            Anchor = SWF.AnchorStyles.Top | SWF.AnchorStyles.Right,
            BackColor = SD.Color.FromArgb(62, 135, 65), ForeColor = SD.Color.White,
            FlatStyle = SWF.FlatStyle.Flat,
            Font = new SD.Font("Segoe UI", 13, SD.FontStyle.Bold)
        };
        btnRefresh.FlatAppearance.BorderSize = 0;
        btnRefresh.Click += (_, _) => RefreshFromExcel();
        toolbar.Controls.AddRange(new SWF.Control[] { lblFile, btnRefresh });

        lblRealStatus = new SWF.Label
        {
            Dock = SWF.DockStyle.Bottom, Height = 40,
            TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
            Font = new SD.Font("Segoe UI", 12),
            Padding = new SWF.Padding(8, 0, 0, 0),
            BackColor = SD.Color.FromArgb(228, 242, 228),
            BorderStyle = SWF.BorderStyle.FixedSingle,
            ForeColor = SD.Color.DimGray
        };

        dgvRealPrices = new SWF.DataGridView
        {
            Dock = SWF.DockStyle.Fill,
            AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false,
            SelectionMode = SWF.DataGridViewSelectionMode.FullRowSelect,
            RowHeadersVisible = false,
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
            RowTemplate = { Height = 38 }
        };
        dgvRealPrices.ColumnHeadersHeightSizeMode = SWF.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        dgvRealPrices.ColumnHeadersHeight = 42;
        dgvRealPrices.EnableHeadersVisualStyles = false;

        // Все столбцы — FillWeight, растягиваются по ширине окна
        dgvRealPrices.Columns.Add(new SWF.DataGridViewTextBoxColumn { Name = "RpApp",  HeaderText = "Наш продукт",         FillWeight = 20, ReadOnly = true });
        dgvRealPrices.Columns.Add(new SWF.DataGridViewTextBoxColumn { Name = "RpUnit", HeaderText = "Ед.",                 FillWeight = 4,  ReadOnly = true,
            DefaultCellStyle = new SWF.DataGridViewCellStyle { Alignment = SWF.DataGridViewContentAlignment.MiddleCenter } });

        colExcelName = new SWF.DataGridViewComboBoxColumn
        {
            Name = "RpExcel", HeaderText = "Название в расходах", FillWeight = 20,
            FlatStyle = SWF.FlatStyle.Flat,
            DefaultCellStyle = new SWF.DataGridViewCellStyle { Alignment = SWF.DataGridViewContentAlignment.MiddleLeft }
        };
        colExcelName.Items.Add("");
        dgvRealPrices.Columns.Add(colExcelName);

        dgvRealPrices.Columns.Add(new SWF.DataGridViewTextBoxColumn { Name = "RpExUnit", HeaderText = "Ед. Excel", FillWeight = 5, ReadOnly = true,
            DefaultCellStyle = new SWF.DataGridViewCellStyle { Alignment = SWF.DataGridViewContentAlignment.MiddleCenter, ForeColor = SD.Color.DimGray } });
        dgvRealPrices.Columns.Add(new SWF.DataGridViewTextBoxColumn { Name = "RpMult",   HeaderText = "Коэф.",      FillWeight = 5,
            DefaultCellStyle = new SWF.DataGridViewCellStyle { Alignment = SWF.DataGridViewContentAlignment.MiddleCenter } });
        dgvRealPrices.Columns.Add(new SWF.DataGridViewTextBoxColumn { Name = "RpLast",   HeaderText = "Посл. цена", FillWeight = 10, ReadOnly = true,
            DefaultCellStyle = new SWF.DataGridViewCellStyle { Alignment = SWF.DataGridViewContentAlignment.MiddleRight, Font = new SD.Font("Segoe UI", 13, SD.FontStyle.Bold), BackColor = SD.Color.FromArgb(240, 255, 240) } });
        dgvRealPrices.Columns.Add(new SWF.DataGridViewTextBoxColumn { Name = "RpAvg30",  HeaderText = "Ср. 30 дн.", FillWeight = 10, ReadOnly = true,
            DefaultCellStyle = new SWF.DataGridViewCellStyle { Alignment = SWF.DataGridViewContentAlignment.MiddleRight, Font = new SD.Font("Segoe UI", 13, SD.FontStyle.Bold) } });
        dgvRealPrices.Columns.Add(new SWF.DataGridViewTextBoxColumn { Name = "RpAvg90",  HeaderText = "Ср. 90 дн.", FillWeight = 10, ReadOnly = true,
            DefaultCellStyle = new SWF.DataGridViewCellStyle { Alignment = SWF.DataGridViewContentAlignment.MiddleRight, ForeColor = SD.Color.DimGray } });
        dgvRealPrices.Columns.Add(new SWF.DataGridViewTextBoxColumn { Name = "RpOur",    HeaderText = "Наша цена",  FillWeight = 9,  ReadOnly = true,
            DefaultCellStyle = new SWF.DataGridViewCellStyle { Alignment = SWF.DataGridViewContentAlignment.MiddleRight, ForeColor = SD.Color.DimGray } });
        dgvRealPrices.Columns.Add(new SWF.DataGridViewTextBoxColumn { Name = "RpDiff",   HeaderText = "Разница",    FillWeight = 7,  ReadOnly = true,
            DefaultCellStyle = new SWF.DataGridViewCellStyle { Alignment = SWF.DataGridViewContentAlignment.MiddleCenter, Font = new SD.Font("Segoe UI", 12, SD.FontStyle.Bold) } });

        foreach (SWF.DataGridViewColumn col in dgvRealPrices.Columns) col.SortMode = SWF.DataGridViewColumnSortMode.NotSortable;
        dgvRealPrices.DataError += (s, e) => e.Cancel = true;
        dgvRealPrices.CurrentCellDirtyStateChanged += (s, e) =>
        {
            if (dgvRealPrices.IsCurrentCellDirty) dgvRealPrices.CommitEdit(SWF.DataGridViewDataErrorContexts.Commit);
        };
        dgvRealPrices.CellValueChanged += DgvRealPrices_CellValueChanged;

        var panel = new SWF.Panel { Dock = SWF.DockStyle.Fill };
        panel.Controls.Add(dgvRealPrices);
        panel.Controls.Add(toolbar);
        panel.Controls.Add(lblRealStatus);
        return panel;
    }

    // ══════════════════════════════════════════════════ ЗАГРУЗКА ДАННЫХ

    internal void LoadData()
    {
        LoadMealPlan();
        LoadPrices();
        LoadRealPrices();
        LoadPaidData();
        FillDashboardTab();
        FillMenuTab();
        FillProductsTab();
        FillShoppingTab();
        FillWeeklyShoppingTab();
        FillMonthlyShoppingTab();
        FillRealPricesTab();
    }

    private void LoadMealPlan()
    {
        mealPlan.Clear();
        string? path = FindDataFile("30_day_meal_plan.txt");
        if (path == null) return;
        foreach (string line in File.ReadAllLines(path, System.Text.Encoding.UTF8))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            int dash = line.IndexOf('—');
            if (dash < 0) continue;
            string dateStr = line[..dash].Trim().TrimEnd('.').TrimEnd('г').TrimEnd().TrimEnd('.');
            string meals   = line[(dash + 1)..].Trim();
            mealPlan.Add(new MealDay(dateStr.Trim(), ExtractMeal(meals, "Завтрак"), ExtractMeal(meals, "Обед"), ExtractMeal(meals, "Полдник"), ExtractMeal(meals, "Ужин")));
        }
    }

    private static string ExtractMeal(string text, string label)
    {
        int i = text.IndexOf(label + ":", StringComparison.OrdinalIgnoreCase);
        if (i < 0) return "";
        i += label.Length + 1;
        int end = text.IndexOf(';', i);
        return (end > 0 ? text[i..end] : text[i..]).Trim();
    }

    private void LoadPrices()
    {
        prices.Clear();
        string? path = FindDataFile("средними ценами.json");
        if (path == null) return;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path, System.Text.Encoding.UTF8));
            foreach (var e in doc.RootElement.GetProperty("prices").EnumerateArray())
                prices.Add(new PriceItem(
                    e.GetProperty("name").GetString()  ?? "",
                    e.GetProperty("price").GetDecimal(),
                    e.GetProperty("unit").GetString()  ?? "",
                    e.TryGetProperty("frequency", out var freq) ? freq.GetString() ?? "еженедельно" : "еженедельно"));
        }
        catch { }
    }

    // ══════════════════════════════════════════════════ ЗАПОЛНЕНИЕ ВКЛАДОК

    internal void FillDashboardTab()
    {
        var culture = new CultureInfo("ru-RU");
        DateTime today = DateTime.Today;
        MealDay? meal  = FindMealForDate(today);
        string[] meals = meal != null ? new[] { meal.Breakfast, meal.Lunch, meal.Snack, meal.Dinner } : new[] { "", "", "", "" };

        int totalCal = 0; decimal totalCost = 0;
        int pDays    = Math.Max(1, (int)(periodEnd - periodStart).TotalDays + 1);

        for (int i = 0; i < 4; i++)
        {
            string text = meals[i];
            _txMeal[i].Text = string.IsNullOrEmpty(text) ? "—" : text;
            if (!string.IsNullOrEmpty(text))
            {
                int cal = CalcMealCalories(text);
                decimal cost = 0;
                foreach (var (name, grams) in GetIngredients(text))
                    cost += EstimatePrice(name, grams * familyCount);
                cost = Math.Round(cost, 0);
                _txCal[i].Text  = cal  > 0 ? $"{cal} кКал"    : "";
                _txCost[i].Text = cost > 0 ? $"~{cost:F0} грн" : "";
                totalCal += cal; totalCost += cost;
            }
            else { _txCal[i].Text = ""; _txCost[i].Text = ""; }
            // Полдник (индекс 2) всегда активен — ведёт к сайту о полднике
            _btnRecipe[i].IsEnabled = !string.IsNullOrEmpty(text) || i == 2;
        }

        string dateStr = today.ToString("ddd, d MMM", culture);
        TxDayDate.Text = meal != null
            ? $"{dateStr}  |  ~{totalCost:F0} грн  |  {totalCal} ккал"
            : $"{dateStr}  |  вне плана";
    }

    private void FillMenuTab()
    {
        dgvMenu.Rows.Clear();
        if (mealPlan.Count == 0) return;

        int normPerPerson = calorieNorm;
        var culture = new CultureInfo("ru-RU");
        decimal budget = PeriodBudget;

        var days = new List<(DateTime d, MealDay meal, int bf, int ln, int sn, int dn, int tot, decimal raw)>();
        for (DateTime d = periodStart; d <= periodEnd; d = d.AddDays(1))
        {
            MealDay? meal = FindMealForDate(d);
            if (meal == null) continue;
            int bf = CalcMealCalories(meal.Breakfast);
            int ln = CalcMealCalories(meal.Lunch);
            int sn = CalcMealCalories(meal.Snack);
            int dn = CalcMealCalories(meal.Dinner);
            decimal raw = 0;
            foreach (string t in new[] { meal.Breakfast, meal.Lunch, meal.Snack, meal.Dinner })
                foreach (var (name, grams) in GetIngredients(t))
                    raw += EstimatePrice(name, grams * familyCount);
            days.Add((d, meal, bf, ln, sn, dn, bf + ln + sn + dn, raw));
        }
        if (days.Count == 0) return;

        decimal perDay   = budget / days.Count;
        decimal totalRaw = days.Sum(x => x.raw);
        long rawCalSum   = days.Sum(x => (long)x.tot);

        var scaledCost = new decimal[days.Count];
        if (totalRaw > 0)
        {
            int zeros     = days.Count(x => x.raw == 0);
            decimal pool  = budget - zeros * perDay;
            decimal scale = pool > 0 ? pool / totalRaw : 1m;
            for (int i = 0; i < days.Count; i++)
                scaledCost[i] = days[i].raw > 0 ? Math.Round(days[i].raw * scale, 0) : Math.Round(perDay, 0);
        }
        else { for (int i = 0; i < days.Count; i++) scaledCost[i] = Math.Round(perDay, 0); }
        scaledCost[^1] += budget - scaledCost.Sum();

        long targetKcal = ComputeProductsTotalKcal() / Math.Max(1, familyCount);
        var scaledKcal  = new int[days.Count];
        if (rawCalSum > 0)
        {
            int calZeros   = days.Count(x => x.tot == 0);
            long perDayCal = targetKcal / days.Count;
            long calPool   = targetKcal - calZeros * perDayCal;
            decimal calSc  = calPool > 0 ? (decimal)calPool / rawCalSum : 1m;
            for (int i = 0; i < days.Count; i++)
                scaledKcal[i] = days[i].tot > 0 ? (int)Math.Round(days[i].tot * calSc) : (int)perDayCal;
        }
        else { long pdc = targetKcal / days.Count; for (int i = 0; i < days.Count; i++) scaledKcal[i] = (int)pdc; }
        scaledKcal[^1] += (int)(targetKcal - scaledKcal.Sum());

        long totalCal = 0; decimal totalCost = 0;
        for (int i = 0; i < days.Count; i++)
        {
            var (d, meal, bf, ln, sn, dn, _, _) = days[i];
            decimal dayCost = scaledCost[i]; int dayCal = scaledKcal[i];
            int rowIdx = dgvMenu.Rows.Add(
                d.ToString("d MMMM (ddd)", culture), meal.Breakfast, meal.Lunch, meal.Snack, meal.Dinner,
                bf > 0 ? bf.ToString() : "", ln > 0 ? ln.ToString() : "", sn > 0 ? sn.ToString() : "", dn > 0 ? dn.ToString() : "",
                dayCal > 0 ? dayCal.ToString() : "", normPerPerson.ToString(), $"~{dayCost:F0}");
            if (dayCal > 0)
                dgvMenu.Rows[rowIdx].Cells["CalDay"].Style.ForeColor =
                    dayCal >= normPerPerson ? SD.Color.DarkGreen : dayCal >= 1500 ? SD.Color.DarkOrange : SD.Color.Crimson;
            var rowRef = dgvMenu.Rows[rowIdx];
            var unknownBg = SD.Color.FromArgb(255, 243, 205);
            if (!string.IsNullOrWhiteSpace(meal.Breakfast) && bf == 0) rowRef.Cells["Breakfast"].Style.BackColor = unknownBg;
            if (!string.IsNullOrWhiteSpace(meal.Lunch)    && ln == 0) rowRef.Cells["Lunch"].Style.BackColor     = unknownBg;
            if (!string.IsNullOrWhiteSpace(meal.Snack)    && sn == 0) rowRef.Cells["Snack"].Style.BackColor     = unknownBg;
            if (!string.IsNullOrWhiteSpace(meal.Dinner)   && dn == 0) rowRef.Cells["Dinner"].Style.BackColor    = unknownBg;
            totalCal += dayCal; totalCost += dayCost;
        }

        long normTotal = (long)normPerPerson * days.Count;
        int totIdx = dgvMenu.Rows.Add(
            $"ИТОГО ({days.Count} дн.)", "", "", "", "", "", "", "", "",
            totalCal > 0 ? totalCal.ToString("N0") : "", normTotal.ToString("N0"), $"~{totalCost:F0}");
        var totRow = dgvMenu.Rows[totIdx];
        totRow.ReadOnly = true;
        totRow.DefaultCellStyle.BackColor = SD.Color.FromArgb(30, 58, 30);
        totRow.DefaultCellStyle.ForeColor = SD.Color.White;
        totRow.DefaultCellStyle.Font      = new SD.Font("Segoe UI", 13, SD.FontStyle.Bold);
        totRow.Cells["CalDay"].Style.ForeColor  = SD.Color.FromArgb(150, 220, 150);
        totRow.Cells["DayCost"].Style.ForeColor = SD.Color.FromArgb(130, 230, 130);
        totRow.Cells["CalNorm"].Style.ForeColor = SD.Color.FromArgb(140, 190, 255);
    }

    private void FillProductsTab()
    {
        dgvProducts.Rows.Clear();
        decimal ratio = familyCount / 2m;
        int periodDays    = (int)(periodEnd - periodStart).TotalDays + 1;
        decimal periodScale  = periodDays / 30m;
        decimal periodBudget = PeriodBudget;

        decimal comfortTotal = prices.Sum(p => BaseQty.TryGetValue(p.Name, out decimal q) ? p.Price * Math.Round(q * ratio * periodScale, 1) : 0);
        decimal budgetScale  = comfortTotal > 0 && periodBudget < comfortTotal ? periodBudget / comfortTotal : 1m;

        var itemData = prices.Select(p => {
            decimal bq = BaseQty.TryGetValue(p.Name, out decimal q) ? Math.Round(q * ratio * periodScale, 1) : 0;
            decimal rq = bq > 0 ? Math.Round(bq * budgetScale, 2) : 0;
            bool hasPack = PackStep.TryGetValue(p.Name, out decimal st) && st > 0 && rq > 0;
            int fp = hasPack ? (int)Math.Floor(rq / st)   : 0;
            int cp = hasPack ? (int)Math.Ceiling(rq / st) : 0;
            return (p, step: hasPack ? st : 0m, rq, fp, cp);
        }).ToList();

        int[] packs = itemData.Select(x => x.fp).ToArray();
        decimal floorCost = itemData.Select((x, i) => x.step > 0 ? Math.Round(x.p.Price * packs[i] * x.step, 2) : 0m).Sum();
        decimal leftover  = periodBudget - floorCost;

        var upgradeOrder = itemData
            .Select((x, i) => (i, packCost: x.step > 0 ? x.p.Price * x.step : decimal.MaxValue, x))
            .Where(t => t.x.step > 0 && t.x.fp < t.x.cp).OrderBy(t => t.packCost).ToList();
        foreach (var (i, packCost, _) in upgradeOrder)
        {
            decimal cost = Math.Round(packCost, 2);
            if (cost <= leftover) { packs[i]++; leftover -= cost; }
        }

        var allocated = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < itemData.Count; i++)
        {
            var (p, step, rq, _, _) = itemData[i];
            decimal qty = step > 0 ? packs[i] * step
                : (leftover > 0 ? Math.Min(rq, Math.Floor(leftover / p.Price * 100m) / 100m) : 0m);
            if (step == 0 && qty > 0) leftover -= Math.Round(p.Price * qty, 2);
            allocated[p.Name] = qty;
        }

        foreach (var p in prices)
        {
            decimal qty = allocated.GetValueOrDefault(p.Name, 0);
            decimal sum = qty > 0 ? Math.Round(p.Price * qty, 2) : 0;
            int kcal = 0;
            if (qty > 0 && UnitGrams.TryGetValue(p.Unit, out decimal gPU) && CaloriesPer100g.TryGetValue(p.Name, out decimal cal100))
                kcal = (int)Math.Round(qty * gPU * cal100 / 100m);

            bool isMin   = MinimumBasket.ContainsKey(p.Name);
            bool isBasic = !isMin && BasicBasket.ContainsKey(p.Name);
            string tierText  = isMin ? "Минимум" : isBasic ? "Базовый" : "Комфорт";
            Color  tierColor = isMin ? SD.Color.Crimson : isBasic ? SD.Color.DarkOrange : SD.Color.DarkGreen;
            Color  rowBg     = isMin ? SD.Color.FromArgb(255, 235, 235) : isBasic ? SD.Color.FromArgb(255, 252, 220) : SD.Color.FromArgb(235, 255, 235);

            realPriceData.TryGetValue(p.Name, out var rp);
            var    rpMap  = priceMappings.FirstOrDefault(m2 => m2.AppProduct == p.Name);
            decimal mult  = rpMap?.Multiplier is > 0 ? rpMap.Multiplier : 1.0m;
            decimal realP = rp?.LastPrice > 0 ? rp.LastPrice / mult : rp?.Avg30d > 0 ? rp.Avg30d / mult : rp?.Avg90d > 0 ? rp.Avg90d / mult : 0;
            string realPStr = realP > 0 ? realP.ToString("F2") : "";

            int rowIdx = dgvProducts.Rows.Add(
                p.Name, tierText, p.Frequency, p.Unit, p.Price.ToString("F2"), realPStr,
                qty > 0 ? qty.ToString("F2") : "", FormatPackInfo(p.Name, p.Unit, qty),
                sum > 0 ? sum.ToString("F2") : "", kcal > 0 ? kcal.ToString("N0") : "");

            var row = dgvProducts.Rows[rowIdx];
            row.DefaultCellStyle.BackColor    = rowBg;
            row.Cells["Tier"].Style.ForeColor = tierColor;
            row.Cells["Tier"].Style.Font      = new SD.Font("Segoe UI", 10, SD.FontStyle.Bold);
            if (realP > 0)
            {
                decimal diff = (realP - p.Price) / p.Price;
                row.Cells["RealPrice"].Style.ForeColor = diff < -0.05m ? SD.Color.DarkGreen : diff > 0.05m ? SD.Color.Crimson : SD.Color.DarkOrange;
            }
        }

        int totIdx = dgvProducts.Rows.Add("ИТОГО", "", "", "", "", "", "", "");
        var totRow = dgvProducts.Rows[totIdx];
        totRow.Tag = "total"; totRow.ReadOnly = true;
        totRow.DefaultCellStyle.BackColor = SD.Color.FromArgb(30, 58, 30);
        totRow.DefaultCellStyle.ForeColor = SD.Color.White;
        totRow.DefaultCellStyle.Font      = new SD.Font("Segoe UI", 13, SD.FontStyle.Bold);
        totRow.Cells["Sum"].Style.ForeColor  = SD.Color.FromArgb(130, 230, 130);
        totRow.Cells["Kcal"].Style.ForeColor = SD.Color.FromArgb(180, 220, 255);
        totRow.Cells["Sum"].Style.Alignment  = SWF.DataGridViewContentAlignment.MiddleRight;
        totRow.Cells["Kcal"].Style.Alignment = SWF.DataGridViewContentAlignment.MiddleRight;
        UpdateProductsTotal();
    }

    private void DgvProducts_CellValueChanged(object? sender, SWF.DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;
        var row = dgvProducts.Rows[e.RowIndex];
        if (row.IsNewRow) return;
        string? colName = dgvProducts.Columns[e.ColumnIndex]?.Name;

        if (colName == "Frequency")
        {
            string freqProduct = row.Cells["ProductName"].Value?.ToString() ?? "";
            string newFreq     = row.Cells["Frequency"].Value?.ToString()  ?? "еженедельно";
            int idx = prices.FindIndex(x => x.Name.Equals(freqProduct, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0) prices[idx] = prices[idx] with { Frequency = newFreq };
            SavePrices();
            FillWeeklyShoppingTab();
            FillMonthlyShoppingTab();
            return;
        }
        if (colName != "Price" && colName != "Qty") return;

        if (!decimal.TryParse(row.Cells["Price"].Value?.ToString(), out decimal price) || price <= 0 ||
            !decimal.TryParse(row.Cells["Qty"].Value?.ToString(),   out decimal qty)   || qty < 0)
        {
            row.Cells["Sum"].Value = ""; row.Cells["Kcal"].Value = ""; row.Cells["PackInfo"].Value = "";
            UpdateProductsTotal(); return;
        }

        string productName = row.Cells["ProductName"].Value?.ToString() ?? "";
        string unit        = row.Cells["Unit"].Value?.ToString()        ?? "";
        decimal otherTotal = ComputeTotalExcluding(e.RowIndex);
        decimal maxThisRow = Math.Max(0, PeriodBudget - otherTotal);

        if (PackStep.TryGetValue(productName, out decimal step) && step > 0 && qty > 0)
        {
            decimal snapUp = Math.Ceiling(qty / step) * step, snapDown = Math.Floor(qty / step) * step;
            qty = (price * snapUp <= maxThisRow) ? snapUp : (snapDown > 0 ? snapDown : qty);
        }

        decimal propSum = Math.Round(price * qty, 2);
        if (propSum > maxThisRow) { qty = Math.Floor(maxThisRow / price * 100m) / 100m; propSum = Math.Round(price * qty, 2); }

        string qtyStr = qty.ToString("F2");
        if (row.Cells["Qty"].Value?.ToString() != qtyStr)
        {
            dgvProducts.CellValueChanged -= DgvProducts_CellValueChanged;
            row.Cells["Qty"].Value = qtyStr;
            dgvProducts.CellValueChanged += DgvProducts_CellValueChanged;
        }
        row.Cells["Sum"].Value      = propSum.ToString("F2");
        row.Cells["PackInfo"].Value = FormatPackInfo(productName, unit, qty);
        if (UnitGrams.TryGetValue(unit, out decimal gPU) && CaloriesPer100g.TryGetValue(productName, out decimal cal100))
            row.Cells["Kcal"].Value = ((int)Math.Round(qty * gPU * cal100 / 100m)).ToString("N0");
        else row.Cells["Kcal"].Value = "";
        UpdateProductsTotal();
    }

    private decimal ComputeTotalExcluding(int excludeRowIndex)
    {
        decimal total = 0;
        foreach (SWF.DataGridViewRow row in dgvProducts.Rows)
        {
            if (row.IsNewRow || row.Index == excludeRowIndex || row.Tag?.ToString() == "total") continue;
            if (decimal.TryParse(row.Cells["Sum"].Value?.ToString(), out decimal s)) total += s;
        }
        return total;
    }

    private void UpdateProductsTotal()
    {
        decimal total = 0; long totalKcal = 0;
        SWF.DataGridViewRow? totRow = null;
        foreach (SWF.DataGridViewRow row in dgvProducts.Rows)
        {
            if (row.IsNewRow) continue;
            if (row.Tag?.ToString() == "total") { totRow = row; continue; }
            if (decimal.TryParse(row.Cells["Sum"].Value?.ToString(), out decimal s)) total += s;
            string rawK = new string((row.Cells["Kcal"].Value?.ToString() ?? "").Where(char.IsDigit).ToArray());
            if (long.TryParse(rawK, out long k)) totalKcal += k;
        }
        if (totRow != null)
        {
            totRow.Cells["Sum"].Value  = total > 0 ? total.ToString("F2") : "";
            totRow.Cells["Kcal"].Value = totalKcal > 0 ? totalKcal.ToString("N0") : "";
        }

        decimal budget = PeriodBudget, remaining = budget - total;
        decimal overPct = total > budget ? (total - budget) / budget * 100m : 0;

        if (total <= budget)
        {
            lblBudgetStatus.Text      = $"  ✓ Итого: {total:N0} грн  |  Бюджет: {budget:N0} грн  |  Остаток: {remaining:N0} грн";
            lblBudgetStatus.ForeColor = SD.Color.DarkGreen;
        }
        else if (overPct < 5m)
        {
            lblBudgetStatus.Text      = $"  📦 Итого: {total:N0} грн  |  Бюджет: {budget:N0} грн  |  +{-remaining:N0} грн (округл.)";
            lblBudgetStatus.ForeColor = SD.Color.DarkOrange;
        }
        else
        {
            lblBudgetStatus.Text      = $"  ⚠ Итого: {total:N0} грн  |  Бюджет: {budget:N0} грн  |  Превышение на {-remaining:N0} грн!";
            lblBudgetStatus.ForeColor = SD.Color.Crimson;
        }
    }

    // ══════════════════════════════════════════════════ ЗАПОЛНЕНИЕ ПОКУПОК

    private void FillShoppingTab()
    {
        DateTime d1 = periodStart, d2 = periodStart.AddDays(1);
        bool isToday = d1.Date == DateTime.Today;
        FillShoppingDay(d1, dgvShoppingToday,    lblTodayTitle,    isToday ? "Сегодня"   : "1-й день периода");
        FillShoppingDay(d2, dgvShoppingTomorrow, lblTomorrowTitle, isToday ? "Завтра"    : "2-й день периода");
    }

    private void FillShoppingDay(DateTime date, SWF.DataGridView dgv, SWF.Label title, string prefix)
    {
        var culture = new CultureInfo("ru-RU");
        title.Text  = $"{prefix} — {date.ToString("dddd, d MMMM", culture)}";
        dgv.Rows.Clear();

        MealDay? meal = FindMealForDate(date);
        if (meal == null) { dgv.Rows.Add(false, "(нет данных для этой даты)", "", ""); return; }

        var agg = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (string mealText in new[] { meal.Breakfast, meal.Lunch, meal.Snack, meal.Dinner })
            foreach (var (name, grams) in GetIngredients(mealText))
                agg[name] = (agg.GetValueOrDefault(name) + grams * familyCount);

        if (!agg.ContainsKey("Молоко")) agg["Молоко"] = 250m * familyCount;

        decimal dayTotal = 0;
        foreach (var (name, grams) in agg.OrderBy(kv => kv.Key))
        {
            string qty = grams >= 1000 ? $"{grams / 1000:F2} кг" : $"{(int)grams} г";
            decimal est = EstimatePrice(name, grams);
            dayTotal += est;
            dgv.Rows.Add(false, name, qty, est > 0 ? $"~{est:F0}" : "");
        }

        int totIdx = dgv.Rows.Add(false, "ИТОГО", "", dayTotal > 0 ? $"~{dayTotal:F0}" : "", "");
        var totRow = dgv.Rows[totIdx];
        totRow.Tag = "total"; totRow.ReadOnly = true;
        totRow.Cells["Done"].ReadOnly = true;
        totRow.DefaultCellStyle.BackColor = SD.Color.FromArgb(30, 58, 30);
        totRow.DefaultCellStyle.ForeColor = SD.Color.White;
        totRow.DefaultCellStyle.Font      = new SD.Font("Segoe UI", 13, SD.FontStyle.Bold);
        totRow.Cells["Price"].Style.ForeColor = SD.Color.FromArgb(160, 160, 160);
        totRow.Cells["Paid"].Style.ForeColor  = SD.Color.FromArgb(130, 230, 130);
        totRow.Cells["Paid"].Style.BackColor  = SD.Color.FromArgb(30, 58, 30);

        string dateKey = date.ToString("yyyy-MM-dd");
        dgv.Tag = $"daily:{dateKey}";
        RestorePaidValues(dgv, "daily", dateKey);
    }

    private void FillWeeklyShoppingTab()
    {
        DateTime weekEnd = periodStart.AddDays(6);
        lblWeeklyTitle.Text = $"Покупки на неделю:  {periodStart:d MMMM} — {weekEnd:d MMMM yyyy}  ({familyCount} чел.)";
        dgvShoppingWeekly.Rows.Clear();
        decimal ratio = familyCount / 2m, total = 0;
        foreach (var p in prices.Where(p => p.Frequency == "еженедельно"))
        {
            if (!BaseQty.TryGetValue(p.Name, out decimal mq)) continue;
            decimal rawQty = mq * ratio * (7m / 30m);
            decimal snap   = SnapToPack(p.Name, rawQty);
            if (snap <= 0) snap = Math.Round(rawQty, 2);
            var (qtyStr, cost) = GetRealQtyAndCost(p.Name, p.Unit, snap, p.Price);
            total += cost;
            dgvShoppingWeekly.Rows.Add(false, p.Name, qtyStr, $"~{cost:F0}", "");
        }
        AddPeriodicTotalRow(dgvShoppingWeekly, total);
        string weekKey = periodStart.ToString("yyyy-MM-dd");
        dgvShoppingWeekly.Tag = $"weekly:{weekKey}";
        RestorePaidValues(dgvShoppingWeekly, "weekly", weekKey);
        decimal weekBudget = Math.Round(monthlyBudget / 4m, 0);
        lblWeeklyInfo.Text      = $"  {(total <= weekBudget ? "✓" : "⚠")} Итого на неделю: ~{total:N0} грн  |  ~¼ бюджета: {weekBudget:N0} грн  ";
        lblWeeklyInfo.ForeColor = total <= weekBudget ? SD.Color.DarkGreen : SD.Color.Crimson;
    }

    private void FillMonthlyShoppingTab()
    {
        DateTime monthEnd = periodStart.AddDays(29);
        lblMonthlyTitle.Text = $"Покупки на месяц:  {periodStart:d MMMM} — {monthEnd:d MMMM yyyy}  ({familyCount} чел.)";
        dgvShoppingMonthly.Rows.Clear();
        decimal ratio = familyCount / 2m, total = 0;
        foreach (var p in prices.Where(p => p.Frequency == "ежемесячно"))
        {
            if (!BaseQty.TryGetValue(p.Name, out decimal mq)) continue;
            decimal rawQty = mq * ratio;
            decimal snap   = SnapToPack(p.Name, rawQty);
            if (snap <= 0) snap = Math.Round(rawQty, 2);
            var (qtyStr, cost) = GetRealQtyAndCost(p.Name, p.Unit, snap, p.Price);
            total += cost;
            dgvShoppingMonthly.Rows.Add(false, p.Name, qtyStr, $"~{cost:F0}", "");
        }
        AddPeriodicTotalRow(dgvShoppingMonthly, total);
        string monthKey = periodStart.ToString("yyyy-MM-dd");
        dgvShoppingMonthly.Tag = $"monthly:{monthKey}";
        RestorePaidValues(dgvShoppingMonthly, "monthly", monthKey);
        lblMonthlyInfo.Text      = $"  {(total <= monthlyBudget ? "✓" : "⚠")} Итого на месяц: ~{total:N0} грн  |  Бюджет: {monthlyBudget:N0} грн  ";
        lblMonthlyInfo.ForeColor = total <= monthlyBudget ? SD.Color.DarkGreen : SD.Color.Crimson;
    }

    // ══════════════════════════════════════════════════ РЕАЛЬНЫЕ ЦЕНЫ

    private void FillRealPricesTab()
    {
        colExcelName.Items.Clear();
        colExcelName.Items.Add("");
        foreach (var n in excelNames) colExcelName.Items.Add(n);

        dgvRealPrices.CellValueChanged -= DgvRealPrices_CellValueChanged;
        dgvRealPrices.Rows.Clear();

        foreach (var p in prices)
        {
            var m = priceMappings.FirstOrDefault(x => x.AppProduct == p.Name) ?? new PriceMapping { AppProduct = p.Name };
            realPriceData.TryGetValue(p.Name, out var rp);
            decimal lastP = rp?.LastPrice ?? 0, avg30 = rp?.Avg30d ?? 0, avg90 = rp?.Avg90d ?? 0;
            string exUnit = rp?.LastUnit ?? "";
            decimal ourInExcel = m.Multiplier > 0 ? Math.Round(p.Price * m.Multiplier, 2) : p.Price;
            string diffStr = ""; Color diffColor = SD.Color.DimGray;
            if (lastP > 0 && ourInExcel > 0)
            {
                decimal diff = (lastP - ourInExcel) / ourInExcel * 100m;
                diffStr   = $"{diff:+0.0;-0.0}%";
                diffColor = diff < -5m ? SD.Color.DarkGreen : diff > 5m ? SD.Color.Crimson : SD.Color.DarkOrange;
            }
            int ri = dgvRealPrices.Rows.Add(p.Name, p.Unit, m.ExcelName, exUnit, m.Multiplier.ToString("F2"),
                lastP > 0 ? lastP.ToString("F2") : "", avg30 > 0 ? avg30.ToString("F2") : "",
                avg90 > 0 ? avg90.ToString("F2") : "", ourInExcel.ToString("F2"), diffStr);
            if (diffStr != "")
            {
                dgvRealPrices.Rows[ri].Cells["RpDiff"].Style.ForeColor = diffColor;
                dgvRealPrices.Rows[ri].Cells["RpLast"].Style.ForeColor = diffColor;
            }
        }

        dgvRealPrices.CellValueChanged += DgvRealPrices_CellValueChanged;

        int mapped = priceMappings.Count(m => !string.IsNullOrWhiteSpace(m.ExcelName));
        int total  = priceMappings.Count;
        int loaded = excelPurchases.Count;
        bool fromShared = ExcelPriceService.LastSource == "SeniorHub";

        if (fromShared)
        {
            lblRealStatus.Text      = $"  Загружено {loaded} записей из общей базы «Офиса пенсионера»  |  Сопоставлено: {mapped} из {total} продуктов";
            lblRealStatus.ForeColor = SD.Color.SeaGreen;
        }
        else
        {
            bool fileOk = File.Exists(ExcelPriceService.ExcelFilePath);
            lblRealStatus.Text = fileOk
                ? $"  Загружено {loaded} записей из файла расходов  |  Сопоставлено: {mapped} из {total} продуктов"
                : $"  ⚠ Нет данных: общая база пуста и файл не найден ({ExcelPriceService.ExcelFilePath})";
            lblRealStatus.ForeColor = fileOk ? SD.Color.DimGray : SD.Color.Crimson;
        }
    }

    private void DgvRealPrices_CellValueChanged(object? sender, SWF.DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;
        var row = dgvRealPrices.Rows[e.RowIndex];
        string appProduct = row.Cells["RpApp"].Value?.ToString() ?? "";
        string colName    = dgvRealPrices.Columns[e.ColumnIndex].Name;

        var mapping = priceMappings.FirstOrDefault(m => m.AppProduct == appProduct);
        if (mapping == null) return;

        if (colName == "RpExcel")
        {
            string newName = row.Cells["RpExcel"].Value?.ToString() ?? "";
            mapping.ExcelName = newName;
            if (!string.IsNullOrEmpty(newName))
            {
                string appUnit = prices.Find(p => p.Name == appProduct)?.Unit ?? "";
                string exUnit  = excelPurchases.Where(p => p.Name.Equals(newName, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(p => p.Date).FirstOrDefault()?.Unit ?? "";
                decimal mult = ExcelPriceService.DefaultMultiplier(exUnit, appUnit);
                mapping.Multiplier = mult;
                dgvRealPrices.CellValueChanged -= DgvRealPrices_CellValueChanged;
                row.Cells["RpMult"].Value   = mult.ToString("F2");
                row.Cells["RpExUnit"].Value = exUnit;
                dgvRealPrices.CellValueChanged += DgvRealPrices_CellValueChanged;
            }
        }
        else if (colName == "RpMult")
        {
            if (decimal.TryParse(row.Cells["RpMult"].Value?.ToString(), out decimal mult) && mult > 0)
                mapping.Multiplier = mult;
        }
        else return;

        ExcelPriceService.SaveMappings(priceMappings);
        realPriceData = ExcelPriceService.ComputeRealPrices(priceMappings, excelPurchases);
        FillRealPricesTab();
        FillProductsTab();
    }

    private void RefreshFromExcel()
    {
        excelPurchases = ExcelPriceService.LoadPurchases();
        excelNames     = ExcelPriceService.GetDistinctNames(excelPurchases);
        realPriceData  = ExcelPriceService.ComputeRealPrices(priceMappings, excelPurchases);
        FillRealPricesTab();
        FillProductsTab();
    }

    private void LoadRealPrices()
    {
        priceMappings  = ExcelPriceService.LoadMappings();
        excelPurchases = ExcelPriceService.LoadPurchases();
        excelNames     = ExcelPriceService.GetDistinctNames(excelPurchases);
        foreach (var p in prices)
        {
            if (priceMappings.Any(m => m.AppProduct == p.Name)) continue;
            string? matched = ExcelPriceService.AutoMatch(p.Name, excelNames);
            string exUnit = matched != null
                ? excelPurchases.Where(x => x.Name.Equals(matched, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(x => x.Date).FirstOrDefault()?.Unit ?? "" : "";
            priceMappings.Add(new PriceMapping { AppProduct = p.Name, ExcelName = matched ?? "",
                Multiplier = matched != null ? ExcelPriceService.DefaultMultiplier(exUnit, p.Unit) : 1.0m });
        }
        bool changed = false;
        foreach (var m in priceMappings.Where(m => string.IsNullOrEmpty(m.ExcelName)))
        {
            string? matched = ExcelPriceService.AutoMatch(m.AppProduct, excelNames);
            if (matched == null) continue;
            m.ExcelName = matched;
            string appUnit = prices.Find(p => p.Name == m.AppProduct)?.Unit ?? "";
            string exUnit  = excelPurchases.Where(x => x.Name.Equals(matched, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.Date).FirstOrDefault()?.Unit ?? "";
            m.Multiplier = ExcelPriceService.DefaultMultiplier(exUnit, appUnit);
            changed = true;
        }
        realPriceData = ExcelPriceService.ComputeRealPrices(priceMappings, excelPurchases);
        if (changed) ExcelPriceService.SaveMappings(priceMappings);
    }

    // ══════════════════════════════════════════════════ ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ

    private static void AddPeriodicTotalRow(SWF.DataGridView dgv, decimal estimatedTotal)
    {
        int ti = dgv.Rows.Add(false, "ИТОГО", "", estimatedTotal > 0 ? $"~{estimatedTotal:F0}" : "", "");
        var tr = dgv.Rows[ti];
        tr.Tag = "total"; tr.ReadOnly = true;
        tr.DefaultCellStyle.BackColor = SD.Color.FromArgb(30, 58, 30);
        tr.DefaultCellStyle.ForeColor = SD.Color.White;
        tr.DefaultCellStyle.Font      = new SD.Font("Segoe UI", 13, SD.FontStyle.Bold);
        tr.Cells["Price"].Style.ForeColor = SD.Color.FromArgb(160, 160, 160);
        tr.Cells["Paid"].Style.ForeColor  = SD.Color.FromArgb(130, 230, 130);
        tr.Cells["Paid"].Style.BackColor  = SD.Color.FromArgb(30, 58, 30);
    }

    private void UpdateShoppingPaidTotal(SWF.DataGridView dgv)
    {
        decimal total = 0;
        SWF.DataGridViewRow? totRow = null;
        var amounts = new Dictionary<string, decimal>();
        foreach (SWF.DataGridViewRow row in dgv.Rows)
        {
            if (row.IsNewRow) continue;
            if (row.Tag?.ToString() == "total") { totRow = row; continue; }
            string prod = row.Cells["Product"]?.Value?.ToString() ?? "";
            if (decimal.TryParse(row.Cells["Paid"]?.Value?.ToString(), out decimal v) && v > 0)
                { total += v; if (!string.IsNullOrEmpty(prod)) amounts[prod] = v; }
        }
        if (totRow != null) totRow.Cells["Paid"].Value = total > 0 ? total.ToString("F0") : "";

        if (dgv.Tag is string tag && tag.Contains(':'))
        {
            var parts = tag.Split(':', 2);
            string type = parts[0], dateKey = parts[1];
            if (!paidData.ContainsKey(type)) paidData[type] = new();
            paidData[type][dateKey] = amounts;
            SavePaidData();
        }
    }

    private static void CopyShoppingListToClipboard(SWF.DataGridView dgv, string title)
    {
        var sb = new StringBuilder();
        sb.AppendLine(title);
        sb.AppendLine(new string('─', 40));
        foreach (SWF.DataGridViewRow row in dgv.Rows)
        {
            if (row.IsNewRow) continue;
            bool isTotal = row.Tag?.ToString() == "total";
            bool done    = row.Cells["Done"].Value is true;
            string prod  = row.Cells["Product"].Value?.ToString()   ?? "";
            string qty   = row.Cells["Quantity"]?.Value?.ToString() ?? "";
            string price = row.Cells["Price"].Value?.ToString()     ?? "";
            string paid  = row.Cells["Paid"]?.Value?.ToString()     ?? "";
            if (isTotal) { sb.AppendLine(new string('─', 40)); sb.Append($"ИТОГО: {price}"); if (!string.IsNullOrEmpty(paid)) sb.Append($"  |  Заплачено: {paid}"); sb.AppendLine(); }
            else { string check = done ? "☑" : "☐"; sb.Append($"{check} {prod}"); if (!string.IsNullOrEmpty(qty)) sb.Append($": {qty}"); if (!string.IsNullOrEmpty(price)) sb.Append($"  ({price})"); if (!string.IsNullOrEmpty(paid) && paid != "0") sb.Append($"  ✓{paid}"); sb.AppendLine(); }
        }
        try { System.Windows.Clipboard.SetText(sb.ToString()); } catch { }
    }

    private void RestorePaidValues(SWF.DataGridView dgv, string sessionType, string dateKey)
    {
        if (!paidData.TryGetValue(sessionType, out var sessions)) return;
        if (!sessions.TryGetValue(dateKey, out var amounts)) return;
        foreach (SWF.DataGridViewRow row in dgv.Rows)
        {
            if (row.IsNewRow || row.Tag?.ToString() == "total") continue;
            string prod = row.Cells["Product"].Value?.ToString() ?? "";
            if (amounts.TryGetValue(prod, out decimal amt) && amt > 0) row.Cells["Paid"].Value = amt.ToString("F0");
        }
        UpdateShoppingPaidTotal(dgv);
    }

    // ══════════════════════════════════════════════════ РАСЧЁТЫ

    private (string qty, decimal cost) GetRealQtyAndCost(string appProduct, string appUnit, decimal snappedAppQty, decimal jsonPrice)
    {
        var mapping = priceMappings.FirstOrDefault(m => m.AppProduct == appProduct);
        realPriceData.TryGetValue(appProduct, out var rp);
        if (rp?.LastPrice > 0 && mapping != null && !string.IsNullOrEmpty(rp.LastUnit) && mapping.Multiplier > 0)
        {
            string exUnit = rp.LastUnit.TrimEnd('.');
            bool discrete = !exUnit.Equals("кг", StringComparison.OrdinalIgnoreCase) && !exUnit.Equals("л", StringComparison.OrdinalIgnoreCase);
            decimal raw   = snappedAppQty / mapping.Multiplier;
            decimal exQty = discrete ? Math.Ceiling(raw) : Math.Round(raw, 2);
            decimal cost  = Math.Round(rp.LastPrice * exQty, 2);
            return (discrete ? $"{exQty:F0} {exUnit}" : $"{exQty:F2} {exUnit}", cost);
        }
        return (FormatShoppingQty(appProduct, appUnit, snappedAppQty), Math.Round(jsonPrice * snappedAppQty, 2));
    }

    private long ComputeProductsTotalKcal()
    {
        decimal ratio = familyCount / 2m, periodScale = (int)(periodEnd - periodStart).TotalDays + 1 / 30m;
        decimal comfortTotal = prices.Sum(p => BaseQty.TryGetValue(p.Name, out decimal q) ? p.Price * Math.Round(q * ratio * periodScale, 1) : 0);
        decimal budgetScale  = comfortTotal > 0 && PeriodBudget < comfortTotal ? PeriodBudget / comfortTotal : 1m;
        long total = 0;
        foreach (var p in prices)
        {
            if (!BaseQty.TryGetValue(p.Name, out decimal q)) continue;
            decimal qty = Math.Round(q * ratio * periodScale * budgetScale, 1);
            if (qty <= 0) continue;
            if (UnitGrams.TryGetValue(p.Unit, out decimal gPU) && CaloriesPer100g.TryGetValue(p.Name, out decimal cal100))
                total += (long)Math.Round(qty * gPU * cal100 / 100m);
        }
        return total;
    }

    private decimal CalcTierBudget(Dictionary<string, decimal> basket)
    {
        if (prices.Count == 0) return 0;
        decimal ratio = familyCount / 2m, total = 0;
        foreach (var p in prices)
            if (basket.TryGetValue(p.Name, out decimal qty)) total += p.Price * Math.Round(qty * ratio, 1);
        return Math.Round(total, 0);
    }

    private static int CalcMealCalories(string mealText)
    {
        decimal total = 0;
        foreach (var (name, grams) in GetIngredients(mealText))
            if (CaloriesPer100g.TryGetValue(name, out decimal cal)) total += grams * cal / 100m;
        return (int)Math.Round(total);
    }

    private decimal EstimatePrice(string ingredient, decimal totalGrams)
    {
        var p = prices.Find(x => x.Name.Equals(ingredient, StringComparison.OrdinalIgnoreCase));
        return p == null ? 0 : Math.Round(p.Price * totalGrams / 1000m, 1);
    }

    private static readonly DateTime PlanStart = new DateTime(2026, 4, 24);

    private MealDay? FindMealForDate(DateTime date)
    {
        if (mealPlan.Count == 0) return null;
        int totalDays = (int)(date.Date - PlanStart).TotalDays;
        int cycleLen  = Math.Min(mealPlan.Count, 7);
        int idx = ((totalDays % cycleLen) + cycleLen) % cycleLen;
        return mealPlan[idx];
    }

    // ══════════════════════════════════════════════════ СТАТИЧЕСКИЕ ДАННЫЕ

    private static readonly Dictionary<string, decimal> CaloriesPer100g = new()
    {
        ["Хлеб"]=265,["Батон"]=258,["Макароны"]=338,["Мука"]=334,["Гречка"]=313,["Рис"]=344,
        ["Говядина"]=187,["Свинина"]=263,["Курица"]=165,["Филе куриное"]=113,["Рыба мороженая"]=75,
        ["Молоко"]=52,["Сыр"]=350,["Сметана"]=206,["Яйца"]=157,["Масло сливочное"]=717,
        ["Масло подсолнечное"]=884,["Картофель"]=77,["Капуста"]=27,["Лук"]=41,["Морковь"]=41,
        ["Яблоки"]=52,["Чеснок"]=149,["Творог"]=121,["Овсянка"]=352,
    };

    private static readonly Dictionary<string, decimal> UnitGrams = new()
    {
        ["кг"]=1000m,["л"]=1000m,["500 г"]=500m,["200 г"]=200m,["десяток"]=600m,
    };

    private static readonly Dictionary<string, decimal> MinimumBasket = new()
    {
        ["Хлеб"]=12,["Батон"]=4,["Макароны"]=3,["Мука"]=2,["Гречка"]=3,["Рис"]=3,
        ["Масло подсолнечное"]=2,["Картофель"]=20,["Капуста"]=5,["Лук"]=5,["Морковь"]=5,
        ["Яйца"]=4,["Молоко"]=8,["Масло сливочное"]=4,
    };

    private static readonly Dictionary<string, decimal> BasicBasket = new()
    {
        ["Хлеб"]=18,["Батон"]=6,["Макароны"]=6,["Мука"]=3,["Гречка"]=5,["Рис"]=5,
        ["Масло подсолнечное"]=2,["Картофель"]=24,["Капуста"]=7,["Лук"]=7,["Морковь"]=7,
        ["Яйца"]=8,["Молоко"]=16,["Масло сливочное"]=6,["Яблоки"]=6,["Сыр"]=1.5m,
        ["Сметана"]=1.5m,["Курица"]=6,["Рыба мороженая"]=4,
    };

    private static readonly Dictionary<string, decimal> BaseQty = new()
    {
        ["Хлеб"]=28,["Батон"]=8,["Макароны"]=8,["Мука"]=4,["Гречка"]=8,["Рис"]=8,
        ["Говядина"]=2,["Свинина"]=4,["Курица"]=12,["Филе куриное"]=4,["Рыба мороженая"]=8,
        ["Молоко"]=28,["Сыр"]=4,["Сметана"]=3.2m,["Яйца"]=12,["Масло сливочное"]=8,
        ["Масло подсолнечное"]=2,["Картофель"]=28,["Капуста"]=8,["Лук"]=8,["Морковь"]=8,
        ["Яблоки"]=12,["Чеснок"]=1.2m,
    };

    private static readonly Dictionary<string, decimal> PackStep = new()
    {
        ["Хлеб"]=0.6m,["Батон"]=1.0m,["Макароны"]=0.4m,["Мука"]=1.0m,["Гречка"]=1.0m,["Рис"]=1.0m,
        ["Говядина"]=0.5m,["Свинина"]=0.5m,["Курица"]=0.5m,["Филе куриное"]=0.5m,["Рыба мороженая"]=0.5m,
        ["Молоко"]=1.0m,["Сыр"]=0.2m,["Сметана"]=0.4m,["Яйца"]=1.0m,["Масло сливочное"]=1.0m,
        ["Масло подсолнечное"]=1.0m,["Картофель"]=1.0m,["Капуста"]=0.5m,["Лук"]=1.0m,["Морковь"]=1.0m,
        ["Яблоки"]=1.0m,["Чеснок"]=0.1m,
    };

    private static decimal SnapToPack(string name, decimal qty)
    {
        if (!PackStep.TryGetValue(name, out decimal step) || step <= 0 || qty <= 0) return qty;
        return Math.Ceiling(qty / step) * step;
    }

    private static string FormatPackInfo(string name, string unit, decimal qty)
    {
        if (!PackStep.TryGetValue(name, out decimal step) || step <= 0 || qty <= 0) return "";
        int packs = (int)Math.Ceiling(qty / step);
        if (packs <= 0) return "";
        return unit switch { "кг" when step < 1m => $"{packs} × {(int)(step*1000)}г", "кг" => $"{packs} кг", "л" => $"{packs} л", _ => $"{packs} уп." };
    }

    private static string FormatShoppingQty(string name, string unit, decimal qty)
    {
        if (qty <= 0) return "";
        if (!PackStep.TryGetValue(name, out decimal step) || step <= 0) return $"{qty:F2} {unit}";
        int packs = (int)Math.Ceiling(qty / step);
        if (packs <= 0) return "";
        return unit switch { "кг" when step < 1m => $"{packs} × {(int)(step*1000)}г", "кг" => $"{packs} кг", "л" => $"{packs} л", "200 г" => $"{packs} × 200г", "500 г" => $"{packs} × 500г", "десяток" => $"{packs} дес.", _ => $"{packs} уп." };
    }

    private static readonly List<(string keyword, (string name, decimal grams)[] ingredients)> IngMap = new()
    {
        ("овсянка",            new[]{("Овсянка",80m),("Яблоки",100m)}),
        ("яйца варён",         new[]{("Яйца",60m),("Хлеб",100m),("Масло сливочное",15m)}),
        ("яичница",            new[]{("Яйца",90m),("Масло сливочное",8m)}),
        ("омлет",              new[]{("Яйца",100m),("Молоко",50m),("Масло сливочное",8m)}),
        ("хлеб с маслом",      new[]{("Хлеб",100m),("Масло сливочное",25m)}),
        ("творог со сметан",   new[]{("Творог",150m),("Сметана",40m)}),
        ("творог с яг",        new[]{("Творог",150m)}),
        ("творог",             new[]{("Творог",150m)}),
        ("рисовая каша",       new[]{("Рис",60m),("Молоко",200m),("Масло сливочное",8m)}),
        ("молочная каша",      new[]{("Рис",50m),("Молоко",250m),("Масло сливочное",8m)}),
        ("гречневая каша",     new[]{("Гречка",80m),("Молоко",150m)}),
        ("манная каша",        new[]{("Мука",50m),("Молоко",250m)}),
        ("манка",              new[]{("Мука",50m),("Молоко",200m)}),
        ("сырники",            new[]{("Творог",150m),("Яйца",25m),("Мука",25m),("Сметана",30m)}),
        ("запеканка",          new[]{("Творог",150m),("Яйца",30m),("Мука",20m),("Сметана",30m)}),
        ("бутерброды с сыром", new[]{("Хлеб",120m),("Сыр",60m)}),
        ("вареники",           new[]{("Мука",100m),("Картофель",150m),("Лук",30m),("Масло сливочное",10m)}),
        ("деруны",             new[]{("Картофель",200m),("Мука",20m),("Яйца",25m),("Сметана",30m)}),
        ("драники",            new[]{("Картофель",200m),("Мука",20m),("Яйца",25m),("Сметана",30m)}),
        ("пельмени",           new[]{("Свинина",120m),("Мука",80m),("Лук",20m)}),
        ("рыбный суп",         new[]{("Картофель",100m),("Морковь",30m),("Лук",25m),("Рыба мороженая",120m)}),
        ("борщ",               new[]{("Капуста",150m),("Картофель",100m),("Морковь",50m),("Лук",40m),("Свинина",80m)}),
        ("суп с чечевиц",      new[]{("Картофель",80m),("Морковь",30m),("Лук",25m),("Курица",70m)}),
        ("куриный суп",        new[]{("Картофель",80m),("Морковь",30m),("Лук",25m),("Курица",100m)}),
        ("суп-пюре из тыквы",  new[]{("Картофель",50m),("Лук",30m),("Сметана",30m)}),
        ("гречневый суп",      new[]{("Гречка",50m),("Морковь",30m),("Лук",25m)}),
        ("картофельный суп",   new[]{("Картофель",150m),("Морковь",40m),("Лук",30m)}),
        ("молочный суп",       new[]{("Молоко",300m),("Макароны",50m)}),
        ("вермишелевый суп",   new[]{("Макароны",50m),("Морковь",30m),("Лук",25m)}),
        ("щи",                 new[]{("Капуста",120m),("Картофель",80m),("Морковь",30m),("Лук",25m)}),
        ("рассольник",         new[]{("Картофель",80m),("Морковь",30m),("Лук",25m),("Рис",20m)}),
        ("харчо",              new[]{("Рис",50m),("Говядина",80m),("Лук",30m),("Морковь",20m)}),
        ("суп",                new[]{("Картофель",100m),("Морковь",30m),("Лук",25m)}),
        ("гречка с куриц",     new[]{("Гречка",80m),("Курица",120m)}),
        ("гречка",             new[]{("Гречка",80m)}),
        ("рис с овощ",         new[]{("Рис",80m),("Морковь",50m),("Лук",25m)}),
        ("рис с куриц",        new[]{("Рис",80m),("Курица",100m)}),
        ("рис",                new[]{("Рис",80m)}),
        ("картофельное пюре",  new[]{("Картофель",200m),("Молоко",70m),("Масло сливочное",12m)}),
        ("жареная картошк",    new[]{("Картофель",250m),("Масло подсолнечное",15m),("Лук",40m)}),
        ("жареный картоф",     new[]{("Картофель",250m),("Масло подсолнечное",15m),("Лук",40m)}),
        ("варёный картоф",     new[]{("Картофель",250m),("Масло сливочное",15m)}),
        ("вареный картоф",     new[]{("Картофель",250m),("Масло сливочное",15m)}),
        ("тушёная капуста",    new[]{("Капуста",200m),("Морковь",40m),("Лук",30m),("Масло подсолнечное",15m)}),
        ("голубцы",            new[]{("Рис",60m),("Свинина",100m),("Капуста",150m),("Морковь",30m),("Лук",25m)}),
        ("тефтели",            new[]{("Свинина",100m),("Рис",40m),("Лук",25m)}),
        ("биточки",            new[]{("Свинина",100m),("Рис",40m),("Лук",25m)}),
        ("фрикадельки",        new[]{("Свинина",80m),("Рис",30m),("Лук",20m)}),
        ("зразы",              new[]{("Говядина",120m),("Яйца",25m),("Лук",30m)}),
        ("винегрет",           new[]{("Картофель",80m),("Морковь",40m),("Лук",20m)}),
        ("тушён",              new[]{("Картофель",100m),("Морковь",50m),("Капуста",80m),("Лук",30m)}),
        ("котлет",             new[]{("Свинина",130m)}),
        ("рыба",               new[]{("Рыба мороженая",150m)}),
        ("запечённая курица",  new[]{("Курица",200m)}),
        ("курица",             new[]{("Курица",150m)}),
        ("макароны с соусом",  new[]{("Макароны",100m),("Сыр",40m)}),
        ("макарон",            new[]{("Макароны",100m)}),
        ("вермишель",          new[]{("Макароны",100m),("Масло сливочное",10m)}),
        ("лапша",              new[]{("Макароны",100m),("Масло сливочное",10m)}),
        ("плов",               new[]{("Рис",120m),("Курица",150m),("Морковь",80m),("Лук",40m)}),
        ("овощное рагу",       new[]{("Картофель",100m),("Морковь",50m),("Капуста",80m),("Лук",30m)}),
        ("пицца",              new[]{("Мука",100m),("Сыр",60m)}),
        ("овощи на гриле",     new[]{("Картофель",100m),("Морковь",50m),("Лук",40m)}),
        ("овощи",              new[]{("Картофель",80m),("Морковь",30m),("Лук",25m)}),
        ("сметан",             new[]{("Сметана",50m)}),
        ("сыр",                new[]{("Сыр",50m)}),
        ("хлеб",               new[]{("Хлеб",100m)}),
    };

    private static IEnumerable<(string name, decimal grams)> GetIngredients(string mealText)
    {
        if (string.IsNullOrEmpty(mealText)) yield break;
        string lower = mealText.ToLowerInvariant();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (kw, items) in IngMap)
            if (lower.Contains(kw))
                foreach (var (name, g) in items)
                    if (seen.Add(name)) yield return (name, g);
    }

    // ══════════════════════════════════════════════════ СОХРАНЕНИЕ / ЗАГРУЗКА

    internal void LoadSettings()
    {
        string path = Path.Combine(AppDir, "settings.json");
        if (!File.Exists(path)) return;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            monthlyBudget = doc.RootElement.GetProperty("Budget").GetDecimal();
            familyCount   = doc.RootElement.GetProperty("FamilyCount").GetInt32();
            if (doc.RootElement.TryGetProperty("CalorieNorm", out var cn)) calorieNorm = cn.GetInt32();
        }
        catch { }
    }

    internal void SaveSettings()
    {
        string path = Path.Combine(AppDir, "settings.json");
        var obj = new { Budget = monthlyBudget, FamilyCount = familyCount, CalorieNorm = calorieNorm };
        File.WriteAllText(path, JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true }));
    }

    private void LoadPaidData()
    {
        string path = Path.Combine(AppDir, "paid_history.json");
        if (!File.Exists(path)) return;
        try
        {
            paidData = JsonSerializer.Deserialize<
                Dictionary<string, Dictionary<string, Dictionary<string, decimal>>>>(
                File.ReadAllText(path, System.Text.Encoding.UTF8)) ?? new();
        }
        catch { }
    }

    private void SavePaidData()
    {
        try
        {
            File.WriteAllText(Path.Combine(AppDir, "paid_history.json"),
                JsonSerializer.Serialize(paidData, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                }), System.Text.Encoding.UTF8);
        }
        catch { }
    }

    private void SavePrices()
    {
        string? path = FindDataFile("средними ценами.json") ?? Path.Combine(AppDir, "средними ценами.json");
        var arr = prices.Select(p => new { name = p.Name, price = p.Price, unit = p.Unit, frequency = p.Frequency });
        File.WriteAllText(path, JsonSerializer.Serialize(new { prices = arr },
            new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }),
            System.Text.Encoding.UTF8);
    }

    internal static void EnsureDataFiles()
    {
        WriteIfAbsent(Path.Combine(AppDir, "средними ценами.json"), DefaultData.PricesJson);
        WriteIfAbsent(Path.Combine(AppDir, "30_day_meal_plan.txt"),  DefaultData.MealPlanTxt);
    }

    private static void WriteIfAbsent(string path, string content)
    {
        if (!File.Exists(path)) File.WriteAllText(path, content, System.Text.Encoding.UTF8);
    }

    private static string? FindDataFile(string name)
    {
        string[] candidates = {
            Path.Combine(AppDir, name),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, name),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", name),
            @"C:\Users\User\Opus 4.6\Food\MenuApp\" + name
        };
        return Array.Find(candidates, File.Exists);
    }
}


