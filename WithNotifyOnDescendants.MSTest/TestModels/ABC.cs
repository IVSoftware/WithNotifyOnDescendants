using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WithNotifyOnDescendants.Proto.MSTest.TestModels
{
    class ABC : INotifyPropertyChanged
    {
        public override string ToString()
            => $"{A} | {B} | {C}";

        public ABC() { }
        public ABC(object? a = null, object? b = null, object? c = null)
        {
            if (a is not null) A = a;
            if (b is not null) B = b;
            if (c is not null) C = c;
        }
        public object A
        {
            get => _a;
            set
            {
                if (!Equals(_a, value))
                {
                    _a = value;
                    OnPropertyChanged();
                }
            }
        }
        object _a = "A";

        public object B
        {
            get => _b;
            set
            {
                if (!Equals(_b, value))
                {
                    _b = value;
                    OnPropertyChanged();
                }
            }
        }
        object _b = "B";

        public object C
        {
            get => _c;
            set
            {
                if (!Equals(_c, value))
                {
                    _c = value;
                    OnPropertyChanged();
                }
            }
        }
        object _c = "C";

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
