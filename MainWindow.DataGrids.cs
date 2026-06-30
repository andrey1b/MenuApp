using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using SWC    = System.Windows.Controls;
using WMedia = System.Windows.Media;

namespace MenuApp;

// Частичный класс: WinForms-хосты, DataGridView, вся бизнес-логика
public partial class MainWindow
{
    // ══════════════════════════════════════════════════ ПОЛЯ ДАННЫХ


    // Списки покупок переведены на WPF DataGrid (dgShop* в MainWindow.xaml);
    // данные хранятся в коллекциях ShoppingRow.
    private readonly ObservableCollection<ShoppingRow> shopToday    = new();
    private readonly ObservableCollection<ShoppingRow> shopTomorrow = new();
    private readonly ObservableCollection<ShoppingRow> shopWeekly   = new();
    private readonly ObservableCollection<ShoppingRow> shopMonthly  = new();

    // Вкладка «Продукты» — WPF DataGrid dgProducts (MainWindow.xaml)
    private readonly ObservableCollection<ProductRow> products = new();

    // Вкладка «Реальные цены» — WPF DataGrid dgRealPrices (MainWindow.xaml)
    private readonly ObservableCollection<RealPriceRow> realPrices = new();

    private List<MealDay>   mealPlan         = new();   // активный план (выбран по бюджету)
    private List<MealDay>   mealPlanStandard = new();   // «Стандарт» — из файла 30_day_meal_plan.txt
    private string          mealTier         = "Стандарт";
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

    // ══════════════════════════════════════════════════ WPF-ГРИД «РЕАЛЬНЫЕ ЦЕНЫ»

    internal void InitRealPricesGrid()
    {
        dgRealPrices.ItemsSource = realPrices;
        lblRealFile.Text = "Файл расходов: " + ExcelPriceService.ExcelFilePath;
        btnRealRefresh.Click += (_, _) => RefreshFromExcel();

        dgRealPrices.CellEditEnding += (_, e) =>
        {
            if (e.EditAction != SWC.DataGridEditAction.Commit) return;
            if (e.Row?.Item is not RealPriceRow r) return;
            string? col = e.Column?.Header as string;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (col == "Название в расходах") OnRealExcelChanged(r);
                else if (col == "Коэф.")          OnRealMultChanged(r);
            }), System.Windows.Threading.DispatcherPriority.Background);
        };
    }

    // ══════════════════════════════════════════════════ WPF-ГРИД «ПРОДУКТЫ»

    internal void InitProductsGrid()
    {
        dgProducts.ItemsSource = products;
        colFreq.ItemsSource    = new[] { "ежедневно", "еженедельно", "ежемесячно" };

        // Строка ИТОГО не редактируется
        dgProducts.BeginningEdit += (_, e) =>
        {
            if (e.Row?.Item is ProductRow r && r.IsTotal) e.Cancel = true;
        };

        // После коммита правки — пересчёт (после применения биндинга)
        dgProducts.CellEditEnding += (_, e) =>
        {
            if (e.EditAction != SWC.DataGridEditAction.Commit) return;
            if (e.Row?.Item is not ProductRow r || r.IsTotal) return;
            string? col = e.Column?.Header as string;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (col == "Частота")                         OnProductFrequencyChanged(r);
                else if (col == "Цена (грн)" || col == "Кол-во") RecomputeProductRow(r);
            }), System.Windows.Threading.DispatcherPriority.Background);
        };
    }

    // ══════════════════════════════════════════════════ WPF-ГРИДЫ ПОКУПОК

    // Привязка коллекций и обработчиков к WPF-гридам списков покупок (вызывать после InitializeComponent)
    internal void InitShoppingGrids()
    {
        void Wire(SWC.DataGrid dg, SWC.Button copyBtn, Func<string> titleGetter)
        {
            // В строке ИТОГО редактирование запрещено
            dg.BeginningEdit += (_, e) =>
            {
                if (e.Row?.Item is ShoppingRow r && r.IsTotal) e.Cancel = true;
            };
            // После правки «Заплачено» — пересчёт итога и сохранение (после коммита биндинга)
            dg.CellEditEnding += (_, e) =>
            {
                if (e.EditAction == SWC.DataGridEditAction.Commit && (e.Column?.Header as string) == "Заплачено")
                    Dispatcher.BeginInvoke(new Action(() => UpdateShoppingPaidTotal(dg)),
                                           System.Windows.Threading.DispatcherPriority.Background);
            };
            copyBtn.Click += (_, _) => CopyShoppingListToClipboard(dg, titleGetter());
        }

        Wire(dgShopToday,    btnCopyToday,    () => lblTodayTitle.Text);
        Wire(dgShopTomorrow, btnCopyTomorrow, () => lblTomorrowTitle.Text);
        Wire(dgShopWeekly,   btnCopyWeekly,   () => lblWeeklyTitle.Text);
        Wire(dgShopMonthly,  btnCopyMonthly,  () => lblMonthlyTitle.Text);

        dgShopToday.ItemsSource    = shopToday;
        dgShopTomorrow.ItemsSource = shopTomorrow;
        dgShopWeekly.ItemsSource   = shopWeekly;
        dgShopMonthly.ItemsSource  = shopMonthly;
    }

    // ══════════════════════════════════════════════════ МЕНЮ
    // Вкладка «Меню» переведена на нативный WPF DataGrid (dgMenu в MainWindow.xaml).
    // Заполняется в FillMenuTab() через коллекцию MenuRow (см. MenuRow.cs).

    // ══════════════════════════════════════════════════ ПРОДУКТЫ
    // Переведена на нативный WPF DataGrid (dgProducts в MainWindow.xaml).
    // Привязка/обработчики — InitProductsGrid(); заполнение — FillProductsTab() через ProductRow.
    // ══════════════════════════════════════════════════ ПОКУПКИ / НЕДЕЛЯ / МЕСЯЦ
    // Переведены на нативные WPF DataGrid (dgShopToday/Tomorrow/Weekly/Monthly в
    // MainWindow.xaml). Привязка и обработчики — InitShoppingGrids(); заполнение —
    // FillShoppingDay / FillWeeklyShoppingTab / FillMonthlyShoppingTab через ShoppingRow.

    // ══════════════════════════════════════════════════ РЕАЛЬНЫЕ ЦЕНЫ
    // Переведена на нативный WPF DataGrid (dgRealPrices в MainWindow.xaml).
    // Привязка/обработчики — InitRealPricesGrid(); заполнение — FillRealPricesTab() через RealPriceRow.

    // ══════════════════════════════════════════════════ ЗАГРУЗКА ДАННЫХ

    internal void LoadData()
    {
        LoadMealPlan();
        LoadPrices();
        LoadRealPrices();
        LoadPaidData();
        SelectMealPlan();   // выбрать уровень рациона по бюджету
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
        mealPlanStandard.Clear();
        string? path = FindDataFile("30_day_meal_plan.txt");
        if (path == null) { mealPlan = mealPlanStandard; return; }
        foreach (string line in File.ReadAllLines(path, System.Text.Encoding.UTF8))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            int dash = line.IndexOf('—');
            if (dash < 0) continue;
            string dateStr = line[..dash].Trim().TrimEnd('.').TrimEnd('г').TrimEnd().TrimEnd('.');
            string meals   = line[(dash + 1)..].Trim();
            mealPlanStandard.Add(new MealDay(dateStr.Trim(), ExtractMeal(meals, "Завтрак"), ExtractMeal(meals, "Обед"), ExtractMeal(meals, "Полдник"), ExtractMeal(meals, "Ужин")));
        }
        mealPlan = mealPlanStandard;   // до выбора по бюджету
    }

    // ── Уровни рациона: Эконом / Стандарт / Премиум ──────────────
    // Эконом — крупы/овощи/картофель, минимум мяса; Премиум — мясо/рыба/сыр каждый день.
    // Все блюда распознаются IngMap. Дата не важна (FindMealForDate берёт фактическую дату).
    private static readonly List<MealDay> MealPlanEconomy = new()
    {
        new("", "Овсянка",                  "Картофельный суп",   "1 яблоко",   "Гречка с овощами"),
        new("", "Манная каша",              "Гречневый суп",      "1 банан",    "Картофельное пюре"),
        new("", "Рисовая каша",             "Щи",                 "1 яблоко",   "Тушёная капуста"),
        new("", "Молочная каша",            "Вермишелевый суп",   "1 груша",    "Отварные макароны"),
        new("", "Гречневая каша с молоком", "Суп-пюре из тыквы",  "1 яблоко",   "Жареная картошка"),
        new("", "Хлеб с маслом",            "Картофельный суп",   "1 банан",    "Овощное рагу"),
        new("", "Манная каша",              "Молочный суп",       "1 яблоко",   "Рис с овощами"),
    };

    private static readonly List<MealDay> MealPlanPremium = new()
    {
        new("", "Сырники со сметаной",            "Борщ",             "1 банан",  "Запечённая курица + картофельное пюре"),
        new("", "Омлет",                          "Куриный суп",      "1 яблоко", "Рыба на пару + овощи на гриле"),
        new("", "Бутерброды с сыром",             "Харчо",            "1 банан",  "Котлеты + картофельное пюре"),
        new("", "Запеканка творожная",            "Плов с курицей",   "1 груша",  "Голубцы"),
        new("", "Яичница",                        "Рыбный суп",       "1 яблоко", "Зразы"),
        new("", "Творог со сметаной",             "Суп с чечевицей",  "1 банан",  "Пельмени"),
        new("", "Яйца варёные + хлеб с маслом",   "Рис с курицей",    "1 яблоко", "Тефтели + рис"),
    };

    // Стоимость полноценного рациона варианта (порции доведены до нормы калорий), на семью за период
    private decimal VariantRationCost(List<MealDay> plan)
    {
        if (plan.Count == 0) return decimal.MaxValue;
        long baseCal = 0; decimal baseCost = 0; int days = 0;
        int cycleLen = Math.Min(plan.Count, 7);
        for (DateTime d = periodStart; d <= periodEnd; d = d.AddDays(1))
        {
            int totalDays = (int)(d.Date - PlanStart).TotalDays;
            int idx = ((totalDays % cycleLen) + cycleLen) % cycleLen;
            var meal = plan[idx];
            days++;
            foreach (string t in new[] { meal.Breakfast, meal.Lunch, meal.Snack, meal.Dinner })
            {
                baseCal += CalcMealCalories(t);
                foreach (var (name, grams) in GetIngredients(t))
                    baseCost += EstimatePrice(name, grams * familyCount);
            }
        }
        if (days == 0 || baseCal == 0) return decimal.MaxValue;
        decimal scaleToNorm = (decimal)calorieNorm * days / baseCal;
        return baseCost * scaleToNorm;
    }

    // Выбрать самый «богатый» вариант, чей полноценный рацион укладывается в бюджет (иначе Эконом)
    private void SelectMealPlan()
    {
        decimal budget = PeriodBudget;
        if (mealPlanStandard.Count == 0) { mealPlan = mealPlanStandard; mealTier = "Стандарт"; return; }

        if (VariantRationCost(MealPlanPremium) <= budget) { mealPlan = MealPlanPremium; mealTier = "Премиум"; }
        else if (VariantRationCost(mealPlanStandard) <= budget) { mealPlan = mealPlanStandard; mealTier = "Стандарт"; }
        else { mealPlan = MealPlanEconomy; mealTier = "Эконом"; }
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
        decimal ps = MenuPortionScale();   // тот же масштаб порций, что и на вкладке «Меню»

        for (int i = 0; i < 4; i++)
        {
            string text = meals[i];
            _txMeal[i].Text = string.IsNullOrEmpty(text) ? "—" : text;
            if (!string.IsNullOrEmpty(text))
            {
                int cal = (int)Math.Round(CalcMealCalories(text) * ps);
                decimal cost = 0;
                foreach (var (name, grams) in GetIngredients(text))
                    cost += EstimatePrice(name, grams * familyCount);
                cost = Math.Round(cost * ps, 0);
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

    // Масштаб порций меню под бюджет: довести рацион до нормы калорий, но не дороже бюджета.
    // Бюджета хватает → полноценный рацион (норма); не хватает → урезаем порции под бюджет.
    private decimal MenuPortionScale()
    {
        long baseCal = 0; decimal baseCost = 0; int dayCount = 0;
        for (DateTime d = periodStart; d <= periodEnd; d = d.AddDays(1))
        {
            MealDay? meal = FindMealForDate(d);
            if (meal == null) continue;
            dayCount++;
            foreach (string t in new[] { meal.Breakfast, meal.Lunch, meal.Snack, meal.Dinner })
            {
                baseCal += CalcMealCalories(t);                       // на человека
                foreach (var (name, grams) in GetIngredients(t))
                    baseCost += EstimatePrice(name, grams * familyCount);  // на семью
            }
        }
        if (dayCount == 0 || baseCal == 0) return 1m;

        decimal scaleToNorm  = (decimal)calorieNorm * dayCount / baseCal;  // довести калории до нормы
        decimal rationCost   = baseCost * scaleToNorm;                     // стоимость полноценного рациона
        decimal budgetFactor = rationCost > 0 ? PeriodBudget / rationCost : 1m;
        return scaleToNorm * Math.Min(1m, budgetFactor);
    }

    // Праздничная добавка к ужину выходного дня (режим «весь бюджет на меню»):
    // деликатес на сумму moneyFamily (на семью). Возвращает (доп. цена на семью, доп. ккал на человека).
    private (decimal price, int cal) FestiveAddon(decimal moneyFamily)
    {
        if (moneyFamily <= 0) return (0, 0);
        // Красная рыба ~600 грн/кг, ~130 ккал/100 г. Калории на человека ограничиваем (премиум-деликатесы
        // вроде икры дают мало калорий на гривну) — «праздник желудка», но без переедания.
        decimal gramsFamily = moneyFamily / 600m * 1000m;
        int cal = (int)Math.Round(gramsFamily / Math.Max(1, familyCount) * 130m / 100m);
        return (Math.Floor(moneyFamily), Math.Min(cal, 600));   // floor — не выходим за бюджет
    }

    private void FillMenuTab()
    {
        var rows = new List<MenuRow>();
        if (mealPlan.Count == 0) { dgMenu.ItemsSource = rows; lblMenuBudget.Text = ""; return; }

        int normPerPerson = calorieNorm;
        var culture = new CultureInfo("ru-RU");
        decimal ps = MenuPortionScale();   // масштаб порций под бюджет/норму

        // Калории/цена приёма пищи с учётом масштаба (калории — на человека, цена — на семью)
        int     SCal(string m)   => (int)Math.Round(CalcMealCalories(m) * ps);
        decimal SPrice(string m)
        {
            decimal c = 0;
            foreach (var (name, grams) in GetIngredients(m))
                c += EstimatePrice(name, grams * familyCount);
            return Math.Round(c * ps, 0);
        }

        decimal budget = PeriodBudget;

        // Пред-проход: базовая стоимость меню и число выходных дней (для режима «весь бюджет на меню»)
        decimal baseTotal = 0; int weekendDays = 0;
        for (DateTime d = periodStart; d <= periodEnd; d = d.AddDays(1))
        {
            MealDay? m = FindMealForDate(d);
            if (m == null) continue;
            baseTotal += SPrice(m.Breakfast) + SPrice(m.Lunch) + SPrice(m.Snack) + SPrice(m.Dinner);
            if (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday) weekendDays++;
        }
        // Праздничные выходные: остаток бюджета сверх базового меню распределяем поровну по выходным дням
        bool    festive       = fullBudgetMode && weekendDays > 0 && (budget - baseTotal) > 50m;
        decimal festPerWeekend = festive ? (budget - baseTotal) / weekendDays : 0m;

        // Накопители для строки ИТОГО (суммы по колонкам)
        int sCalBf = 0, sCalLn = 0, sCalSn = 0, sCalDn = 0;
        decimal sPrBf = 0, sPrLn = 0, sPrSn = 0, sPrDn = 0;
        long sCalDay = 0; decimal sCost = 0; int dayCount = 0;

        for (DateTime d = periodStart; d <= periodEnd; d = d.AddDays(1))
        {
            MealDay? meal = FindMealForDate(d);
            if (meal == null) continue;
            dayCount++;

            int bf = SCal(meal.Breakfast);
            int ln = SCal(meal.Lunch);
            int sn = SCal(meal.Snack);
            int dn = SCal(meal.Dinner);

            decimal pBf = SPrice(meal.Breakfast);
            decimal pLn = SPrice(meal.Lunch);
            decimal pSn = SPrice(meal.Snack);
            decimal pDn = SPrice(meal.Dinner);

            // Праздничный деликатес к ужину выходного дня
            string dinnerText = meal.Dinner;
            bool   feastDay   = festive && (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday);
            if (feastDay)
            {
                var (fp, fc) = FestiveAddon(festPerWeekend);
                if (fp > 0)
                {
                    pDn += fp; dn += fc;
                    dinnerText = meal.Dinner + "  ✨ + деликатес (красная рыба/икра)";
                }
                else feastDay = false;
            }

            int dayCal = bf + ln + sn + dn;
            decimal dayCost = pBf + pLn + pSn + pDn;
            var dnBg    = feastDay ? MenuBrushes.Festive : MenuBrushes.MealBg(meal.Dinner, dn, MenuBrushes.GroupDn);
            var dnGroup = feastDay ? MenuBrushes.Festive : MenuBrushes.GroupDn;

            rows.Add(new MenuRow
            {
                Date      = d.ToString("d MMMM (ddd)", culture),
                Breakfast = meal.Breakfast, Lunch = meal.Lunch, Snack = meal.Snack, Dinner = dinnerText,
                PriceBf = pBf > 0 ? $"~{pBf:F0}" : "", PriceLn = pLn > 0 ? $"~{pLn:F0}" : "",
                PriceSn = pSn > 0 ? $"~{pSn:F0}" : "", PriceDn = pDn > 0 ? $"~{pDn:F0}" : "",
                CalBf = bf > 0 ? bf.ToString() : "", CalLn = ln > 0 ? ln.ToString() : "",
                CalSn = sn > 0 ? sn.ToString() : "", CalDn = dn > 0 ? dn.ToString() : "",
                CalDay  = dayCal > 0 ? dayCal.ToString() : "",
                CalNorm = normPerPerson.ToString(),
                DayCost = dayCost > 0 ? $"~{dayCost:F0}" : "",
                CalDayBrush = MenuBrushes.CalDay(dayCal, normPerPerson),
                BreakfastBg = MenuBrushes.MealBg(meal.Breakfast, bf, MenuBrushes.GroupBf),
                LunchBg     = MenuBrushes.MealBg(meal.Lunch,     ln, MenuBrushes.GroupLn),
                SnackBg     = MenuBrushes.MealBg(meal.Snack,     sn, MenuBrushes.GroupSn),
                DinnerBg    = dnBg,
                BfGroup = MenuBrushes.GroupBf, LnGroup = MenuBrushes.GroupLn,
                SnGroup = MenuBrushes.GroupSn, DnGroup = dnGroup,
            });

            sCalBf += bf; sCalLn += ln; sCalSn += sn; sCalDn += dn;
            sPrBf += pBf; sPrLn += pLn; sPrSn += pSn; sPrDn += pDn;
            sCalDay += dayCal; sCost += dayCost;
        }
        if (dayCount == 0) { dgMenu.ItemsSource = rows; lblMenuBudget.Text = ""; return; }

        long normTotal = (long)normPerPerson * dayCount;
        rows.Add(new MenuRow
        {
            Date    = $"ИТОГО ({dayCount} дн.)",
            PriceBf = sPrBf > 0 ? $"~{sPrBf:F0}" : "", PriceLn = sPrLn > 0 ? $"~{sPrLn:F0}" : "",
            PriceSn = sPrSn > 0 ? $"~{sPrSn:F0}" : "", PriceDn = sPrDn > 0 ? $"~{sPrDn:F0}" : "",
            CalBf = sCalBf > 0 ? sCalBf.ToString("N0") : "", CalLn = sCalLn > 0 ? sCalLn.ToString("N0") : "",
            CalSn = sCalSn > 0 ? sCalSn.ToString("N0") : "", CalDn = sCalDn > 0 ? sCalDn.ToString("N0") : "",
            CalDay  = sCalDay > 0 ? sCalDay.ToString("N0") : "",
            CalNorm = normTotal.ToString("N0"),
            DayCost = sCost > 0 ? $"~{sCost:F0}" : "",
            IsTotal = true,
            CalDayBrush  = MenuBrushes.TotCalDay,
            DayCostBrush = MenuBrushes.TotCost,
            CalNormBrush = MenuBrushes.TotNorm,
        });

        dgMenu.ItemsSource = rows;

        // ── Сводка: рацион vs бюджет ─────────────────────────────
        int avgCal = (int)Math.Round((decimal)sCalDay / dayCount);   // ккал/чел в день (после масштаба)
        if (avgCal >= normPerPerson * 0.98m)
        {
            decimal free = Math.Max(0, budget - sCost);
            string tail = festive
                ? $"Весь бюджет в меню: ~{sCost:N0} грн из {budget:N0} грн — остаток ~{free:N0} грн распределён по выходным (праздничные блюда). ✨"
                : $"Стоимость меню на {dayCount} дн.: ~{sCost:N0} грн из бюджета {budget:N0} грн.  Свободный остаток: ~{free:N0} грн.";
            lblMenuBudget.Text = $"  Уровень рациона: {mealTier}.  ✓ Полноценный рацион (~{normPerPerson} ккал/чел в день).  " + tail;
            lblMenuBudget.Foreground = WMedia.Brushes.DarkGreen;
        }
        else
        {
            int pct = normPerPerson > 0 ? (int)Math.Round(avgCal * 100m / normPerPerson) : 0;
            lblMenuBudget.Text = $"  Уровень рациона: {mealTier}.  ⚠ Бюджета хватает на {pct}% нормы калорий (~{avgCal} из {normPerPerson} ккал/чел в день).  " +
                $"Рацион урезан под бюджет: ~{sCost:N0} грн.";
            lblMenuBudget.Foreground = WMedia.Brushes.DarkOrange;
        }
    }

    private void FillProductsTab()
    {
        products.Clear();
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
            string tierText   = isMin ? "Минимум" : isBasic ? "Базовый" : "Комфорт";
            var    tierBrush  = isMin ? WMedia.Brushes.Crimson : isBasic ? WMedia.Brushes.DarkOrange : WMedia.Brushes.DarkGreen;
            var    rowBgBrush = isMin ? ProductBrushes.MinBg   : isBasic ? ProductBrushes.BasicBg    : ProductBrushes.ComfortBg;

            realPriceData.TryGetValue(p.Name, out var rp);
            var    rpMap  = priceMappings.FirstOrDefault(m2 => m2.AppProduct == p.Name);
            decimal mult  = rpMap?.Multiplier is > 0 ? rpMap.Multiplier : 1.0m;
            decimal realP = rp?.LastPrice > 0 ? rp.LastPrice / mult : rp?.Avg30d > 0 ? rp.Avg30d / mult : rp?.Avg90d > 0 ? rp.Avg90d / mult : 0;
            string realPStr = realP > 0 ? realP.ToString("F2") : "";

            System.Windows.Media.Brush realPriceBrush = WMedia.Brushes.Black;
            if (realP > 0)
            {
                decimal diff = (realP - p.Price) / p.Price;
                realPriceBrush = diff < -0.05m ? WMedia.Brushes.DarkGreen : diff > 0.05m ? WMedia.Brushes.Crimson : WMedia.Brushes.DarkOrange;
            }

            products.Add(new ProductRow
            {
                ProductName = p.Name,
                Tier        = tierText,
                Frequency   = p.Frequency,
                Unit        = p.Unit,
                Price       = p.Price.ToString("F2"),
                RealPrice   = realPStr,
                Qty         = qty > 0 ? qty.ToString("F2") : "",
                PackInfo    = FormatPackInfo(p.Name, p.Unit, qty),
                Sum         = sum > 0 ? sum.ToString("F2") : "",
                Kcal        = kcal > 0 ? kcal.ToString("N0") : "",
                TierBrush      = tierBrush,
                RowBg          = rowBgBrush,
                RealPriceBrush = realPriceBrush,
            });
        }

        products.Add(new ProductRow { ProductName = "ИТОГО", IsTotal = true });
        UpdateProductsTotal();
    }

    // Изменили частоту покупки продукта → сохранить и перестроить недельный/месячный списки
    private void OnProductFrequencyChanged(ProductRow r)
    {
        int idx = prices.FindIndex(x => x.Name.Equals(r.ProductName, StringComparison.OrdinalIgnoreCase));
        if (idx >= 0) prices[idx] = prices[idx] with { Frequency = r.Frequency };
        SavePrices();
        FillWeeklyShoppingTab();
        FillMonthlyShoppingTab();
    }

    // Пересчёт строки после правки «Цена»/«Кол-во»: привязка к упаковкам и потолку бюджета
    private void RecomputeProductRow(ProductRow r)
    {
        if (!decimal.TryParse(r.Price, out decimal price) || price <= 0 ||
            !decimal.TryParse(r.Qty,   out decimal qty)   || qty < 0)
        {
            r.Sum = ""; r.Kcal = ""; r.PackInfo = "";
            UpdateProductsTotal(); return;
        }

        string productName = r.ProductName;
        string unit        = r.Unit;
        decimal otherTotal = ComputeTotalExcluding(r);
        decimal maxThisRow = Math.Max(0, PeriodBudget - otherTotal);

        if (PackStep.TryGetValue(productName, out decimal step) && step > 0 && qty > 0)
        {
            decimal snapUp = Math.Ceiling(qty / step) * step, snapDown = Math.Floor(qty / step) * step;
            qty = (price * snapUp <= maxThisRow) ? snapUp : (snapDown > 0 ? snapDown : qty);
        }

        decimal propSum = Math.Round(price * qty, 2);
        if (propSum > maxThisRow) { qty = Math.Floor(maxThisRow / price * 100m) / 100m; propSum = Math.Round(price * qty, 2); }

        string qtyStr = qty.ToString("F2");
        if (r.Qty != qtyStr) r.Qty = qtyStr;
        r.Sum      = propSum.ToString("F2");
        r.PackInfo = FormatPackInfo(productName, unit, qty);
        if (UnitGrams.TryGetValue(unit, out decimal gPU) && CaloriesPer100g.TryGetValue(productName, out decimal cal100))
            r.Kcal = ((int)Math.Round(qty * gPU * cal100 / 100m)).ToString("N0");
        else r.Kcal = "";
        UpdateProductsTotal();
    }

    private decimal ComputeTotalExcluding(ProductRow exclude)
    {
        decimal total = 0;
        foreach (var row in products)
        {
            if (row == exclude || row.IsTotal) continue;
            if (decimal.TryParse(row.Sum, out decimal s)) total += s;
        }
        return total;
    }

    private void UpdateProductsTotal()
    {
        decimal total = 0; long totalKcal = 0;
        ProductRow? totRow = null;
        foreach (var row in products)
        {
            if (row.IsTotal) { totRow = row; continue; }
            if (decimal.TryParse(row.Sum, out decimal s)) total += s;
            string rawK = new string((row.Kcal ?? "").Where(char.IsDigit).ToArray());
            if (long.TryParse(rawK, out long k)) totalKcal += k;
        }
        if (totRow != null)
        {
            totRow.Sum  = total > 0 ? total.ToString("F2") : "";
            totRow.Kcal = totalKcal > 0 ? totalKcal.ToString("N0") : "";
        }

        decimal budget = PeriodBudget, remaining = budget - total;
        decimal overPct = total > budget ? (total - budget) / budget * 100m : 0;

        if (total <= budget)
        {
            lblBudgetStatus.Text       = $"  ✓ Итого: {total:N0} грн  |  Бюджет: {budget:N0} грн  |  Остаток: {remaining:N0} грн";
            lblBudgetStatus.Foreground = WMedia.Brushes.DarkGreen;
        }
        else if (overPct < 5m)
        {
            lblBudgetStatus.Text       = $"  📦 Итого: {total:N0} грн  |  Бюджет: {budget:N0} грн  |  +{-remaining:N0} грн (округл.)";
            lblBudgetStatus.Foreground = WMedia.Brushes.DarkOrange;
        }
        else
        {
            lblBudgetStatus.Text       = $"  ⚠ Итого: {total:N0} грн  |  Бюджет: {budget:N0} грн  |  Превышение на {-remaining:N0} грн!";
            lblBudgetStatus.Foreground = WMedia.Brushes.Crimson;
        }
    }

    // ══════════════════════════════════════════════════ ЗАПОЛНЕНИЕ ПОКУПОК

    private void FillShoppingTab()
    {
        DateTime d1 = periodStart, d2 = periodStart.AddDays(1);
        bool isToday = d1.Date == DateTime.Today;
        FillShoppingDay(d1, shopToday,    dgShopToday,    lblTodayTitle,    isToday ? "Сегодня" : "1-й день периода");
        FillShoppingDay(d2, shopTomorrow, dgShopTomorrow, lblTomorrowTitle, isToday ? "Завтра"  : "2-й день периода");
    }

    private void FillShoppingDay(DateTime date, ObservableCollection<ShoppingRow> rows, SWC.DataGrid dg, SWC.TextBlock title, string prefix)
    {
        var culture = new CultureInfo("ru-RU");
        title.Text  = $"{prefix} — {date.ToString("dddd, d MMMM", culture)}";
        rows.Clear();

        MealDay? meal = FindMealForDate(date);
        if (meal == null) { rows.Add(new ShoppingRow { Product = "(нет данных для этой даты)" }); return; }

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
            rows.Add(new ShoppingRow { Product = name, Quantity = qty, Price = est > 0 ? $"~{est:F0}" : "" });
        }

        rows.Add(new ShoppingRow { Product = "ИТОГО", Price = dayTotal > 0 ? $"~{dayTotal:F0}" : "", IsTotal = true });

        string dateKey = date.ToString("yyyy-MM-dd");
        dg.Tag = $"daily:{dateKey}";
        RestorePaidValues(dg, "daily", dateKey);
    }

    private void FillWeeklyShoppingTab()
    {
        DateTime weekEnd = periodStart.AddDays(6);
        lblWeeklyTitle.Text = $"Покупки на неделю:  {periodStart:d MMMM} — {weekEnd:d MMMM yyyy}  ({familyCount} чел.)";
        shopWeekly.Clear();
        decimal ratio = familyCount / 2m, total = 0;
        foreach (var p in prices.Where(p => p.Frequency == "еженедельно"))
        {
            if (!BaseQty.TryGetValue(p.Name, out decimal mq)) continue;
            decimal rawQty = mq * ratio * (7m / 30m);
            decimal snap   = SnapToPack(p.Name, rawQty);
            if (snap <= 0) snap = Math.Round(rawQty, 2);
            var (qtyStr, cost) = GetRealQtyAndCost(p.Name, p.Unit, snap, p.Price);
            total += cost;
            shopWeekly.Add(new ShoppingRow { Product = p.Name, Quantity = qtyStr, Price = $"~{cost:F0}" });
        }
        AddPeriodicTotalRow(shopWeekly, total);
        string weekKey = periodStart.ToString("yyyy-MM-dd");
        dgShopWeekly.Tag = $"weekly:{weekKey}";
        RestorePaidValues(dgShopWeekly, "weekly", weekKey);
        decimal weekBudget = Math.Round(monthlyBudget / 4m, 0);
        lblWeeklyInfo.Text       = $"  {(total <= weekBudget ? "✓" : "⚠")} Итого на неделю: ~{total:N0} грн  |  ~¼ бюджета: {weekBudget:N0} грн  ";
        lblWeeklyInfo.Foreground = total <= weekBudget ? WMedia.Brushes.DarkGreen : WMedia.Brushes.Crimson;
    }

    private void FillMonthlyShoppingTab()
    {
        DateTime monthEnd = periodStart.AddDays(29);
        lblMonthlyTitle.Text = $"Покупки на месяц:  {periodStart:d MMMM} — {monthEnd:d MMMM yyyy}  ({familyCount} чел.)";
        shopMonthly.Clear();
        decimal ratio = familyCount / 2m, total = 0;
        foreach (var p in prices.Where(p => p.Frequency == "ежемесячно"))
        {
            if (!BaseQty.TryGetValue(p.Name, out decimal mq)) continue;
            decimal rawQty = mq * ratio;
            decimal snap   = SnapToPack(p.Name, rawQty);
            if (snap <= 0) snap = Math.Round(rawQty, 2);
            var (qtyStr, cost) = GetRealQtyAndCost(p.Name, p.Unit, snap, p.Price);
            total += cost;
            shopMonthly.Add(new ShoppingRow { Product = p.Name, Quantity = qtyStr, Price = $"~{cost:F0}" });
        }
        AddPeriodicTotalRow(shopMonthly, total);
        string monthKey = periodStart.ToString("yyyy-MM-dd");
        dgShopMonthly.Tag = $"monthly:{monthKey}";
        RestorePaidValues(dgShopMonthly, "monthly", monthKey);
        lblMonthlyInfo.Text       = $"  {(total <= monthlyBudget ? "✓" : "⚠")} Итого на месяц: ~{total:N0} грн  |  Бюджет: {monthlyBudget:N0} грн  ";
        lblMonthlyInfo.Foreground = total <= monthlyBudget ? WMedia.Brushes.DarkGreen : WMedia.Brushes.Crimson;
    }

    // ══════════════════════════════════════════════════ РЕАЛЬНЫЕ ЦЕНЫ

    private void FillRealPricesTab()
    {
        colRpExcel.ItemsSource = new[] { "" }.Concat(excelNames).ToArray();
        realPrices.Clear();

        foreach (var p in prices)
        {
            var m = priceMappings.FirstOrDefault(x => x.AppProduct == p.Name) ?? new PriceMapping { AppProduct = p.Name };
            realPriceData.TryGetValue(p.Name, out var rp);
            decimal lastP = rp?.LastPrice ?? 0, avg30 = rp?.Avg30d ?? 0, avg90 = rp?.Avg90d ?? 0;
            string exUnit = rp?.LastUnit ?? "";
            decimal ourInExcel = m.Multiplier > 0 ? Math.Round(p.Price * m.Multiplier, 2) : p.Price;

            string diffStr = "";
            System.Windows.Media.Brush diffBrush = WMedia.Brushes.DimGray;
            System.Windows.Media.Brush lastBrush = WMedia.Brushes.Black;
            if (lastP > 0 && ourInExcel > 0)
            {
                decimal diff = (lastP - ourInExcel) / ourInExcel * 100m;
                diffStr = $"{diff:+0.0;-0.0}%";
                var c = diff < -5m ? WMedia.Brushes.DarkGreen : diff > 5m ? WMedia.Brushes.Crimson : WMedia.Brushes.DarkOrange;
                diffBrush = c; lastBrush = c;
            }

            realPrices.Add(new RealPriceRow
            {
                RpApp    = p.Name,
                RpUnit   = p.Unit,
                RpExcel  = m.ExcelName,
                RpExUnit = exUnit,
                RpMult   = m.Multiplier.ToString("F2"),
                RpLast   = lastP > 0 ? lastP.ToString("F2") : "",
                RpAvg30  = avg30 > 0 ? avg30.ToString("F2") : "",
                RpAvg90  = avg90 > 0 ? avg90.ToString("F2") : "",
                RpOur    = ourInExcel.ToString("F2"),
                RpDiff   = diffStr,
                DiffBrush = diffBrush,
                LastBrush = lastBrush,
            });
        }

        int mapped = priceMappings.Count(m => !string.IsNullOrWhiteSpace(m.ExcelName));
        int total  = priceMappings.Count;
        int loaded = excelPurchases.Count;
        bool fromShared = ExcelPriceService.LastSource == "SeniorHub";

        if (fromShared)
        {
            lblRealStatus.Text       = $"  Загружено {loaded} записей из общей базы «Офиса пенсионера»  |  Сопоставлено: {mapped} из {total} продуктов";
            lblRealStatus.Foreground = WMedia.Brushes.SeaGreen;
        }
        else
        {
            bool fileOk = File.Exists(ExcelPriceService.ExcelFilePath);
            lblRealStatus.Text = fileOk
                ? $"  Загружено {loaded} записей из файла расходов  |  Сопоставлено: {mapped} из {total} продуктов"
                : $"  ⚠ Нет данных: общая база пуста и файл не найден ({ExcelPriceService.ExcelFilePath})";
            lblRealStatus.Foreground = fileOk ? WMedia.Brushes.DimGray : WMedia.Brushes.Crimson;
        }
    }

    // Изменили «Название в расходах» (ComboBox) → подобрать коэффициент по единицам, сохранить, пересчитать
    private void OnRealExcelChanged(RealPriceRow r)
    {
        var mapping = priceMappings.FirstOrDefault(m => m.AppProduct == r.RpApp);
        if (mapping == null) return;

        string newName = r.RpExcel ?? "";
        mapping.ExcelName = newName;
        if (!string.IsNullOrEmpty(newName))
        {
            string appUnit = prices.Find(p => p.Name == r.RpApp)?.Unit ?? "";
            string exUnit  = excelPurchases.Where(p => p.Name.Equals(newName, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(p => p.Date).FirstOrDefault()?.Unit ?? "";
            mapping.Multiplier = ExcelPriceService.DefaultMultiplier(exUnit, appUnit);
        }
        SaveRecomputeRefillRealPrices();
    }

    // Изменили коэффициент вручную
    private void OnRealMultChanged(RealPriceRow r)
    {
        var mapping = priceMappings.FirstOrDefault(m => m.AppProduct == r.RpApp);
        if (mapping == null) return;
        if (decimal.TryParse(r.RpMult, out decimal mult) && mult > 0) mapping.Multiplier = mult;
        SaveRecomputeRefillRealPrices();
    }

    private void SaveRecomputeRefillRealPrices()
    {
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

    private static void AddPeriodicTotalRow(ObservableCollection<ShoppingRow> rows, decimal estimatedTotal)
    {
        rows.Add(new ShoppingRow
        {
            Product = "ИТОГО",
            Price   = estimatedTotal > 0 ? $"~{estimatedTotal:F0}" : "",
            IsTotal = true
        });
    }

    private static ObservableCollection<ShoppingRow>? ShopRows(SWC.DataGrid dg) =>
        dg.ItemsSource as ObservableCollection<ShoppingRow>;

    private void UpdateShoppingPaidTotal(SWC.DataGrid dg)
    {
        if (ShopRows(dg) is not { } rows) return;
        decimal total = 0;
        ShoppingRow? totRow = null;
        var amounts = new Dictionary<string, decimal>();
        foreach (var row in rows)
        {
            if (row.IsTotal) { totRow = row; continue; }
            if (decimal.TryParse(row.Paid, out decimal v) && v > 0)
                { total += v; if (!string.IsNullOrEmpty(row.Product)) amounts[row.Product] = v; }
        }
        if (totRow != null) totRow.Paid = total > 0 ? total.ToString("F0") : "";

        if (dg.Tag is string tag && tag.Contains(':'))
        {
            var parts = tag.Split(':', 2);
            string type = parts[0], dateKey = parts[1];
            if (!paidData.ContainsKey(type)) paidData[type] = new();
            paidData[type][dateKey] = amounts;
            SavePaidData();
        }
    }

    private static void CopyShoppingListToClipboard(SWC.DataGrid dg, string title)
    {
        if (ShopRows(dg) is not { } rows) return;
        var sb = new StringBuilder();
        sb.AppendLine(title);
        sb.AppendLine(new string('─', 40));
        foreach (var row in rows)
        {
            if (row.IsTotal)
            {
                sb.AppendLine(new string('─', 40));
                sb.Append($"ИТОГО: {row.Price}");
                if (!string.IsNullOrEmpty(row.Paid)) sb.Append($"  |  Заплачено: {row.Paid}");
                sb.AppendLine();
            }
            else
            {
                string check = row.Done ? "☑" : "☐";
                sb.Append($"{check} {row.Product}");
                if (!string.IsNullOrEmpty(row.Quantity)) sb.Append($": {row.Quantity}");
                if (!string.IsNullOrEmpty(row.Price))    sb.Append($"  ({row.Price})");
                if (!string.IsNullOrEmpty(row.Paid) && row.Paid != "0") sb.Append($"  ✓{row.Paid}");
                sb.AppendLine();
            }
        }
        try { System.Windows.Clipboard.SetText(sb.ToString()); } catch { }
    }

    private void RestorePaidValues(SWC.DataGrid dg, string sessionType, string dateKey)
    {
        if (ShopRows(dg) is not { } rows) return;
        if (paidData.TryGetValue(sessionType, out var sessions) && sessions.TryGetValue(dateKey, out var amounts))
        {
            foreach (var row in rows)
            {
                if (row.IsTotal) continue;
                if (amounts.TryGetValue(row.Product, out decimal amt) && amt > 0) row.Paid = amt.ToString("F0");
            }
        }
        UpdateShoppingPaidTotal(dg);
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
        if (p != null) return Math.Round(p.Price * totalGrams / 1000m, 1);
        if (FruitPricePerKg.TryGetValue(ingredient, out decimal perKg))
            return Math.Round(perKg * totalGrams / 1000m, 1);
        return 0;
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
        ["Бананы"]=89,["Груши"]=57,
    };

    // Цены фруктов-полдников (грн/кг) — fallback, если фрукта нет в загруженном прайсе.
    // Из розничных цен супермаркетов Украины 2026 (АТБ/Сільпо/Varus): яблоко ~40, банан ~65, груша ~70.
    private static readonly Dictionary<string, decimal> FruitPricePerKg = new()
    {
        ["Яблоки"]=40m,["Бананы"]=65m,["Груши"]=70m,
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
        // Фрукты-полдники (1 шт на человека): средний вес плода
        ("яблоко",             new[]{("Яблоки",180m)}),
        ("банан",              new[]{("Бананы",120m)}),
        ("груша",              new[]{("Груши",180m)}),
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
            if (doc.RootElement.TryGetProperty("FullBudgetMode", out var fb)) fullBudgetMode = fb.GetBoolean();
        }
        catch { }
    }

    internal void SaveSettings()
    {
        string path = Path.Combine(AppDir, "settings.json");
        var obj = new { Budget = monthlyBudget, FamilyCount = familyCount, CalorieNorm = calorieNorm, FullBudgetMode = fullBudgetMode };
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


