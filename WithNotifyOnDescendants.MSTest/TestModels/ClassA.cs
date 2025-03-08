using WithNotifyOnDescendants.Proto;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml.Linq;
using IVSoftware.Portable.Threading;
using static WithNotifyOnDescendants.Proto.WNODExtensions;
using System;
using IVSoftware.Portable.Xml.Linq.XBoundObject;

namespace WithNotifyOnDescendants.Proto.MSTest.TestModels
{
    public class ClassA
    {
        public ClassA()
        {
            // ===========================================================================================
            // INITIALIZE
            // "The idea is to have a collection of objects that not only notify changes
            //  to their own properties but also notify changes in their nested objects."
            this
                .WithNotifyOnDescendants(   // The "Extension"
                out XElement originModel,   // The root node of the generated model
                OnPropertyChanged,          // Who to call when PropertyChanged events are raised.
                OnCollectionChanged         // Who to call when NotifyClooectionChanged events are raised.
            );
            // We 'could' just apply the extension to BCollection (making it a 'FullyObservableCollection')
            // but it's even more powerful to go one level up from that.
            // ===========================================================================================
            OriginModel = originModel;
        }
        public int TotalCost { get; private set; } = 0;
        private void RefreshTotalCost(SenderEventPair sep)
        {
            TotalCost = BCollection.Sum(_ => _.C.Cost);

            // MS Test Reporting only:
            switch (sep.e)
            {
                case PropertyChangedEventArgs:
                    switch (sep.PropertyName)
                    {
                        case nameof(ClassC.Cost):
                            // The event model is navigable!
                            if (sep.SenderModel?.AncestorOfType<ClassB>() is { } b )
                            {
                                localSendDebugTrackingMessageToMSTest(b);
                            }
                            break;
                    }
                    break;
                case NotifyCollectionChangedEventArgs:
                    sep
                    .NotifyCollectionChangedEventArgs?
                    .NewItems?
                    .OfType<ClassB>()
                    .Where(_=>_.C.Cost != 0)
                    .ToList()
                    .ForEach(b =>localSendDebugTrackingMessageToMSTest(b));
                    break;
            }
            void localSendDebugTrackingMessageToMSTest(ClassB b)
            {
                this.OnAwaited(new AwaitedEventArgs(
                        args: $"Item at index {BCollection.IndexOf(b)} shows a new cost value of {b.C.Cost}."));
                 this.OnAwaited(new AwaitedEventArgs(
                     args: $"Total of C.Cost {TotalCost}"));
            }
        }
        public ObservableCollection<ClassB> BCollection { get; } = new();

        [IgnoreNOD]
        public XElement OriginModel { get; }

        // Forward this for testing purposes
        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            RefreshTotalCost(new SenderEventPair(sender,e));
            CollectionChanged?.Invoke(sender, e);
        }
        protected virtual void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(ClassC.Cost):
                    RefreshTotalCost(new SenderEventPair(sender, e));
                    break;
            }
            // [Careful]
            // - Forward `sender` not `this`.
            // - Basically, this is for testing purposes since ClassA
            //   doesn't implement INPC as things now stand.
            PropertyChanged?.Invoke(sender, e); 
        }
        public event PropertyChangedEventHandler? PropertyChanged;
        public event NotifyCollectionChangedEventHandler? CollectionChanged;
    }
}
