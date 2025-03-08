using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace WithNotifyOnDescendants.Proto
{
    class EmptyClass { }

    class EmptyOnNotifyClass : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    class ClassWithOnePropertyINPC : INotifyPropertyChanged
    {
        public object? A
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
        object? _a = null;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
