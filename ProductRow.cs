using System.ComponentModel;
using System.Runtime.CompilerServices;
using Brush           = System.Windows.Media.Brush;
using Brushes         = System.Windows.Media.Brushes;
using Color           = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace MenuApp;

// Строка вкладки «Продукты» для WPF DataGrid (этап миграции гибрид → WPF).
// Редактируемые: ProductName, Frequency, Unit, Price, Qty. Остальное пересчитывается.
internal sealed class ProductRow : INotifyPropertyChanged
{
    public bool IsTotal { get; init; }

    private string _productName = ""; public string ProductName { get => _productName; set => Set(ref _productName, value); }
    private string _tier        = ""; public string Tier        { get => _tier;        set => Set(ref _tier, value); }
    private string _frequency   = ""; public string Frequency   { get => _frequency;   set => Set(ref _frequency, value); }
    private string _unit        = ""; public string Unit        { get => _unit;        set => Set(ref _unit, value); }
    private string _price       = ""; public string Price       { get => _price;       set => Set(ref _price, value); }
    private string _realPrice   = ""; public string RealPrice   { get => _realPrice;   set => Set(ref _realPrice, value); }
    private string _qty         = ""; public string Qty         { get => _qty;         set => Set(ref _qty, value); }
    private string _packInfo    = ""; public string PackInfo    { get => _packInfo;    set => Set(ref _packInfo, value); }
    private string _sum         = ""; public string Sum         { get => _sum;         set => Set(ref _sum, value); }
    private string _kcal        = ""; public string Kcal        { get => _kcal;        set => Set(ref _kcal, value); }

    private Brush _tierBrush      = Brushes.Black; public Brush TierBrush      { get => _tierBrush;      set => Set(ref _tierBrush, value); }
    private Brush _realPriceBrush = Brushes.Black; public Brush RealPriceBrush { get => _realPriceBrush; set => Set(ref _realPriceBrush, value); }
    private Brush _rowBg          = Brushes.White; public Brush RowBg          { get => _rowBg;          set => Set(ref _rowBg, value); }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (!Equals(field, value)) { field = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)); }
    }
}

internal static class ProductBrushes
{
    public static readonly Brush MinBg     = Frozen(255, 235, 235);  // уровень «Минимум»
    public static readonly Brush BasicBg   = Frozen(255, 252, 220);  // «Базовый»
    public static readonly Brush ComfortBg = Frozen(235, 255, 235);  // «Комфорт»
    public static readonly Brush TotSum    = Frozen(130, 230, 130);  // ИТОГО: сумма
    public static readonly Brush TotKcal   = Frozen(180, 220, 255);  // ИТОГО: ккал

    private static Brush Frozen(byte r, byte g, byte b)
    {
        var br = new SolidColorBrush(Color.FromRgb(r, g, b));
        br.Freeze();
        return br;
    }
}
