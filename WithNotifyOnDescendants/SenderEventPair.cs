using WithNotifyOnDescendants.Proto;
using IVSoftware.Portable.Xml.Linq;
using IVSoftware.Portable.Xml.Linq.XBoundObject;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace WithNotifyOnDescendants.Proto
{
    public class SenderEventPair
    {
        public static implicit operator EventArgs(SenderEventPair @this)
            => @this.e;
        public SenderEventPair(object sender, EventArgs e)
        {
            this.sender = sender;
            this.SenderModel = sender as XElement;
            this.e = e;
            PropertyChangedEventArgs = e as PropertyChangedEventArgs;
            NotifyCollectionChangedEventArgs = e as NotifyCollectionChangedEventArgs;
            XObjectChangeEventArgs = e as XObjectChangeEventArgs;
            PropertyName = e switch
            {
                // This class supports 'any' EventArgs class so set
                // this property only if the args type supports it.
                PropertyChangedEventArgs pc => pc.PropertyName,
                PropertyChangingEventArgs pc => pc.PropertyName,
                _=> null
            };

        }
        public object sender { get; }
        public EventArgs e { get; }
        public PropertyChangedEventArgs? PropertyChangedEventArgs { get; }
        public NotifyCollectionChangedEventArgs? NotifyCollectionChangedEventArgs { get; }
        public XObjectChangeEventArgs? XObjectChangeEventArgs { get; }
        public string? PropertyName { get; }
        public XElement? SenderModel { get; }
        public XElement OriginModel
        {
            get
            {
                if (_originModel is null)
                {
                    _originModel =
                        SenderModel?
                        .AncestorsAndSelf()
                        .Last() ?? throw new NullReferenceException();
                }
                return _originModel;
            }
        }
        XElement? _originModel = null;
        public override string? ToString()
        {
            if(PropertyChangedEventArgs is not null)
            {

            }
            else if(NotifyCollectionChangedEventArgs is not null)
            {

            }
            else if(XObjectChangeEventArgs is not null)
            {
                return $"[{sender.GetType().Name}.{XObjectChangeEventArgs.ObjectChange}] {sender}";
            }
            return base.ToString();
        }
    }
}
