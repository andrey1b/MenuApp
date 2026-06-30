using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfBtn  = System.Windows.Controls.Button;

namespace MenuApp;

public partial class MainWindow : Window
{
    // ══════════════════════════════════════════════════ ПОЛЯ

    private decimal monthlyBudget = 10000;
    private int familyCount  = 2;
    private int calorieNorm  = 2000;
    // Режим остатка бюджета: false (по умолчанию) — остаток пользователю; true — весь бюджет на меню (праздничные выходные)
    private bool fullBudgetMode = false;

    private DateTime periodStart;
    private DateTime periodEnd;

    private decimal PeriodBudget
    {
        get
        {
            int days = (int)(periodEnd - periodStart).TotalDays + 1;
            return Math.Min(monthlyBudget, Math.Round(monthlyBudget * days / 30m, 0));
        }
    }

    // WPF-карточки «Сегодня»
    private readonly TextBlock[]  _txMeal    = new TextBlock[4];
    private readonly TextBlock[]  _txCal     = new TextBlock[4];
    private readonly TextBlock[]  _txCost    = new TextBlock[4];
    private readonly WpfBtn[]     _btnRecipe = new WpfBtn[4];

    // WebView2
    private bool    webViewReady      = false;
    private string? _currentSearchDish;

    private static readonly (string title, string headerHex, string bgHex)[] CardThemesWpf =
    {
        ("ЗАВТРАК", "#78B47A", "#EEFCEE"),
        ("ОБЕД",    "#58965A", "#E6F6E6"),
        ("ПОЛДНИК", "#4A9B4D", "#E8F6E8"),
        ("УЖИН",    "#3E8741", "#DEF2DE"),
    };

    private static readonly string[] SearchEngines =
    {
        "https://www.google.com/search?q={0}",
        "https://www.bing.com/search?q={0}",
        "https://www.youtube.com/results?search_query={0}",
    };

    // ══════════════════════════════════════════════════ КОНСТРУКТОР

    public MainWindow()
    {
        InitializeComponent();
        EnsureDataFiles();
        ExcelPriceService.DataDirectory = AppDir;
        LoadSettings();
        LoadAiSettings();
        periodStart = DateTime.Today;
        periodEnd   = DateTime.Today.AddDays(30);
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        TxVersion.Text        = "v" + UpdateChecker.CurrentVersion;
        DtpStart.SelectedDate = periodStart;
        DtpEnd.SelectedDate   = periodEnd;

        InitMealCards();
        InitShoppingGrids();
        InitShoppingListTab();
        InitProductsGrid();
        InitRealPricesGrid();
        InitAiQuestionsTab();
        LoadData();
        PopulateShoppingList();   // список продуктов для вкладки «Составить список» (после загрузки prices)
        UpdateBudgetLabel();
        ValidatePeriod();
        BuildFoodExpenses();      // фактические расходы из «Денег» (read-only)

        await InitWebViewAsync();
        _ = UpdateChecker.CheckAsync(OnUpdateChecked);
    }

    // ══════════════════════════════════════════════════ РАСХОДЫ ИЗ «ДЕНЕГ» (read-only)

    // Строка для таблицы расходов (отформатированные поля).
    private sealed record FoodExpenseRow(string DateText, string Name, string Account, string AmountText);

    private void BuildFoodExpenses()
    {
        if (!HomeAccountingReader.IsAvailable)
        {
            lblFoodTotal.Text = "Расходы на продукты (из «Денег»)";
            lblFoodInfo.Text  = "Программа «Деньги» не установлена — фактические расходы недоступны.";
            dgFoodExpenses.ItemsSource = null;
            return;
        }

        var items = HomeAccountingReader.GetFoodExpenses();
        decimal total = HomeAccountingReader.Total(items);

        lblFoodTotal.Text = $"Потрачено на продукты: {total:N2} грн";

        var rows = items.Select(e =>
        {
            string name = !string.IsNullOrWhiteSpace(e.Subcategory) ? e.Subcategory : e.Category;
            if (!string.IsNullOrWhiteSpace(e.Note)) name += " — " + e.Note;
            return new FoodExpenseRow(FmtExpenseDate(e.Date), name, e.Account, e.Amount.ToString("N2"));
        }).ToList();

        dgFoodExpenses.ItemsSource = rows;
        lblFoodInfo.Text = rows.Count == 0
            ? "Записей нет. Расходы ведутся в программе «Деньги» (категория «Продукты питания»)."
            : $"Записей: {rows.Count}. Источник: «Деньги», только чтение.";
    }

    private void BtnFoodRefresh_Click(object sender, RoutedEventArgs e) => BuildFoodExpenses();

    private void BtnFoodOpenMoney_Click(object sender, RoutedEventArgs e) => HomeAccountingReader.OpenHomeAccounting();

    // Дата из «Денег» хранится как yyyy-MM-dd → показываем dd.MM.yyyy
    private static string FmtExpenseDate(string iso)
        => DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)
           ? dt.ToString("dd.MM.yyyy") : iso;

    // ══════════════════════════════════════════════════ КАРТОЧКИ «СЕГОДНЯ»

    private void InitMealCards()
    {
        for (int i = 0; i < 4; i++)
            CardsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        CardsGrid.RowDefinitions.Add(new RowDefinition());

        for (int i = 0; i < 4; i++)
        {
            var card = CreateMealCard(i);
            Grid.SetColumn(card, i);
            Grid.SetRow(card, 0);
            CardsGrid.Children.Add(card);
        }
    }

    private UIElement CreateMealCard(int index)
    {
        var (title, headerHex, bgHex) = CardThemesWpf[index];
        var headerBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(headerHex));
        var bgBrush     = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bgHex));

        var outer = new Border
        {
            Margin        = new Thickness(4),
            CornerRadius  = new CornerRadius(6),
            BorderBrush   = headerBrush,
            BorderThickness = new Thickness(1)
        };

        var innerGrid = new Grid { Background = bgBrush };
        innerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(44) });
        innerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        innerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) });

        // Ряд 0: заголовок + кнопка рецепта
        var headerGrid = new Grid { Background = headerBrush };
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(135) });
        Grid.SetRow(headerGrid, 0);

        var lblTitle = new TextBlock
        {
            Text              = title,
            Foreground        = Brushes.White,
            FontSize          = 15,
            FontWeight        = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(8, 0, 0, 0)
        };
        Grid.SetColumn(lblTitle, 0);

        int cap = index;
        _btnRecipe[index] = new WpfBtn
        {
            Content  = "Найти рецепт",
            Style    = (Style)FindResource("HdrBtn"),
            FontSize = 13,
            Margin   = new Thickness(3, 3, 4, 3)
        };
        _btnRecipe[index].Click += (_, _) => SearchRecipe(cap);
        Grid.SetColumn(_btnRecipe[index], 1);

        headerGrid.Children.Add(lblTitle);
        headerGrid.Children.Add(_btnRecipe[index]);

        // Ряд 1: текст блюда
        _txMeal[index] = new TextBlock
        {
            Text              = "—",
            FontSize          = 16,
            TextAlignment     = TextAlignment.Center,
            TextWrapping      = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground        = new SolidColorBrush(Color.FromRgb(35, 40, 60)),
            Padding           = new Thickness(6, 2, 6, 2)
        };
        Grid.SetRow(_txMeal[index], 1);

        // Ряд 2: цена + калории
        var bottomGrid = new Grid { Background = bgBrush };
        bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(bottomGrid, 2);

        _txCost[index] = new TextBlock
        {
            FontSize          = 15,
            FontWeight        = FontWeights.Bold,
            Foreground        = new SolidColorBrush(Color.FromRgb(40, 90, 60)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(8, 0, 0, 0)
        };
        Grid.SetColumn(_txCost[index], 0);

        _txCal[index] = new TextBlock
        {
            FontSize          = 14,
            Foreground        = Brushes.Gray,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment     = TextAlignment.Right,
            Margin            = new Thickness(0, 0, 8, 0)
        };
        Grid.SetColumn(_txCal[index], 1);

        bottomGrid.Children.Add(_txCost[index]);
        bottomGrid.Children.Add(_txCal[index]);

        innerGrid.Children.Add(headerGrid);
        innerGrid.Children.Add(_txMeal[index]);
        innerGrid.Children.Add(bottomGrid);

        outer.Child = innerGrid;
        return outer;
    }

    // ══════════════════════════════════════════════════ WEBVIEW2

    private async System.Threading.Tasks.Task InitWebViewAsync()
    {
        try
        {
            await WebBrowser.EnsureCoreWebView2Async(null);
            webViewReady = true;

            WebBrowser.CoreWebView2.NavigationCompleted += (s, e) =>
            {
                if (!e.IsSuccess && !string.IsNullOrEmpty(_currentSearchDish))
                    WebBrowser.NavigateToString(BuildFallbackPage(_currentSearchDish));
            };

            TriggerAutoSearch();
        }
        catch (Exception ex)
        {
            webViewReady = false;
            WebBrowser.Visibility = Visibility.Collapsed;
            TxWebError.Text       = $"Браузер не инициализирован.\n{ex.Message}";
            TxWebError.Visibility = Visibility.Visible;
        }
    }

    private void TriggerAutoSearch()
    {
        int hour = DateTime.Now.Hour;
        int idx  = (hour >= 5  && hour < 11) ? 0   // Завтрак
                 : (hour >= 11 && hour < 15) ? 1   // Обед
                 : (hour >= 15 && hour < 18) ? 2   // Полдник
                 : (hour >= 18 && hour < 22) ? 3   // Ужин
                 : 0;

        string text = _txMeal[idx].Text;
        if (!string.IsNullOrEmpty(text) && text != "—")
            SearchRecipe(idx);
        else
            WebBrowser.NavigateToString(BuildWelcomePage());
    }

    private void SearchRecipe(int cardIndex)
    {
        if (!webViewReady || WebBrowser?.CoreWebView2 == null) return;
        string text = _txMeal[cardIndex].Text;

        if (string.IsNullOrEmpty(text) || text == "—")
        {
            // Для Полдника без данных — показываем рецепты полдника
            if (cardIndex == 2)
            {
                _currentSearchDish = "полдник рецепты фрукты";
                SetEngineButtonsEnabled(true);
                SearchOnEngine(0);
            }
            return;
        }

        _currentSearchDish = text.Split(new[] { '+', ';' }, 2)[0].Trim();
        SetEngineButtonsEnabled(true);
        SearchOnEngine(0);
    }

    private void SearchOnEngine(int engineIndex)
    {
        if (!webViewReady || WebBrowser?.CoreWebView2 == null || string.IsNullOrEmpty(_currentSearchDish)) return;
        string query = Uri.EscapeDataString("рецепт " + _currentSearchDish);
        if (engineIndex == 2) query = Uri.EscapeDataString(_currentSearchDish + " рецепт");
        WebBrowser.CoreWebView2.Navigate(string.Format(SearchEngines[engineIndex], query));
    }

    private void SetEngineButtonsEnabled(bool enabled)
    {
        BtnGoogle.IsEnabled = enabled;
        BtnBing.IsEnabled   = enabled;
        BtnYT.IsEnabled     = enabled;
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

    private static string BuildFallbackPage(string dish)
    {
        string q    = Uri.EscapeDataString("рецепт " + dish);
        string qEn  = Uri.EscapeDataString(dish);
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
</div></body></html>";
    }

    // ══════════════════════════════════════════════════ ПЕРИОД

    private void DtpPeriod_Changed(object? sender, SelectionChangedEventArgs e)
    {
        DateTime start = DtpStart.SelectedDate ?? DateTime.Today;
        DateTime end   = DtpEnd.SelectedDate   ?? DateTime.Today;
        int days       = end >= start ? (int)(end - start).TotalDays + 1 : 1;
        TxPeriodWarn.Text       = days < 7 ? $"⚠ {days} дн. — слишком коротко" : $"({days} дн.)";
        TxPeriodWarn.Foreground = days < 7 ? Brushes.LightSalmon : Brushes.LightGreen;
    }

    private void ValidatePeriod()
    {
        int days = (int)(periodEnd - periodStart).TotalDays + 1;
        TxPeriodWarn.Text       = days < 7 ? $"⚠ {days} дн. — слишком коротко" : $"({days} дн.)";
        TxPeriodWarn.Foreground = days < 7 ? Brushes.LightSalmon : Brushes.LightGreen;
    }

    private void BtnApply_Click(object sender, RoutedEventArgs e) => ApplyPeriod();
    private void Btn7d_Click (object sender, RoutedEventArgs e)
    {
        DtpStart.SelectedDate = DateTime.Today;
        DtpEnd.SelectedDate   = DateTime.Today.AddDays(6);
        ApplyPeriod();
    }
    private void Btn30d_Click(object sender, RoutedEventArgs e)
    {
        DtpStart.SelectedDate = DateTime.Today;
        DtpEnd.SelectedDate   = DateTime.Today.AddDays(29);
        ApplyPeriod();
    }

    private void ApplyPeriod()
    {
        periodStart = DtpStart.SelectedDate ?? DateTime.Today;
        periodEnd   = DtpEnd.SelectedDate   ?? DateTime.Today;
        if (periodEnd < periodStart) { periodEnd = periodStart; DtpEnd.SelectedDate = periodEnd; }

        ValidatePeriod();
        UpdateBudgetLabel();
        SelectMealPlan();
        FillMenuTab();
        FillProductsTab();
        FillShoppingTab();
        FillWeeklyShoppingTab();
        FillMonthlyShoppingTab();
        FillDashboardTab();
    }

    // ══════════════════════════════════════════════════ БЮДЖЕТ

    internal void UpdateBudgetLabel()
    {
        int periodDays = (int)(periodEnd - periodStart).TotalDays + 1;
        decimal pb     = PeriodBudget;
        TxBudget.Text =
            $"Бюджет: {monthlyBudget:N0} грн/мес  |  Семья: {familyCount} чел.  |  " +
            $"Период {periodDays} дн.: {pb:N0} грн  |  В день в среднем: {pb / Math.Max(1, periodDays):N0} грн.";

        decimal minCost   = CalcTierBudget(MinimumBasket);
        decimal basicCost = CalcTierBudget(BasicBasket);

        string tierLabel;
        Brush  tierBrush;

        if (minCost == 0)
        {
            tierLabel = "загрузка цен…"; tierBrush = Brushes.LightYellow;
        }
        else if (monthlyBudget < minCost)
        {
            tierLabel = $"❌ Недостаточно даже для выживания! Минимум: {minCost:N0} грн";
            tierBrush = Brushes.Salmon;
        }
        else if (monthlyBudget < basicCost)
        {
            tierLabel = $"⚠ Базовый рацион не покрыт. Нужно ещё {basicCost - monthlyBudget:N0} грн";
            tierBrush = Brushes.LightGoldenrodYellow;
        }
        else
        {
            decimal reserve = monthlyBudget - basicCost;
            tierLabel = $"✓ Полноценное питание. Резерв сверх базового: {reserve:N0} грн";
            tierBrush = Brushes.LightGreen;
        }

        TxTier.Text       = $"Минимум выживания: {minCost:N0} грн  |  Базовый рацион: {basicCost:N0} грн  |  {tierLabel}";
        TxTier.Foreground = tierBrush;
    }

    // ══════════════════════════════════════════════════ ОБНОВЛЕНИЕ

    private void OnUpdateChecked(string? latest)
    {
        if (latest == null) return;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            TxUpdate.Text       = $"●  Доступно обновление v{latest} — скачать";
            TxUpdate.Visibility = Visibility.Visible;
        }));
    }

    private void TxUpdate_Click(object sender, MouseButtonEventArgs e)
    {
        Process.Start(new ProcessStartInfo { FileName = UpdateChecker.ReleasesUrl, UseShellExecute = true });
    }

    // ══════════════════════════════════════════════════ НАСТРОЙКИ

    private void BtnSettings_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SettingsWindow(monthlyBudget, familyCount, calorieNorm, fullBudgetMode) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        monthlyBudget  = dlg.BudgetValue;
        familyCount    = dlg.FamilyCount;
        calorieNorm    = dlg.CalorieNorm;
        fullBudgetMode = dlg.FullBudget;
        UpdateBudgetLabel();
        SelectMealPlan();
        FillMenuTab();
        FillDashboardTab();
        FillProductsTab();
        FillShoppingTab();
        FillWeeklyShoppingTab();
        FillMonthlyShoppingTab();
        SaveSettings();
    }

    // ══════════════════════════════════════════════════ ПОИСК РЕЦЕПТОВ

    private void BtnGoogle_Click(object sender, RoutedEventArgs e) => SearchOnEngine(0);
    private void BtnBing_Click  (object sender, RoutedEventArgs e) => SearchOnEngine(1);
    private void BtnYT_Click    (object sender, RoutedEventArgs e) => SearchOnEngine(2);
}
