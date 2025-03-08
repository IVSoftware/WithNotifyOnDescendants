using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace WithNotifyOnDescendants.Proto.MSTest.TestModels
{
    /// <summary>
    /// A fundamentally illegal null INPC with no OnPropertyChanged.
    /// </summary>
    class InvalidSingletonTestModelA : INotifyPropertyChanged
    {
        /// <summary>
        /// It is ILLEGAL in this framework for a property that
        /// is INPC to not be a bindable property. 
        /// SO IF:
        /// ☑ The PROPERTY INFO claims that it 'is' INPC.
        /// ☑ It returns null in discovery.
        /// ☑ It lacks the [WaitForObject] attribute.
        /// ☐ OR resides in a class that itself does NOT implement INPC.
        /// THEN: An InvalidOperation exception will be thrown.
        /// </summary>
        public INotifyPropertyChanged? D { get; set; } = null;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    /// <summary>
    /// Otherwise valid WaitForObject. HOWEVER the enclosing class must now implement INPC.
    /// </summary>
    class InvalidSingletonTestModelB
    {
        /// <summary>
        /// It is ILLEGAL in this framework for a property that
        /// is INPC to not be a bindable property. 
        /// SO IF:
        /// ☑ The PROPERTY INFO claims that it 'is' INPC.
        /// ☑ It returns null in discovery.
        /// ☑ It lacks the [WaitForObject] attribute.
        /// ☑ OR resides in a class that itself does NOT implement INPC.
        /// THEN: An InvalidOperation exception will be thrown.
        /// </summary>
        /// 
        public INotifyPropertyChanged? D
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
        INotifyPropertyChanged? _d = default;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
