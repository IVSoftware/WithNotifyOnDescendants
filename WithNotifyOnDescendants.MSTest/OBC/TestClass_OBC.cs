using WithNotifyOnDescendants.Proto;
using IVSoftware.Portable.Xml.Linq.XBoundObject;
using IVSoftware.WinOS.MSTest.Extensions;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Xml.Linq;
using static CollectionsMSTest.OBC.TestExtensions;
using static IVSoftware.Portable.Threading.Extensions;
using IVSoftware.Portable.Threading;
using Microsoft.VisualBasic;
using WithNotifyOnDescendants.Proto.MSTest.TestModels;
using System.Collections;
using System.Collections.Specialized;
using System.DirectoryServices.ActiveDirectory;

namespace CollectionsMSTest.OBC
{
    // Internal class with public methods.
    static partial class TestExtensions
    {
        public static SemaphoreSlim AwaiterPC = new SemaphoreSlim(1, 1);
        public static string Dump(this Dictionary<Enum, int> @this)
            => string.Join(Environment.NewLine, @this.Select(_ => $"{_.Key}: {_.Value}")) ?? string.Empty;
        public static string Dump(this List<string> @this)
            => string.Join(Environment.NewLine, @this);
    }
    enum ActionOBC { PropertyChanged }
    [TestClass]
    public sealed class TestClass_OBC
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

        /// <summary>
        /// This test verifies that the Lazy<T> proxy correctly triggers notifications 
        /// for property changes, allowing the model to run discovery.
        /// 
        /// Problem: The Lazy<T> implementation does not provide notifications when 
        ///          its value is created or updated.
        /// 
        /// Solution: By utilizing a notification mechanism, this test ensures that 
        ///           property change events are properly raised, allowing discovery 
        ///           to detect changes in singleton instances.
        ///           
        /// Test Flow:
        /// 1. Initialize a ValidSingletonTestModel with notification handlers.
        /// 2. Validate that the model structure is as expected before activation.
        /// 3. Trigger property access and confirm notifications fire correctly.
        /// 4. Verify that Lazy<T> watcher mechanism allows property changes to be detected.
        /// 5. Ensure that the model updates only after the LazyProxy expires and fires a change.
        /// </summary>
        [TestMethod]
        public async Task Test_Singleton()
        {
            object @lock = new ();
            Stopwatch stopwatch = new Stopwatch();

            ValidSingletonTestModel vstm = 
                new ValidSingletonTestModel()
                .WithNotifyOnDescendants(
                    out XElement originModel,
                    onPC: (sender, e) =>
                    {
                        eventsPC.Enqueue(new SenderEventPair(sender, e));
                        switch (e.PropertyName)
                        {
                            case nameof(ValidSingletonTestModel.ABC2):
                                // RAISED by the LazyProxy ONLY!
                                AwaiterPC.Release();
                                break;
                        }
                    },
                    onCC: (sender, e) =>eventsCC.Enqueue(new SenderEventPair(sender, e))
                );
            actual = originModel.SortAttributes<SortOrderNOD>().ToString();
            expected = @" 
<model name=""(Origin)ValidSingletonTestModel"" statusnod=""INPCSource"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ValidSingletonTestModel]"" onpc=""[OnPC]"" notifyinfo=""[NotifyInfo]"">
  <member name=""ABC1IsValueCreated"" statusnod=""NoObservableMemberProperties"" pi=""[System.Boolean]"" />
  <member name=""ABC1"" statusnod=""WaitingForValue"" pi=""[System.ComponentModel.INotifyPropertyChanged]"" />
  <member name=""ABC2IsValueCreated"" statusnod=""NoObservableMemberProperties"" pi=""[System.Boolean]"" />
  <member name=""ABC2"" statusnod=""WaitingForValue"" pi=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ABC]"" />
</model>";
            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting discovery without activating singletons."
            );

            clearQueues();
            Assert.IsFalse(vstm.ABC1IsValueCreated);
            var abc1 = (ABC)vstm.ABC1;
            Assert.IsTrue(vstm.ABC1IsValueCreated);
            currentEvent = eventsPC.DequeueSingle();
            Assert.AreEqual(
                nameof(ValidSingletonTestModel.ABC1), 
                currentEvent.PropertyName,
                "Expecting property change when singleton is activated"
            );

            actual = originModel.SortAttributes<SortOrderNOD>().ToString();
            expected = @" 
<model name=""(Origin)ValidSingletonTestModel"" statusnod=""INPCSource"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ValidSingletonTestModel]"" onpc=""[OnPC]"" notifyinfo=""[NotifyInfo]"">
  <member name=""ABC1IsValueCreated"" statusnod=""NoObservableMemberProperties"" pi=""[System.Boolean]"" />
  <member name=""ABC1"" statusnod=""INPCSource"" pi=""[System.ComponentModel.INotifyPropertyChanged]"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ABC]"" onpc=""[OnPC]"">
    <member name=""A"" statusnod=""NoObservableMemberProperties"" pi=""[System.Object]"" runtimetype=""System.String"" />
    <member name=""B"" statusnod=""NoObservableMemberProperties"" pi=""[System.Object]"" runtimetype=""System.String"" />
    <member name=""C"" statusnod=""NoObservableMemberProperties"" pi=""[System.Object]"" runtimetype=""System.String"" />
  </member>
  <member name=""ABC2IsValueCreated"" statusnod=""NoObservableMemberProperties"" pi=""[System.Boolean]"" />
  <member name=""ABC2"" statusnod=""WaitingForValue"" pi=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ABC]"" />
</model>";

            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting ran discovery on new singleton instance."
            );

            // =========================================================
            // Test the Lazy T watcher by getting it and waiting a second.
            Assert.IsFalse(vstm.ABC2IsValueCreated);
            AwaiterPC.Wait(0);
            ABC abc2;
            clearQueues();
            lock (@lock)
            {
                stopwatch.Restart();
                abc2 = vstm.ABC2;
                Assert.IsTrue(vstm.ABC2IsValueCreated, "Expecting the effect on the BOOLEAN is instantaneous!");
            }
            Assert.AreEqual(0, eventsPC.Count, "Expecting time sensitive PC has NOT fired yet to refresh the model.");
            // Make sure you leave the lock before using clipboard!!
            actual = originModel.SortAttributes<SortOrderNOD>().ToString();
            expected = @" 
<model name=""(Origin)ValidSingletonTestModel"" statusnod=""INPCSource"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ValidSingletonTestModel]"" onpc=""[OnPC]"" notifyinfo=""[NotifyInfo]"">
  <member name=""ABC1IsValueCreated"" statusnod=""NoObservableMemberProperties"" pi=""[System.Boolean]"" />
  <member name=""ABC1"" statusnod=""INPCSource"" pi=""[System.ComponentModel.INotifyPropertyChanged]"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ABC]"" onpc=""[OnPC]"">
    <member name=""A"" statusnod=""NoObservableMemberProperties"" pi=""[System.Object]"" runtimetype=""System.String"" />
    <member name=""B"" statusnod=""NoObservableMemberProperties"" pi=""[System.Object]"" runtimetype=""System.String"" />
    <member name=""C"" statusnod=""NoObservableMemberProperties"" pi=""[System.Object]"" runtimetype=""System.String"" />
  </member>
  <member name=""ABC2IsValueCreated"" statusnod=""NoObservableMemberProperties"" pi=""[System.Boolean]"" />
  <member name=""ABC2"" statusnod=""WaitingForValue"" pi=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ABC]"" />
</model>";

            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting the model DOES NOT UPDATE! It needs the LazyProxy to expire and fire the property change."
            );

            // =========================================================
            // Make sure watcher period has a chance to expire.
            try
            {
                await AwaiterPC.WaitAsync(new CancellationTokenSource(TimeSpan.FromSeconds(100.1)).Token);
                stopwatch.Stop();
                Debug.WriteLine($@"Watcher fired in {stopwatch.Elapsed:s\.fff} seconds.");
                currentEvent = eventsPC.DequeueSingle();
                { }
            }
            catch (OperationCanceledException oex)
            {
                Assert.Fail("Expecting watcher to have fired by now.");
            }
            finally
            {
                AwaiterPC.Release();
            }
            ;
            actual = originModel.SortAttributes<SortOrderNOD>().ToString();
            actual.ToClipboard();
            actual.ToClipboardAssert("Expecting the model HAS UPDATED as a result of the propery change fired by LazyProxy.");
            { }
            expected = @" 
<model name=""(Origin)ValidSingletonTestModel"" statusnod=""INPCSource"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ValidSingletonTestModel]"" onpc=""[OnPC]"" notifyinfo=""[NotifyInfo]"">
  <member name=""ABC1IsValueCreated"" statusnod=""NoObservableMemberProperties"" pi=""[System.Boolean]"" />
  <member name=""ABC1"" statusnod=""INPCSource"" pi=""[System.ComponentModel.INotifyPropertyChanged]"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ABC]"" onpc=""[OnPC]"">
    <member name=""A"" statusnod=""NoObservableMemberProperties"" pi=""[System.Object]"" runtimetype=""System.String"" />
    <member name=""B"" statusnod=""NoObservableMemberProperties"" pi=""[System.Object]"" runtimetype=""System.String"" />
    <member name=""C"" statusnod=""NoObservableMemberProperties"" pi=""[System.Object]"" runtimetype=""System.String"" />
  </member>
  <member name=""ABC2IsValueCreated"" statusnod=""NoObservableMemberProperties"" pi=""[System.Boolean]"" />
  <member name=""ABC2"" statusnod=""INPCSource"" pi=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ABC]"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ABC]"" onpc=""[OnPC]"">
    <member name=""A"" statusnod=""NoObservableMemberProperties"" pi=""[System.Object]"" runtimetype=""System.String"" />
    <member name=""B"" statusnod=""NoObservableMemberProperties"" pi=""[System.Object]"" runtimetype=""System.String"" />
    <member name=""C"" statusnod=""NoObservableMemberProperties"" pi=""[System.Object]"" runtimetype=""System.String"" />
  </member>
</model>";

            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting the model HAS UPDATED as a result of the propery change fired by LazyProxy."
            );
        }

        [TestMethod]
        public void Test_OBC()
        {
            // +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
            string actual, expected, defaultMsg = "Expecting schema to match.";

            // +++++++++++++++++++++++++++++++++++++++++++++++++++++++++
            // Instances where member properties ABC1 and ABC2 are null.
            // +++++++++++++++++++++++++++++++++++++++++++++++++++++++++
            var obc = new ObservableCollection<INPCwithINPCs>()
            {
                new INPCwithINPCs (),
                new INPCwithINPCs (),
            }.WithNotifyOnDescendants(
                out XElement originModel,
                onPC: (sender, e) => eventsPC.Enqueue(new SenderEventPair(sender, e)),
                onCC: (sender, e) => eventsCC.Enqueue(new SenderEventPair(sender, e)));

            actual = originModel.SortAttributes<SortOrderNOD>().ToString();
            expected = @" 
<model name=""(Origin)ObservableCollection"" statusnod=""INPCSource, INCCSource"" instance=""[System.Collections.ObjectModel.ObservableCollection]"" onpc=""[OnPC]"" oncc=""[OnCC]"" notifyinfo=""[NotifyInfo]"">
  <member name=""Count"" statusnod=""NoObservableMemberProperties"" pi=""[System.Int32]"" />
  <model name=""(Origin)INPCwithINPCs"" statusnod=""INPCSource"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.INPCwithINPCs]"" onpc=""[OnPC]"" notifyinfo=""[NotifyInfo]"">
    <member name=""ABC1"" statusnod=""WaitingForValue"" pi=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ABC]"" />
    <member name=""ABC2"" statusnod=""WaitingForValue"" pi=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ABC]"" />
  </model>
  <model name=""(Origin)INPCwithINPCs"" statusnod=""INPCSource"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.INPCwithINPCs]"" onpc=""[OnPC]"" notifyinfo=""[NotifyInfo]"">
    <member name=""ABC1"" statusnod=""WaitingForValue"" pi=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ABC]"" />
    <member name=""ABC2"" statusnod=""WaitingForValue"" pi=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ABC]"" />
  </model>
</model>";

            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting model to match."
            );

            // CRITICAL BEHAVIOR !
            // +++++++++++++++++++++++++++++++++++++++++++++++++++++++++
            // ABC new instance must come up connected!
            // +++++++++++++++++++++++++++++++++++++++++++++++++++++++++
            obc[0].ABC1 = new ABC();
            currentEvent = eventsPC.DequeueSingle();

            // +++++++++++++++++++++++++++++++++++++++++++++++++++++++++
            // What Just Happened:
            // - ABC1 just went from null status of WaitingForValue to
            //   a new state of having a model and running discovery on it.
            // +++++++++++++++++++++++++++++++++++++++++++++++++++++++++

            // EXPECT
            // - There should be 'runtimetype' attributes because the declared property is different (i.e. 'object').
            // - The ABC1 property model should be populated and no longer 'WaitingForValue'.
            actual =
                currentEvent.SenderModel.SortAttributes<SortOrderNOD>().ToString();
            expected = @" 
<member name=""ABC1"" statusnod=""INPCSource"" pi=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ABC]"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ABC]"" onpc=""[OnPC]"">
  <member name=""A"" statusnod=""NoObservableMemberProperties"" pi=""[System.Object]"" runtimetype=""System.String"" />
  <member name=""B"" statusnod=""NoObservableMemberProperties"" pi=""[System.Object]"" runtimetype=""System.String"" />
  <member name=""C"" statusnod=""NoObservableMemberProperties"" pi=""[System.Object]"" runtimetype=""System.String"" />
</member>";

            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting property changed for ABC1"
            );
            Assert.AreEqual(
                currentEvent.PropertyName,
                nameof(INPCwithINPCs.ABC1),
                "Expecting ABC1 is modeled as a NON-NULL instance");
            Assert.IsNotNull(currentEvent.PropertyChangedEventArgs);

            // +++++++++++++++++++++++++++++++++++++++++++++++++++++++++
            // Set back to null
            // +++++++++++++++++++++++++++++++++++++++++++++++++++++++++

            // CRITICAL BEHAVIOR !
            obc[0].ABC1 = null;
            currentEvent = eventsPC.DequeueSingle();
            { }
            actual =
                currentEvent.SenderModel.SortAttributes<SortOrderNOD>().ToString();
            expected = @" 
<member name=""ABC1"" statusnod=""WaitingForValue"" pi=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ABC]"" />";

            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting ABC1 is modeled as a NULL instance"
            );

            // INSTANCES
            obc.Clear();

            actual = originModel.SortAttributes<SortOrderNOD>().ToString();
            expected = @" 
<model name=""(Origin)ObservableCollection"" statusnod=""INPCSource, INCCSource"" instance=""[System.Collections.ObjectModel.ObservableCollection]"" onpc=""[OnPC]"" oncc=""[OnCC]"" notifyinfo=""[NotifyInfo]"" />";

            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting model shows empty observable collection."
            );

            // Instances where ABC1 and ABC2 are instantiated to begin with.
            obc.Add(new() { ABC1 = new(), ABC2 = new(), });
            obc.Add(new() { ABC1 = new(), ABC2 = new(), });

            actual = originModel.SortAttributes<SortOrderNOD>().ToString();
            expected = @" 
<model name=""(Origin)ObservableCollection"" statusnod=""INPCSource, INCCSource"" instance=""[System.Collections.ObjectModel.ObservableCollection]"" onpc=""[OnPC]"" oncc=""[OnCC]"" notifyinfo=""[NotifyInfo]"">
  <model name=""(Origin)INPCwithINPCs"" statusnod=""INPCSource"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.INPCwithINPCs]"" onpc=""[OnPC]"" notifyinfo=""[NotifyInfo]"">
    <member name=""ABC1"" statusnod=""INPCSource"" pi=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ABC]"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ABC]"" onpc=""[OnPC]"">
      <member name=""A"" statusnod=""NoObservableMemberProperties"" pi=""[System.Object]"" runtimetype=""System.String"" />
      <member name=""B"" statusnod=""NoObservableMemberProperties"" pi=""[System.Object]"" runtimetype=""System.String"" />
      <member name=""C"" statusnod=""NoObservableMemberProperties"" pi=""[System.Object]"" runtimetype=""System.String"" />
    </member>
    <member name=""ABC2"" statusnod=""INPCSource"" pi=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ABC]"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ABC]"" onpc=""[OnPC]"">
      <member name=""A"" statusnod=""NoObservableMemberProperties"" pi=""[System.Object]"" runtimetype=""System.String"" />
      <member name=""B"" statusnod=""NoObservableMemberProperties"" pi=""[System.Object]"" runtimetype=""System.String"" />
      <member name=""C"" statusnod=""NoObservableMemberProperties"" pi=""[System.Object]"" runtimetype=""System.String"" />
    </member>
  </model>
  <model name=""(Origin)INPCwithINPCs"" statusnod=""INPCSource"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.INPCwithINPCs]"" onpc=""[OnPC]"" notifyinfo=""[NotifyInfo]"">
    <member name=""ABC1"" statusnod=""INPCSource"" pi=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ABC]"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ABC]"" onpc=""[OnPC]"">
      <member name=""A"" statusnod=""NoObservableMemberProperties"" pi=""[System.Object]"" runtimetype=""System.String"" />
      <member name=""B"" statusnod=""NoObservableMemberProperties"" pi=""[System.Object]"" runtimetype=""System.String"" />
      <member name=""C"" statusnod=""NoObservableMemberProperties"" pi=""[System.Object]"" runtimetype=""System.String"" />
    </member>
    <member name=""ABC2"" statusnod=""INPCSource"" pi=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ABC]"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ABC]"" onpc=""[OnPC]"">
      <member name=""A"" statusnod=""NoObservableMemberProperties"" pi=""[System.Object]"" runtimetype=""System.String"" />
      <member name=""B"" statusnod=""NoObservableMemberProperties"" pi=""[System.Object]"" runtimetype=""System.String"" />
      <member name=""C"" statusnod=""NoObservableMemberProperties"" pi=""[System.Object]"" runtimetype=""System.String"" />
    </member>
  </model>
</model>";
            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting NON-NULL property instances."
            );

            // +++++++++++++++++++++++++++++++++++++++++++++++++++++++++
            // Change a value that is NOT an INPC model
            // +++++++++++++++++++++++++++++++++++++++++++++++++++++++++
            var abc1 = obc[0].ABC1 ?? throw new NullReferenceException();
            clearQueues();
            abc1.A = 1; // Property Change
            currentEvent = eventsPC.DequeueSingle();

            actual = currentEvent.SenderModel?.ToString() ?? throw new NullReferenceException();
            expected = @" 
<member name=""A"" statusnod=""NoObservableMemberProperties"" pi=""[System.Object]"" runtimetype=""System.Int32"" />";
            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting 'A' now has Int32 runtime type because its value is 1."
            );
        }

        // EXPECT
        // - The ObservableCollection is initialized with four ABC instances.
        // - CRITICAL: The origin model should correctly reflect all pre-existing instances in the collection.
        // - Enumerating the collection should NOT trigger any events.
        // - The collection's ToString() override should return the expected formatted string representation.

        [TestMethod]
        public void Test_ABC()
        {
            XElement originModel;
            Dictionary<Enum, int> eventDict = new();
            var obc = new ObservableCollection<ABC>
            {
                new ABC(),
                new ABC(),
                new ABC(),
                new ABC(),
            }.WithNotifyOnDescendants(
                out originModel,
                onPC: (sender, e) => eventsPC.Enqueue(new SenderEventPair(sender, e)),
                onCC: (sender, e) => eventsCC.Enqueue(new SenderEventPair(sender, e)));


            // ====================================================================
            // Use the ToString() override of class ABC to show the contents of OBC.
            // ====================================================================

            var joined = string.Join(Environment.NewLine, obc);
            actual = joined;
            actual.ToClipboard();
            actual.ToClipboardAssert("Expecting msg");
            { }
            expected = @" 
A | B | C
A | B | C
A | B | C
A | B | C";

            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting msg"
            );

            Assert.AreEqual(0, eventDict.Count, "Expecting no events yet");


            actual = originModel.SortAttributes<SortOrderNOD>().ToString();
            actual.ToClipboard();
            actual.ToClipboardAssert("Expecting origin model to match");
            { }
            expected = @" 
<model name=""(Origin)ObservableCollection"" statusnod=""INPCSource, INCCSource"" instance=""[System.Collections.ObjectModel.ObservableCollection]"" onpc=""[OnPC]"" oncc=""[OnCC]"" notifyinfo=""[NotifyInfo]"">
  <member name=""Count"" statusnod=""NoObservableMemberProperties"" pi=""[System.Int32]"" />
  <model name=""(Origin)ABC"" statusnod=""INPCSource"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ABC]"" onpc=""[OnPC]"" notifyinfo=""[NotifyInfo]"">
    <member name=""A"" statusnod=""NoObservableMemberProperties"" pi=""[System.Object]"" runtimetype=""System.String"" />
    <member name=""B"" statusnod=""NoObservableMemberProperties"" pi=""[System.Object]"" runtimetype=""System.String"" />
    <member name=""C"" statusnod=""NoObservableMemberProperties"" pi=""[System.Object]"" runtimetype=""System.String"" />
  </model>
  <model name=""(Origin)ABC"" statusnod=""INPCSource"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ABC]"" onpc=""[OnPC]"" notifyinfo=""[NotifyInfo]"">
    <member name=""A"" statusnod=""NoObservableMemberProperties"" pi=""[System.Object]"" runtimetype=""System.String"" />
    <member name=""B"" statusnod=""NoObservableMemberProperties"" pi=""[System.Object]"" runtimetype=""System.String"" />
    <member name=""C"" statusnod=""NoObservableMemberProperties"" pi=""[System.Object]"" runtimetype=""System.String"" />
  </model>
  <model name=""(Origin)ABC"" statusnod=""INPCSource"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ABC]"" onpc=""[OnPC]"" notifyinfo=""[NotifyInfo]"">
    <member name=""A"" statusnod=""NoObservableMemberProperties"" pi=""[System.Object]"" runtimetype=""System.String"" />
    <member name=""B"" statusnod=""NoObservableMemberProperties"" pi=""[System.Object]"" runtimetype=""System.String"" />
    <member name=""C"" statusnod=""NoObservableMemberProperties"" pi=""[System.Object]"" runtimetype=""System.String"" />
  </model>
  <model name=""(Origin)ABC"" statusnod=""INPCSource"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.ABC]"" onpc=""[OnPC]"" notifyinfo=""[NotifyInfo]"">
    <member name=""A"" statusnod=""NoObservableMemberProperties"" pi=""[System.Object]"" runtimetype=""System.String"" />
    <member name=""B"" statusnod=""NoObservableMemberProperties"" pi=""[System.Object]"" runtimetype=""System.String"" />
    <member name=""C"" statusnod=""NoObservableMemberProperties"" pi=""[System.Object]"" runtimetype=""System.String"" />
  </model>
</model>";

            Assert.AreEqual(
                expected.NormalizeResult(),
                actual.NormalizeResult(),
                "Expecting origin model to match"
            );

            // ====================================================================
            // We are REPLACING the FIRST LIST ITEM in its entirity.
            // ====================================================================
            clearQueues();
            obc[0] = new ABC();

            currentEvent = eventsCC.DequeueSingle();

            Assert.AreEqual(
                NotifyCollectionChangedAction.Replace,
                currentEvent.NotifyCollectionChangedEventArgs.Action
            );

            // =====================================================================
            // NOW make sure the new instance has successfully bound PropertyChanges.
            // =====================================================================

            obc[0].A = "AA";    // Property change
            currentEvent = eventsPC.DequeueSingle();
            Assert.AreEqual(
                nameof(ABC.A),
                currentEvent.PropertyName,
                "Expecting property changed event has been raised.");
        }
    }
}
