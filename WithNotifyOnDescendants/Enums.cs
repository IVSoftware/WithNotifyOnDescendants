using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WithNotifyOnDescendants.Proto
{
    [Flags]
    public enum StatusNOD
    {
        /// <summary>
        /// The property is set to an instance that does not expose any change notification events,
        /// despite being a type that could theoretically do so.
        /// </summary>
        /// <remarks>
        /// This means that the instance is a reference type whose declared type does not implement 
        /// INotifyPropertyChanged or INotifyCollectionChanged. However, this does not 
        /// rule out the possibility that another type in the same hierarchy 
        /// (such as a derived type) *could* implement these interfaces.
        /// 
        /// This is distinct from <see cref="NoObservableMemberProperties"/>, which indicates 
        /// that the type itself is fundamentally incapable of hosting observable sources.
        /// 
        /// Example cases:
        /// - A plain POCO (Plain Old CLR Object) that does not implement change tracking
        /// - A dynamically resolved object that *could* be of a type that implements INPC or INCC, 
        ///   but in this case, its declared type does not
        /// - A base class that has no observable properties, though a derived class might
        /// 
        /// If this flag is set, the instance does not participate in change notifications, 
        /// but it is still a valid reference type that could theoretically support them.
        /// </remarks>
        NoAvailableChangedEvents = 0x00,

        /// <summary>
        /// The property is set to an instance that implements INotifyPropertyChanged.
        /// </summary>
        /// <remarks>
        /// This might be the case even if the property itself is declared
        /// (for example) as object, i.e. some class that is not native INPC.
        /// </remarks>
        INPCSource = 0x01,

        /// <summary>
        /// The property is set to an instance that implements INotifyCollectionChanged.
        /// </summary>
        /// <remarks>
        /// This might be the case even if the property itself is declared
        /// (for example) as object, i.e. some class that is not native INPC.
        /// </remarks>
        INCCSource = 0x02,

        /// <summary>
        /// GENERALLY: 
        /// The getter returned a default value of null
        /// SO:
        /// - If parent raises PropertyChanged on this property, the
        ///   object will be evaluated at that time and if it implements
        ///   INPC then its event will be subscribed, otherwise no.
        /// HOWEVER: 
        /// You may wish to avoid calling the getter if the property is designed
        /// to load on demand. 
        /// SO:
        /// - To avoid instantiating a singleton use the [WaitForObject] Attribute to 
        ///   inform the discovery process explicitly and avoid calling the getter. 
        /// </summary>
        WaitingForValue = 0x08,

        /// <summary>
        /// The instance does not have any member properties that could serve as observable sources.
        /// </summary>
        /// <remarks>
        /// This means that none of the instance's properties are expected to implement 
        /// INotifyPropertyChanged or INotifyCollectionChanged.
        /// 
        /// This is commonly the case for:
        /// - Value types (e.g., structs, primitives)
        /// - Enums
        /// - Any type that contains only non-notifiable properties
        /// 
        /// If this flag is set, the discovery process does not need to check 
        /// the instance's member properties for observability.
        /// </remarks>
        NoObservableMemberProperties = 0x80,
    }
    public enum SortOrderNOD
    {
        /// <summary>
        /// The property name, OR in the case of the 
        /// origin model this holds the name of the type.
        /// </summary>
        name,

        /// <summary>
        /// When part of an observable bindable collection, this optional
        /// attribute broadcasts status including node-specific bindings.
        /// </summary>
        statusnod,

        /// <summary>
        /// XBound object containing PropertyInfo.
        /// </summary>
        pi,

        /// <summary>
        /// XBound instance of a reference type.
        /// </summary>
        instance,

        /// <summary>
        /// For Enum, ValueType, and String, this indicates that
        /// the instance of this property has a runtime type that
        /// differs from its declared type (e.g., object).
        /// </summary>
        runtimetype,

        /// <summary>
        /// OnPropertyChanged delegate
        /// </summary>
        onpc,

        /// <summary>
        /// OnCollectionChanged delegate
        /// </summary>
        oncc,

        /// <summary>
        /// Configuration of delegates for notifications.
        /// </summary>
        notifyinfo,
    }
    enum StdFrameworkName
    {
        xel,
        model,
        OnPC,
        OnCC,
        member,
    }
}
