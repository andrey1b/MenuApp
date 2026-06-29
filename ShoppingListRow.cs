using System.ComponentModel;

namespace MenuApp;

// Строка вкладки «Составить список» для WPF DataGrid (этап миграции гибрид → WPF).
// Редактируемые: Check (отметка) и Qty (количество).
internal sealed class ShoppingListRow : INotifyPropertyChanged
{
    public string Name { get; init; } = "";
    public string Unit { get; init; } = "";

    private bool _check;
    public bool Check
    {
        get => _check;
        set { if (_check != value) { _check = value; OnChanged(nameof(Check)); } }
    }

    private string _qty = "1";
    public string Qty
    {
        get => _qty;
        set { if (_qty != value) { _qty = value; OnChanged(nameof(Qty)); } }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
