using System.Windows;

namespace MenuApp;

public partial class SettingsWindow : Window
{
    public decimal BudgetValue { get; private set; }
    public int     FamilyCount { get; private set; }
    public int     CalorieNorm { get; private set; }

    public SettingsWindow(decimal budget, int family, int calNorm)
    {
        InitializeComponent();
        TxBudget.Text  = budget.ToString("F2");
        TxFamily.Text  = family.ToString();
        TxCalNorm.Text = calNorm.ToString();
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        if (!decimal.TryParse(TxBudget.Text,  out var b) || b < 0) { TxBudget.Focus();  return; }
        if (!int.TryParse(TxFamily.Text,       out var f) || f < 1) { TxFamily.Focus();  return; }
        if (!int.TryParse(TxCalNorm.Text,      out var c) || c < 100) { TxCalNorm.Focus(); return; }
        BudgetValue = b;
        FamilyCount = f;
        CalorieNorm = c;
        DialogResult = true;
    }
}
