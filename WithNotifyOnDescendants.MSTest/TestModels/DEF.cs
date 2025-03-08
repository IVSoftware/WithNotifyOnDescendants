using System.ComponentModel;
using System.Runtime.CompilerServices;

class DEF
{
    public override string ToString()
        => $"{D} | {E} | {F}";

    public DEF() { }
    public DEF(object? d = null, object? e = null, object? f = null)
    {
        if (d is not null) D = d;
        if (e is not null) E = e;
        if (f is not null) F = f;
    }

    public object D
    {
        get => _d;
        set
        {
            if (!Equals(_d, value))
            {
                _d = value;
                OnPropertyChanged();
            }
        }
    }
    object _d = "D";

    public object E
    {
        get => _e;
        set
        {
            if (!Equals(_e, value))
            {
                _e = value;
                OnPropertyChanged();
            }
        }
    }
    object _e = "E";

    public object F
    {
        get => _f;
        set
        {
            if (!Equals(_f, value))
            {
                _f = value;
                OnPropertyChanged();
            }
        }
    }
    object _f = "F";

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    public event PropertyChangedEventHandler? PropertyChanged;
}