using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace WithNotifyOnDescendants.Proto.Internal
{
    class DistinctO2X
    {
        public DistinctO2X(EventHandler<XObjectChangeEventArgs> callback)
            => _callback = callback;

        private readonly Dictionary<object, XElement> _impl = new();
        public EventHandler<XObjectChangeEventArgs> _callback { get; }
        public XElement this[object key]
        {
            get
            {
                XElement? xel;
                lock (_lock)
                {
                    if (!_impl.TryGetValue(key, out xel))
                    {
                        xel = new XElement($"{StdFrameworkName.xel}");
                        xel.Changing += onChanging;
                        xel.Changed += onChanged;
                        _impl[key] = xel;
                    }
                }
                return xel;
            }
            set
            {
                if(!localIsValid())
                {
                    Debug.Fail(nameof(InvalidOperationException));
                    return;
                }
                XElement? xel;
                lock (_lock)
                {
                    if (_impl.TryGetValue(key, out xel))
                    {
                        if (xel.Parent is null && value.Parent is not null)
                        {
                            Debug.Fail("FIRST TIME IS GOOD");
                            _impl[key] = value;
                        }
                    }
                    else
                    {
                        _impl[key] = value;
                    }
                }
                bool localIsValid()
                {
                    if (value is null) return false;
                    if (value is INotifyPropertyChanged) return true;
                    if (value is INotifyCollectionChanged) return true;
                    if (value.Parent is null) return true;
                    return false;
                }
            }
        }
        object _lock = new object();

        private void onChanging(object? sender, XObjectChangeEventArgs e)
        {
            var eOBC = new XOBCObjectChangeEventArgs(e, sender, true);
            if (eOBC.Parent is not null)
            {
                _callback?.Invoke(sender, eOBC);
            }
        }

        private void onChanged(object? sender, XObjectChangeEventArgs e)
        {
            var eOBC = new XOBCObjectChangeEventArgs(e, sender, false);
            if (eOBC.Parent is null)
            {
                Debug.Fail("Expecting Parent always.");
            }
            _callback?.Invoke(sender, eOBC);
        }

        internal XElement? Remove(object item)
        {
            if (_impl.TryGetValue(item, out var model))
            {

                Debug.Assert(DateTime.Now.Date == new DateTime(2025, 3, 3).Date, "Don't forget disabled");
                //model.Changing -= onChanging;
                //model.Changed -= onChanged;
                //_impl.Remove(item);
            }
            return model;
        }
        internal bool ContainsKey(object item) => _impl.ContainsKey(item);

        internal bool TryGetValue(object item, out XElement? model) =>
            _impl.TryGetValue(item, out model);

        internal void Clear() => _impl.Clear();
        public int Count => _impl.Count;
    }
}
