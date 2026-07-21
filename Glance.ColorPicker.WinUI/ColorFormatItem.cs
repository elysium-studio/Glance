using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Glance.ColorPicker.WinUI;

public sealed class ColorFormatItem :
    INotifyPropertyChanged
{
    private readonly Action copy;
    private readonly Action pick;
    private string value;

    public ColorFormatItem(
        string label,
        string value,
        Action copy,
        Action pick)
    {
        Label = label;
        this.value = value;
        this.copy = copy;
        this.pick = pick;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Label { get; }

    public string Value
    {
        get => value;
        private set
        {
            if (this.value == value)
            {
                return;
            }

            this.value = value;
            OnPropertyChanged();
        }
    }

    public void Copy() => copy();

    public void Pick() => pick();

    public void Update(string newValue) => Value = newValue;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
