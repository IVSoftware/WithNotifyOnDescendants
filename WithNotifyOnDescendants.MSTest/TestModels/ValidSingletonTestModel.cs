using WithNotifyOnDescendants.Proto;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace WithNotifyOnDescendants.Proto.MSTest.TestModels
{
    class ValidSingletonTestModel : INotifyPropertyChanged
    {
        // On demand pattern
        public bool ABC1IsValueCreated => _abc1 is not null;

        [WaitForValueCreated(propertyName: nameof(ABC1IsValueCreated))]
        public INotifyPropertyChanged ABC1
        {
            get
            {
                if (_abc1 is null)
                {
                    _abc1 = new ABC();
                    OnPropertyChanged();
                }
                return _abc1;
            }
        }
        INotifyPropertyChanged? _abc1 = default;

        #region L A Z Y    T

        /// <summary>
        /// Step One: 
        /// Watch for a value without inadvertently creating an instance/
        /// </summary>
        public bool ABC2IsValueCreated => 
            LazyProxy.Notify[_abc2, ()=>OnPropertyChanged(nameof(ABC2))];

        /// <summary>
        /// Step Two:
        /// Tell discovery to check the IsValueCreated handle
        /// first, before calling the getter on this object.
        /// </summary>
        [WaitForValueCreated(nameof(ABC2IsValueCreated))]
        public ABC ABC2 => _abc2.Value;

        /// <summary>
        /// Lazy T
        /// </summary>
        private readonly Lazy<ABC> _abc2 = new Lazy<ABC>(() => new ABC());

        #endregion L A Z Y    T

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
