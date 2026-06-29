using System.ComponentModel;

namespace MenuApp;

// Строка списка покупок для WPF DataGrid (этап миграции гибрид → WPF).
// Редактируемая: Done (куплено) и Paid (заплачено) с уведомлением об изменении —
// зачёркивание и пересчёт «Заплачено» в строке ИТОГО делаются через биндинг/триггеры.
internal sealed class ShoppingRow : INotifyPropertyChanged
{
    public string Product  { get; init; } = "";
    public string Quantity { get; init; } = "";
    public string Price    { get; init; } = "";
    public bool   IsTotal  { get; init; }

    private bool _done;
    public bool Done
    {
        get => _done;
        set { if (_done != value) { _done = value; OnChanged(nameof(Done)); } }
    }

    private string _paid = "";
    public string Paid
    {
        get => _paid;
        set { if (_paid != value) { _paid = value; OnChanged(nameof(Paid)); } }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
