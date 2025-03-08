using IVSoftware.Portable;
using IVSoftware.Portable.Xml.Linq;
using IVSoftware.Portable.Xml.Linq.XBoundObject;
using Newtonsoft.Json;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Xml.Linq;

namespace WithNotifyOnDescendants.Proto
{
    public class OBCPropertyChangedEventArgs : PropertyChangedEventArgs
    {
        public OBCPropertyChangedEventArgs(
            string? propertyName,
            XElement model
            ) : base(propertyName)
            => Model = model.SortAttributes<SortOrderNOD>();
        public XElement Model { get; }

        public override string ToString() => Model.ToString();
    }
    class XObjectChangedOrChangingEventArgs : XObjectChangeEventArgs
    {
        public XObjectChangedOrChangingEventArgs(XObjectChangeEventArgs e, bool isChanging )
            : base(e.ObjectChange)
        {
            IsChanging = isChanging;
        }
        public bool IsChanging { get; }
    }

    #region I N T E R N A L
    class XOBCObjectChangeEventArgs : XObjectChangeEventArgs
    {
        private readonly object _lock = new();
        WatchdogTimer WdtCleanup
        {
            get
            {
                if (_wdtCleanup is null)
                {
#if DEBUG
                    _wdtCleanup = new WatchdogTimer { Interval = TimeSpan.FromMinutes(1) };
#else
                        _wdtCleanup = new WatchdogTimer();
#endif
                    _wdtCleanup.RanToCompletion += (sender, e) =>
                    {
                        _pendingRemoveParent.Clear();
                        _pendingRemoveRefCount.Clear();
                    };
                }
                return _wdtCleanup;
            }
        }
        WatchdogTimer? _wdtCleanup = null;

        class Entry
        {
            public DateTime Timestamp { get; } = DateTime.Now;
        }
        private static readonly Dictionary<object, XElement> _pendingRemoveParent = new();
        private static readonly Dictionary<object, int> _pendingRemoveRefCount = new();
        public XOBCObjectChangeEventArgs(XObjectChangeEventArgs e, object? sender, bool isChanging)
            : base(e.ObjectChange)
        {
            if (sender is not null)
            {
                if (sender is XObject xobj)
                {
                    if (e.ObjectChange == XObjectChange.Remove)
                    {
                        if (isChanging)
                        {
                            if (xobj.Parent is null)
                            {
                                Debug.Fail("Expecting parent");
                            }
                            else
                            {
                                lock (_lock)
                                {
                                    _pendingRemoveParent[xobj] = xobj.Parent;
                                    if (_pendingRemoveRefCount.TryGetValue(xobj, out var count))
                                    {
                                        _pendingRemoveRefCount[xobj] = count + 1;
                                    }
                                    else _pendingRemoveRefCount[xobj] = 1;
                                }
                                WdtCleanup.StartOrRestart();
                            }
                        }
                        else
                        {
                            lock (_lock)
                            {
                                if (_pendingRemoveParent.TryGetValue(xobj, out var value))
                                {
                                    Parent = value;
                                    _pendingRemoveRefCount[xobj]--;
                                }
                            }
                        }
                    }
                    else
                    {
                        Parent = xobj.Parent;
                    }
                }
            }
        }
        public XElement? Parent { get; private set; }
    }
    #endregion I N T E R N A L

}
