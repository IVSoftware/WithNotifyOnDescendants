using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WithNotifyOnDescendants.Proto.MSTest.TestModels
{
    /// <summary>
    /// Best case - Works because changes to the C object itself are notified.
    /// </summary>
    public class ClassB : INotifyPropertyChanged
    {
        public ClassC C
        {
            get => _c;
            set
            {
                if (value is not null && !Equals(_c, value))
                {
                    _c = value;
                    OnPropertyChanged();
                }
            }
        }
        ClassC _c = new();

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    /// <summary>
    /// This also WORKS, because `C` is not dynamic
    /// </summary>
    public class ClassBAlternate
    {
        public ClassC C { get; } = new();
    }

    public class Pathological
    {
        public ClassC? C { get; set; } = null;
    }
}
