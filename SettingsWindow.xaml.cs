using System.Windows;
using System.Windows.Controls;

namespace MenuApp;

public partial class SettingsWindow : Window
{
    public decimal BudgetValue { get; private set; }
    public int     FamilyCount { get; private set; }
    public int     CalorieNorm { get; private set; }
    public bool    FullBudget  { get; private set; }   // true → весь бюджет на меню (праздничные выходные)

    private bool _syncing;

    public SettingsWindow(decimal budget, int family, int calNorm, bool fullBudget)
    {
        InitializeComponent();
        TxBudget.Text  = budget.ToString("F2");
        TxFamily.Text  = family.ToString();
        TxCalNorm.Text = calNorm.ToString();
        ChkFullBudget.IsChecked = fullBudget;
        ChkSurplus.IsChecked    = !fullBudget;
    }

    // Два взаимоисключающих чекбокса: включение одного выключает другой.
    private void ChkSurplus_Checked(object sender, RoutedEventArgs e)
    {
        if (_syncing) return;
        _syncing = true;
        ChkFullBudget.IsChecked = false;
        _syncing = false;
    }

    private void ChkFullBudget_Checked(object sender, RoutedEventArgs e)
    {
        if (_syncing) return;
        _syncing = true;
        ChkSurplus.IsChecked = false;
        _syncing = false;
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        if (!decimal.TryParse(TxBudget.Text,  out var b) || b < 0) { TxBudget.Focus();  return; }
        if (!int.TryParse(TxFamily.Text,       out var f) || f < 1) { TxFamily.Focus();  return; }
        if (!int.TryParse(TxCalNorm.Text,      out var c) || c < 100) { TxCalNorm.Focus(); return; }
        // Если пользователь снял обе галочки — считаем режимом по умолчанию (остаток пользователю).
        BudgetValue = b;
        FamilyCount = f;
        CalorieNorm = c;
        FullBudget  = ChkFullBudget.IsChecked == true;
        DialogResult = true;
    }
}
