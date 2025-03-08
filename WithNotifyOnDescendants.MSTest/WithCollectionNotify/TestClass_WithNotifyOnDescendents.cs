using IVSoftware.Portable.Threading;
using IVSoftware.Portable.Xml.Linq;
using IVSoftware.Portable.Xml.Linq.XBoundObject;
using IVSoftware.WinOS.MSTest.Extensions;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml.Linq;
using WithNotifyOnDescendants.Proto.MSTest.TestModels;
using static IVSoftware.Portable.Threading.Extensions;

namespace WithNotifyOnDescendants.Proto;


[TestClass]
public class TestClass_WithNotifyOnDescendents
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
    SenderEventPair currentEvent = null!;
    Queue<SenderEventPair> eventsPC = new();
    Queue<SenderEventPair> eventsCC = new();
    Queue<SenderEventPair> eventsXO = new();
    Queue<SenderEventPair> eventsOA = new();
    void clearQueues(ClearQueue clearQueue = ClearQueue.PropertyChangedEvents | ClearQueue.NotifyCollectionChangedEvents)
    {
        if (clearQueue.HasFlag(ClearQueue.PropertyChangedEvents)) eventsPC.Clear();
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



    [TestMethod]
    public void Test_ModelInit()
    {
        void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            throw new NotImplementedException();
        }
        void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            throw new NotImplementedException();
        }

        string actual, expected;
        XElement? model = null;

        // =========================================================
        // Empty class with no available changed events.
        // =========================================================
        new EmptyClass()
            .WithNotifyOnDescendants(out model, OnPropertyChanged, OnCollectionChanged);

        actual = model.SortAttributes<SortOrderNOD>().ToString();
        expected = @" 
<model name=""(Origin)EmptyClass"" statusnod=""NoAvailableChangedEvents"" instance=""[WithNotifyOnDescendants.Proto.EmptyClass]"" notifyinfo=""[NotifyInfo]"" />";

        Assert.AreEqual(
            expected.NormalizeResult(),
            actual.NormalizeResult(),
            "Expecting no event handlers for this instance"
        );

        // =========================================================
        // Empty class with available INPC
        // =========================================================
        _ = new EmptyOnNotifyClass()
            .WithNotifyOnDescendants(out model, OnPropertyChanged, OnCollectionChanged);

        actual = model.SortAttributes<SortOrderNOD>().ToString();
        expected = @" 
<model name=""(Origin)EmptyOnNotifyClass"" statusnod=""INPCSource"" instance=""[WithNotifyOnDescendants.Proto.EmptyOnNotifyClass]"" onpc=""[OnPC]"" notifyinfo=""[NotifyInfo]"" />";

        Assert.AreEqual(
            expected.NormalizeResult(),
            actual.NormalizeResult(),
            "Expecting INPC detected on class with no child properties."
        );

        // =========================================================
        // Observable Collection
        // =========================================================
        _ = new ObservableCollection<object>()
            .WithNotifyOnDescendants(out model, OnPropertyChanged, OnCollectionChanged);

        actual = model.SortAttributes<SortOrderNOD>().ToString();
        expected = @" 
<model name=""(Origin)ObservableCollection"" statusnod=""INPCSource, INCCSource"" instance=""[System.Collections.ObjectModel.ObservableCollection]"" onpc=""[OnPC]"" oncc=""[OnCC]"" notifyinfo=""[NotifyInfo]"">
  <member name=""Count"" statusnod=""NoObservableMemberProperties"" pi=""[System.Int32]"" />
</model>";

        Assert.AreEqual(
            expected.NormalizeResult(),
            actual.NormalizeResult(),
            "Expecting INPC and INCC both detected on ObservableCollection T."
        );
    }

    [TestMethod]
    public void Test_SetNullPropertyToString()
    {
        void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
            => eventsPC.Enqueue(new SenderEventPair(sender, e));

        void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
            => eventsPC.Enqueue(new SenderEventPair(sender, e));


        // =========================================================
        // Class with available INPC and one child property.
        // - PropertyType is System.Object 'A'
        // - Default instance has a null 'A' property.
        // =========================================================
       
        var cwopLevel0 = 
            new ClassWithOnePropertyINPC()
            .WithNotifyOnDescendants(out XElement originModel, OnPropertyChanged, OnCollectionChanged);
        // Loopback
        _ = originModel.To<ClassWithOnePropertyINPC>(@throw: true);


        actual = originModel.SortAttributes<SortOrderNOD>().ToString();
        expected = @" 
<model name=""(Origin)ClassWithOnePropertyINPC"" statusnod=""INPCSource"" instance=""[WithNotifyOnDescendants.Proto.ClassWithOnePropertyINPC]"" onpc=""[OnPC]"" notifyinfo=""[NotifyInfo]"">
  <member name=""A"" statusnod=""WaitingForValue"" pi=""[System.Object]"" />
</model>";
        Assert.AreEqual(
            expected.NormalizeResult(),
            actual.NormalizeResult(),
            "Expecting null instance of System.Object"
        );

        // =========================================================
        // Setting 'A' to an instance come with certain expectations.
        // - Doing this must raise a PropertyChanged event.
        // =========================================================

        cwopLevel0.A = Guid.NewGuid().ToString(); // Property Change

        currentEvent = eventsPC.Dequeue();
        Assert.AreEqual(0, eventsPC.Count);
        Assert.AreEqual(1, currentEvent.SenderModel?.Ancestors().Count());

        actual = originModel.SortAttributes<SortOrderNOD>().ToString();
        expected = @" 
<model name=""(Origin)ClassWithOnePropertyINPC"" statusnod=""INPCSource"" instance=""[WithNotifyOnDescendants.Proto.ClassWithOnePropertyINPC]"" onpc=""[OnPC]"" notifyinfo=""[NotifyInfo]"">
  <member name=""A"" statusnod=""NoObservableMemberProperties"" pi=""[System.Object]"" runtimetype=""System.String"" />
</model>";
        Assert.AreEqual(
            expected.NormalizeResult(),
            actual.NormalizeResult(),
            "Expecting origin model to match"
        );
        // Look at PROPERTY model
        actual = currentEvent.SenderModel.SortAttributes<SortOrderNOD>().ToString() ?? throw new NullReferenceException();
        expected = @" 
<member name=""A"" statusnod=""NoObservableMemberProperties"" pi=""[System.Object]"" runtimetype=""System.String"" />";

        Assert.AreEqual(
            expected.NormalizeResult(),
            actual.NormalizeResult(),
            "Expecting refreshed property model"
        );

        // =========================================================
        // Setting 'A' back to null
        // =========================================================

        cwopLevel0.A = null; // Property Change

        currentEvent = eventsPC.Dequeue();
        Assert.AreEqual(0, eventsPC.Count);

        actual = originModel.SortAttributes<SortOrderNOD>().ToString();
        expected = @" 
<model name=""(Origin)ClassWithOnePropertyINPC"" statusnod=""INPCSource"" instance=""[WithNotifyOnDescendants.Proto.ClassWithOnePropertyINPC]"" onpc=""[OnPC]"" notifyinfo=""[NotifyInfo]"">
  <member name=""A"" statusnod=""WaitingForValue"" pi=""[System.Object]"" />
</model>";
        Assert.AreEqual(
            expected.NormalizeResult(),
            actual.NormalizeResult(),
            "Expecting INPCSource"
        );
        // =========================================================
        // Setting 'A'to a new INPC instance at level 1
        // =========================================================

        cwopLevel0.A = new ClassWithOnePropertyINPC(); // Property Change

        currentEvent = eventsPC.Dequeue();
        Assert.AreEqual(0, eventsPC.Count);

        actual = originModel.SortAttributes<SortOrderNOD>().ToString();
        expected = @" 
<model name=""(Origin)ClassWithOnePropertyINPC"" statusnod=""INPCSource"" instance=""[WithNotifyOnDescendants.Proto.ClassWithOnePropertyINPC]"" onpc=""[OnPC]"" notifyinfo=""[NotifyInfo]"">
  <member name=""A"" statusnod=""INPCSource"" pi=""[System.Object]"" instance=""[WithNotifyOnDescendants.Proto.ClassWithOnePropertyINPC]"" onpc=""[OnPC]"">
    <member name=""A"" statusnod=""WaitingForValue"" pi=""[System.Object]"" />
  </member>
</model>";

        Assert.AreEqual(
            expected.NormalizeResult(),
            actual.NormalizeResult(),
            "Expecting INPCSource"
        );
        Assert.AreEqual(1, currentEvent.SenderModel.Ancestors().Count());

        object? objectLevel1 =
            currentEvent.SenderModel.Attribute(nameof(SortOrderNOD.instance)) is XBoundAttribute xba
            ? xba.Tag
            : null;
        Assert.AreEqual(objectLevel1?.GetType(), typeof(ClassWithOnePropertyINPC));


        // =========================================================
        // Setting 'A' on the leaf instance to a string
        // =========================================================

        var cwopLevel1 = (ClassWithOnePropertyINPC)objectLevel1;

        cwopLevel1.A = Guid.NewGuid().ToString(); // Property Change

        currentEvent = eventsPC.Dequeue();
        Assert.AreEqual(0, eventsPC.Count);

        Assert.AreEqual(2, currentEvent.SenderModel?.Ancestors().Count());

        actual = originModel.SortAttributes<SortOrderNOD>().ToString() ?? throw new NullReferenceException();
        actual.ToClipboard();
        actual.ToClipboardAssert("Expecting model is RELATIVE TO SENDER AT LEVEL 1");
        { }
        expected = @" 
<model name=""(Origin)ClassWithOnePropertyINPC"" statusnod=""INPCSource"" instance=""[WithNotifyOnDescendants.Proto.ClassWithOnePropertyINPC]"" onpc=""[OnPC]"" notifyinfo=""[NotifyInfo]"">
  <member name=""A"" statusnod=""INPCSource"" pi=""[System.Object]"" instance=""[WithNotifyOnDescendants.Proto.ClassWithOnePropertyINPC]"" onpc=""[OnPC]"">
    <member name=""A"" statusnod=""NoObservableMemberProperties"" pi=""[System.Object]"" runtimetype=""System.String"" />
  </member>
</model>";

        Assert.AreEqual(
            expected.NormalizeResult(),
            actual.NormalizeResult(),
            "Expecting origin model to match"
        );

        // =========================================================
        // GETTING READY TO FOR UNSUBSCRIBE TESTS - first make sure
        // we've got at least two INPS under the main one.
        // =========================================================

        var leafABC = new ABC
        {
            A = new EmptyClass()
        };

        cwopLevel1.A = leafABC; // [Careful] THIS IS RIGHT. It's a change on level 1. Property Change

        currentEvent = eventsPC.Dequeue();
        Assert.AreEqual(0, eventsPC.Count);
        Assert.AreEqual(2, currentEvent.SenderModel?.Ancestors().Count());

        Assert.ReferenceEquals(
            cwopLevel1, 
            currentEvent!
            .SenderModel?
            .Parent?
            .To<ClassWithOnePropertyINPC>(@throw: true));

        var abcLevel2 = currentEvent.SenderModel.To<ABC>(@throw: true);

        actual = originModel.SortAttributes<SortOrderNOD>().ToString();
        expected = @" 
<model name=""(Origin)ClassWithOnePropertyINPC"" statusnod=""INPCSource"" instance=""[WithNotifyOnDescendants.Proto.ClassWithOnePropertyINPC]"" onpc=""[OnPC]"" notifyinfo=""[NotifyInfo]"">
  <member name=""A"" statusnod=""INPCSource"" pi=""[System.Object]"" instance=""[WithNotifyOnDescendants.Proto.ClassWithOnePropertyINPC]"" onpc=""[OnPC]"">
    <member name=""A"" statusnod=""INPCSource"" pi=""[System.Object]"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ABC]"" onpc=""[OnPC]"">
      <member name=""A"" statusnod=""NoAvailableChangedEvents"" pi=""[System.Object]"" instance=""[WithNotifyOnDescendants.Proto.EmptyClass]"" />
      <member name=""B"" statusnod=""NoObservableMemberProperties"" pi=""[System.Object]"" runtimetype=""System.String"" />
      <member name=""C"" statusnod=""NoObservableMemberProperties"" pi=""[System.Object]"" runtimetype=""System.String"" />
    </member>
  </member>
</model>";
        Assert.AreEqual(
            expected.NormalizeResult(),
            actual.NormalizeResult(),
            "Expecting to see all layers in their glory."
        );

        // +++++++++++++++++++++++++++++++++++++++++++++++++++++++++ P R O P E R T Y    C H A N G E   ++++++++++++++++++++++++
        abcLevel2.B = 250309; // 15 years!

        currentEvent = eventsPC.Dequeue();
        Assert.AreEqual(0, eventsPC.Count);
        Assert.AreEqual(3, currentEvent.SenderModel?.Ancestors().Count());

        actual = originModel.SortAttributes<SortOrderNOD>().ToString();
        expected = @" 
<model name=""(Origin)ClassWithOnePropertyINPC"" statusnod=""INPCSource"" instance=""[WithNotifyOnDescendants.Proto.ClassWithOnePropertyINPC]"" onpc=""[OnPC]"" notifyinfo=""[NotifyInfo]"">
  <member name=""A"" statusnod=""INPCSource"" pi=""[System.Object]"" instance=""[WithNotifyOnDescendants.Proto.ClassWithOnePropertyINPC]"" onpc=""[OnPC]"">
    <member name=""A"" statusnod=""INPCSource"" pi=""[System.Object]"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ABC]"" onpc=""[OnPC]"">
      <member name=""A"" statusnod=""NoAvailableChangedEvents"" pi=""[System.Object]"" instance=""[WithNotifyOnDescendants.Proto.EmptyClass]"" />
      <member name=""B"" statusnod=""NoObservableMemberProperties"" pi=""[System.Object]"" runtimetype=""System.Int32"" />
      <member name=""C"" statusnod=""NoObservableMemberProperties"" pi=""[System.Object]"" runtimetype=""System.String"" />
    </member>
  </member>
</model>";
        Assert.AreEqual(
            expected.NormalizeResult(),
            actual.NormalizeResult(),
            "Expecting origin model to match"
        );

        // +++++++++++++++++++++++++++++++++++++++++++++++++++++++++ P R O P E R T Y    C H A N G E   ++++++++++++++++++++++++
        abcLevel2.C = new EmptyOnNotifyClass();

        currentEvent = eventsPC.Dequeue();
        Assert.AreEqual(0, eventsPC.Count);

        Assert.AreEqual(3, currentEvent.SenderModel.Ancestors().Count());

        actual = originModel.SortAttributes<SortOrderNOD>().ToString();
        actual.ToClipboard();
        actual.ToClipboardAssert("Expecting origin model to match.");
        expected = @" 
<model name=""(Origin)ClassWithOnePropertyINPC"" statusnod=""INPCSource"" instance=""[WithNotifyOnDescendants.Proto.ClassWithOnePropertyINPC]"" onpc=""[OnPC]"" notifyinfo=""[NotifyInfo]"">
  <member name=""A"" statusnod=""INPCSource"" pi=""[System.Object]"" instance=""[WithNotifyOnDescendants.Proto.ClassWithOnePropertyINPC]"" onpc=""[OnPC]"">
    <member name=""A"" statusnod=""INPCSource"" pi=""[System.Object]"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ABC]"" onpc=""[OnPC]"">
      <member name=""A"" statusnod=""NoAvailableChangedEvents"" pi=""[System.Object]"" instance=""[WithNotifyOnDescendants.Proto.EmptyClass]"" />
      <member name=""B"" statusnod=""NoObservableMemberProperties"" pi=""[System.Object]"" runtimetype=""System.Int32"" />
      <member name=""C"" statusnod=""INPCSource"" pi=""[System.Object]"" instance=""[WithNotifyOnDescendants.Proto.EmptyOnNotifyClass]"" onpc=""[OnPC]"" />
    </member>
  </member>
</model>";

        Assert.AreEqual(
            expected.NormalizeResult(),
            actual.NormalizeResult(),
            "Expecting origin model to match."
        );

        // =========================================================
        // NOW READY TO FOR UNSUBSCRIBE TESTS
        // =========================================================

        // +++++++++++++++++++++++++++++++++++++++++++++++++++++++++ P R O P E R T Y    C H A N G E   ++++++++++++++++++++++++
        abcLevel2.C = null!;

        currentEvent = eventsPC.Dequeue();
        Assert.AreEqual(0, eventsPC.Count);
        Assert.AreEqual(3, currentEvent.SenderModel.Ancestors().Count());

        actual = originModel.SortAttributes<SortOrderNOD>().ToString();
        expected = @" 
<model name=""(Origin)ClassWithOnePropertyINPC"" statusnod=""INPCSource"" instance=""[WithNotifyOnDescendants.Proto.ClassWithOnePropertyINPC]"" onpc=""[OnPC]"" notifyinfo=""[NotifyInfo]"">
  <member name=""A"" statusnod=""INPCSource"" pi=""[System.Object]"" instance=""[WithNotifyOnDescendants.Proto.ClassWithOnePropertyINPC]"" onpc=""[OnPC]"">
    <member name=""A"" statusnod=""INPCSource"" pi=""[System.Object]"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ABC]"" onpc=""[OnPC]"">
      <member name=""A"" statusnod=""NoAvailableChangedEvents"" pi=""[System.Object]"" instance=""[WithNotifyOnDescendants.Proto.EmptyClass]"" />
      <member name=""B"" statusnod=""NoObservableMemberProperties"" pi=""[System.Object]"" runtimetype=""System.Int32"" />
      <member name=""C"" statusnod=""WaitingForValue"" pi=""[System.Object]"" />
    </member>
  </member>
</model>";

        Assert.AreEqual(
            expected.NormalizeResult(),
            actual.NormalizeResult(),
            "Expecting abcLevel2.C is now null."
        );
    }
    [TestMethod]
    public void Test_ObservableCollectionSolo()
    {
        void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            Debug.WriteLine($"{e.GetType().Name} {e.PropertyName}");
            eventsPC.Enqueue(new SenderEventPair(sender, e));
        }
        void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            eventsCC.Enqueue(new SenderEventPair(sender, e));
        }

        var oc = 
            new ObservableCollection<ABC>()
            .WithNotifyOnDescendants(out XElement originModel, OnPropertyChanged, OnCollectionChanged);


        actual = originModel.SortAttributes<SortOrderNOD>().ToString();
        expected = @" 
<model name=""(Origin)ObservableCollection"" statusnod=""INPCSource, INCCSource"" instance=""[System.Collections.ObjectModel.ObservableCollection]"" onpc=""[OnPC]"" oncc=""[OnCC]"" notifyinfo=""[NotifyInfo]"">
  <member name=""Count"" statusnod=""NoObservableMemberProperties"" pi=""[System.Int32]"" />
</model>";

        Assert.AreEqual(
            expected.NormalizeResult(),
            actual.NormalizeResult(),
            "Expecting model"
        );
        oc.Add(new());

        // Thows an exception and fails the test if event wasn't received.
        currentEvent = eventsCC.DequeueSingle();
    }
}
static partial class TestExtensions
{
    public static T DequeueSingle<T>(this Queue<T> @this)
    {
        T eventPair = @this.Dequeue();
        if (@this.Count > 0)
        {
            throw new InvalidOperationException($"{typeof(T).Name} objects remain after single dequeue");
        }
        return eventPair;
    }
}
