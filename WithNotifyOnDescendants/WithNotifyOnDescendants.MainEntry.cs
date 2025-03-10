using IVSoftware.Portable.Threading;
using IVSoftware.Portable.Xml.Linq;
using IVSoftware.Portable.Xml.Linq.XBoundObject;
using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Xml.Linq;
using WithNotifyOnDescendants.Proto.Internal;

namespace WithNotifyOnDescendants.Proto
{
    partial class WNODExtensions
    {
        /// <summary>
        /// Attaches notification delegates to the descendants of the given object,
        /// allowing for property changes, collection changes, and object changes to be monitored.
        /// </summary>
        /// <typeparam name="T">The type of the object, which must have a parameterless constructor.</typeparam>
        /// <param name="this">The object whose descendants should be monitored.</param>
        /// <param name="model">The XElement representing the model, returned as an output parameter.</param>
        /// <param name="onPC">The delegate to handle property change notifications (required).</param>
        /// <param name="onCC">The delegate to handle collection change notifications (optional).</param>
        /// <param name="onXO">The delegate to handle object change notifications (optional).</param>
        /// <returns>The same instance of <typeparamref name="T"/> for fluent chaining.</returns>
        public static T WithNotifyOnDescendants<T>(
            this T @this,
            out XElement model,
            PropertyChangedDelegate onPC,
            NotifyCollectionChangedDelegate? onCC = null,
            XObjectChangeDelegate? onXO = null)
        {
            model = @this.internalWithNotifyOnDescendants(onPC, onCC, onXO);
            return @this;
        }

        /// <summary>
        /// Recursive discovery method.
        /// </summary>
        private static XElement RunDiscoveryOnCurrentLevel(
                object currentInstance,
                XElement model)
        {
            var type = currentInstance.GetType();
            if (!model.Ancestors().Any())
            {
                model.SetAttributeValue(nameof(SortOrderNOD.name), $"(Origin){type.ToShortTypeNameText()}");
            }
            model = model ?? throw new ArgumentNullException(paramName: nameof(model));

            if (ReferenceEquals(
                currentInstance,
                model.Attribute(SortOrderNOD.instance.ToString())?.Value<object>()))
            {
                Debug.Fail("Unexpected! This is supposed to be a property 'change'.");
            }
            else
            {
                // Allow recursion only for types that might host other INPCs.
                if(currentInstance.IsEnumOrValueTypeOrString())
                {   // Note that the runtime type and the declared type (e.g. 'object')
                    if( model.To<PropertyInfo>() is { } pi &&
                        !Equals(type, pi.PropertyType) )
                    {
                        model.SetAttributeValue(
                            nameof(SortOrderNOD.runtimetype), 
                            type.ToTypeNameText());
                    }
                    model.SetAttributeValue(nameof(SortOrderNOD.statusnod), nameof(StatusNOD.NoObservableMemberProperties));
                    return model; // Short-circuit regardless in this case.
                }
                else
                {
                    model.SetBoundAttributeValue(
                        tag: currentInstance,
                        name: SortOrderNOD.instance.ToString(),
                        text: type.ToTypeNameText().InSquareBrackets());
                }
            }

            StatusNOD statusNOD = 0;
            NotifyInfo notifyInfo = model.AncestorOfType<NotifyInfo>(includeSelf: true, @throw: true);
            if (currentInstance is INotifyPropertyChanged inpc)
            {
                statusNOD |= StatusNOD.INPCSource;
                if (model.Attribute(nameof(SortOrderNOD.onpc)) is null)   // Check for refresh before adding
                {
                    if (notifyInfo.OnPropertyChanged is not null)
                    {
                        PropertyChangedEventHandler onPC = (sender, e) =>
                        {
                            if (model
                                .Elements()
                                .FirstOrDefault(_ =>
                                    _
                                    .Attribute(nameof(SortOrderNOD.name))?
                                    .Value == e.PropertyName) is
                                { } propertyModel)
                            {
                                if (propertyModel.TryGetAttributeValue(out StatusNOD value))
                                {
                                    if (propertyModel.To<PropertyInfo>() is { } pi)
                                    {
                                        if (pi.IsEnumOrValueTypeOrString())
                                        {   /* G T K */
                                            // [Careful]
                                            // We can only do this on PropertyType not on Instance Type.
                                            // That is, a property of type 'object' can go in and out
                                            // of being any other status. But if the property type is
                                            // fixed that way, then it is safe to ignore.
                                        }
                                        else
                                        {
                                            propertyModel.RefreshModel(newValue: pi.GetValue(sender));
                                        }
                                    }
                                }
                                notifyInfo.OnPropertyChanged?.Invoke(propertyModel, e);
                            }
                            else
                            {
                                // This event is "bubbling up" and was
                                // not raised by a direct child of this model.
                            }
                        };
                        model.SetBoundAttributeValue(
                            tag: onPC,
                            SortOrderNOD.onpc,
                            text: StdFrameworkName.OnPC.InSquareBrackets());
                        inpc.PropertyChanged += onPC;
                    }
                }
            }
            if (currentInstance is INotifyCollectionChanged incc)
            {
                statusNOD |= StatusNOD.INCCSource;
                if (model.Attribute(SortOrderNOD.oncc.ToString()) is null)   // Check for refresh
                {
                    NotifyCollectionChangedEventHandler onCC = (sender, e) =>
                    {
                        localOnCollectionChanged(model, e);
                        notifyInfo?.OnCollectionChanged?.Invoke(model, e);
                    };
                    model.SetBoundAttributeValue(
                        tag: onCC,
                        SortOrderNOD.oncc,
                        text: StdFrameworkName.OnCC.InSquareBrackets());

                    incc.CollectionChanged += onCC;

                    void localOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
                    {
                        if (sender is XElement model &&
                            model.AncestorOfType<NotifyInfo>(includeSelf: true) is { } notifyInfo)
                        {
                            switch (e.Action)
                            {
                                case NotifyCollectionChangedAction.Add: onAdd(); break;
                                case NotifyCollectionChangedAction.Remove: onRemove(); break;
                                case NotifyCollectionChangedAction.Replace: onReplace(); break;
                                case NotifyCollectionChangedAction.Reset: onReset(); break;
                            }

                            void onAdd()
                            {
                                e.NewItems?.OfType<object>().ToList().ForEach(newItem =>
                                {
                                    _ =
                                        notifyInfo.OnPropertyChanged is not null
                                        ? newItem
                                            .WithNotifyOnDescendants(
                                                out XElement addedModel,
                                                notifyInfo.OnPropertyChanged,
                                                notifyInfo.OnCollectionChanged,
                                                notifyInfo.OnXObjectEvent)
                                        : throw new NullReferenceException("Unexpected indicates that OnPropertyChanged is null");
                                    model.Add(addedModel);
                                });
                            }

                            void onRemove()
                            {
                                e.OldItems?.OfType<object>().ToList().ForEach(_ =>
                                {
                                    var removeModel = 
                                        model
                                        .Elements()
                                        .First(desc => ReferenceEquals(_, desc.GetInstance()));
                                    removeModel.Remove();
                                });
                            }

                            void onReplace()
                            {
                                onRemove();
                                onAdd();
                            }
                            void onReset()
                            {
                                foreach (var removeModel in model.Elements().ToArray())
                                {
                                    removeModel.Remove();
                                }
                            }
                        }
                    }
                }
            }
            model.SetAttributeValue(statusNOD);

            // Latent stale associations can be avoided
            // by disconnecting each child node individually.
            foreach (var el in model.Elements().ToArray())
            {
                foreach (var desc in el.DescendantsAndSelf().ToArray())
                {
                    desc.Remove();
                }
            }
            foreach (var pi in 
                     type.GetNODProperties()
                     .Where(_=>_.GetCustomAttribute<IgnoreNODAttribute>() is null))
            {
                var member = new XElement($"{StdFrameworkName.member}");
                member.SetAttributeValue(nameof(SortOrderNOD.name), pi.Name);
                member.SetBoundAttributeValue(
                    name: SortOrderNOD.pi.ToString(),
                    tag: pi,
                    // text: pi.ToPropertyInfoText().InSquareBrackets()
                    text: pi.PropertyType.ToTypeNameText().InSquareBrackets()
                );
                model.Add(member);
                if(pi.GetCustomAttribute<WaitForValueCreatedAttribute>() is { } attr)
                {
                    if(type.GetProperty(attr.IsValueCreatedPropertyName) is { } piInspector)
                    {
                        if (Equals(piInspector.GetValue(currentInstance), true))
                        {   /* G T K */
                            // Singleton or Lazy T has a value. Proceed!
                        }
                        else
                        {
                            // Do NOT invoke getter. It will prematurely invoke the singleton.{
                            member.SetAttributeValue(StatusNOD.WaitingForValue);
                            continue;

                        }
                    }
                }
                if (pi.GetValue(currentInstance) is { } childInstance)
                {
                    RunDiscoveryOnCurrentLevel(childInstance, member);
                }
                else
                {
                    member.SetAttributeValue(StatusNOD.WaitingForValue);
                }
            }
            // If items are in the list aleady, synchronize them.
            if (currentInstance is IList list)
            {
                foreach (var item in list.OfType<object>())
                {
                    _ =
                        notifyInfo.OnPropertyChanged is not null
                        ? item
                            .WithNotifyOnDescendants(
                                out XElement addedModel,
                                notifyInfo.OnPropertyChanged,
                                notifyInfo.OnCollectionChanged,
                                notifyInfo.OnXObjectEvent)
                        : throw new NullReferenceException("Unexpected indicates that OnPropertyChanged is null");
                    model.Add(addedModel);
                }
            }
            return model;
        }
    }
}
