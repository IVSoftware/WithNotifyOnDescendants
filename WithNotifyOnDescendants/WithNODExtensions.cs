using IVSoftware.Portable;
using IVSoftware.Portable.Threading;
using IVSoftware.Portable.Xml.Linq;
using IVSoftware.Portable.Xml.Linq.XBoundObject;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using static IVSoftware.Portable.Threading.Extensions;

[assembly: InternalsVisibleTo("CollectionsMSTest")]
namespace WithNotifyOnDescendants.Proto
{
    public static partial class WNODExtensions
    {
        private static XElement internalWithNotifyOnDescendants<T>(
            this T @this,
            PropertyChangedDelegate? onPropertyChanged,
            NotifyCollectionChangedDelegate? onCollectionChanged,
            XObjectChangeDelegate? onXObjectEvent = null)
        {
            if (@this?.GetType() is not null)
            {
                var model = new XElement(StdFrameworkName.model.ToString());
                model.SetBoundAttributeValue(
                    new NotifyInfo(
                        model: model,
                        onPropertyChanged,
                        onCollectionChanged,
                        onXObjectEvent: onXObjectEvent));
                return 
                    RunDiscoveryOnCurrentLevel(
                        @this,
                        model)
                    .SortAttributes<SortOrderNOD>();

            }
            else throw new ArgumentNullException(nameof(@this), message: "Receiver cannot be null.");
        }

        /// <summary>
        /// Attaches notification delegates to the descendants of the given object, 
        /// allowing property, collection, and object changes to be monitored. 
        /// This overload discards the model output.
        /// </summary>
        /// <typeparam name="T">The type of the object, which must have a parameterless constructor.</typeparam>
        /// <param name="this">The object whose descendants should be monitored.</param>
        /// <param name="onPC">The delegate to handle property change notifications (required).</param>
        /// <param name="onCC">The delegate to handle collection change notifications (optional).</param>
        /// <param name="onXO">The delegate to handle object change notifications (optional).</param>
        /// <returns>The same instance of <typeparamref name="T"/> for fluent chaining.</returns>
        public static T WithNotifyOnDescendants<T>(
            this T @this,
            PropertyChangedDelegate onPC,
            NotifyCollectionChangedDelegate? onCC = null,
            XObjectChangeDelegate? onXO = null)
            => @this.WithNotifyOnDescendants(out XElement _, onPC, onCC, onXO);

        public static void RefreshModel(this XElement model, object? newValue)
        {
            var attrsB4 = model.Attributes().ToArray();
            // Perform an unconditional complete reset.
            foreach (var element in model.Elements().ToArray())
            {
                foreach (var desc in element.DescendantsAndSelf().ToArray())
                {
                    desc.Remove();
                }
            }
            foreach (var attr in model.Attributes().ToArray())
            {
                switch (attr.Name.LocalName)
                {
                    case nameof(SortOrderNOD.name):
                    case nameof(SortOrderNOD.pi):
                        break;
                    case nameof(SortOrderNOD.statusnod):
                    case nameof(SortOrderNOD.instance):
                    case nameof(SortOrderNOD.runtimetype):
                    case nameof(SortOrderNOD.onpc):
                    case nameof(SortOrderNOD.oncc):
                    case nameof(SortOrderNOD.notifyinfo):
                        attr.Remove();
                        break;
                    default:
                        throw new NotImplementedException();
                }
            }
            if (newValue is null)
            {
                model.SetAttributeValue(StatusNOD.WaitingForValue);
            }
            else
            {
                RunDiscoveryOnCurrentLevel(newValue, model).SortAttributes<SortOrderNOD>();
            }
        }


        public static T? GetInstance<T>(this XElement @this, bool @throw = false)
            =>
            @this.Attribute(nameof(SortOrderNOD.instance)) is XBoundAttribute xba
            ? xba.Tag is T instance
                ? instance
                : @throw
                    ? throw new NullReferenceException($"Expecting {nameof(XBoundAttribute)}.Tag is not null.")
                    : default
            : throw new NotImplementedException();

        public static object? GetInstance(this XElement @this, bool @throw = false)
            =>
            @this.Attribute(nameof(SortOrderNOD.instance)) is XBoundAttribute xba
            ? xba.Tag is object instance
                ? instance
                : @throw
                    ? throw new NullReferenceException($"Expecting {nameof(XBoundAttribute)}.Tag is not null.")
                    : default
            : @throw
                ? throw new NotImplementedException()
                : null;

        static IEnumerable<PropertyInfo> GetNODProperties(this Type @this)
            => @this
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(_ =>
                    _.CanRead &&
                    _.GetMethod?.IsStatic != true &&
                    _.GetIndexParameters().Length == 0);

        public delegate void PropertyChangedDelegate(object sender, PropertyChangedEventArgs e);
        public delegate void NotifyCollectionChangedDelegate(object sender,NotifyCollectionChangedEventArgs e);
        public delegate void XObjectChangeDelegate(object sender, XObjectChangeEventArgs e);
        public class DelegateWrapper
        {
            public static implicit operator PropertyChangedDelegate(DelegateWrapper @this) => @this.pc;
            public static implicit operator DelegateWrapper(PropertyChangedDelegate @this) => new DelegateWrapper { pc = @this };

            public static implicit operator NotifyCollectionChangedDelegate(DelegateWrapper @this) => @this.cc;
            public static implicit operator DelegateWrapper(NotifyCollectionChangedDelegate @this) => new DelegateWrapper { cc = @this };

            public static implicit operator XObjectChangeDelegate(DelegateWrapper @this) => @this.xo;
            public static implicit operator DelegateWrapper(XObjectChangeDelegate @this) => new DelegateWrapper { xo = @this };

            PropertyChangedDelegate pc;
            NotifyCollectionChangedDelegate cc;
            XObjectChangeDelegate xo;
        }
        public static string ToTypeNameText(this Type @this)
        {
            if (@this?.FullName == null) return "Unknown";

            var fullName = @this.FullName.Split('`').First(); // Remove generic type info
            int lastPlusIndex = fullName.LastIndexOf('+');

            if (lastPlusIndex < 0) return fullName; // No nested class, return as is

            int lastDotIndex = fullName.LastIndexOf('.', lastPlusIndex);
            return fullName.Remove(lastDotIndex + 1, lastPlusIndex - lastDotIndex);
        }
        public static string ToShortTypeNameText(this Type @this)
            => @this.ToTypeNameText().Split('.').Last();

        public static string InSquareBrackets(this string @this) => $"[{@this}]";
        public static string InSquareBrackets(this Enum @this) => $"[{@this.ToString()}]";

        public static T? Value<T>(this XAttribute @this)
            => @this is XBoundAttribute xba
                ? xba.Tag is T value
                    ? value
                    : default
                : throw new InvalidCastException($"The receiver must be of type {nameof(XBoundAttribute)}");

        internal class NotifyInfo
        {
            public NotifyInfo(
                XElement model,
                PropertyChangedDelegate onPropertyChanged, 
                NotifyCollectionChangedDelegate? onCollectionChanged,
                XObjectChangeDelegate? onXObjectEvent)
            {
                bool error = true;
                if (onPropertyChanged is null)
                {
                    throw new ArgumentNullException(nameof(onPropertyChanged));
                }
                else
                {
                    error = false;
                    OnPropertyChanged = onPropertyChanged;
                }
                if (onCollectionChanged is not null)
                {
                    error = false;
                    OnCollectionChanged = onCollectionChanged;
                }
                if(error)
                    throw new InvalidOperationException(
                        $"Requires either or both: {nameof(PropertyChangedDelegate)} {nameof(NotifyCollectionChangedDelegate)}");
                model.Changed += (sender, e)
                    => onXObjectCommon(sender, new XObjectChangedOrChangingEventArgs(e, isChanging: false));
                model.Changing += (sender, e)
                    => onXObjectCommon(sender, new XObjectChangedOrChangingEventArgs(e, isChanging: true));
                OnXObjectEvent = onXObjectEvent;
            }

            private void onXObjectCommon(object? sender, XObjectChangedOrChangingEventArgs e)
            {
                XElement? xtarget =
                    sender is XElement xel
                    ? xel
                    : sender is XAttribute xattr
                    ? xattr.Parent
                    : null;
                Debug
                    .WriteLineIf(
                        true,
                        $"[250306.A {sender?.GetType().Name}.{e.ObjectChange} {(e.IsChanging ? "Changing" : "Changed")}] {xtarget?.ToShallow().SortAttributes<SortOrderNOD>().ToString()}");

                if (xtarget is not null)
                {
                    switch (e.ObjectChange)
                    {
                        case XObjectChange.Remove:
                            if (e.IsChanging)   // Handle remove while the parent is still intact.
                            {
                                switch (sender)
                                {
                                    case XElement:
                                        localUnsubscribeNotify();
                                        break;
                                }
                                void localUnsubscribeNotify()
                                {
                                    foreach (var desc in xtarget.DescendantsAndSelf())
                                    {
                                        if (desc.To<INotifyPropertyChanged>() is { } inpc1 &&
                                        desc.To<PropertyChangedEventHandler>() is { } onpc)
                                        {
                                            inpc1.PropertyChanged -= onpc;
                                            desc.OnAwaited(new AwaitedEventArgs(args: $"Removing INPC Subscription"));
                                            desc.OnAwaited(new AwaitedEventArgs(args: $"{e.ObjectChange} {desc.ToShallow()}"));
                                        }
                                        if (desc.To<INotifyCollectionChanged>() is { } incc1 &&
                                            desc.To<NotifyCollectionChangedEventHandler>() is { } oncc)
                                        {
                                            incc1.CollectionChanged -= oncc;
                                            desc.OnAwaited(new AwaitedEventArgs(args: $"Removing INCC Subscription"));
                                            desc.OnAwaited(new AwaitedEventArgs(args: $"{e.ObjectChange} {desc.ToShallow()}"));
                                        }
                                    }
                                }
                            }
                            break;
                        case XObjectChange.Add:
                        case XObjectChange.Name:
                        case XObjectChange.Value:
                        default:
                            break;
                    }
                    OnXObjectEvent?.Invoke(sender, e);
                }
            }

            public XObjectChangeDelegate? OnXObjectEvent { get; }
            public PropertyChangedDelegate? OnPropertyChanged { get; }
            public  NotifyCollectionChangedDelegate? OnCollectionChanged { get; }
            // TODO - GC
            public WatchdogTimer WDTDiscard
            {
                get
                {
                    if (_wdtDiscard is null)
                    {
                        _wdtDiscard = new WatchdogTimer();
                    }
                    return _wdtDiscard;
                }
            }
            WatchdogTimer? _wdtDiscard = null;
        }

        #region I N T E R N A L
        static int _debugCountE = 0, _debugCountA = 0, _debugMin = 0;
        static string? _prevEventE, _prevEventA;

        private static void AttachChangesDebugger(XElement model)
        {
            _prevEventA = _prevEventE = null;
            model.Changing -= localDebugChanging;
            model.Changing += localDebugChanging;
            model.Changed -= localDebugChanged;
            model.Changed += localDebugChanged;

            void localDebugChanging(object? sender, XObjectChangeEventArgs e)
            {
                switch (sender)
                {
                    case XAttribute xattr:
                        if (_debugCountE >= _debugMin)  // Looks WRONG but is RIGHT. Both A and E trigger on E count.
                        {
                            var eventA = $"{Environment.NewLine}{sender.GetType().Name} Changing.{xattr.Name}.{e.ObjectChange}";
                            if (_prevEventA != eventA)
                            {
                                _prevEventA = eventA;
                                Debug.WriteLine(eventA);
                                Debug.WriteLine($"[{_debugCountA.ToString().PadLeft(4, '0')}] {DateTime.Now}");
                                Debug.WriteLine(
                                    model
                                    .AncestorsAndSelf()
                                    .Last()
                                    .ToString());
                            }
                        }
                        _debugCountA++;                 // But for this reason we can't combine the trigger.
                        break;
                    case XElement xel:
                        if (_debugCountE >= _debugMin)
                        {
                            var eventE = $"{Environment.NewLine}{sender.GetType().Name} Changing.{xel.Name}.{e.ObjectChange}";
                            if (_prevEventE != eventE)
                            {
                                _prevEventE = eventE;
                                Debug.WriteLine(eventE);
                                Debug.WriteLine($"[{_debugCountE.ToString().PadLeft(4, '0')}] {DateTime.Now}");
                                Debug.WriteLine(
                                    model
                                    .AncestorsAndSelf()
                                    .Last()
                                    .ToString());
                            }
                        }
                        _debugCountE++;
                        break;
                }
            }

            void localDebugChanged(object? sender, XObjectChangeEventArgs e)
            {
                switch (sender)
                {
                    case XAttribute xattr:
                        if (_debugCountE >= _debugMin)  // Looks WRONG but is RIGHT. Both A and E trigger on E count.
                        {
                            var eventA = $"{Environment.NewLine}{sender.GetType().Name} Changed.{xattr.Name}.{e.ObjectChange}";
                            if (_prevEventA != eventA)
                            {
                                _prevEventA = eventA;
                                Debug.WriteLine(eventA);
                                Debug.WriteLine($"[{_debugCountA.ToString().PadLeft(4, '0')}] {DateTime.Now}");
                                Debug.WriteLine(
                                    model
                                    .AncestorsAndSelf()
                                    .Last()
                                    .ToString());
                            }
                        }
                        _debugCountA++;                 // But for this reason we can't combine the trigger.
                        break;
                    case XElement xel:
                        if (_debugCountE >= _debugMin)
                        {
                            var eventE = $"{Environment.NewLine}{sender.GetType().Name} Changed.{xel.Name}.{e.ObjectChange}";
                            if (_prevEventE != eventE)
                            {
                                _prevEventE = eventE;
                                Debug.WriteLine(eventE);
                                Debug.WriteLine($"[{_debugCountE.ToString().PadLeft(4, '0')}] {DateTime.Now}");
                                Debug.WriteLine(
                                    model
                                    .AncestorsAndSelf()
                                    .Last()
                                    .ToString());
                            }
                        }
                        _debugCountE++;
                        break;
                }
            }
        }
        internal static bool IsEnumOrValueTypeOrString(this object @this)
            => @this is Enum || @this is ValueType || @this is string;
        internal static bool IsNotEnumOrValueTypeOrString(this object @this) =>
            !@this.IsEnumOrValueTypeOrString();
        internal static bool IsEnumOrValueTypeOrString(this Type @this)
            => @this.IsEnum || @this.IsValueType || Equals(@this, typeof(string));
        internal static bool IsNotEnumOrValueTypeOrString(this Type @this) =>
            !@this.IsEnumOrValueTypeOrString();
        #endregion I N T E R N A L
    }
}
