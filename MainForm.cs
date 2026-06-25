using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;

namespace MenuApp
{
    public class MainForm : Form
    {
        // ── Settings ──────────────────────────────────────────────
        private decimal monthlyBudget = 10000;
        private int familyCount  = 2;
        private int calorieNorm  = 2000;

        // ── Period ────────────────────────────────────────────────
        private DateTime periodStart;
        private DateTime periodEnd;
        private DateTimePicker dtpPeriodStart = null!;
        private DateTimePicker dtpPeriodEnd   = null!;
        private Label lblPeriodWarning        = null!;

        private decimal PeriodBudget
        {
            get
            {
                int days = (int)(periodEnd - periodStart).TotalDays + 1;
                // Never exceed the monthly budget — extra days don't create extra money
                return Math.Min(monthlyBudget, Math.Round(monthlyBudget * days / 30m, 0));
            }
        }

        // ── UI refs ───────────────────────────────────────────────
        private Label lblBudgetInfo = null!;
        private Label lblTierInfo   = null!;

        // Dashboard "Сегодня"
        private WebView2? webView;
        private bool      webViewReady     = false;
        private string?   _currentSearchDish;
        private Button?   _btnSearchGoogle;
        private Button?   _btnSearchBing;
        private Button?   _btnSearchYT;

        private Label    lblDayDate  = null!;
        private Label[]  lblCardMeal     = new Label[4];
        private Label[]  lblCardCalories = new Label[4];
        private Label[]  lblCardCost     = new Label[4];
        private Button[] btnCardRecipe   = new Button[4];

        private DataGridView dgvMenu     = null!;
        private DataGridView dgvProducts = null!;
        private Label        lblBudgetStatus = null!;
        private DataGridView dgvShoppingToday    = null!;
        private DataGridView dgvShoppingTomorrow = null!;
        private Label lblTodayTitle    = null!;
        private Label lblTomorrowTitle = null!;
        private DataGridView dgvShoppingWeekly  = null!;
        private DataGridView dgvShoppingMonthly = null!;
        private Label lblWeeklyTitle = null!;
        private Label lblMonthlyTitle = null!;
        private Label lblWeeklyInfo  = null!;
        private Label lblMonthlyInfo = null!;

        // ── Paid history (persisted to paid_history.json) ────────
        // Structure: type("daily"/"weekly"/"monthly") → dateKey → product → amount
        private Dictionary<string, Dictionary<string, Dictionary<string, decimal>>> paidData = new();

        // ── Real prices (from HomeB Excel) ────────────────────────
        private List<PriceMapping>  priceMappings  = new();
        private List<FoodPurchase>  excelPurchases = new();
        private List<string>        excelNames     = new();
        private Dictionary<string, RealPriceResult> realPriceData = new();
        private DataGridView dgvRealPrices   = null!;
        private Label        lblRealStatus   = null!;
        private DataGridViewComboBoxColumn colExcelName = null!;

        // ── Data ──────────────────────────────────────────────────
        private List<MealDay>   mealPlan = new();
        private List<PriceItem> prices   = new();

        // ─────────────────────────────────────────────────────────
        // Directory where the exe lives (works for both regular and single-file publish)
        private static string AppDir =>
            Path.GetDirectoryName(
                System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName)
            ?? AppDomain.CurrentDomain.BaseDirectory;

        // Extract default data files next to the exe on first run
        private static void EnsureDataFiles()
        {
            WriteIfAbsent(Path.Combine(AppDir, "средними ценами.json"), DefaultData.PricesJson);
            WriteIfAbsent(Path.Combine(AppDir, "30_day_meal_plan.txt"),  DefaultData.MealPlanTxt);
        }

        private static void WriteIfAbsent(string path, string content)
        {
            if (!File.Exists(path))
                File.WriteAllText(path, content, System.Text.Encoding.UTF8);
        }

        public MainForm()
        {
            EnsureDataFiles();
            ExcelPriceService.DataDirectory = AppDir;
            Icon = Icon.ExtractAssociatedIcon(
                System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName
                ?? Application.ExecutablePath);
            LoadSettings();
            periodStart = DateTime.Today;
            periodEnd   = DateTime.Today.AddDays(30);
            BuildUI();
            LoadData();
            Shown += async (_, _) =>
            {
                await InitWebViewAsync();
                _ = UpdateChecker.CheckAsync();
            };
        }

        // ═══════════════════════════════════════════════════ BUILD UI

        private void BuildUI()
        {
            Text = "Меню питания семьи";
            Width = 1100;
            Height = 720;
            MinimumSize = new Size(900, 590);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.WhiteSmoke;

            // ── Header ──
            var header = new Panel { Dock = DockStyle.Top, Height = 100, BackColor = Color.SteelBlue };

            var lblTitle = new Label
            {
                Text = "Меню питания",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(14, 7),
                AutoSize = true
            };

            lblBudgetInfo = new Label
            {
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.LightCyan,
                Location = new Point(220, 10),
                AutoSize = true
            };

            lblTierInfo = new Label
            {
                Font = new Font("Segoe UI", 8),
                ForeColor = Color.LightYellow,
                Location = new Point(14, 42),
                AutoSize = true
            };

            // ── Period row + today's date + recipe buttons (all in header, row 3) ──
            dtpPeriodStart = new DateTimePicker
            {
                Format = DateTimePickerFormat.Short,
                Value  = periodStart,
                Location = new Point(14, 68),
                Width = 110
            };

            dtpPeriodEnd = new DateTimePicker
            {
                Format = DateTimePickerFormat.Short,
                Value  = periodEnd,
                Location = new Point(128, 68),
                Width = 120
            };

            var btnApplyPeriod = new Button
            {
                Text = "▶ Применить",
                Location = new Point(254, 65),
                Width = 112, Height = 28,
                BackColor = Color.FromArgb(60, 100, 155),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            btnApplyPeriod.FlatAppearance.BorderColor = Color.FromArgb(40, 80, 130);
            btnApplyPeriod.Click += BtnApplyPeriod_Click;

            // Quick period buttons
            Button MakeQuickBtn(string text) => new Button
            {
                Text = text, Height = 28,
                BackColor = Color.FromArgb(45, 85, 135),
                ForeColor = Color.White, FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8, FontStyle.Bold)
            };
            var btn7d  = MakeQuickBtn("7 дн.");
            var btn30d = MakeQuickBtn("30 дн.");
            btn7d.Width  = 54; btn7d.Location  = new Point(372, 65);
            btn30d.Width = 64; btn30d.Location = new Point(430, 65);
            btn7d.FlatAppearance.BorderColor  = Color.FromArgb(30, 70, 120);
            btn30d.FlatAppearance.BorderColor = Color.FromArgb(30, 70, 120);
            btn7d.Click  += (_, _) => { dtpPeriodStart.Value = DateTime.Today; dtpPeriodEnd.Value = DateTime.Today.AddDays(6);  BtnApplyPeriod_Click(null, EventArgs.Empty); };
            btn30d.Click += (_, _) => { dtpPeriodStart.Value = DateTime.Today; dtpPeriodEnd.Value = DateTime.Today.AddDays(29); BtnApplyPeriod_Click(null, EventArgs.Empty); };

            lblPeriodWarning = new Label
            {
                Font = new Font("Segoe UI", 8, FontStyle.Italic),
                ForeColor = Color.LightSalmon,
                Location = new Point(500, 71),
                AutoSize = true
            };

            // Today's date + daily stats — filled by FillDashboardTab
            lblDayDate = new Label
            {
                Text = DateTime.Today.ToString("ddd, d MMM", new CultureInfo("ru-RU")),
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(590, 71),
                AutoSize = true
            };

            // Recipe engine buttons — right-anchored, just before Settings button
            var lblRecipeHint = new Label
            {
                Text = "Рецепт:",
                Font = new Font("Segoe UI", 8),
                ForeColor = Color.LightCyan,
                AutoSize = true
            };
            _btnSearchGoogle = MakeEngineBtn("Google",  Color.FromArgb(66, 133, 244), 0, 65);
            _btnSearchBing   = MakeEngineBtn("Bing",    Color.FromArgb(0,  120, 215), 0, 65);
            _btnSearchYT     = MakeEngineBtn("YouTube", Color.FromArgb(200, 30,  30), 0, 65);
            _btnSearchGoogle.Click += (_, _) => SearchOnEngine(0);
            _btnSearchBing.Click   += (_, _) => SearchOnEngine(1);
            _btnSearchYT.Click     += (_, _) => SearchOnEngine(2);
            SetEngineButtonsEnabled(false);

            void PlaceRecipeRow()
            {
                int r = header.Width - 128;
                _btnSearchYT!.Location     = new Point(r - 70, 65);
                _btnSearchBing!.Location   = new Point(r - 133, 65);
                _btnSearchGoogle!.Location = new Point(r - 204, 65);
                lblRecipeHint.Location     = new Point(r - 260, 71);
            }

            dtpPeriodStart.ValueChanged += OnPeriodChanged;
            dtpPeriodEnd.ValueChanged   += OnPeriodChanged;

            var btnSettings = new Button
            {
                Text = "Настройки",
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Width = 110, Height = 32,
                BackColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.SteelBlue
            };
            btnSettings.FlatAppearance.BorderColor = Color.LightSteelBlue;
            header.Resize += (_, _) => { btnSettings.Location = new Point(header.Width - 120, 10); PlaceRecipeRow(); };
            btnSettings.Click += BtnSettings_Click;
            Shown += (_, _) => { btnSettings.Location = new Point(header.Width - 120, 10); PlaceRecipeRow(); };

            header.Controls.AddRange(new Control[]
            {
                lblTitle, lblBudgetInfo, lblTierInfo,
                dtpPeriodStart, dtpPeriodEnd,
                btnApplyPeriod, btn7d, btn30d, lblPeriodWarning, lblDayDate,
                lblRecipeHint, _btnSearchGoogle!, _btnSearchBing!, _btnSearchYT!,
                btnSettings
            });

            // ── Tabs ──
            var tabs = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10),
                DrawMode = TabDrawMode.OwnerDrawFixed,
                SizeMode = TabSizeMode.FillToRight,
                ItemSize = new Size(0, 34),
                Padding = new Point(6, 4)
            };
            tabs.DrawItem += (s, e) =>
            {
                var tc  = (TabControl)s!;
                var pg  = tc.TabPages[e.Index];
                bool sel = tc.SelectedIndex == e.Index;
                Color bg  = sel ? Color.SteelBlue : Color.FromArgb(228, 235, 248);
                Color fg  = sel ? Color.White : Color.FromArgb(30, 55, 95);
                using var bgBrush = new SolidBrush(bg);
                e.Graphics.FillRectangle(bgBrush, e.Bounds);
                // separator line on right edge
                using var sep = new Pen(Color.FromArgb(145, 175, 210));
                e.Graphics.DrawLine(sep, e.Bounds.Right - 1, e.Bounds.Top + 4,
                                         e.Bounds.Right - 1, e.Bounds.Bottom - 4);
                // blue underline for unselected tabs
                if (!sel)
                {
                    using var ul = new Pen(Color.SteelBlue, 2);
                    e.Graphics.DrawLine(ul, e.Bounds.Left, e.Bounds.Bottom - 1,
                                            e.Bounds.Right - 1, e.Bounds.Bottom - 1);
                }
                var font = sel ? new Font(tc.Font, FontStyle.Bold) : tc.Font;
                using var fgBrush = new SolidBrush(fg);
                var fmt = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisCharacter
                };
                e.Graphics.DrawString(pg.Text.Trim(), font, fgBrush,
                    new RectangleF(e.Bounds.X + 1, e.Bounds.Y,
                                   e.Bounds.Width - 2, e.Bounds.Height - (sel ? 0 : 2)), fmt);
                if (sel) font.Dispose();
            };
            tabs.TabPages.Add(CreateDashboardTab());
            tabs.TabPages.Add(CreateMenuTab());
            tabs.TabPages.Add(CreateProductsTab());
            tabs.TabPages.Add(CreateShoppingTab());
            tabs.TabPages.Add(CreateWeeklyShoppingTab());
            tabs.TabPages.Add(CreateMonthlyShoppingTab());
            tabs.TabPages.Add(CreateRealPricesTab());

            Controls.Add(tabs);
            Controls.Add(header);

            ValidatePeriod();
            UpdateBudgetLabel();
        }

        // ═══════════════════════════════════════════════ TAB 1 — MENU

        private TabPage CreateMenuTab()
        {
            var tab = new TabPage("  Меню  ");

            dgvMenu = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToResizeRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9),
                GridColor = Color.LightSteelBlue,
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.SteelBlue,
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 9, FontStyle.Bold),
                    Alignment = DataGridViewContentAlignment.MiddleCenter
                },
                AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.AliceBlue },
                RowTemplate = { Height = 28 }
            };
            dgvMenu.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dgvMenu.ColumnHeadersHeight = 30;
            dgvMenu.EnableHeadersVisualStyles = false;

            dgvMenu.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Date", HeaderText = "День", Width = 155, AutoSizeMode = DataGridViewAutoSizeColumnMode.None });
            dgvMenu.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Breakfast", HeaderText = "Завтрак", FillWeight = 23 });
            dgvMenu.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Lunch", HeaderText = "Обед", FillWeight = 32 });
            dgvMenu.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Dinner", HeaderText = "Ужин", FillWeight = 30 });
            dgvMenu.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Snack", HeaderText = "Полдник", FillWeight = 15 });

            var calStyle = new DataGridViewCellStyle
            { Alignment = DataGridViewContentAlignment.MiddleRight, ForeColor = Color.DimGray };
            dgvMenu.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "CalBf",  HeaderText = "Ккал завтрак", Width = 88, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, ReadOnly = true, DefaultCellStyle = calStyle });
            dgvMenu.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "CalLn",  HeaderText = "Ккал обед",    Width = 80, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, ReadOnly = true, DefaultCellStyle = calStyle });
            dgvMenu.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "CalDn",  HeaderText = "Ккал ужин",    Width = 80, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, ReadOnly = true, DefaultCellStyle = calStyle });
            dgvMenu.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "CalDay", HeaderText = "Ккал/день",    Width = 82, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, ReadOnly = true,
              DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Font = new Font("Segoe UI", 9, FontStyle.Bold) } });
            dgvMenu.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "CalNorm", HeaderText = "Норма ккал",  Width = 85, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, ReadOnly = true,
              DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, ForeColor = Color.SteelBlue } });
            dgvMenu.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "DayCost", HeaderText = "~Стоим. грн", Width = 90, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, ReadOnly = true,
              DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, ForeColor = Color.DarkGreen } });

            foreach (DataGridViewColumn col in dgvMenu.Columns)
                col.SortMode = DataGridViewColumnSortMode.NotSortable;

            dgvMenu.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            dgvMenu.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;

            tab.Controls.Add(dgvMenu);
            return tab;
        }

        // ═══════════════════════════════════════════ TAB 2 — PRODUCTS

        private TabPage CreateProductsTab()
        {
            var tab = new TabPage("  Продукты  ");

            dgvProducts = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = true,
                AllowUserToDeleteRows = true,
                AllowUserToResizeRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9),
                GridColor = Color.LightSteelBlue,
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.SteelBlue,
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 9, FontStyle.Bold),
                    Alignment = DataGridViewContentAlignment.MiddleCenter
                },
                AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.AliceBlue },
                RowTemplate = { Height = 26 }
            };
            dgvProducts.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dgvProducts.ColumnHeadersHeight = 30;
            dgvProducts.EnableHeadersVisualStyles = false;

            dgvProducts.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "ProductName", HeaderText = "Продукт", FillWeight = 38 });
            dgvProducts.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Tier", HeaderText = "Уровень", Width = 70, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, ReadOnly = true,
              DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 8) } });

            var colFreq = new DataGridViewComboBoxColumn
            {
                Name = "Frequency", HeaderText = "Частота", Width = 110,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                FlatStyle = FlatStyle.Flat,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 8) }
            };
            colFreq.Items.AddRange("ежедневно", "еженедельно", "ежемесячно");
            dgvProducts.Columns.Add(colFreq);

            dgvProducts.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Unit", HeaderText = "Ед.", Width = 68, AutoSizeMode = DataGridViewAutoSizeColumnMode.None });
            dgvProducts.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Price", HeaderText = "Цена (грн)", Width = 95, AutoSizeMode = DataGridViewAutoSizeColumnMode.None });
            dgvProducts.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "RealPrice", HeaderText = "Реал. цена", Width = 90,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None, ReadOnly = true,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight,
                    Font = new Font("Segoe UI", 8)
                }
            });
            dgvProducts.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Qty", HeaderText = "Кол-во", Width = 80, AutoSizeMode = DataGridViewAutoSizeColumnMode.None });
            dgvProducts.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "PackInfo", HeaderText = "Упаковок", Width = 110,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                ReadOnly = true,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    ForeColor = Color.MidnightBlue,
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 8)
                }
            });
            dgvProducts.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Sum", HeaderText = "Сумма (грн)", Width = 105,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                ReadOnly = true,
                DefaultCellStyle = new DataGridViewCellStyle { ForeColor = Color.DarkGreen, Alignment = DataGridViewContentAlignment.MiddleRight }
            });
            dgvProducts.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Kcal", HeaderText = "Ккал/пер.", Width = 90,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                ReadOnly = true,
                DefaultCellStyle = new DataGridViewCellStyle { ForeColor = Color.DarkBlue, Alignment = DataGridViewContentAlignment.MiddleRight }
            });

            if (dgvProducts.Columns["Price"] != null) dgvProducts.Columns["Price"]!.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            if (dgvProducts.Columns["Qty"]   != null) dgvProducts.Columns["Qty"]!.DefaultCellStyle.Alignment   = DataGridViewContentAlignment.MiddleRight;

            foreach (DataGridViewColumn col in dgvProducts.Columns)
                col.SortMode = DataGridViewColumnSortMode.NotSortable;

            dgvProducts.CellValueChanged  += DgvProducts_CellValueChanged;
            dgvProducts.RowsRemoved       += (_, _) => UpdateProductsTotal();
            dgvProducts.UserDeletingRow   += (s, e) => { if (e.Row.Tag?.ToString() == "total") e.Cancel = true; };
            dgvProducts.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (dgvProducts.IsCurrentCellDirty) dgvProducts.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            dgvProducts.DataError += (s, e) => e.Cancel = true;

            lblBudgetStatus = new Label
            {
                Dock = DockStyle.Bottom, Height = 30,
                TextAlign = ContentAlignment.MiddleRight,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Padding = new Padding(0, 0, 12, 0),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            tab.Controls.Add(dgvProducts);
            tab.Controls.Add(lblBudgetStatus);
            return tab;
        }

        // ═══════════════════════════════════════════ TAB 3 — SHOPPING

        private TabPage CreateShoppingTab()
        {
            var tab = new TabPage("  Покупки  ");

            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2, RowCount = 1,
                BackColor = Color.WhiteSmoke
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            var (panelToday,    gridToday,    titleToday)    = BuildShoppingPanel(Color.FromArgb(255, 251, 214));
            var (panelTomorrow, gridTomorrow, titleTomorrow) = BuildShoppingPanel(Color.FromArgb(214, 241, 214));

            dgvShoppingToday    = gridToday;
            dgvShoppingTomorrow = gridTomorrow;
            lblTodayTitle       = titleToday;
            lblTomorrowTitle    = titleTomorrow;

            table.Controls.Add(panelToday,    0, 0);
            table.Controls.Add(panelTomorrow, 1, 0);

            tab.Controls.Add(table);
            return tab;
        }

        private (Panel panel, DataGridView dgv, Label title) BuildShoppingPanel(Color titleColor)
        {
            var title = new Label
            {
                Dock = DockStyle.Top, Height = 38,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                BackColor = titleColor,
                ForeColor = Color.DarkSlateGray
            };

            var dgv = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9),
                GridColor = Color.LightSteelBlue,
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.SlateGray,
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 9, FontStyle.Bold),
                    Alignment = DataGridViewContentAlignment.MiddleCenter
                },
                AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(248, 248, 255) },
                RowTemplate = { Height = 26 }
            };
            dgv.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dgv.ColumnHeadersHeight = 30;
            dgv.EnableHeadersVisualStyles = false;

            var colCheck = new DataGridViewCheckBoxColumn
            { Name = "Done", HeaderText = "✓", Width = 34, AutoSizeMode = DataGridViewAutoSizeColumnMode.None };
            dgv.Columns.Add(colCheck);
            dgv.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Product", HeaderText = "Продукт", FillWeight = 50, ReadOnly = true });
            dgv.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Quantity", HeaderText = "Количество", FillWeight = 28, ReadOnly = true });
            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Price", HeaderText = "~Цена (грн)", FillWeight = 22, ReadOnly = true,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight }
            });
            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Paid", HeaderText = "Заплачено", FillWeight = 22, ReadOnly = false,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight,
                    BackColor = Color.FromArgb(255, 255, 230)
                }
            });

            foreach (DataGridViewColumn col in dgv.Columns)
                col.SortMode = DataGridViewColumnSortMode.NotSortable;

            dgv.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (dgv.IsCurrentCellDirty && dgv.CurrentCell is DataGridViewCheckBoxCell)
                    dgv.CommitEdit(DataGridViewDataErrorContexts.Commit);
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
                    style.ForeColor = done ? Color.Gray : Color.Empty;
                    style.Font      = done ? new Font(dgv.Font, FontStyle.Strikeout) : null;
                    dgv.InvalidateRow(e.RowIndex);
                }
                else if (e.ColumnIndex == paidIdx)
                {
                    UpdateShoppingPaidTotal(dgv);
                }
            };

            var btnCopy = new Button
            {
                Text = "📋", Width = 30, Height = 30,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat, BackColor = Color.Transparent,
                ForeColor = Color.DarkSlateGray, Font = new Font("Segoe UI", 10),
                Cursor = Cursors.Hand
            };
            btnCopy.FlatAppearance.BorderSize = 0;
            btnCopy.Click += (_, _) => CopyShoppingListToClipboard(dgv, title.Text);
            title.Controls.Add(btnCopy);
            title.Resize += (_, _) => btnCopy.Location = new Point(title.Width - 34, 2);

            var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4) };
            panel.Controls.Add(dgv);
            panel.Controls.Add(title);
            return (panel, dgv, title);
        }

        // ═══════════════════════════════════════════════════ LOAD DATA

        private void LoadData()
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
            UpdateBudgetLabel();
            ValidatePeriod();
        }

        // ── Meal plan ─────────────────────────────────────────────

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

                mealPlan.Add(new MealDay(
                    dateStr.Trim(),
                    ExtractMeal(meals, "Завтрак"),
                    ExtractMeal(meals, "Обед"),
                    ExtractMeal(meals, "Ужин")));
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

        // ── Prices ────────────────────────────────────────────────

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

        // ── Fill Tab 1 ────────────────────────────────────────────

        // Total kcal in the purchased product basket for the current period (whole family).
        private long ComputeProductsTotalKcal()
        {
            decimal ratio       = familyCount / 2m;
            int     periodDays  = (int)(periodEnd - periodStart).TotalDays + 1;
            decimal periodScale = periodDays / 30m;

            decimal comfortTotal = prices.Sum(p =>
                BaseQty.TryGetValue(p.Name, out decimal q)
                    ? p.Price * Math.Round(q * ratio * periodScale, 1) : 0);
            decimal budgetScale = comfortTotal > 0 && PeriodBudget < comfortTotal
                ? PeriodBudget / comfortTotal : 1m;

            long total = 0;
            foreach (var p in prices)
            {
                if (!BaseQty.TryGetValue(p.Name, out decimal q)) continue;
                decimal qty = Math.Round(q * ratio * periodScale * budgetScale, 1);
                if (qty <= 0) continue;
                if (UnitGrams.TryGetValue(p.Unit, out decimal gPerUnit) &&
                    CaloriesPer100g.TryGetValue(p.Name, out decimal calPer100))
                    total += (long)Math.Round(qty * gPerUnit * calPer100 / 100m);
            }
            return total;
        }

        private void FillMenuTab()
        {
            dgvMenu.Rows.Clear();
            if (mealPlan.Count == 0) return;

            int normPerPerson = calorieNorm;
            var culture  = new CultureInfo("ru-RU");
            decimal budget = PeriodBudget;

            // ── Pass 1: collect calories and raw ingredient-based costs ──
            var days = new List<(DateTime d, MealDay meal, int bf, int ln, int dn, int tot, decimal raw)>();
            for (DateTime d = periodStart; d <= periodEnd; d = d.AddDays(1))
            {
                MealDay? meal = FindMealForDate(d);
                if (meal == null) continue;
                int bf  = CalcMealCalories(meal.Breakfast);
                int ln  = CalcMealCalories(meal.Lunch);
                int dn  = CalcMealCalories(meal.Dinner);
                decimal raw = 0;
                foreach (string t in new[] { meal.Breakfast, meal.Lunch, meal.Dinner })
                    foreach (var (name, grams) in GetIngredients(t))
                        raw += EstimatePrice(name, grams * familyCount);
                days.Add((d, meal, bf, ln, dn, bf + ln + dn, raw));
            }
            if (days.Count == 0) return;

            // ── Pass 2: scale costs AND calories to their targets ──
            decimal perDay    = budget / days.Count;
            decimal totalRaw  = days.Sum(x => x.raw);
            long    rawCalSum = days.Sum(x => (long)x.tot);

            // Cost target = PeriodBudget
            var scaledCost = new decimal[days.Count];
            if (totalRaw > 0)
            {
                int     zeros = days.Count(x => x.raw == 0);
                decimal pool  = budget - zeros * perDay;
                decimal scale = pool > 0 ? pool / totalRaw : 1m;
                for (int i = 0; i < days.Count; i++)
                    scaledCost[i] = days[i].raw > 0
                        ? Math.Round(days[i].raw * scale, 0)
                        : Math.Round(perDay, 0);
            }
            else
            {
                for (int i = 0; i < days.Count; i++)
                    scaledCost[i] = Math.Round(perDay, 0);
            }
            scaledCost[^1] += budget - scaledCost.Sum();

            // Calorie target = Products basket kcal ÷ familyCount (per person for period)
            long targetKcal  = ComputeProductsTotalKcal() / Math.Max(1, familyCount);
            var scaledKcal   = new int[days.Count];
            if (rawCalSum > 0)
            {
                int  calZeros = days.Count(x => x.tot == 0);
                long perDayCal = targetKcal / days.Count;
                long calPool   = targetKcal - calZeros * perDayCal;
                decimal calScale = calPool > 0 ? (decimal)calPool / rawCalSum : 1m;
                for (int i = 0; i < days.Count; i++)
                    scaledKcal[i] = days[i].tot > 0
                        ? (int)Math.Round(days[i].tot * calScale)
                        : (int)perDayCal;
            }
            else
            {
                long perDayCal = targetKcal / days.Count;
                for (int i = 0; i < days.Count; i++) scaledKcal[i] = (int)perDayCal;
            }
            scaledKcal[^1] += (int)(targetKcal - scaledKcal.Sum());

            // ── Pass 3: fill grid ──
            long    totalCal  = 0;
            decimal totalCost = 0;

            for (int i = 0; i < days.Count; i++)
            {
                var (d, meal, bf, ln, dn, _, _) = days[i];
                decimal dayCost = scaledCost[i];
                int     dayCal  = scaledKcal[i];

                int rowIdx = dgvMenu.Rows.Add(
                    d.ToString("d MMMM (ddd)", culture),
                    meal.Breakfast, meal.Lunch, meal.Dinner, "",
                    bf     > 0 ? bf.ToString()     : "",
                    ln     > 0 ? ln.ToString()     : "",
                    dn     > 0 ? dn.ToString()     : "",
                    dayCal > 0 ? dayCal.ToString() : "",
                    normPerPerson.ToString(),
                    $"~{dayCost:F0}");

                if (dayCal > 0)
                {
                    var cell = dgvMenu.Rows[rowIdx].Cells["CalDay"];
                    cell.Style.ForeColor = dayCal >= normPerPerson ? Color.DarkGreen
                                        : dayCal >= 1500          ? Color.DarkOrange
                                                                  : Color.Crimson;
                }
                // Highlight meal cells where text exists but calories are 0 (dish not in IngMap)
                var rowRef = dgvMenu.Rows[rowIdx];
                var unknownBg = Color.FromArgb(255, 243, 205);
                if (!string.IsNullOrWhiteSpace(meal.Breakfast) && bf == 0)
                    rowRef.Cells["Breakfast"].Style.BackColor = unknownBg;
                if (!string.IsNullOrWhiteSpace(meal.Lunch)    && ln == 0)
                    rowRef.Cells["Lunch"].Style.BackColor = unknownBg;
                if (!string.IsNullOrWhiteSpace(meal.Dinner)   && dn == 0)
                    rowRef.Cells["Dinner"].Style.BackColor = unknownBg;
                totalCal  += dayCal;
                totalCost += dayCost;
            }

            // ── ИТОГО row ─────────────────────────────────────────
            // CalNorm total: norm per person × days (CalDay is per-person)
            long normTotal = (long)normPerPerson * days.Count;
            int totIdx = dgvMenu.Rows.Add(
                $"ИТОГО ({days.Count} дн.)", "", "", "", "", "", "", "",
                totalCal > 0 ? totalCal.ToString("N0") : "",
                normTotal.ToString("N0"),
                $"~{totalCost:F0}");

            var totRow = dgvMenu.Rows[totIdx];
            totRow.ReadOnly = true;
            totRow.DefaultCellStyle.BackColor = Color.FromArgb(35, 55, 85);
            totRow.DefaultCellStyle.ForeColor = Color.White;
            totRow.DefaultCellStyle.Font      = new Font("Segoe UI", 9, FontStyle.Bold);
            totRow.Cells["CalDay"].Style.ForeColor  = Color.FromArgb(150, 220, 150);
            totRow.Cells["DayCost"].Style.ForeColor = Color.FromArgb(130, 230, 130);
            totRow.Cells["CalNorm"].Style.ForeColor = Color.FromArgb(140, 190, 255);
        }

        private static int CalcMealCalories(string mealText)
        {
            decimal total = 0;
            foreach (var (name, grams) in GetIngredients(mealText))
                if (CaloriesPer100g.TryGetValue(name, out decimal cal))
                    total += grams * cal / 100m;
            return (int)Math.Round(total);
        }

        // ── Fill Tab 2 ────────────────────────────────────────────

        private static readonly Dictionary<string, decimal> CaloriesPer100g = new()
        {
            ["Хлеб"]               = 265,
            ["Батон"]              = 258,
            ["Макароны"]           = 338,
            ["Мука"]               = 334,
            ["Гречка"]             = 313,
            ["Рис"]                = 344,
            ["Говядина"]           = 187,
            ["Свинина"]            = 263,
            ["Курица"]             = 165,
            ["Филе куриное"]       = 113,
            ["Рыба мороженая"]     =  75,
            ["Молоко"]             =  52,
            ["Сыр"]                = 350,
            ["Сметана"]            = 206,
            ["Яйца"]               = 157,
            ["Масло сливочное"]    = 717,
            ["Масло подсолнечное"] = 884,
            ["Картофель"]          =  77,
            ["Капуста"]            =  27,
            ["Лук"]                =  41,
            ["Морковь"]            =  41,
            ["Яблоки"]             =  52,
            ["Чеснок"]             = 149,
            ["Творог"]             = 121,
            ["Овсянка"]            = 352,
        };

        private static readonly Dictionary<string, decimal> UnitGrams = new()
        {
            ["кг"]      = 1000m,
            ["л"]       = 1000m,
            ["500 г"]   =  500m,
            ["200 г"]   =  200m,
            ["десяток"] =  600m,
        };

        // ── Tier baskets ──────────────────────────────────────────

        private static readonly Dictionary<string, decimal> MinimumBasket = new()
        {
            ["Хлеб"] = 12, ["Батон"] = 4, ["Макароны"] = 3, ["Мука"] = 2,
            ["Гречка"] = 3, ["Рис"] = 3, ["Масло подсолнечное"] = 2,
            ["Картофель"] = 20, ["Капуста"] = 5, ["Лук"] = 5, ["Морковь"] = 5,
            ["Яйца"] = 4, ["Молоко"] = 8, ["Масло сливочное"] = 4,
        };

        private static readonly Dictionary<string, decimal> BasicBasket = new()
        {
            ["Хлеб"] = 18, ["Батон"] = 6, ["Макароны"] = 6, ["Мука"] = 3,
            ["Гречка"] = 5, ["Рис"] = 5, ["Масло подсолнечное"] = 2,
            ["Картофель"] = 24, ["Капуста"] = 7, ["Лук"] = 7, ["Морковь"] = 7,
            ["Яйца"] = 8, ["Молоко"] = 16, ["Масло сливочное"] = 6,
            ["Яблоки"] = 6, ["Сыр"] = 1.5m, ["Сметана"] = 1.5m,
            ["Курица"] = 6, ["Рыба мороженая"] = 4,
        };

        private static readonly Dictionary<string, decimal> BaseQty = new()
        {
            ["Хлеб"] = 28, ["Батон"] = 8, ["Макароны"] = 8, ["Мука"] = 4,
            ["Гречка"] = 8, ["Рис"] = 8, ["Говядина"] = 2, ["Свинина"] = 4,
            ["Курица"] = 12, ["Филе куриное"] = 4, ["Рыба мороженая"] = 8,
            ["Молоко"] = 28, ["Сыр"] = 4, ["Сметана"] = 3.2m, ["Яйца"] = 12,
            ["Масло сливочное"] = 8, ["Масло подсолнечное"] = 2,
            ["Картофель"] = 28, ["Капуста"] = 8, ["Лук"] = 8, ["Морковь"] = 8,
            ["Яблоки"] = 12, ["Чеснок"] = 1.2m,
        };

        // ── Package sizes (minimum purchase increment per product unit) ──
        // кг-products: in kg. л-products: in litres. Others: in units (packs/dozens).
        private static readonly Dictionary<string, decimal> PackStep = new()
        {
            ["Хлеб"]               = 0.6m,  // ~600г буханка
            ["Батон"]              = 1.0m,  // 1 батон (500г)
            ["Макароны"]           = 0.4m,  // 400г пачка
            ["Мука"]               = 1.0m,  // 1 кг пачка
            ["Гречка"]             = 1.0m,  // 1 кг пачка
            ["Рис"]                = 1.0m,  // 1 кг пачка
            ["Говядина"]           = 0.5m,  // от 500г
            ["Свинина"]            = 0.5m,
            ["Курица"]             = 0.5m,  // от полкурицы
            ["Филе куриное"]       = 0.5m,
            ["Рыба мороженая"]     = 0.5m,  // 500г упаковка
            ["Молоко"]             = 1.0m,  // 1л пакет
            ["Сыр"]                = 0.2m,  // 200г нарезка
            ["Сметана"]            = 0.4m,  // 400г стакан
            ["Яйца"]               = 1.0m,  // 1 десяток
            ["Масло сливочное"]    = 1.0m,  // 1 пачка (200г)
            ["Масло подсолнечное"] = 1.0m,  // 1л бутылка
            ["Картофель"]          = 1.0m,  // 1 кг
            ["Капуста"]            = 0.5m,  // пол-кочана мин.
            ["Лук"]                = 1.0m,  // 1 кг сетка
            ["Морковь"]            = 1.0m,  // 1 кг пучок
            ["Яблоки"]             = 1.0m,  // 1 кг
            ["Чеснок"]             = 0.1m,  // 100г головка
        };

        // Rounds qty up to the nearest full pack. Returns qty unchanged if no pack rule.
        private static decimal SnapToPack(string name, decimal qty)
        {
            if (!PackStep.TryGetValue(name, out decimal step) || step <= 0 || qty <= 0) return qty;
            return Math.Ceiling(qty / step) * step;
        }

        // Human-readable pack count: "9 × 400г", "4 пач.", "6 л" etc.
        private static string FormatPackInfo(string name, string unit, decimal qty)
        {
            if (!PackStep.TryGetValue(name, out decimal step) || step <= 0 || qty <= 0) return "";
            int packs = (int)Math.Ceiling(qty / step);
            if (packs <= 0) return "";
            return unit switch
            {
                "кг" when step < 1m => $"{packs} × {(int)(step * 1000)}г",
                "кг"               => $"{packs} кг",
                "л"                => $"{packs} л",
                _                  => $"{packs} уп.",  // 200г, 500г, десяток
            };
        }

        private decimal CalcTierBudget(Dictionary<string, decimal> basket)
        {
            if (prices.Count == 0) return 0;
            decimal ratio = familyCount / 2m;
            decimal total = 0;
            foreach (var p in prices)
                if (basket.TryGetValue(p.Name, out decimal qty))
                    total += p.Price * Math.Round(qty * ratio, 1);
            return Math.Round(total, 0);
        }

        private void FillProductsTab()
        {
            dgvProducts.Rows.Clear();
            decimal ratio = familyCount / 2m;
            int periodDays = (int)(periodEnd - periodStart).TotalDays + 1;
            decimal periodScale  = periodDays / 30m;
            decimal periodBudget = PeriodBudget;

            decimal comfortTotal = prices.Sum(p =>
                BaseQty.TryGetValue(p.Name, out decimal q)
                    ? p.Price * Math.Round(q * ratio * periodScale, 1)
                    : 0);
            decimal budgetScale = comfortTotal > 0 && periodBudget < comfortTotal
                ? periodBudget / comfortTotal
                : 1m;

            // ── Floor-first allocation ────────────────────────────────────────
            // Phase 1: give every item floor(rawQty/step) packs.
            //   Math proof: sum(price × floor(raw/step) × step) ≤ sum(price × raw) = periodBudget
            //   → total is ALWAYS within budget, every item gets at least its floor share.
            // Phase 2: greedily add +1 pack (floor→ceil) to items where we rounded down,
            //   starting from cheapest pack, until the leftover runs out.

            // Collect per-item data
            var itemData = prices.Select(p => {
                decimal bq = BaseQty.TryGetValue(p.Name, out decimal q)
                    ? Math.Round(q * ratio * periodScale, 1) : 0;
                decimal rq = bq > 0 ? Math.Round(bq * budgetScale, 2) : 0;
                bool hasPack = PackStep.TryGetValue(p.Name, out decimal st) && st > 0 && rq > 0;
                int fp = hasPack ? (int)Math.Floor(rq / st)   : 0;
                int cp = hasPack ? (int)Math.Ceiling(rq / st) : 0;
                return (p, step: hasPack ? st : 0m, rq, fp, cp);
            }).ToList();

            // Phase 1: assign floor packs
            int[] packs = itemData.Select(x => x.fp).ToArray();
            decimal floorCost = itemData.Select((x, i) =>
                x.step > 0 ? Math.Round(x.p.Price * packs[i] * x.step, 2) : 0m).Sum();
            decimal leftover = periodBudget - floorCost;

            // Phase 2: add +1 pack (cheapest first) where floor < ceil
            var upgradeOrder = itemData
                .Select((x, i) => (i, packCost: x.step > 0 ? x.p.Price * x.step : decimal.MaxValue, x))
                .Where(t => t.x.step > 0 && t.x.fp < t.x.cp)
                .OrderBy(t => t.packCost)
                .ToList();

            foreach (var (i, packCost, _) in upgradeOrder)
            {
                decimal cost = Math.Round(packCost, 2);
                if (cost <= leftover) { packs[i]++; leftover -= cost; }
            }

            // Build allocated dictionary
            var allocated = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < itemData.Count; i++)
            {
                var (p, step, rq, _, _) = itemData[i];
                decimal qty = step > 0 ? packs[i] * step
                                       : (leftover > 0
                                          ? Math.Min(rq, Math.Floor(leftover / p.Price * 100m) / 100m)
                                          : 0m);
                if (step == 0 && qty > 0) leftover -= Math.Round(p.Price * qty, 2);
                allocated[p.Name] = qty;
            }

            // Pass 2 — display in original JSON order
            foreach (var p in prices)
            {
                decimal qty = allocated.GetValueOrDefault(p.Name, 0);
                decimal sum = qty > 0 ? Math.Round(p.Price * qty, 2) : 0;
                int kcal = 0;
                if (qty > 0
                    && UnitGrams.TryGetValue(p.Unit, out decimal gPerUnit)
                    && CaloriesPer100g.TryGetValue(p.Name, out decimal calPer100))
                    kcal = (int)Math.Round(qty * gPerUnit * calPer100 / 100m);

                bool isMin   = MinimumBasket.ContainsKey(p.Name);
                bool isBasic = !isMin && BasicBasket.ContainsKey(p.Name);
                string tierText  = isMin ? "Минимум" : isBasic ? "Базовый" : "Комфорт";
                Color  tierColor = isMin ? Color.Crimson : isBasic ? Color.DarkOrange : Color.DarkGreen;
                Color  rowBg     = isMin ? Color.FromArgb(255, 235, 235)
                                 : isBasic ? Color.FromArgb(255, 252, 220)
                                           : Color.FromArgb(235, 255, 235);

                realPriceData.TryGetValue(p.Name, out var rp);
                var    rpMap  = priceMappings.FirstOrDefault(m2 => m2.AppProduct == p.Name);
                decimal mult  = rpMap?.Multiplier is > 0 ? rpMap.Multiplier : 1.0m;
                // Convert per-Excel-unit price back to per-app-unit for comparison with "Наша цена"
                decimal realP = rp?.LastPrice > 0 ? rp.LastPrice / mult
                              : rp?.Avg30d    > 0 ? rp.Avg30d    / mult
                              : rp?.Avg90d    > 0 ? rp.Avg90d    / mult : 0;
                string realPStr = realP > 0 ? realP.ToString("F2") : "";

                int rowIdx = dgvProducts.Rows.Add(
                    p.Name, tierText, p.Frequency, p.Unit,
                    p.Price.ToString("F2"),
                    realPStr,
                    qty > 0 ? qty.ToString("F2") : "",
                    FormatPackInfo(p.Name, p.Unit, qty),
                    sum > 0 ? sum.ToString("F2") : "",
                    kcal > 0 ? kcal.ToString("N0") : "");

                var row = dgvProducts.Rows[rowIdx];
                row.DefaultCellStyle.BackColor    = rowBg;
                row.Cells["Tier"].Style.ForeColor = tierColor;
                row.Cells["Tier"].Style.Font      = new Font("Segoe UI", 8, FontStyle.Bold);

                if (realP > 0)
                {
                    decimal diff = (realP - p.Price) / p.Price;
                    row.Cells["RealPrice"].Style.ForeColor =
                        diff < -0.05m ? Color.DarkGreen :
                        diff >  0.05m ? Color.Crimson   : Color.DarkOrange;
                }
            }

            // ── ИТОГО row ─────────────────────────────────────────
            int totIdx = dgvProducts.Rows.Add("ИТОГО", "", "", "", "", "", "", "");
            var totRow = dgvProducts.Rows[totIdx];
            totRow.Tag = "total";
            totRow.ReadOnly = true;
            totRow.DefaultCellStyle.BackColor = Color.FromArgb(35, 55, 85);
            totRow.DefaultCellStyle.ForeColor = Color.White;
            totRow.DefaultCellStyle.Font      = new Font("Segoe UI", 9, FontStyle.Bold);
            totRow.Cells["Sum"].Style.ForeColor  = Color.FromArgb(130, 230, 130);
            totRow.Cells["Kcal"].Style.ForeColor = Color.FromArgb(180, 220, 255);
            totRow.Cells["Sum"].Style.Alignment  = DataGridViewContentAlignment.MiddleRight;
            totRow.Cells["Kcal"].Style.Alignment = DataGridViewContentAlignment.MiddleRight;

            UpdateProductsTotal();
        }

        private void DgvProducts_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var row = dgvProducts.Rows[e.RowIndex];
            if (row.IsNewRow) return;

            string? colName = dgvProducts.Columns[e.ColumnIndex]?.Name;

            // Frequency change: update prices list and save to JSON
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

            // Only react to Price or Qty edits
            if (colName != "Price" && colName != "Qty") return;

            if (!decimal.TryParse(row.Cells["Price"].Value?.ToString(), out decimal price) || price <= 0 ||
                !decimal.TryParse(row.Cells["Qty"].Value?.ToString(),   out decimal qty)   || qty < 0)
            {
                row.Cells["Sum"].Value      = "";
                row.Cells["Kcal"].Value     = "";
                row.Cells["PackInfo"].Value = "";
                UpdateProductsTotal();
                return;
            }

            string productName = row.Cells["ProductName"].Value?.ToString() ?? "";
            string unit        = row.Cells["Unit"].Value?.ToString()        ?? "";

            // Budget headroom for this row
            decimal otherTotal = ComputeTotalExcluding(e.RowIndex);
            decimal maxThisRow = Math.Max(0, PeriodBudget - otherTotal);

            // Snap to package size: prefer rounding up; fall back to rounding down if budget exceeded
            if (PackStep.TryGetValue(productName, out decimal step) && step > 0 && qty > 0)
            {
                decimal snapUp   = Math.Ceiling(qty / step) * step;
                decimal snapDown = Math.Floor  (qty / step) * step;
                qty = (price * snapUp <= maxThisRow) ? snapUp
                    : (snapDown > 0 ? snapDown : qty);
            }

            // Hard budget cap (safety net after snapping)
            decimal propSum = Math.Round(price * qty, 2);
            if (propSum > maxThisRow)
            {
                qty     = Math.Floor(maxThisRow / price * 100m) / 100m;
                propSum = Math.Round(price * qty, 2);
            }

            // Write back Qty if snapping changed it
            string qtyStr = qty.ToString("F2");
            if (row.Cells["Qty"].Value?.ToString() != qtyStr)
            {
                dgvProducts.CellValueChanged -= DgvProducts_CellValueChanged;
                row.Cells["Qty"].Value = qtyStr;
                dgvProducts.CellValueChanged += DgvProducts_CellValueChanged;
            }

            row.Cells["Sum"].Value      = propSum.ToString("F2");
            row.Cells["PackInfo"].Value = FormatPackInfo(productName, unit, qty);

            if (UnitGrams.TryGetValue(unit, out decimal gPerUnit) &&
                CaloriesPer100g.TryGetValue(productName, out decimal calPer100))
                row.Cells["Kcal"].Value = ((int)Math.Round(qty * gPerUnit * calPer100 / 100m)).ToString("N0");
            else
                row.Cells["Kcal"].Value = "";

            UpdateProductsTotal();
        }

        private decimal ComputeTotalExcluding(int excludeRowIndex)
        {
            decimal total = 0;
            foreach (DataGridViewRow row in dgvProducts.Rows)
            {
                if (row.IsNewRow || row.Index == excludeRowIndex || row.Tag?.ToString() == "total") continue;
                if (decimal.TryParse(row.Cells["Sum"].Value?.ToString(), out decimal s)) total += s;
            }
            return total;
        }

        private void UpdateProductsTotal()
        {
            decimal total     = 0;
            long    totalKcal = 0;
            DataGridViewRow? totRow = null;
            foreach (DataGridViewRow row in dgvProducts.Rows)
            {
                if (row.IsNewRow) continue;
                if (row.Tag?.ToString() == "total") { totRow = row; continue; }
                if (decimal.TryParse(row.Cells["Sum"].Value?.ToString(), out decimal s)) total += s;
                string rawK = new string((row.Cells["Kcal"].Value?.ToString() ?? "")
                    .Where(char.IsDigit).ToArray());
                if (long.TryParse(rawK, out long k)) totalKcal += k;
            }

            // Write totals into the ИТОГО row
            if (totRow != null)
            {
                totRow.Cells["Sum"].Value  = total > 0 ? total.ToString("F2") : "";
                totRow.Cells["Kcal"].Value = totalKcal > 0 ? totalKcal.ToString("N0") : "";
            }

            decimal budget    = PeriodBudget;
            decimal remaining = budget - total;
            decimal overPct   = total > budget ? (total - budget) / budget * 100m : 0;

            if (total <= budget)
            {
                lblBudgetStatus.Text      = $"  ✓ Итого: {total:N0} грн  |  Бюджет: {budget:N0} грн  |  Остаток: {remaining:N0} грн";
                lblBudgetStatus.ForeColor = Color.DarkGreen;
            }
            else if (overPct < 5m)
            {
                lblBudgetStatus.Text      = $"  📦 Итого: {total:N0} грн  |  Бюджет: {budget:N0} грн  |  +{-remaining:N0} грн (округл. до упаковок)";
                lblBudgetStatus.ForeColor = Color.DarkOrange;
            }
            else
            {
                lblBudgetStatus.Text      = $"  ⚠ Итого: {total:N0} грн  |  Бюджет: {budget:N0} грн  |  Превышение на {-remaining:N0} грн!";
                lblBudgetStatus.ForeColor = Color.Crimson;
            }
        }

        // ── Fill Tab 3 ────────────────────────────────────────────

        private void FillShoppingTab()
        {
            DateTime d1 = periodStart;
            DateTime d2 = periodStart.AddDays(1);
            bool isToday = d1.Date == DateTime.Today;
            FillShoppingDay(d1, dgvShoppingToday,    lblTodayTitle,    isToday ? "Сегодня"          : "1-й день периода");
            FillShoppingDay(d2, dgvShoppingTomorrow, lblTomorrowTitle, isToday ? "Завтра"            : "2-й день периода");
        }

        private void FillShoppingDay(DateTime date, DataGridView dgv, Label title, string prefix)
        {
            var culture = new CultureInfo("ru-RU");
            title.Text  = $"{prefix} — {date.ToString("dddd, d MMMM", culture)}";
            dgv.Rows.Clear();

            MealDay? meal = FindMealForDate(date);
            if (meal == null)
            {
                dgv.Rows.Add(false, "(нет данных для этой даты)", "", "");
                return;
            }

            var agg = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            foreach (string mealText in new[] { meal.Breakfast, meal.Lunch, meal.Dinner })
                foreach (var (name, grams) in GetIngredients(mealText))
                {
                    decimal scaled = grams * familyCount;
                    if (agg.ContainsKey(name)) agg[name] += scaled;
                    else agg[name] = scaled;
                }

            if (!agg.ContainsKey("Молоко")) agg["Молоко"] = 250m * familyCount;

            var items = agg.Select(kv => (name: kv.Key, grams: kv.Value)).OrderBy(x => x.name).ToList();
            decimal dayTotal = 0;
            foreach (var (name, grams) in items)
            {
                string qty  = grams >= 1000 ? $"{grams / 1000:F2} кг" : $"{(int)grams} г";
                decimal est = EstimatePrice(name, grams);
                dayTotal += est;
                dgv.Rows.Add(false, name, qty, est > 0 ? $"~{est:F0}" : "");
            }

            int totIdx = dgv.Rows.Add(false, "ИТОГО", "", dayTotal > 0 ? $"~{dayTotal:F0}" : "", "");
            var totRow = dgv.Rows[totIdx];
            totRow.Tag = "total";
            totRow.ReadOnly = true;
            totRow.Cells["Done"].ReadOnly = true;
            totRow.DefaultCellStyle.BackColor = Color.FromArgb(35, 55, 85);
            totRow.DefaultCellStyle.ForeColor = Color.White;
            totRow.DefaultCellStyle.Font      = new Font(dgv.Font, FontStyle.Bold);
            totRow.Cells["Price"].Style.ForeColor = Color.FromArgb(160, 160, 160);
            totRow.Cells["Paid"].Style.ForeColor  = Color.FromArgb(130, 230, 130);
            totRow.Cells["Paid"].Style.BackColor  = Color.FromArgb(35, 55, 85);

            // Tag grid for paid-data persistence and restore saved values
            string dateKey = date.ToString("yyyy-MM-dd");
            dgv.Tag = $"daily:{dateKey}";
            RestorePaidValues(dgv, "daily", dateKey);
        }

        // ═══════════════════════ TAB 4 — WEEKLY SHOPPING
        private TabPage CreateWeeklyShoppingTab()
        {
            var tab = new TabPage("  Покупки на неделю  ");

            lblWeeklyTitle = new Label
            {
                Dock = DockStyle.Top, Height = 36,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                BackColor = Color.FromArgb(214, 234, 255),
                ForeColor = Color.DarkSlateGray
            };
            lblWeeklyInfo = new Label
            {
                Dock = DockStyle.Bottom, Height = 30,
                TextAlign = ContentAlignment.MiddleRight,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Padding = new Padding(0, 0, 12, 0),
                BackColor = Color.FromArgb(220, 240, 255),
                BorderStyle = BorderStyle.FixedSingle
            };

            dgvShoppingWeekly = BuildPeriodicShoppingGrid();
            void AddWeeklyCopyBtn()
            {
                var b = new Button { Text = "📋", Width = 30, Height = 30, Anchor = AnchorStyles.Top | AnchorStyles.Right,
                    FlatStyle = FlatStyle.Flat, BackColor = Color.Transparent, ForeColor = Color.DarkSlateGray,
                    Font = new Font("Segoe UI", 10), Cursor = Cursors.Hand };
                b.FlatAppearance.BorderSize = 0;
                b.Click += (_, _) => CopyShoppingListToClipboard(dgvShoppingWeekly, lblWeeklyTitle.Text);
                lblWeeklyTitle.Controls.Add(b);
                lblWeeklyTitle.Resize += (_, _) => b.Location = new Point(lblWeeklyTitle.Width - 34, 3);
            }
            AddWeeklyCopyBtn();
            tab.Controls.Add(dgvShoppingWeekly);
            tab.Controls.Add(lblWeeklyTitle);
            tab.Controls.Add(lblWeeklyInfo);
            return tab;
        }

        // ═══════════════════════ TAB 5 — MONTHLY SHOPPING
        private TabPage CreateMonthlyShoppingTab()
        {
            var tab = new TabPage("  Покупки на месяц  ");

            lblMonthlyTitle = new Label
            {
                Dock = DockStyle.Top, Height = 36,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                BackColor = Color.FromArgb(214, 245, 225),
                ForeColor = Color.DarkSlateGray
            };
            lblMonthlyInfo = new Label
            {
                Dock = DockStyle.Bottom, Height = 30,
                TextAlign = ContentAlignment.MiddleRight,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Padding = new Padding(0, 0, 12, 0),
                BackColor = Color.FromArgb(220, 255, 235),
                BorderStyle = BorderStyle.FixedSingle
            };

            dgvShoppingMonthly = BuildPeriodicShoppingGrid();
            void AddMonthlyCopyBtn()
            {
                var b = new Button { Text = "📋", Width = 30, Height = 30, Anchor = AnchorStyles.Top | AnchorStyles.Right,
                    FlatStyle = FlatStyle.Flat, BackColor = Color.Transparent, ForeColor = Color.DarkSlateGray,
                    Font = new Font("Segoe UI", 10), Cursor = Cursors.Hand };
                b.FlatAppearance.BorderSize = 0;
                b.Click += (_, _) => CopyShoppingListToClipboard(dgvShoppingMonthly, lblMonthlyTitle.Text);
                lblMonthlyTitle.Controls.Add(b);
                lblMonthlyTitle.Resize += (_, _) => b.Location = new Point(lblMonthlyTitle.Width - 34, 3);
            }
            AddMonthlyCopyBtn();
            tab.Controls.Add(dgvShoppingMonthly);
            tab.Controls.Add(lblMonthlyTitle);
            tab.Controls.Add(lblMonthlyInfo);
            return tab;
        }

        private DataGridView BuildPeriodicShoppingGrid()
        {
            var dgv = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9),
                GridColor = Color.LightSteelBlue,
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.SlateGray,
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 9, FontStyle.Bold),
                    Alignment = DataGridViewContentAlignment.MiddleCenter
                },
                AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(248, 248, 255) },
                RowTemplate = { Height = 28 }
            };
            dgv.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dgv.ColumnHeadersHeight = 30;
            dgv.EnableHeadersVisualStyles = false;

            var colCheck = new DataGridViewCheckBoxColumn
            { Name = "Done", HeaderText = "✓", Width = 34, AutoSizeMode = DataGridViewAutoSizeColumnMode.None };
            dgv.Columns.Add(colCheck);
            dgv.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Product", HeaderText = "Продукт", FillWeight = 40, ReadOnly = true });
            dgv.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Quantity", HeaderText = "Количество", FillWeight = 28, ReadOnly = true,
              DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Price", HeaderText = "~Цена (грн)", FillWeight = 20, ReadOnly = true,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight }
            });
            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Paid", HeaderText = "Заплачено", FillWeight = 20, ReadOnly = false,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight,
                    BackColor = Color.FromArgb(255, 255, 230)
                }
            });

            foreach (DataGridViewColumn col in dgv.Columns)
                col.SortMode = DataGridViewColumnSortMode.NotSortable;

            dgv.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (dgv.IsCurrentCellDirty && dgv.CurrentCell is DataGridViewCheckBoxCell)
                    dgv.CommitEdit(DataGridViewDataErrorContexts.Commit);
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
                    style.ForeColor = done ? Color.Gray : Color.Empty;
                    style.Font      = done ? new Font(dgv.Font, FontStyle.Strikeout) : null;
                    dgv.InvalidateRow(e.RowIndex);
                }
                else if (e.ColumnIndex == paidIdx)
                {
                    UpdateShoppingPaidTotal(dgv);
                }
            };

            return dgv;
        }

        private void FillWeeklyShoppingTab()
        {
            var culture = new System.Globalization.CultureInfo("ru-RU");
            DateTime weekEnd = periodStart.AddDays(6);
            lblWeeklyTitle.Text = $"Покупки на неделю:  {periodStart:d MMMM} — {weekEnd:d MMMM yyyy}  ({familyCount} чел.)";

            dgvShoppingWeekly.Rows.Clear();
            decimal ratio = familyCount / 2m;
            decimal total = 0;

            foreach (var p in prices.Where(p => p.Frequency == "еженедельно"))
            {
                if (!BaseQty.TryGetValue(p.Name, out decimal monthQty)) continue;
                decimal rawQty  = monthQty * ratio * (7m / 30m);
                decimal snapped = SnapToPack(p.Name, rawQty);
                if (snapped <= 0) snapped = Math.Round(rawQty, 2);
                var (qtyStr, cost) = GetRealQtyAndCost(p.Name, p.Unit, snapped, p.Price);
                total += cost;
                dgvShoppingWeekly.Rows.Add(false, p.Name, qtyStr, $"~{cost:F0}", "");
            }

            AddPeriodicTotalRow(dgvShoppingWeekly, total);
            string weekKey = periodStart.ToString("yyyy-MM-dd");
            dgvShoppingWeekly.Tag = $"weekly:{weekKey}";
            RestorePaidValues(dgvShoppingWeekly, "weekly", weekKey);
            decimal weekBudget = Math.Round(monthlyBudget / 4m, 0);
            string marker = total <= weekBudget ? "✓" : "⚠";
            lblWeeklyInfo.Text      = $"  {marker} Итого на неделю: ~{total:N0} грн  |  ~¼ бюджета: {weekBudget:N0} грн  ";
            lblWeeklyInfo.ForeColor = total <= weekBudget ? Color.DarkGreen : Color.Crimson;
        }

        private void FillMonthlyShoppingTab()
        {
            var culture = new System.Globalization.CultureInfo("ru-RU");
            DateTime monthEnd = periodStart.AddDays(29);
            lblMonthlyTitle.Text = $"Покупки на месяц:  {periodStart:d MMMM} — {monthEnd:d MMMM yyyy}  ({familyCount} чел.)";

            dgvShoppingMonthly.Rows.Clear();
            decimal ratio = familyCount / 2m;
            decimal total = 0;

            foreach (var p in prices.Where(p => p.Frequency == "ежемесячно"))
            {
                if (!BaseQty.TryGetValue(p.Name, out decimal monthQty)) continue;
                decimal rawQty  = monthQty * ratio;
                decimal snapped = SnapToPack(p.Name, rawQty);
                if (snapped <= 0) snapped = Math.Round(rawQty, 2);
                var (qtyStr, cost) = GetRealQtyAndCost(p.Name, p.Unit, snapped, p.Price);
                total += cost;
                dgvShoppingMonthly.Rows.Add(false, p.Name, qtyStr, $"~{cost:F0}", "");
            }

            AddPeriodicTotalRow(dgvShoppingMonthly, total);
            string monthKey = periodStart.ToString("yyyy-MM-dd");
            dgvShoppingMonthly.Tag = $"monthly:{monthKey}";
            RestorePaidValues(dgvShoppingMonthly, "monthly", monthKey);
            string marker = total <= monthlyBudget ? "✓" : "⚠";
            lblMonthlyInfo.Text      = $"  {marker} Итого на месяц: ~{total:N0} грн  |  Бюджет: {monthlyBudget:N0} грн  ";
            lblMonthlyInfo.ForeColor = total <= monthlyBudget ? Color.DarkGreen : Color.Crimson;
        }

        // Returns quantity string and cost using real last price when available.
        // Falls back to JSON price if no mapping/data.
        private (string qty, decimal cost) GetRealQtyAndCost(
            string appProduct, string appUnit, decimal snappedAppQty, decimal jsonPrice)
        {
            var mapping = priceMappings.FirstOrDefault(m => m.AppProduct == appProduct);
            realPriceData.TryGetValue(appProduct, out var rp);

            if (rp?.LastPrice > 0 && mapping != null
                && !string.IsNullOrEmpty(rp.LastUnit) && mapping.Multiplier > 0)
            {
                string exUnit = rp.LastUnit.TrimEnd('.');
                bool discrete = !exUnit.Equals("кг", StringComparison.OrdinalIgnoreCase)
                             && !exUnit.Equals("л",  StringComparison.OrdinalIgnoreCase);
                decimal raw    = snappedAppQty / mapping.Multiplier;
                decimal exQty  = discrete ? Math.Ceiling(raw) : Math.Round(raw, 2);
                decimal cost   = Math.Round(rp.LastPrice * exQty, 2);
                string  qtyStr = discrete
                    ? $"{exQty:F0} {exUnit}"
                    : $"{exQty:F2} {exUnit}";
                return (qtyStr, cost);
            }

            return (FormatShoppingQty(appProduct, appUnit, snappedAppQty),
                    Math.Round(jsonPrice * snappedAppQty, 2));
        }

        // ═══════════════════════════ TAB 6 — REAL PRICES (from HomeB Excel)

        private TabPage CreateRealPricesTab()
        {
            var tab = new TabPage("  Реальные цены  ");

            // ── Toolbar ──
            var toolbar = new Panel
            {
                Dock = DockStyle.Top, Height = 42,
                BackColor = Color.FromArgb(240, 244, 248),
                Padding = new Padding(6, 6, 6, 0)
            };

            var lblFile = new Label
            {
                Text = "Файл расходов: " + ExcelPriceService.ExcelFilePath,
                AutoSize = true, Location = new Point(8, 12),
                Font = new Font("Segoe UI", 8), ForeColor = Color.DimGray
            };

            var btnRefresh = new Button
            {
                Text = "⟳ Обновить из файла",
                Location = new Point(toolbar.Width - 175, 7),
                Width = 160, Height = 28,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = Color.SteelBlue, ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            btnRefresh.FlatAppearance.BorderColor = Color.FromArgb(40, 90, 150);
            btnRefresh.Click += (_, _) => RefreshFromExcel();

            toolbar.Controls.AddRange(new Control[] { lblFile, btnRefresh });

            // ── Status bar ──
            lblRealStatus = new Label
            {
                Dock = DockStyle.Bottom, Height = 28,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 8),
                Padding = new Padding(8, 0, 0, 0),
                BackColor = Color.FromArgb(235, 240, 248),
                BorderStyle = BorderStyle.FixedSingle,
                ForeColor = Color.DimGray
            };

            // ── Grid ──
            dgvRealPrices = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9),
                GridColor = Color.LightSteelBlue,
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.SteelBlue, ForeColor = Color.White,
                    Font = new Font("Segoe UI", 9, FontStyle.Bold),
                    Alignment = DataGridViewContentAlignment.MiddleCenter
                },
                AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(246, 249, 255) },
                RowTemplate = { Height = 26 }
            };
            dgvRealPrices.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dgvRealPrices.ColumnHeadersHeight = 30;
            dgvRealPrices.EnableHeadersVisualStyles = false;

            // Columns
            dgvRealPrices.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "RpApp", HeaderText = "Наш продукт", FillWeight = 22, ReadOnly = true });
            dgvRealPrices.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "RpUnit", HeaderText = "Ед.", Width = 62, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, ReadOnly = true,
              DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });

            colExcelName = new DataGridViewComboBoxColumn
            {
                Name = "RpExcel", HeaderText = "Название в расходах", FillWeight = 24,
                FlatStyle = FlatStyle.Flat,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleLeft }
            };
            colExcelName.Items.Add("");
            dgvRealPrices.Columns.Add(colExcelName);

            dgvRealPrices.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "RpExUnit", HeaderText = "Ед. Excel", Width = 72, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, ReadOnly = true,
              DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter, ForeColor = Color.DimGray } });
            dgvRealPrices.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "RpMult", HeaderText = "Коэф.", Width = 65, AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
              DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });

            dgvRealPrices.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "RpLast", HeaderText = "Посл. цена", Width = 90,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None, ReadOnly = true,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight,
                    Font = new Font("Segoe UI", 9, FontStyle.Bold),
                    BackColor = Color.FromArgb(240, 255, 240)
                }
            });
            dgvRealPrices.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "RpAvg30", HeaderText = "Ср. 30 дн.", Width = 88, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, ReadOnly = true,
              DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Font = new Font("Segoe UI", 9, FontStyle.Bold) } });
            dgvRealPrices.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "RpAvg90", HeaderText = "Ср. 90 дн.", Width = 88, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, ReadOnly = true,
              DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, ForeColor = Color.DimGray } });
            dgvRealPrices.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "RpOur", HeaderText = "Наша цена", Width = 85, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, ReadOnly = true,
              DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, ForeColor = Color.DimGray } });
            dgvRealPrices.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "RpDiff", HeaderText = "Разница", Width = 72, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, ReadOnly = true,
              DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 8, FontStyle.Bold) } });

            foreach (DataGridViewColumn col in dgvRealPrices.Columns)
                col.SortMode = DataGridViewColumnSortMode.NotSortable;

            dgvRealPrices.DataError += (s, e) => e.Cancel = true;
            dgvRealPrices.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (dgvRealPrices.IsCurrentCellDirty) dgvRealPrices.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            dgvRealPrices.CellValueChanged += DgvRealPrices_CellValueChanged;

            tab.Controls.Add(dgvRealPrices);
            tab.Controls.Add(toolbar);
            tab.Controls.Add(lblRealStatus);
            return tab;
        }

        private void FillRealPricesTab()
        {
            // Update ComboBox items from latest excel names
            colExcelName.Items.Clear();
            colExcelName.Items.Add("");
            foreach (var n in excelNames) colExcelName.Items.Add(n);

            dgvRealPrices.CellValueChanged -= DgvRealPrices_CellValueChanged;
            dgvRealPrices.Rows.Clear();

            foreach (var p in prices)
            {
                var m = priceMappings.FirstOrDefault(x => x.AppProduct == p.Name)
                        ?? new PriceMapping { AppProduct = p.Name };

                realPriceData.TryGetValue(p.Name, out var rp);
                decimal lastP = rp?.LastPrice ?? 0;
                decimal avg30 = rp?.Avg30d    ?? 0;
                decimal avg90 = rp?.Avg90d    ?? 0;
                string exUnit = rp?.LastUnit  ?? "";

                // "Наша цена" в тех же единицах, что Excel: price_per_app_unit × multiplier
                decimal ourInExcel = m.Multiplier > 0 ? Math.Round(p.Price * m.Multiplier, 2) : p.Price;

                string diffStr = "";
                Color  diffColor = Color.DimGray;
                if (lastP > 0 && ourInExcel > 0)
                {
                    decimal diff = (lastP - ourInExcel) / ourInExcel * 100m;
                    diffStr   = $"{diff:+0.0;-0.0}%";
                    diffColor = diff < -5m ? Color.DarkGreen : diff > 5m ? Color.Crimson : Color.DarkOrange;
                }

                int ri = dgvRealPrices.Rows.Add(
                    p.Name, p.Unit,
                    m.ExcelName,
                    exUnit,
                    m.Multiplier.ToString("F2"),
                    lastP > 0 ? lastP.ToString("F2") : "",
                    avg30 > 0 ? avg30.ToString("F2") : "",
                    avg90 > 0 ? avg90.ToString("F2") : "",
                    ourInExcel.ToString("F2"),
                    diffStr);

                if (diffStr != "")
                {
                    dgvRealPrices.Rows[ri].Cells["RpDiff"].Style.ForeColor = diffColor;
                    dgvRealPrices.Rows[ri].Cells["RpLast"].Style.ForeColor = diffColor;
                }
            }

            dgvRealPrices.CellValueChanged += DgvRealPrices_CellValueChanged;

            // Status bar
            int mapped = priceMappings.Count(m => !string.IsNullOrWhiteSpace(m.ExcelName));
            int total  = priceMappings.Count;
            int loaded = excelPurchases.Count;
            bool fileOk = System.IO.File.Exists(ExcelPriceService.ExcelFilePath);
            lblRealStatus.Text = fileOk
                ? $"  Загружено {loaded} записей из файла расходов  |  Сопоставлено: {mapped} из {total} продуктов"
                : $"  ⚠ Файл не найден: {ExcelPriceService.ExcelFilePath}";
            lblRealStatus.ForeColor = fileOk ? Color.DimGray : Color.Crimson;
        }

        private void DgvRealPrices_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
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

                // Auto-detect multiplier from Excel unit vs app unit
                if (!string.IsNullOrEmpty(newName))
                {
                    string appUnit = prices.Find(p => p.Name == appProduct)?.Unit ?? "";
                    string exUnit  = excelPurchases
                        .Where(p => p.Name.Equals(newName, StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(p => p.Date)
                        .FirstOrDefault()?.Unit ?? "";
                    decimal mult = ExcelPriceService.DefaultMultiplier(exUnit, appUnit);
                    mapping.Multiplier = mult;

                    dgvRealPrices.CellValueChanged -= DgvRealPrices_CellValueChanged;
                    row.Cells["RpMult"].Value = mult.ToString("F2");
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
            excelPurchases = ExcelPriceService.ReadPurchases();
            excelNames     = ExcelPriceService.GetDistinctNames(excelPurchases);
            realPriceData  = ExcelPriceService.ComputeRealPrices(priceMappings, excelPurchases);
            FillRealPricesTab();
            FillProductsTab();
        }

        private void LoadRealPrices()
        {
            priceMappings  = ExcelPriceService.LoadMappings();
            excelPurchases = ExcelPriceService.ReadPurchases();
            excelNames     = ExcelPriceService.GetDistinctNames(excelPurchases);

            // Ensure every app product has a mapping entry (auto-match if missing)
            foreach (var p in prices)
            {
                if (priceMappings.Any(m => m.AppProduct == p.Name)) continue;

                string? matched = ExcelPriceService.AutoMatch(p.Name, excelNames);
                string exUnit = matched != null
                    ? excelPurchases.Where(x => x.Name.Equals(matched, StringComparison.OrdinalIgnoreCase))
                                    .OrderByDescending(x => x.Date).FirstOrDefault()?.Unit ?? ""
                    : "";
                priceMappings.Add(new PriceMapping
                {
                    AppProduct = p.Name,
                    ExcelName  = matched ?? "",
                    Multiplier = matched != null
                        ? ExcelPriceService.DefaultMultiplier(exUnit, p.Unit)
                        : 1.0m
                });
            }

            // Re-match entries that were saved without an ExcelName (empty → try again now)
            bool changed = false;
            foreach (var m in priceMappings.Where(m => string.IsNullOrEmpty(m.ExcelName)))
            {
                string? matched = ExcelPriceService.AutoMatch(m.AppProduct, excelNames);
                if (matched == null) continue;
                m.ExcelName = matched;
                string appUnit = prices.Find(p => p.Name == m.AppProduct)?.Unit ?? "";
                string exUnit  = excelPurchases
                    .Where(x => x.Name.Equals(matched, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(x => x.Date).FirstOrDefault()?.Unit ?? "";
                m.Multiplier = ExcelPriceService.DefaultMultiplier(exUnit, appUnit);
                changed = true;
            }

            realPriceData = ExcelPriceService.ComputeRealPrices(priceMappings, excelPurchases);
            if (changed) ExcelPriceService.SaveMappings(priceMappings);
        }

        // Human-readable quantity label with pack rounding
        private static string FormatShoppingQty(string name, string unit, decimal qty)
        {
            if (qty <= 0) return "";
            if (!PackStep.TryGetValue(name, out decimal step) || step <= 0)
                return $"{qty:F2} {unit}";
            int packs = (int)Math.Ceiling(qty / step);
            if (packs <= 0) return "";
            return unit switch
            {
                "кг" when step < 1m => $"{packs} × {(int)(step * 1000)}г",
                "кг"                => $"{packs} кг",
                "л"                 => $"{packs} л",
                "200 г"             => $"{packs} × 200г",
                "500 г"             => $"{packs} × 500г",
                "десяток"           => $"{packs} дес.",
                _                   => $"{packs} уп.",
            };
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

        // ── Ingredient map ────────────────────────────────────────

        private static readonly List<(string keyword, (string name, decimal grams)[] ingredients)> IngMap = new()
        {
            ("овсянка",            new[] { ("Овсянка",        80m), ("Яблоки",  100m) }),
            ("яйца варён",         new[] { ("Яйца",           60m), ("Хлеб",   100m), ("Масло сливочное", 15m) }),
            ("яичница",            new[] { ("Яйца",           90m), ("Масло сливочное", 8m) }),
            ("омлет",              new[] { ("Яйца",          100m), ("Молоко",  50m), ("Масло сливочное", 8m) }),
            ("хлеб с маслом",      new[] { ("Хлеб",          100m), ("Масло сливочное", 25m) }),
            ("творог со сметан",   new[] { ("Творог",        150m), ("Сметана", 40m) }),
            ("творог с яг",        new[] { ("Творог",        150m) }),
            ("творог",             new[] { ("Творог",        150m) }),
            ("рисовая каша",       new[] { ("Рис",            60m), ("Молоко", 200m), ("Масло сливочное", 8m) }),
            ("молочная каша",      new[] { ("Рис",            50m), ("Молоко", 250m), ("Масло сливочное", 8m) }),
            ("гречневая каша",     new[] { ("Гречка",         80m), ("Молоко", 150m) }),
            ("манная каша",        new[] { ("Мука",           50m), ("Молоко", 250m) }),
            ("манка",              new[] { ("Мука",           50m), ("Молоко", 200m) }),
            ("сырники",            new[] { ("Творог",        150m), ("Яйца",   25m), ("Мука", 25m), ("Сметана", 30m) }),
            ("запеканка",          new[] { ("Творог",        150m), ("Яйца",   30m), ("Мука", 20m), ("Сметана", 30m) }),
            ("бутерброды с сыром", new[] { ("Хлеб",          120m), ("Сыр",    60m) }),
            ("вареники",           new[] { ("Мука",          100m), ("Картофель", 150m), ("Лук", 30m), ("Масло сливочное", 10m) }),
            ("деруны",             new[] { ("Картофель",     200m), ("Мука",   20m), ("Яйца", 25m), ("Сметана", 30m) }),
            ("драники",            new[] { ("Картофель",     200m), ("Мука",   20m), ("Яйца", 25m), ("Сметана", 30m) }),
            ("пельмени",           new[] { ("Свинина",       120m), ("Мука",   80m), ("Лук",  20m) }),
            ("рыбный суп",          new[] { ("Картофель",      100m), ("Морковь",  30m), ("Лук", 25m), ("Рыба мороженая", 120m) }),
            ("борщ",                new[] { ("Капуста",        150m), ("Картофель", 100m), ("Морковь", 50m), ("Лук", 40m), ("Свинина", 80m) }),
            ("суп с чечевиц",       new[] { ("Картофель",       80m), ("Морковь",  30m), ("Лук", 25m), ("Курица", 70m) }),
            ("куриный суп",         new[] { ("Картофель",       80m), ("Морковь",  30m), ("Лук", 25m), ("Курица", 100m) }),
            ("суп-пюре из тыквы",   new[] { ("Картофель",       50m), ("Лук",     30m), ("Сметана", 30m) }),
            ("гречневый суп",       new[] { ("Гречка",          50m), ("Морковь",  30m), ("Лук", 25m) }),
            ("картофельный суп",    new[] { ("Картофель",      150m), ("Морковь",  40m), ("Лук", 30m) }),
            ("молочный суп",        new[] { ("Молоко",         300m), ("Макароны", 50m) }),
            ("вермишелевый суп",    new[] { ("Макароны",        50m), ("Морковь",  30m), ("Лук", 25m) }),
            ("щи",                  new[] { ("Капуста",        120m), ("Картофель", 80m), ("Морковь", 30m), ("Лук", 25m) }),
            ("рассольник",          new[] { ("Картофель",       80m), ("Морковь",  30m), ("Лук", 25m), ("Рис", 20m) }),
            ("харчо",               new[] { ("Рис",             50m), ("Говядина", 80m), ("Лук", 30m), ("Морковь", 20m) }),
            ("суп",                 new[] { ("Картофель",      100m), ("Морковь",  30m), ("Лук", 25m) }),
            ("гречка с куриц",     new[] { ("Гречка",         80m), ("Курица", 120m) }),
            ("гречка",             new[] { ("Гречка",         80m) }),
            ("рис с овощ",         new[] { ("Рис",            80m), ("Морковь", 50m), ("Лук",    25m) }),
            ("рис с куриц",        new[] { ("Рис",            80m), ("Курица", 100m) }),
            ("рис",                new[] { ("Рис",            80m) }),
            ("картофельное пюре",  new[] { ("Картофель",     200m), ("Молоко",  70m), ("Масло сливочное", 12m) }),
            ("жареная картошк",    new[] { ("Картофель",     250m), ("Масло подсолнечное", 15m), ("Лук", 40m) }),
            ("жареный картоф",     new[] { ("Картофель",     250m), ("Масло подсолнечное", 15m), ("Лук", 40m) }),
            ("варёный картоф",     new[] { ("Картофель",     250m), ("Масло сливочное", 15m) }),
            ("вареный картоф",     new[] { ("Картофель",     250m), ("Масло сливочное", 15m) }),
            ("тушёная капуста",    new[] { ("Капуста",       200m), ("Морковь", 40m), ("Лук", 30m), ("Масло подсолнечное", 15m) }),
            ("голубцы",            new[] { ("Рис",            60m), ("Свинина", 100m), ("Капуста", 150m), ("Морковь", 30m), ("Лук", 25m) }),
            ("тефтели",            new[] { ("Свинина",       100m), ("Рис",  40m), ("Лук", 25m) }),
            ("биточки",            new[] { ("Свинина",       100m), ("Рис",  40m), ("Лук", 25m) }),
            ("фрикадельки",        new[] { ("Свинина",        80m), ("Рис",  30m), ("Лук", 20m) }),
            ("зразы",              new[] { ("Говядина",      120m), ("Яйца", 25m), ("Лук", 30m) }),
            ("винегрет",           new[] { ("Картофель",      80m), ("Морковь", 40m), ("Лук", 20m) }),
            ("тушён",              new[] { ("Картофель",     100m), ("Морковь", 50m), ("Капуста", 80m), ("Лук", 30m) }),
            ("котлет",             new[] { ("Свинина",       130m) }),
            ("рыба",               new[] { ("Рыба мороженая", 150m) }),
            ("запечённая курица",  new[] { ("Курица",        200m) }),
            ("курица",             new[] { ("Курица",        150m) }),
            ("макароны с соусом",  new[] { ("Макароны",      100m), ("Сыр",    40m) }),
            ("макарон",            new[] { ("Макароны",      100m) }),
            ("вермишель",          new[] { ("Макароны",      100m), ("Масло сливочное", 10m) }),
            ("лапша",              new[] { ("Макароны",      100m), ("Масло сливочное", 10m) }),
            ("плов",               new[] { ("Рис",           120m), ("Курица", 150m), ("Морковь", 80m), ("Лук", 40m) }),
            ("овощное рагу",       new[] { ("Картофель",     100m), ("Морковь", 50m), ("Капуста", 80m), ("Лук", 30m) }),
            ("пицца",              new[] { ("Мука",          100m), ("Сыр",    60m) }),
            ("овощи на гриле",     new[] { ("Картофель",     100m), ("Морковь", 50m), ("Лук",    40m) }),
            ("овощи",              new[] { ("Картофель",      80m), ("Морковь", 30m), ("Лук",    25m) }),
            ("сметан",             new[] { ("Сметана",        50m) }),
            ("сыр",                new[] { ("Сыр",            50m) }),
            ("хлеб",               new[] { ("Хлеб",          100m) }),
        };

        private static IEnumerable<(string name, decimal grams)> GetIngredients(string mealText)
        {
            if (string.IsNullOrEmpty(mealText)) yield break;
            string lower = mealText.ToLowerInvariant();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (kw, items) in IngMap)
            {
                if (!lower.Contains(kw)) continue;
                foreach (var (name, g) in items)
                    if (seen.Add(name))
                        yield return (name, g);
            }
        }

        private static void CopyShoppingListToClipboard(DataGridView dgv, string title)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(title);
            sb.AppendLine(new string('─', 40));
            foreach (DataGridViewRow row in dgv.Rows)
            {
                if (row.IsNewRow) continue;
                bool isTotal = row.Tag?.ToString() == "total";
                bool done    = row.Cells["Done"].Value is true;
                string prod  = row.Cells["Product"].Value?.ToString()  ?? "";
                string qty   = row.Cells["Quantity"]?.Value?.ToString() ?? "";
                string price = row.Cells["Price"].Value?.ToString()    ?? "";
                string paid  = row.Cells["Paid"]?.Value?.ToString()    ?? "";
                if (isTotal)
                {
                    sb.AppendLine(new string('─', 40));
                    sb.Append($"ИТОГО: {price}");
                    if (!string.IsNullOrEmpty(paid)) sb.Append($"  |  Заплачено: {paid}");
                    sb.AppendLine();
                }
                else
                {
                    string check = done ? "☑" : "☐";
                    sb.Append($"{check} {prod}");
                    if (!string.IsNullOrEmpty(qty)) sb.Append($": {qty}");
                    if (!string.IsNullOrEmpty(price)) sb.Append($"  ({price})");
                    if (!string.IsNullOrEmpty(paid) && paid != "0") sb.Append($"  ✓{paid}");
                    sb.AppendLine();
                }
            }
            try { Clipboard.SetText(sb.ToString()); } catch { }
        }

        private static void AddPeriodicTotalRow(DataGridView dgv, decimal estimatedTotal)
        {
            int ti = dgv.Rows.Add(false, "ИТОГО", "", estimatedTotal > 0 ? $"~{estimatedTotal:F0}" : "", "");
            var tr = dgv.Rows[ti];
            tr.Tag = "total";
            tr.ReadOnly = true;
            tr.DefaultCellStyle.BackColor = Color.FromArgb(35, 55, 85);
            tr.DefaultCellStyle.ForeColor = Color.White;
            tr.DefaultCellStyle.Font      = new Font(dgv.Font, FontStyle.Bold);
            tr.Cells["Price"].Style.ForeColor = Color.FromArgb(160, 160, 160);
            tr.Cells["Paid"].Style.ForeColor  = Color.FromArgb(130, 230, 130);
            tr.Cells["Paid"].Style.BackColor  = Color.FromArgb(35, 55, 85);
        }

        private void UpdateShoppingPaidTotal(DataGridView dgv)
        {
            decimal total = 0;
            DataGridViewRow? totRow = null;
            var amounts = new Dictionary<string, decimal>();
            foreach (DataGridViewRow row in dgv.Rows)
            {
                if (row.IsNewRow) continue;
                if (row.Tag?.ToString() == "total") { totRow = row; continue; }
                string prod = row.Cells["Product"]?.Value?.ToString() ?? "";
                if (decimal.TryParse(row.Cells["Paid"]?.Value?.ToString(), out decimal v) && v > 0)
                {
                    total += v;
                    if (!string.IsNullOrEmpty(prod)) amounts[prod] = v;
                }
            }
            if (totRow != null)
                totRow.Cells["Paid"].Value = total > 0 ? total.ToString("F0") : "";

            // Persist paid amounts keyed by grid tag ("type:dateKey")
            if (dgv.Tag is string tag && tag.Contains(':'))
            {
                var parts = tag.Split(':', 2);
                string type = parts[0], dateKey = parts[1];
                if (!paidData.ContainsKey(type)) paidData[type] = new();
                paidData[type][dateKey] = amounts;
                SavePaidData();
            }
        }

        private decimal EstimatePrice(string ingredient, decimal totalGrams)
        {
            var p = prices.Find(x => x.Name.Equals(ingredient, StringComparison.OrdinalIgnoreCase));
            if (p == null) return 0;
            return Math.Round(p.Price * totalGrams / 1000m, 1);
        }

        // ═══════════════════════════════════════════ TAB 0 — DASHBOARD

        private static readonly (string title, Color header, Color bg)[] CardThemes =
        {
            ("ЗАВТРАК", Color.FromArgb(244, 162,  97), Color.FromArgb(255, 249, 240)),
            ("ОБЕД",    Color.FromArgb(199,  91,  58), Color.FromArgb(255, 245, 242)),
            ("УЖИН",    Color.FromArgb( 69, 123, 157), Color.FromArgb(240, 246, 252)),
            ("ПОЛДНИК", Color.FromArgb( 82, 183, 136), Color.FromArgb(240, 253, 247)),
        };

        private static readonly string[] SearchEngines =
        {
            "https://www.google.com/search?q={0}",
            "https://www.bing.com/search?q={0}",
            "https://www.youtube.com/results?search_query={0}",
        };

        private TabPage CreateDashboardTab()
        {
            var tab = new TabPage("  Сегодня  ");

            // ── Cards row ──
            var cardsTable = new TableLayoutPanel
            {
                Dock = DockStyle.Top, Height = 104,
                ColumnCount = 4, RowCount = 1,
                BackColor = Color.FromArgb(240, 242, 246),
                Padding = new Padding(6, 6, 6, 4)
            };
            for (int i = 0; i < 4; i++)
                cardsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            cardsTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            for (int i = 0; i < 4; i++)
                cardsTable.Controls.Add(CreateMealCard(i), i, 0);

            // ── Browser fills the rest ──
            var pnlBrowser = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            try
            {
                webView = new WebView2 { Dock = DockStyle.Fill };
                pnlBrowser.Controls.Add(webView);
            }
            catch
            {
                pnlBrowser.Controls.Add(new Label
                {
                    Dock = DockStyle.Fill,
                    Text = "Браузер недоступен. Установите Microsoft Edge WebView2 Runtime.",
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 10), ForeColor = Color.Gray
                });
            }

            tab.Controls.Add(pnlBrowser);
            tab.Controls.Add(cardsTable);
            return tab;
        }

        private static Button MakeEngineBtn(string text, Color bg, int x, int y) =>
            new Button
            {
                Text = text, Location = new Point(x, y),
                Width = 74, Height = 28,
                BackColor = bg, ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8, FontStyle.Bold)
            };

        private void SetEngineButtonsEnabled(bool enabled)
        {
            if (_btnSearchGoogle != null) _btnSearchGoogle.Enabled = enabled;
            if (_btnSearchBing   != null) _btnSearchBing.Enabled   = enabled;
            if (_btnSearchYT     != null) _btnSearchYT.Enabled     = enabled;
        }

        private Panel CreateMealCard(int index)
        {
            var (title, headerColor, bgColor) = CardThemes[index];

            var outer = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(4),
                BackColor = Color.Transparent
            };
            var border = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(2),
                BackColor = headerColor
            };
            // ── 3-row layout: [header + button] / [meal text] / [cost + calories] ──
            var tbl = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3, ColumnCount = 1,
                BackColor = bgColor,
                Margin = Padding.Empty, Padding = Padding.Empty
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));   // header
            tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));  // meal text
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));   // cost + cal

            // Row 0: title (left) + recipe button (right)
            var headerRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2, RowCount = 1,
                BackColor = headerColor,
                Margin = Padding.Empty, Padding = Padding.Empty
            };
            headerRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            headerRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96f));
            headerRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            var lblHeader = new Label
            {
                Text = title, Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = headerColor, ForeColor = Color.White,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Padding = new Padding(6, 0, 0, 0), Margin = Padding.Empty
            };

            int captured = index;
            btnCardRecipe[index] = new Button
            {
                Text = "Найти рецепт", Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(248, 248, 252),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                ForeColor = Color.FromArgb(45, 80, 140),
                Margin = new Padding(2, 3, 3, 3)
            };
            btnCardRecipe[index].FlatAppearance.BorderColor = Color.FromArgb(180, 190, 210);
            btnCardRecipe[index].Click += (_, _) => SearchRecipe(captured);

            headerRow.Controls.Add(lblHeader,             0, 0);
            headerRow.Controls.Add(btnCardRecipe[index],  1, 0);

            // Row 1: meal text
            lblCardMeal[index] = new Label
            {
                Text = "—", Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 9), ForeColor = Color.FromArgb(35, 40, 60),
                BackColor = Color.Transparent,
                Padding = new Padding(5, 2, 5, 2), Margin = Padding.Empty
            };

            // Row 2: cost (left) + calories (right)
            var bottomRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2, RowCount = 1,
                BackColor = bgColor,
                Margin = Padding.Empty, Padding = Padding.Empty
            };
            bottomRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            bottomRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            bottomRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            lblCardCost[index] = new Label
            {
                Text = "", Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 90, 60),
                BackColor = Color.Transparent,
                Padding = new Padding(6, 0, 0, 0), Margin = Padding.Empty
            };
            lblCardCalories[index] = new Label
            {
                Text = "", Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                Font = new Font("Segoe UI", 8), ForeColor = Color.Gray,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 0, 6, 0), Margin = Padding.Empty
            };

            bottomRow.Controls.Add(lblCardCost[index],      0, 0);
            bottomRow.Controls.Add(lblCardCalories[index],  1, 0);

            tbl.Controls.Add(headerRow,           0, 0);
            tbl.Controls.Add(lblCardMeal[index],  0, 1);
            tbl.Controls.Add(bottomRow,           0, 2);

            border.Controls.Add(tbl);
            outer.Controls.Add(border);
            return outer;
        }

        private void FillDashboardTab()
        {
            var culture = new CultureInfo("ru-RU");
            DateTime today = DateTime.Today;

            MealDay? meal = FindMealForDate(today);
            string[] meals = meal != null
                ? new[] { meal.Breakfast, meal.Lunch, meal.Dinner, "" }
                : new[] { "", "", "", "" };

            int     totalCal  = 0;
            decimal totalCost = 0;
            int     pDays     = Math.Max(1, (int)(periodEnd - periodStart).TotalDays + 1);
            decimal dayBudget = PeriodBudget / pDays;

            for (int i = 0; i < 4; i++)
            {
                string text = meals[i];
                lblCardMeal[i].Text = string.IsNullOrEmpty(text) ? "—" : text;

                if (!string.IsNullOrEmpty(text))
                {
                    int cal  = CalcMealCalories(text);
                    decimal cost = 0;
                    foreach (var (name, grams) in GetIngredients(text))
                        cost += EstimatePrice(name, grams * familyCount);
                    cost = Math.Round(cost, 0);

                    lblCardCalories[i].Text = cal  > 0 ? $"{cal} кКал" : "";
                    lblCardCost[i].Text     = cost > 0 ? $"~{cost:F0} грн" : "";
                    totalCal  += cal;
                    totalCost += cost;
                }
                else
                {
                    lblCardCalories[i].Text = "";
                    lblCardCost[i].Text     = "";
                }
                btnCardRecipe[i].Enabled = !string.IsNullOrEmpty(text);
            }

            string dateStr = today.ToString("ddd, d MMM", culture);
            lblDayDate.Text = meal != null
                ? $"{dateStr}  |  {totalCal} ккал  |  {totalCost:F0}/{dayBudget:F0} грн  |  {familyCount} чел."
                : $"{dateStr}  |  вне плана  |  {familyCount} чел.";
        }

        // ── Recipe search ─────────────────────────────────────────

        private void SearchRecipe(int cardIndex)
        {
            if (!webViewReady || webView?.CoreWebView2 == null) return;
            string text = lblCardMeal[cardIndex].Text;
            if (string.IsNullOrEmpty(text) || text == "—") return;

            _currentSearchDish = text.Split(new[] { '+', ';' }, 2)[0].Trim();
            SetEngineButtonsEnabled(true);
            SearchOnEngine(0); // Google by default
        }

        private void SearchOnEngine(int engineIndex)
        {
            if (!webViewReady || webView?.CoreWebView2 == null || string.IsNullOrEmpty(_currentSearchDish)) return;
            string query = Uri.EscapeDataString("рецепт " + _currentSearchDish);
            // YouTube works better with direct dish name
            if (engineIndex == 2) query = Uri.EscapeDataString(_currentSearchDish + " рецепт");
            webView.CoreWebView2.Navigate(string.Format(SearchEngines[engineIndex], query));
        }

        private async System.Threading.Tasks.Task InitWebViewAsync()
        {
            if (webView == null) return;
            try
            {
                await webView.EnsureCoreWebView2Async(null);
                webViewReady = true;

                // When a page fails to load, show a friendly fallback with alternative search links
                webView.CoreWebView2.NavigationCompleted += (s, e) =>
                {
                    if (!e.IsSuccess && !string.IsNullOrEmpty(_currentSearchDish))
                        webView.NavigateToString(BuildFallbackPage(_currentSearchDish));
                };

                // Auto-open recipe for current meal time
                TriggerAutoSearch();
            }
            catch (Exception ex)
            {
                webViewReady = false;
                if (webView.Parent is Panel p)
                {
                    p.Controls.Remove(webView);
                    p.Controls.Add(new Label
                    {
                        Dock = DockStyle.Fill,
                        Text = $"Браузер не инициализирован.\n{ex.Message}",
                        TextAlign = ContentAlignment.MiddleCenter,
                        Font = new Font("Segoe UI", 9), ForeColor = Color.Gray
                    });
                }
            }
        }

        private static string BuildWelcomePage() => @"<!DOCTYPE html>
<html lang='ru'><head><meta charset='utf-8'>
<style>
  body{margin:0;display:flex;align-items:center;justify-content:center;
       height:100vh;font-family:'Segoe UI',sans-serif;background:#f3f5fb;}
  .box{text-align:center;color:#5a6a8a;}
  h2{font-size:1.3em;margin-bottom:6px;font-weight:600;}
  p{font-size:.9em;color:#8898b4;}
</style></head><body>
<div class='box'>
  <h2>Нажмите «Найти рецепт» в карточке блюда</h2>
  <p>Поиск откроется на Google — работает без ограничений по региону</p>
</div></body></html>";

        // Picks the meal card matching the current hour and triggers its recipe search.
        // Falls back to welcome page if that card has no meal today.
        private void TriggerAutoSearch()
        {
            int hour = DateTime.Now.Hour;
            int cardIndex = (hour >= 5  && hour < 11) ? 0   // Завтрак
                          : (hour >= 11 && hour < 15) ? 1   // Обед
                          : (hour >= 15 && hour < 21) ? 2   // Ужин
                          :                             3;  // Полдник / ночь

            string text = lblCardMeal[cardIndex].Text;
            if (!string.IsNullOrEmpty(text) && text != "—")
                SearchRecipe(cardIndex);
            else
                webView?.NavigateToString(BuildWelcomePage());
        }

        private static string BuildFallbackPage(string dish)
        {
            string q    = Uri.EscapeDataString("рецепт " + dish);
            string qEn  = Uri.EscapeDataString(dish);
            // Simple inline HTML escaping (avoids extra using directive)
            string safe = dish.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
            return $@"<!DOCTYPE html>
<html lang='ru'><head><meta charset='utf-8'>
<style>
  body{{font-family:'Segoe UI',sans-serif;padding:28px 36px;background:#fff9f9;}}
  h3{{color:#c0392b;margin-bottom:10px;}}
  p{{color:#555;margin-bottom:16px;}}
  .links a{{display:inline-block;margin:6px 8px 6px 0;padding:9px 18px;
            border-radius:6px;color:#fff;text-decoration:none;font-weight:600;font-size:.92em;}}
  .g{{background:#4285f4;}} .b{{background:#0078d7;}}
  .y{{background:#e62117;}} .a{{background:#f07030;}}
  .links a:hover{{opacity:.85;}}
</style></head><body>
<h3>Страница не открылась</h3>
<p>Попробуйте найти рецепт <b>«{safe}»</b> на другом ресурсе:</p>
<div class='links'>
  <a class='g' href='https://www.google.com/search?q={q}'>🔍 Google</a>
  <a class='b' href='https://www.bing.com/search?q={q}'>🔍 Bing</a>
  <a class='y' href='https://www.youtube.com/results?search_query={qEn}+рецепт'>▶ YouTube</a>
  <a class='a' href='https://allrecipes.com/search?q={qEn}'>🍴 AllRecipes</a>
</div>
<p style='margin-top:20px;font-size:.82em;color:#aaa;'>
  На Google и Bing есть функция перевода страниц — нажмите «Translate» в адресной строке.
</p>
</body></html>";
        }

        // ═══════════════════════════════════════════════════ PERIOD

        private void OnPeriodChanged(object? sender, EventArgs e)
        {
            DateTime start = dtpPeriodStart.Value.Date;
            DateTime end   = dtpPeriodEnd.Value.Date;
            int days = end >= start ? (int)(end - start).TotalDays + 1 : 1;

            lblPeriodWarning.Text      = days < 7 ? $"⚠ {days} дн. — слишком коротко" : $"({days} дн.)";
            lblPeriodWarning.ForeColor = days < 7 ? Color.LightSalmon : Color.LightGreen;
        }

        private void BtnApplyPeriod_Click(object? sender, EventArgs e)
        {
            periodStart = dtpPeriodStart.Value.Date;
            periodEnd   = dtpPeriodEnd.Value.Date;

            if (periodEnd < periodStart)
            {
                periodEnd = periodStart;
                dtpPeriodEnd.ValueChanged -= OnPeriodChanged;
                dtpPeriodEnd.Value = periodEnd;
                dtpPeriodEnd.ValueChanged += OnPeriodChanged;
            }

            ValidatePeriod();
            UpdateBudgetLabel();
            FillMenuTab();
            FillProductsTab();
            FillShoppingTab();
            FillWeeklyShoppingTab();
            FillMonthlyShoppingTab();
        }

        private void ValidatePeriod()
        {
            int days = (int)(periodEnd - periodStart).TotalDays + 1;
            lblPeriodWarning.Text      = days < 7 ? $"⚠ {days} дн. — слишком коротко" : $"({days} дн.)";
            lblPeriodWarning.ForeColor = days < 7 ? Color.LightSalmon : Color.LightGreen;
        }

        // ═══════════════════════════════════════════════════ SETTINGS

        private void BtnSettings_Click(object? sender, EventArgs e)
        {
            using var dlg = new SettingsForm(monthlyBudget, familyCount, calorieNorm);
            if (dlg.ShowDialog() != DialogResult.OK) return;
            monthlyBudget = dlg.BudgetValue;
            familyCount   = dlg.FamilyCount;
            calorieNorm   = dlg.CalorieNorm;
            UpdateBudgetLabel();
            FillDashboardTab();
            FillProductsTab();
            FillShoppingTab();
            FillWeeklyShoppingTab();
            FillMonthlyShoppingTab();
            SaveSettings();
        }

        private void UpdateBudgetLabel()
        {
            int periodDays = (int)(periodEnd - periodStart).TotalDays + 1;
            decimal periodBudget = PeriodBudget;
            lblBudgetInfo.Text =
                $"Бюджет: {monthlyBudget:N0} грн/мес  |  Семья: {familyCount} чел.  |  " +
                $"Период {periodDays} дн.: {periodBudget:N0} грн  |  В день: {periodBudget / Math.Max(1, periodDays):N0} грн";

            decimal minCost   = CalcTierBudget(MinimumBasket);
            decimal basicCost = CalcTierBudget(BasicBasket);

            string tierLabel;
            Color  tierColor;
            if (minCost == 0)
            {
                tierLabel = "загрузка цен…";
                tierColor = Color.LightYellow;
            }
            else if (monthlyBudget < minCost)
            {
                tierLabel = $"❌ Недостаточно даже для выживания! Минимум: {minCost:N0} грн";
                tierColor = Color.Salmon;
            }
            else if (monthlyBudget < basicCost)
            {
                tierLabel = $"⚠ Базовый рацион не покрыт. Нужно ещё {basicCost - monthlyBudget:N0} грн";
                tierColor = Color.LightGoldenrodYellow;
            }
            else
            {
                decimal reserve = monthlyBudget - basicCost;
                tierLabel = $"✓ Полноценное питание. Резерв сверх базового: {reserve:N0} грн";
                tierColor = Color.LightGreen;
            }

            lblTierInfo.Text      = $"Минимум выживания: {minCost:N0} грн  |  Базовый рацион: {basicCost:N0} грн  |  {tierLabel}";
            lblTierInfo.ForeColor = tierColor;
        }

        private void SaveSettings()
        {
            string path = Path.Combine(AppDir, "settings.json");
            var obj = new { Budget = monthlyBudget, FamilyCount = familyCount, CalorieNorm = calorieNorm };
            File.WriteAllText(path, JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true }));
        }

        private static string PaidHistoryPath =>
            Path.Combine(AppDir, "paid_history.json");

        private void LoadPaidData()
        {
            if (!File.Exists(PaidHistoryPath)) return;
            try
            {
                var json = File.ReadAllText(PaidHistoryPath, System.Text.Encoding.UTF8);
                paidData = JsonSerializer.Deserialize<
                    Dictionary<string, Dictionary<string, Dictionary<string, decimal>>>>(json) ?? new();
            }
            catch { }
        }

        private void SavePaidData()
        {
            try
            {
                var json = JsonSerializer.Serialize(paidData, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });
                File.WriteAllText(PaidHistoryPath, json, System.Text.Encoding.UTF8);
            }
            catch { }
        }

        // Restores Paid column values from paidData and recalculates total.
        private void RestorePaidValues(DataGridView dgv, string sessionType, string dateKey)
        {
            if (!paidData.TryGetValue(sessionType, out var sessions)) return;
            if (!sessions.TryGetValue(dateKey, out var amounts)) return;
            foreach (DataGridViewRow row in dgv.Rows)
            {
                if (row.IsNewRow || row.Tag?.ToString() == "total") continue;
                string prod = row.Cells["Product"].Value?.ToString() ?? "";
                if (amounts.TryGetValue(prod, out decimal amt) && amt > 0)
                    row.Cells["Paid"].Value = amt.ToString("F0");
            }
            UpdateShoppingPaidTotal(dgv);
        }

        private void SavePrices()
        {
            string? path = FindDataFile("средними ценами.json");
            if (path == null) path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "средними ценами.json");
            var arr = prices.Select(p => new { name = p.Name, price = p.Price, unit = p.Unit, frequency = p.Frequency });
            var json = JsonSerializer.Serialize(new { prices = arr }, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
            File.WriteAllText(path, json, System.Text.Encoding.UTF8);
        }

        private void LoadSettings()
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

        // ═══════════════════════════════════════════════════ HELPERS

        private static string? FindDataFile(string name)
        {
            string[] candidates = {
                Path.Combine(AppDir, name),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, name),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", name),
                Path.Combine(@"C:\Users\User\Opus 4.6\Food\MenuApp", name)
            };
            return Array.Find(candidates, File.Exists);
        }
    }

    // ── Models ────────────────────────────────────────────────────

    record MealDay(string Date, string Breakfast, string Lunch, string Dinner);
    record PriceItem(string Name, decimal Price, string Unit, string Frequency = "еженедельно");

    public class SettingsData
    {
        public decimal Budget      { get; set; }
        public int     FamilyCount { get; set; }
    }
}
