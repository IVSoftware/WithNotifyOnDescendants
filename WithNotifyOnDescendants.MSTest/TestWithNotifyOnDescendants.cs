using IVSoftware.Portable.Threading;
using IVSoftware.Portable.Xml.Linq.XBoundObject;
using IVSoftware.WinOS.MSTest.Extensions;
using System.CodeDom;
using System.Windows.Forms.Design;
using System.Xml.Linq;
using WithNotifyOnDescendants.Proto.MSTest.TestModels.AddRemoveWithDeepNesting;
using static IVSoftware.Portable.Threading.Extensions;

namespace WithNotifyOnDescendants.Proto.MSTest
{
    [TestClass]
    public class WithNotifyOnDescendants
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
        /// CONDITION: Starting with free-standing instances of ModelGO and ModelGOA.
        /// EXPECT: 
        /// 1 - After setting ModelGO.CompleteUnknown to ModelGOA, changes to ModelGOA should notify.
        /// 2 - After setting ModelGO.CompleteUnknown back to null, changes to ModelGOA should no longer notify.
        /// </summary>
        [TestMethod]
        public void RoundTripINPC()
        {
            var classGO = new ModelGO().WithNotifyOnDescendants(onPC: (sender, e) =>
            {
                eventsPC.Enqueue(new SenderEventPair(sender, e));
            });            
            var classGOA = new ModelGO();

            subtestAddNestedClassToModelGO();
            subtestExerciseGuidPropertyOnModelGOA();
            subtestExerciseObjectPropertyAsStringOnModelGOA();
            subtestClearObjectPropertyAndVerifyUnsubscribeOnModelGO();

            #region L o c a l F x       
            void subtestAddNestedClassToModelGO()
            {
                clearQueues();
                classGO.CompleteUnknown = classGOA; // Raise a Property Change
                currentEvent = eventsPC.DequeueSingle();

                actual = currentEvent.OriginModel.SortAttributes<SortOrderNOD>().ToString();
                expected = @" 
<model name=""(Origin)ModelGO"" statusnod=""INPCSource"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.AddRemoveWithDeepNesting.ModelGO]"" onpc=""[OnPC]"" notifyinfo=""[NotifyInfo]"">
  <member name=""Guid"" statusnod=""NoObservableMemberProperties"" pi=""[System.String]"" />
  <member name=""CompleteUnknown"" statusnod=""INPCSource"" pi=""[System.Object]"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.AddRemoveWithDeepNesting.ModelGO]"" onpc=""[OnPC]"">
    <member name=""Guid"" statusnod=""NoObservableMemberProperties"" pi=""[System.String]"" />
    <member name=""CompleteUnknown"" statusnod=""WaitingForValue"" pi=""[System.Object]"" />
  </member>
</model>";
                Assert.AreEqual(
                    expected.NormalizeResult(),
                    actual.NormalizeResult(),
                    "Expecting origin model reflects default instance of ModelGOA"
                );
            }
            void subtestExerciseGuidPropertyOnModelGOA()
            {
                classGOA.Guid = $"{Guid.NewGuid()}";    // Raise Property Change
                currentEvent = eventsPC.DequeueSingle();
                actual = currentEvent.SenderModel.SortAttributes<SortOrderNOD>().ToString();
                expected = @" 
<member name=""Guid"" statusnod=""NoObservableMemberProperties"" pi=""[System.String]"" />";
                Assert.AreEqual(
                    expected.NormalizeResult(),
                    actual.NormalizeResult(),
                    "Expecting property change on nested class."
                );
            }
            void subtestClearObjectPropertyAndVerifyUnsubscribeOnModelGO()
            {
                classGO.CompleteUnknown = null;  // Property Change
                currentEvent = eventsPC.DequeueSingle();

                actual = currentEvent.OriginModel.SortAttributes<SortOrderNOD>().ToString();
                expected = @" 
<model name=""(Origin)ModelGO"" statusnod=""INPCSource"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.AddRemoveWithDeepNesting.ModelGO]"" onpc=""[OnPC]"" notifyinfo=""[NotifyInfo]"">
  <member name=""Guid"" statusnod=""NoObservableMemberProperties"" pi=""[System.String]"" />
  <member name=""CompleteUnknown"" statusnod=""WaitingForValue"" pi=""[System.Object]"" />
</model>";
                Assert.AreEqual(
                    expected.NormalizeResult(),
                    actual.NormalizeResult(),
                    "Expecting the child model to be nulled out."
                );

                // EXPECT
                // - Now, changing a property in classGOA should no longer 
                //   invoke the origin handler.
                clearQueues();
                classGOA.Guid = $"{Guid.NewGuid()}";
                Assert.AreEqual(0, eventsPC.Count, "Expecting property change no longer invokes the origin handler.");

            }
            // EXPECT:
            // The `runtimetype` attribute should note the difference
            // between pi.PropertyType and the currentInstance.GetType()
            void subtestExerciseObjectPropertyAsStringOnModelGOA()
            {
                classGOA.CompleteUnknown = $"{Guid.NewGuid()}";    // Raise Property Change
                currentEvent = eventsPC.DequeueSingle();

                actual = currentEvent.SenderModel.SortAttributes<SortOrderNOD>().ToString();
                expected = @" 
<member name=""CompleteUnknown"" statusnod=""NoObservableMemberProperties"" pi=""[System.Object]"" runtimetype=""System.String"" />";
                Assert.AreEqual(
                    expected.NormalizeResult(),
                    actual.NormalizeResult(),
                    "Expecting property change on nested class."
                );
            }
            #endregion L o c a l F x
        }


        [TestMethod]
        public void AddRemoveWithDeepNesting()
        {
            ModelGO classGO = new ModelGO().WithNotifyOnDescendants(
                out XElement model,
                onPC: (sender, e) => eventsPC.Enqueue(new SenderEventPair(sender, e)),
                onCC: (sender, e) => eventsCC.Enqueue(new SenderEventPair(sender, e)),
                onXO: (sender, e) => eventsXO.Enqueue(new SenderEventPair(sender, e)));

            ModelLevel1GO? classGO1 = null;
            ModelLevel2GO? classGO2 = null;
            subtestInitialModel();
            subtestEventingForGuidChange();
            subtestEventingForObjectChangeLevel0();
            subtestEventingForObjectChangeLevel1();
            subtestDisposeLevel0CU();
            { }

            // EXPECT:
            // - string Guid{ get; }
            // - Initial model waiting for object
            void subtestInitialModel()
            {
                actual = model.SortAttributes<SortOrderNOD>().ToString();
                expected = @" 
<model name=""(Origin)ModelGO"" statusnod=""INPCSource"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.AddRemoveWithDeepNesting.ModelGO]"" onpc=""[OnPC]"" notifyinfo=""[NotifyInfo]"">
  <member name=""Guid"" statusnod=""NoObservableMemberProperties"" pi=""[System.String]"" />
  <member name=""CompleteUnknown"" statusnod=""WaitingForValue"" pi=""[System.Object]"" />
</model>";

                Assert.AreEqual(
                    expected.NormalizeResult(),
                    actual.NormalizeResult(),
                    "Expecting initial model"
                );
            }

            // EXPECT:
            // - The single PE event arrives in eventsPC
            // - Inspection of PropertyModel and SenderModel are accurate.
            void subtestEventingForGuidChange()
            {
                clearQueues();
                classGO.Guid = $"{Guid.NewGuid()}"; // Property Change
                currentEvent = eventsPC.DequeueSingle();

                // The property change.
                actual = currentEvent.SenderModel.SortAttributes<SortOrderNOD>().ToString();
                expected = @" 
<member name=""Guid"" statusnod=""NoObservableMemberProperties"" pi=""[System.String]"" />";

                Assert.AreEqual(
                    expected.NormalizeResult(),
                    actual.NormalizeResult(),
                    "Expecting PROPERTY model"
                );

                Assert.AreEqual(
                    expected.NormalizeResult(),
                    actual.NormalizeResult(),
                    "Expecting PROPERTY model"
                );

                Assert.AreEqual(
                    expected.NormalizeResult(),
                    actual.NormalizeResult(),
                    "Expecting onPC"
                );

                // DIFFERENT! Look at the INPC parent!
                actual = 
                    currentEvent
                    .SenderModel?
                    .Parent?
                    .SortAttributes<SortOrderNOD>().ToString() ?? throw new NullReferenceException();
                actual.ToClipboard();
                actual.ToClipboardAssert("Expecting SENDER model");
                expected = @" 
<model name=""(Origin)ModelGO"" statusnod=""INPCSource"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.AddRemoveWithDeepNesting.ModelGO]"" onpc=""[OnPC]"" notifyinfo=""[NotifyInfo]"">
  <member name=""Guid"" statusnod=""NoObservableMemberProperties"" pi=""[System.String]"" />
  <member name=""CompleteUnknown"" statusnod=""WaitingForValue"" pi=""[System.Object]"" />
</model>";

                Assert.AreEqual(
                    expected.NormalizeResult(),
                    actual.NormalizeResult(),
                    "Expecting SENDER model"
                );
            }

            // EXPECT:
            // - The single PE event arrives in eventsPC
            // - Inspection of PropertyModel and SenderModel are accurate.
            // - TYPE of CU is properly identified in the instance attribute.
            void subtestEventingForObjectChangeLevel0()
            {
                clearQueues();
                classGO.CompleteUnknown = new TestModels.AddRemoveWithDeepNesting.ModelLevel1GO(); // Property Change
                currentEvent = eventsPC.DequeueSingle();

                // The property change.
                actual = currentEvent.SenderModel.SortAttributes<SortOrderNOD>().ToString();
                expected = @" 
<member name=""CompleteUnknown"" statusnod=""INPCSource"" pi=""[System.Object]"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.AddRemoveWithDeepNesting.ModelLevel1GO]"" onpc=""[OnPC]"">
  <member name=""Guid"" statusnod=""NoObservableMemberProperties"" pi=""[System.String]"" />
  <member name=""CompleteUnknown"" statusnod=""WaitingForValue"" pi=""[System.Object]"" />
</member>";

                Assert.AreEqual(
                    expected.NormalizeResult(),
                    actual.NormalizeResult(),
                    "Expecting PROPERTY model"
                );

                Assert.AreEqual(
                    expected.NormalizeResult(),
                    actual.NormalizeResult(),
                    "Expecting PROPERTY model"
                );

                // DIFFERENT! Verify the INPC parent is still ModelGO
                actual = 
                    currentEvent
                    .SenderModel?
                    .Parent?.SortAttributes<SortOrderNOD>().ToString() ?? throw new NullReferenceException();
                expected = @" 
<model name=""(Origin)ModelGO"" statusnod=""INPCSource"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.AddRemoveWithDeepNesting.ModelGO]"" onpc=""[OnPC]"" notifyinfo=""[NotifyInfo]"">
  <member name=""Guid"" statusnod=""NoObservableMemberProperties"" pi=""[System.String]"" />
  <member name=""CompleteUnknown"" statusnod=""INPCSource"" pi=""[System.Object]"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.AddRemoveWithDeepNesting.ModelLevel1GO]"" onpc=""[OnPC]"">
    <member name=""Guid"" statusnod=""NoObservableMemberProperties"" pi=""[System.String]"" />
    <member name=""CompleteUnknown"" statusnod=""WaitingForValue"" pi=""[System.Object]"" />
  </member>
</model>";

                Assert.AreEqual(
                    expected.NormalizeResult(),
                    actual.NormalizeResult(),
                    "Expecting INPC PARENT model"
                );

                // EXPECT
                // - Retrieve instance from Property Model
                classGO1 =
                    currentEvent?
                    .SenderModel?
                    .GetInstance<TestModels.AddRemoveWithDeepNesting.ModelLevel1GO>()
                    ?? null!;
                Assert.IsNotNull(classGO1);
            }

            // EXPECT
            // Property changes of the deepest nested object are responsive.
            void subtestEventingForObjectChangeLevel1()
            {
                clearQueues();
                Assert.IsNotNull(classGO1);
                classGO1.CompleteUnknown = new TestModels.AddRemoveWithDeepNesting.ModelLevel2GO(); // Property Change
                currentEvent = eventsPC.DequeueSingle();

                // ModelLevel2GO.CompleteUnknown has changed.
                actual = currentEvent.SenderModel.SortAttributes<SortOrderNOD>().ToString();
                expected = @" 
<member name=""CompleteUnknown"" statusnod=""INPCSource"" pi=""[System.Object]"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.AddRemoveWithDeepNesting.ModelLevel2GO]"" onpc=""[OnPC]"">
  <member name=""Guid"" statusnod=""NoObservableMemberProperties"" pi=""[System.String]"" />
  <member name=""CompleteUnknown"" statusnod=""WaitingForValue"" pi=""[System.Object]"" />
</member>";

                Assert.AreEqual(
                    expected.NormalizeResult(),
                    actual.NormalizeResult(),
                    "Expecting PROPERTY model"
                );

                // The sender of the property change is now ModelGO1
                actual =
                    currentEvent
                    .SenderModel?
                    .Parent?
                    .SortAttributes<SortOrderNOD>().ToString() ?? throw new NullReferenceException();
                actual.ToClipboard();
                actual.ToClipboardAssert("Expecting INPC PARENT model");
                { }
                expected = @" 
<member name=""CompleteUnknown"" statusnod=""INPCSource"" pi=""[System.Object]"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.AddRemoveWithDeepNesting.ModelLevel1GO]"" onpc=""[OnPC]"">
  <member name=""Guid"" statusnod=""NoObservableMemberProperties"" pi=""[System.String]"" />
  <member name=""CompleteUnknown"" statusnod=""INPCSource"" pi=""[System.Object]"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.AddRemoveWithDeepNesting.ModelLevel2GO]"" onpc=""[OnPC]"">
    <member name=""Guid"" statusnod=""NoObservableMemberProperties"" pi=""[System.String]"" />
    <member name=""CompleteUnknown"" statusnod=""WaitingForValue"" pi=""[System.Object]"" />
  </member>
</member>";

                Assert.AreEqual(
                    expected.NormalizeResult(),
                    actual.NormalizeResult(),
                    "Expecting INPC PARENT model"
                );
                // EXPECT
                // - Retrieve instance of GO2 from PROPERTY Model
                classGO2 =
                    currentEvent?
                    .SenderModel?
                    .GetInstance<TestModels.AddRemoveWithDeepNesting.ModelLevel2GO>()
                    ?? throw new NullReferenceException("Make sure you're retrieving the right instance from the right model");
                { }

                // EXPECT
                // Property change at the deepest level is still connected.
                clearQueues();
                classGO2.Guid = $"{Guid.NewGuid()}";
                currentEvent = eventsPC.DequeueSingle();

                // NOW VIEW PROPERTY, SENDER, AND ROOT.

                actual = currentEvent.SenderModel.SortAttributes<SortOrderNOD>().ToString();;
                actual.ToClipboard();
                actual.ToClipboardAssert("Expecting PROPERTY model");
                expected = @" 
<member name=""Guid"" statusnod=""NoObservableMemberProperties"" pi=""[System.String]"" />";

                Assert.AreEqual(
                    expected.NormalizeResult(),
                    actual.NormalizeResult(),
                    "Expecting PROPERTY model"
                );

                actual =
                    currentEvent
                    .SenderModel?
                    .Parent?
                    .SortAttributes<SortOrderNOD>().ToString() ?? throw new NullReferenceException(); ;
                actual.ToClipboard();
                actual.ToClipboardAssert("Expecting INPC PARENT model");
                expected = @" 
<member name=""CompleteUnknown"" statusnod=""INPCSource"" pi=""[System.Object]"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.AddRemoveWithDeepNesting.ModelLevel2GO]"" onpc=""[OnPC]"">
  <member name=""Guid"" statusnod=""NoObservableMemberProperties"" pi=""[System.String]"" />
  <member name=""CompleteUnknown"" statusnod=""WaitingForValue"" pi=""[System.Object]"" />
</member>";

                Assert.AreEqual(
                    expected.NormalizeResult(),
                    actual.NormalizeResult(),
                    "Expecting INPC PARENT model"
                );

                actual = currentEvent.OriginModel.SortAttributes<SortOrderNOD>().ToString(); 
                expected = @" 
<model name=""(Origin)ModelGO"" statusnod=""INPCSource"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.AddRemoveWithDeepNesting.ModelGO]"" onpc=""[OnPC]"" notifyinfo=""[NotifyInfo]"">
  <member name=""Guid"" statusnod=""NoObservableMemberProperties"" pi=""[System.String]"" />
  <member name=""CompleteUnknown"" statusnod=""INPCSource"" pi=""[System.Object]"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.AddRemoveWithDeepNesting.ModelLevel1GO]"" onpc=""[OnPC]"">
    <member name=""Guid"" statusnod=""NoObservableMemberProperties"" pi=""[System.String]"" />
    <member name=""CompleteUnknown"" statusnod=""INPCSource"" pi=""[System.Object]"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.AddRemoveWithDeepNesting.ModelLevel2GO]"" onpc=""[OnPC]"">
      <member name=""Guid"" statusnod=""NoObservableMemberProperties"" pi=""[System.String]"" />
      <member name=""CompleteUnknown"" statusnod=""WaitingForValue"" pi=""[System.Object]"" />
    </member>
  </member>
</model>";
                Assert.AreEqual(
                    expected.NormalizeResult(),
                    actual.NormalizeResult(),
                    "Expecting PROPERTY model"
                );
            }

            // Expect verified removal of all connection points.
            void subtestDisposeLevel0CU() { }
            {
                clearQueues(ClearQueue.All);
                classGO.CompleteUnknown = null;
                currentEvent = eventsPC.DequeueSingle();

                var joined = string.Join(Environment.NewLine, eventsXO.Select(_=>_.ToString()));
                actual = joined;
                expected = @" 
[XElement.Remove] <member name=""Guid"" statusnod=""NoObservableMemberProperties"" pi=""[System.String]"" />
[XElement.Remove] <member name=""Guid"" statusnod=""NoObservableMemberProperties"" pi=""[System.String]"" />
[XElement.Remove] <member name=""CompleteUnknown"" statusnod=""INPCSource"" pi=""[System.Object]"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.AddRemoveWithDeepNesting.ModelLevel2GO]"" onpc=""[OnPC]"" />
[XElement.Remove] <member name=""CompleteUnknown"" statusnod=""INPCSource"" pi=""[System.Object]"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.AddRemoveWithDeepNesting.ModelLevel2GO]"" onpc=""[OnPC]"" />
[XAttribute.Remove] statusnod=""INPCSource""
[XBoundAttribute.Remove] instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.AddRemoveWithDeepNesting.ModelLevel1GO]""
[XBoundAttribute.Remove] onpc=""[OnPC]""
[XAttribute.Add] statusnod=""WaitingForValue""";

                Assert.AreEqual(
                    expected.NormalizeResult(),
                    actual.NormalizeResult(),
                    "Expecting values to match."
                );

                Assert.AreEqual(
                    expected.NormalizeResult(),
                    actual.NormalizeResult(),
                    "Expecting XObject events resulting from removal of models."
                );

                actual = currentEvent.OriginModel.SortAttributes<SortOrderNOD>().ToString();
                expected = @" 
<model name=""(Origin)ModelGO"" statusnod=""INPCSource"" instance=""[WithNotifyOnDescendants.Proto.MSTest.TestModels.AddRemoveWithDeepNesting.ModelGO]"" onpc=""[OnPC]"" notifyinfo=""[NotifyInfo]"">
  <member name=""Guid"" statusnod=""NoObservableMemberProperties"" pi=""[System.String]"" />
  <member name=""CompleteUnknown"" statusnod=""WaitingForValue"" pi=""[System.Object]"" />
</model>";

                Assert.AreEqual(
                    expected.NormalizeResult(),
                    actual.NormalizeResult(),
                    "Expecting removal (now WaitingForValue at level 0"
                );
            }
        }

        [TestMethod]
        public void CollectionActionReplace()
        {
        }
    }
}
