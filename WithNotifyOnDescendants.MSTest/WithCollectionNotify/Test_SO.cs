using CollectionsMSTest.OBC;
using WithNotifyOnDescendants.Proto;
using static IVSoftware.Portable.Threading.Extensions;
using IVSoftware.Portable.Xml.Linq.XBoundObject;
using IVSoftware.WinOS.MSTest.Extensions;
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Xml.Linq;
using IVSoftware.Portable.Threading;
using WithNotifyOnDescendants.Proto.MSTest.TestModels;

namespace WithNotifyOnDescendants.Proto.MSTest;

[TestClass]
public class TestClass_SO
{
    #region S E T U P
    [Flags]
    internal enum ClearQueue
    {
        PropertyChangedEvents = 0x1,
        NotifyCollectionChangedEvents = 0x2,
        XObjectChangeEvents = 0x4,
        OnAwaitedEvents = 0x8,
        All = 0xF
    }
    string actual = string.Empty, expected = string.Empty, joined = string.Empty;
    XElement? model = null;
    SenderEventPair currentEvent = null!;
    Queue<SenderEventPair> eventsPC = new();
    Queue<SenderEventPair> eventsCC = new();
    Queue<SenderEventPair> eventsXO = new();
    Queue<SenderEventPair> eventsOA = new();
    void clearQueues(ClearQueue clearQueue = ClearQueue.PropertyChangedEvents | ClearQueue.NotifyCollectionChangedEvents) 
    { 
        if(clearQueue.HasFlag(ClearQueue.PropertyChangedEvents)) eventsPC.Clear();
        if (clearQueue.HasFlag(ClearQueue.NotifyCollectionChangedEvents)) eventsCC.Clear();
        if (clearQueue.HasFlag(ClearQueue.XObjectChangeEvents)) eventsXO.Clear();
        if (clearQueue.HasFlag(ClearQueue.OnAwaitedEvents)) eventsOA.Clear(); 
    }
    Random rando = new Random(1);
    private void OnAwaited(object? sender, AwaitedEventArgs e)
        => eventsOA.Enqueue(new SenderEventPair(sender ?? throw new NullReferenceException(), e));
    [TestInitialize]
    public void TestInitialize()
    {
        Awaited += OnAwaited;
        actual = expected = joined = string.Empty;
        rando = new Random(1);
        clearQueues();
    }

    [TestCleanup]
    public void TestCleanup() => Awaited -= OnAwaited;
    #endregion S E T U P

    /// <summary>
    /// Unit test for verifying the event-driven XML model representation of `ClassA`.
    /// This test ensures that:
    /// 1. Adding instances of `ClassB` to `BCollection` triggers collection change events.
    /// 2. Changing the `Cost` property of `ClassC` within `BCollection` triggers property change events.
    /// 3. The XML model correctly reflects the structure and event subscriptions.
    /// 4. The calculated `C.Cost` totals match expected values.
    /// </summary>
    [TestMethod]
    public void Test_SO_79467031_5438626()
    {
        // Make an instance of class A which calls "The Extension" in its CTor.
        ClassA A = new();

        // Queue received PropertyChanged events so we can test them
        A.PropertyChanged += (sender, e) => eventsPC.Enqueue(
            new SenderEventPair(sender ?? throw new NullReferenceException(), e));

        // Queue received NotifyCollectionChanged events so we can test them
        A.CollectionChanged += (sender, e) => eventsCC.Enqueue(
            new SenderEventPair(sender ?? throw new NullReferenceException(), e));

        // EXPECT
        // - ClassA is at the root.
        // - ClassA is shown to have a TotalCost property.
        // - The `notify info` attribute contains the delegates to invoke when child elements raise events.
        // - There is an empty BCollection below it.
        // - BCollection has been identified as a source of INotifyPropertyChanged and INotifyCollectionChanged events.
        // - BCollection is also shown to have a `Count` property that is `Int32`.
        subtestInspectInitialModelForClassA();
        // EXPECT
        // - An instance of ClassB is added to BCollection.
        // - A NotifyCollectionChanged event is triggered with action `Add`.
        // - A PropertyChanged event is triggered for the `Count` property of BCollection.
        // - The new ClassB instance appears in the model within BCollection.
        // - ClassB contains an instance of ClassC with observable properties Cost and Currency.
        subtestAddClassBThenViewModel();
        // EXPECT
        // - The Cost property in ClassC is updated.
        // - A PropertyChanged event is triggered for the Cost property.
        // - The TotalCost property of ClassA is recalculated accordingly.
        subtestExerciseNestedCostProperty();
        // EXPECT
        // - A new instance of ClassB is created and assigned an initial Cost value.
        // - The instance is added to BCollection.
        // - The TotalCost property of ClassA is updated to reflect the new cost.
        subtestAddClassBInstanceWithNonZeroInitialCost();
        // EXPECT
        // - Three new instances of ClassB are created and added to BCollection.
        // - A total of five ClassB instances should now be visible in the model.
        // - Three NotifyCollectionChanged events with action `Add` are triggered.
        // - Three PropertyChanged events for `Count` are triggered as BCollection updates.
        // - Each added ClassB contains a ClassC instance with observable properties Cost and Currency.
        // - Updating the Cost property in each new ClassC instance triggers five cost updates.
        // - The cumulative TotalCost value should be updated accordingly.
        subtestAdd3xClassBInstanceWithZeroInitialCost();
        // EXPECT
        // - The last item in BCollection is retrieved and removed.
        // - A NotifyCollectionChanged event with action `Remove` is triggered.
        // - Unsubscribe events are processed for the removed item and its dependencies.
        // - Further property changes on the removed instance do not trigger PropertyChanged events.
        subtestRemoveLastItemAndVerifyUnsubscribe();
        // EXPECT
        // - All items in BCollection are retrieved and then removed using Clear().
        // - The model should reflect the empty state of BCollection.
        // - Multiple unsubscribe events should be triggered for each removed instance.
        // - Further property changes on the removed instances do not trigger PropertyChanged events.
        subtestClearListAndVerifyUnsubscribe();

        #region S U B T E S T S
        // EXPECT
        // - ClassA is at the root.
        // - ClassA is shown to have a TotalCost property.
        // - The `notify info` attribute contains the delegates to invoke when child elements raise events.
        // - There is an empty BCollection below it.
        // - BCollection has been identified as a source of INotifyPropertyChanged and INotifyCollectionChanged events.
        // - BCollection is also shown to have a `Count` property that is `Int32`.
        void subtestInspectInitialModelForClassA()
        {
            actual = A.OriginModel.SortAttributes<SortOrderNOD>().ToString();

            expected = @" 
<model name=""(Origin)ClassA"" statusnod=""NoAvailableChangedEvents"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ClassA]"" notifyinfo=""[NotifyInfo]"">
  <member name=""TotalCost"" statusnod=""NoObservableMemberProperties"" pi=""[System.Int32]"" />
  <member name=""BCollection"" statusnod=""INPCSource, INCCSource"" pi=""[System.Collections.ObjectModel.ObservableCollection]"" instance=""[System.Collections.ObjectModel.ObservableCollection]"" onpc=""[OnPC]"" oncc=""[OnCC]"">
    <member name=""Count"" statusnod=""NoObservableMemberProperties"" pi=""[System.Int32]"" />
  </member>
</model>";
            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting model of ClassA with TotalCost property and the BCollection which is empty"
            );
        }

        // EXPECT
        // - An instance of ClassB is added to BCollection.
        // - A NotifyCollectionChanged event is triggered with action `Add`.
        // - A PropertyChanged event is triggered for the `Count` property of BCollection.
        // - The new ClassB instance appears in the model within BCollection.
        // - ClassB contains an instance of ClassC with observable properties Cost and Currency.
        void subtestAddClassBThenViewModel()
        {
            clearQueues();
            A.BCollection.Add(new());

            // Inspect CC event
            currentEvent = eventsCC.DequeueSingle();
            Assert.AreEqual(
                NotifyCollectionChangedAction.Add,
                currentEvent.NotifyCollectionChangedEventArgs?.Action,
                "Expecting response to item added.");
            { }
            // Inspect PC event
            currentEvent = eventsPC.DequeueSingle();
            Assert.AreEqual(
                nameof(IList.Count),
                currentEvent.PropertyChangedEventArgs?.PropertyName,
                "Expecting response to BCollection count changed.");

            actual = currentEvent.OriginModel.SortAttributes<SortOrderNOD>().ToString();
            expected = @" 
<model name=""(Origin)ClassA"" statusnod=""NoAvailableChangedEvents"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ClassA]"" notifyinfo=""[NotifyInfo]"">
  <member name=""TotalCost"" statusnod=""NoObservableMemberProperties"" pi=""[System.Int32]"" />
  <member name=""BCollection"" statusnod=""INPCSource, INCCSource"" pi=""[System.Collections.ObjectModel.ObservableCollection]"" instance=""[System.Collections.ObjectModel.ObservableCollection]"" onpc=""[OnPC]"" oncc=""[OnCC]"">
    <member name=""Count"" statusnod=""NoObservableMemberProperties"" pi=""[System.Int32]"" />
    <model name=""(Origin)ClassB"" statusnod=""INPCSource"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ClassB]"" onpc=""[OnPC]"" notifyinfo=""[NotifyInfo]"">
      <member name=""C"" statusnod=""INPCSource"" pi=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ClassC]"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ClassC]"" onpc=""[OnPC]"">
        <member name=""Cost"" statusnod=""NoObservableMemberProperties"" pi=""[System.Int32]"" />
        <member name=""Currency"" statusnod=""NoObservableMemberProperties"" pi=""[System.Int32]"" />
      </member>
    </model>
  </member>
</model>";
            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting the A model reflects the new child."
            );
        }

        // EXPECT
        // - The Cost property in ClassC is updated.
        // - A PropertyChanged event is triggered for the Cost property.
        // - The TotalCost property of ClassA is recalculated accordingly.
        void subtestExerciseNestedCostProperty()
        {
            var classB = A.BCollection.First();
            classB.C.Cost = rando.Next(Int16.MaxValue); // Raise property change
            currentEvent = eventsPC.DequeueSingle();
            actual = currentEvent.SenderModel.SortAttributes<SortOrderNOD>().ToString();
            expected = @" 
<member name=""Cost"" statusnod=""NoObservableMemberProperties"" pi=""[System.Int32]"" />";
            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting property changed event for Cost."
            );

            // Is A.TotalCost updated?
            Assert.AreEqual(
                8148,
                A.TotalCost,
                "Expecting that A has recalculated Total Cost");
        }

        // EXPECT
        // - A new instance of ClassB is created and assigned an initial Cost value.
        // - The instance is added to BCollection.
        // - The TotalCost property of ClassA is updated to reflect the new cost.
        void subtestAddClassBInstanceWithNonZeroInitialCost()
        {
            var newClassB = new ClassB();
            newClassB.C.Cost = rando.Next(Int16.MaxValue);
            A.BCollection.Add(newClassB);

            Assert.AreEqual(
                11776,
                A.TotalCost,
                "Expecting an updated value from the addition of ClassB");
        }

        // EXPECT
        // - Three new instances of ClassB are created and added to BCollection.
        // - A total of five ClassB instances should now be visible in the model.
        // - Three NotifyCollectionChanged events with action `Add` are triggered.
        // - Three PropertyChanged events for `Count` are triggered as BCollection updates.
        // - Each added ClassB contains a ClassC instance with observable properties Cost and Currency.
        // - Updating the Cost property in each new ClassC instance triggers five cost updates.
        // - The cumulative TotalCost value should be updated accordingly.
        void subtestAdd3xClassBInstanceWithZeroInitialCost()
        {
            clearQueues();
            var classBx3 =
                Enumerable.Range(0, 3)
                .Select(_ => new ClassB())
                .ToList();
            classBx3
                .ForEach(_ => A.BCollection.Add(_));

            actual = A.OriginModel.SortAttributes<SortOrderNOD>().ToString();
            expected = @" 
<model name=""(Origin)ClassA"" statusnod=""NoAvailableChangedEvents"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ClassA]"" notifyinfo=""[NotifyInfo]"">
  <member name=""TotalCost"" statusnod=""NoObservableMemberProperties"" pi=""[System.Int32]"" />
  <member name=""BCollection"" statusnod=""INPCSource, INCCSource"" pi=""[System.Collections.ObjectModel.ObservableCollection]"" instance=""[System.Collections.ObjectModel.ObservableCollection]"" onpc=""[OnPC]"" oncc=""[OnCC]"">
    <member name=""Count"" statusnod=""NoObservableMemberProperties"" pi=""[System.Int32]"" />
    <model name=""(Origin)ClassB"" statusnod=""INPCSource"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ClassB]"" onpc=""[OnPC]"" notifyinfo=""[NotifyInfo]"">
      <member name=""C"" statusnod=""INPCSource"" pi=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ClassC]"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ClassC]"" onpc=""[OnPC]"">
        <member name=""Cost"" statusnod=""NoObservableMemberProperties"" pi=""[System.Int32]"" />
        <member name=""Currency"" statusnod=""NoObservableMemberProperties"" pi=""[System.Int32]"" />
      </member>
    </model>
    <model name=""(Origin)ClassB"" statusnod=""INPCSource"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ClassB]"" onpc=""[OnPC]"" notifyinfo=""[NotifyInfo]"">
      <member name=""C"" statusnod=""INPCSource"" pi=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ClassC]"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ClassC]"" onpc=""[OnPC]"">
        <member name=""Cost"" statusnod=""NoObservableMemberProperties"" pi=""[System.Int32]"" />
        <member name=""Currency"" statusnod=""NoObservableMemberProperties"" pi=""[System.Int32]"" />
      </member>
    </model>
    <model name=""(Origin)ClassB"" statusnod=""INPCSource"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ClassB]"" onpc=""[OnPC]"" notifyinfo=""[NotifyInfo]"">
      <member name=""C"" statusnod=""INPCSource"" pi=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ClassC]"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ClassC]"" onpc=""[OnPC]"">
        <member name=""Cost"" statusnod=""NoObservableMemberProperties"" pi=""[System.Int32]"" />
        <member name=""Currency"" statusnod=""NoObservableMemberProperties"" pi=""[System.Int32]"" />
      </member>
    </model>
    <model name=""(Origin)ClassB"" statusnod=""INPCSource"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ClassB]"" onpc=""[OnPC]"" notifyinfo=""[NotifyInfo]"">
      <member name=""C"" statusnod=""INPCSource"" pi=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ClassC]"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ClassC]"" onpc=""[OnPC]"">
        <member name=""Cost"" statusnod=""NoObservableMemberProperties"" pi=""[System.Int32]"" />
        <member name=""Currency"" statusnod=""NoObservableMemberProperties"" pi=""[System.Int32]"" />
      </member>
    </model>
    <model name=""(Origin)ClassB"" statusnod=""INPCSource"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ClassB]"" onpc=""[OnPC]"" notifyinfo=""[NotifyInfo]"">
      <member name=""C"" statusnod=""INPCSource"" pi=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ClassC]"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ClassC]"" onpc=""[OnPC]"">
        <member name=""Cost"" statusnod=""NoObservableMemberProperties"" pi=""[System.Int32]"" />
        <member name=""Currency"" statusnod=""NoObservableMemberProperties"" pi=""[System.Int32]"" />
      </member>
    </model>
  </member>
</model>";
            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting a total of 5 items visible in the model."
            );
            Assert.AreEqual(
                3,
                eventsCC.Count,
                "Expecting 3x Action.Add CC events in this round.");
            joined = string.Join(",", eventsCC.Select(_ => _.NotifyCollectionChangedEventArgs?.Action));
            Assert.AreEqual("Add,Add,Add", joined);

            joined = string.Join(",", eventsPC.Select(_ => _.PropertyChangedEventArgs?.PropertyName));
            Assert.AreEqual("Count,Count,Count", joined);
            Assert.AreEqual(
                3,
                eventsCC.Count,
                "Expecting 3x PropertyChanged PC events (where BCollection.Count changes.");
            clearQueues();

            classBx3
                .ForEach(_ => _.C.Cost = rando.Next(Int16.MaxValue));

            joined = string.Join(
                Environment.NewLine,
                eventsOA.Select(_ => (_.e as AwaitedEventArgs)?.Args));

            actual = joined;
            expected = @" 
Item at index 0 shows a new cost value of 8148.
Total of C.Cost 8148
Item at index 1 shows a new cost value of 3628.
Total of C.Cost 11776
Item at index 2 shows a new cost value of 15302.
Total of C.Cost 27078
Item at index 3 shows a new cost value of 25283.
Total of C.Cost 52361
Item at index 4 shows a new cost value of 21544.
Total of C.Cost 73905";

            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting 5 consistent pseudorandom cost updates in total."
            );
        }

        // EXPECT
        // - The last item in BCollection is retrieved and removed.
        // - A NotifyCollectionChanged event with action `Remove` is triggered.
        // - Unsubscribe events are processed for the removed item and its dependencies.
        // - Further property changes on the removed instance do not trigger PropertyChanged events.
        void subtestRemoveLastItemAndVerifyUnsubscribe()
        {
            clearQueues(ClearQueue.All);

            // Get the last item
            var remove = A.BCollection.Last();
            // Remove it
            A.BCollection.Remove(remove);

            Assert.AreEqual(
                NotifyCollectionChangedAction.Remove,
                eventsCC.DequeueSingle().NotifyCollectionChangedEventArgs?.Action);
            clearQueues();

            var joined = string.Join(
                Environment.NewLine,
                eventsOA.Select(_ => (_.e as AwaitedEventArgs)?.Args?.ToString()));

            actual = joined;
            expected = @" 
Removing INPC Subscription
Remove <model name=""(Origin)ClassB"" statusnod=""INPCSource"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ClassB]"" onpc=""[OnPC]"" notifyinfo=""[NotifyInfo]"" />
Removing INPC Subscription
Remove <member name=""C"" statusnod=""INPCSource"" pi=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ClassC]"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ClassC]"" onpc=""[OnPC]"" />";
            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting removal messages"
            );
            clearQueues(ClearQueue.All);
            remove.C.Cost = rando.Next(Int16.MaxValue);
            Assert.AreEqual(0, eventsPC.Count, "Expecting successful unsubscribe");
        }

        // EXPECT
        // - All items in BCollection are retrieved and then removed using Clear().
        // - The model should reflect the empty state of BCollection.
        // - Multiple unsubscribe events should be triggered for each removed instance.
        // - Further property changes on the removed instances do not trigger PropertyChanged events.
        void subtestClearListAndVerifyUnsubscribe()
        {
            var removes = A.BCollection.ToArray();
            clearQueues(ClearQueue.All);
            A.BCollection.Clear();

            actual = A.OriginModel.SortAttributes<SortOrderNOD>().ToString();
            actual.ToClipboard();
            actual.ToClipboardAssert("Expecting origin model reflects removal.");
            expected = @" 
<model name=""(Origin)ClassA"" statusnod=""NoAvailableChangedEvents"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ClassA]"" notifyinfo=""[NotifyInfo]"">
  <member name=""TotalCost"" statusnod=""NoObservableMemberProperties"" pi=""[System.Int32]"" />
  <member name=""BCollection"" statusnod=""INPCSource, INCCSource"" pi=""[System.Collections.ObjectModel.ObservableCollection]"" instance=""[System.Collections.ObjectModel.ObservableCollection]"" onpc=""[OnPC]"" oncc=""[OnCC]"" />
</model>";
            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting origin model reflects removal."
            );

            var joined = string.Join(
                Environment.NewLine,
                eventsOA.Select(_ => (_.e as AwaitedEventArgs)?.Args?.ToString()));

            actual = joined;
            actual.ToClipboard();
            actual.ToClipboardAssert("Expecting comprehensive unsubscribe based on XObject events.");
            { }
            expected = @" 
Removing INPC Subscription
Remove <model name=""(Origin)ClassB"" statusnod=""INPCSource"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ClassB]"" onpc=""[OnPC]"" notifyinfo=""[NotifyInfo]"" />
Removing INPC Subscription
Remove <member name=""C"" statusnod=""INPCSource"" pi=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ClassC]"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ClassC]"" onpc=""[OnPC]"" />
Removing INPC Subscription
Remove <model name=""(Origin)ClassB"" statusnod=""INPCSource"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ClassB]"" onpc=""[OnPC]"" notifyinfo=""[NotifyInfo]"" />
Removing INPC Subscription
Remove <member name=""C"" statusnod=""INPCSource"" pi=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ClassC]"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ClassC]"" onpc=""[OnPC]"" />
Removing INPC Subscription
Remove <model name=""(Origin)ClassB"" statusnod=""INPCSource"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ClassB]"" onpc=""[OnPC]"" notifyinfo=""[NotifyInfo]"" />
Removing INPC Subscription
Remove <member name=""C"" statusnod=""INPCSource"" pi=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ClassC]"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ClassC]"" onpc=""[OnPC]"" />
Removing INPC Subscription
Remove <model name=""(Origin)ClassB"" statusnod=""INPCSource"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ClassB]"" onpc=""[OnPC]"" notifyinfo=""[NotifyInfo]"" />
Removing INPC Subscription
Remove <member name=""C"" statusnod=""INPCSource"" pi=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ClassC]"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ClassC]"" onpc=""[OnPC]"" />";

            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting comprehensive unsubscribe based on XObject events."
            );

            clearQueues(ClearQueue.All);
            foreach (var remove in removes)
            {
                remove.C.Cost = rando.Next(Int16.MaxValue);
            }
            Assert.AreEqual(0, eventsPC.Count, "Expecting successful unsubscribe");
        }

        #endregion S U B T E S T S
    }
}
