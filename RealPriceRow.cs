using Brush   = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;

namespace MenuApp;

// Строка вкладки «Реальные цены» для WPF DataGrid (этап миграции гибрид → WPF).
// Редактируемые: RpExcel (ComboBox — название в расходах) и RpMult (коэффициент).
// Любая правка вызывает полный refill, поэтому INotifyPropertyChanged не нужен.
internal sealed class RealPriceRow
{
    public string RpApp    { get; init; } = "";
    public string RpUnit   { get; init; } = "";
    public string RpExcel  { get; set;  } = "";   // ComboBox
    public string RpExUnit { get; init; } = "";
    public string RpMult   { get; set;  } = "";   // редактируемый коэффициент
    public string RpLast   { get; init; } = "";
    public string RpAvg30  { get; init; } = "";
    public string RpAvg90  { get; init; } = "";
    public string RpOur    { get; init; } = "";
    public string RpDiff   { get; init; } = "";

    public Brush DiffBrush { get; init; } = Brushes.DimGray;  // цвет «Разница»
    public Brush LastBrush { get; init; } = Brushes.Black;    // цвет «Посл. цена»
}
