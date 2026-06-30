using Brush           = System.Windows.Media.Brush;
using Brushes         = System.Windows.Media.Brushes;
using Color           = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace MenuApp;

// Строка таблицы «Меню» для WPF DataGrid (этап миграции гибрид → WPF).
// Условная окраска вычисляется при заполнении (FillMenuTab) и хранится готовыми кистями,
// чтобы XAML просто привязывался к ним без конвертеров.
internal sealed class MenuRow
{
    public string Date      { get; init; } = "";
    public string Breakfast { get; init; } = "";
    public string Lunch     { get; init; } = "";
    public string Snack     { get; init; } = "";
    public string Dinner    { get; init; } = "";
    // Цены по приёмам пищи (на семью), грн
    public string PriceBf   { get; init; } = "";
    public string PriceLn   { get; init; } = "";
    public string PriceSn   { get; init; } = "";
    public string PriceDn   { get; init; } = "";
    public string CalBf     { get; init; } = "";
    public string CalLn     { get; init; } = "";
    public string CalSn     { get; init; } = "";
    public string CalDn     { get; init; } = "";
    public string CalDay    { get; init; } = "";
    public string CalNorm   { get; init; } = "";
    public string DayCost   { get; init; } = "";

    // Фон ячеек блюд: групповой тон приёма пищи (или коралловый — «блюдо без данных»)
    public Brush BreakfastBg { get; init; } = Brushes.Transparent;
    public Brush LunchBg     { get; init; } = Brushes.Transparent;
    public Brush SnackBg     { get; init; } = Brushes.Transparent;
    public Brush DinnerBg    { get; init; } = Brushes.Transparent;

    // Фон ячеек «Цена»/«Ккал» каждого приёма — тот же групповой тон (на строке ИТОГО прозрачный)
    public Brush BfGroup { get; init; } = Brushes.Transparent;
    public Brush LnGroup { get; init; } = Brushes.Transparent;
    public Brush SnGroup { get; init; } = Brushes.Transparent;
    public Brush DnGroup { get; init; } = Brushes.Transparent;

    // Цвет текста числовых колонок
    public Brush CalDayBrush  { get; init; } = Brushes.Black;
    public Brush CalNormBrush { get; init; } = MenuBrushes.NormGreen;
    public Brush DayCostBrush { get; init; } = Brushes.DarkGreen;

    // Строка ИТОГО — стилизуется через GridRow (тёмный фон, белый жирный)
    public bool IsTotal { get; init; }
}

internal static class MenuBrushes
{
    public static readonly Brush Unknown   = Frozen(255, 214, 196);  // подсветка блюда без данных (коралловый)
    public static readonly Brush NormGreen = Frozen( 44,  95,  45);  // «Норма ккал»
    public static readonly Brush TotCalDay = Frozen(150, 220, 150);  // ИТОГО: ккал/день
    public static readonly Brush TotCost   = Frozen(130, 230, 130);  // ИТОГО: стоимость
    public static readonly Brush TotNorm   = Frozen(140, 190, 255);  // ИТОГО: норма

    // Групповые тона приёмов пищи — объединяют тройку колонок «Блюдо | Цена | Ккал»
    public static readonly Brush GroupBf = Frozen(255, 251, 240);  // Завтрак — тёплый кремовый
    public static readonly Brush GroupLn = Frozen(240, 248, 240);  // Обед — светло-зелёный
    public static readonly Brush GroupSn = Frozen(255, 247, 214);  // Полдник — светло-жёлтый (узнаваемый)
    public static readonly Brush GroupDn = Frozen(240, 246, 252);  // Ужин — светло-голубой
    public static readonly Brush Festive = Frozen(255, 232, 150);  // Праздничный ужин выходного дня (золотистый)

    // Цвет «ккал/день»: зелёный ≥ нормы, оранжевый ≥ 1500, иначе красный
    public static Brush CalDay(int dayCal, int norm) =>
        dayCal <= 0    ? Brushes.Black
        : dayCal >= norm ? Brushes.DarkGreen
        : dayCal >= 1500 ? Brushes.DarkOrange
                         : Brushes.Crimson;

    // Фон ячейки блюда: коралловый, если блюдо указано, но калории неизвестны (cal == 0);
    // иначе — групповой тон приёма пищи.
    public static Brush MealBg(string text, int cal, Brush group) =>
        (!string.IsNullOrWhiteSpace(text) && cal == 0) ? Unknown : group;

    private static Brush Frozen(byte r, byte g, byte b)
    {
        var br = new SolidColorBrush(Color.FromRgb(r, g, b));
        br.Freeze();
        return br;
    }
}
