using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Glance.ColorPicker.WinUI;

public sealed class ColorFormatItem(string label,
    string value,
    Action copy) :
    INotifyPropertyChanged
{
    private readonly Action copy = copy;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Label { get; } = label;

    public string Value
    {
        get;
        private set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            OnPropertyChanged();
        }
    } = value;

    public void Copy() => copy();

    public void Update(string newValue) => Value = newValue;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
