using WithNotifyOnDescendants.Proto;
using IVSoftware.Portable.Xml.Linq.XBoundObject;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace WithNotifyOnDescendants.Proto.MSTest.TestModels
{
    class INPCwithINPCs : INotifyPropertyChanged
    {
        public ABC? ABC1
        {
            get => _abc1;
            set
            {
                if (!Equals(_abc1, value))
                {
                    _abc1 = value;
                    OnPropertyChanged();
                }
            }
        }
        ABC? _abc1 = default;

        public ABC? ABC2
        {
            get => _abc2;
            set
            {
                if (!Equals(_abc2, value))
                {
                    _abc2 = value;
                    OnPropertyChanged();
                }
            }
        }
        ABC? _abc2 = default;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        public event PropertyChangedEventHandler? PropertyChanged;

    }
}
