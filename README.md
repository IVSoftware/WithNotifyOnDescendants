Since you haven't accepted an answer so far, here's something different you could try.

Part of my job description is testing NuGet packages like [XBound Object](https://www.nuget.org/packages/IVSoftware.Portable.Xml.Linq.XBoundObject/1.2.10) which extends `System.Xml.Linq`, providing runtime Tag properties for elements in an XML hierarchy [(see: Source Code on GitHub)](https://github.com/IVSoftware/IVSoftware.Portable.Xml.Linq.XBoundObject.git). Your question presents a good opportunity for a demo solution looking through that lens. Instead of extending `ObservableCollection<T>`, we could leverage `XBoundObject` to make an extension for `object` (we'll call it `WithNotifyOnDescendants`) that improves the recursive discovery that you're already doing. The result is a root `XElement` which is an origin model of the nested object hierarchy to track changes without modifying class structures. To see the current dynamic state of everything below it, simply call `ToString()` on the root element.

And while this extension "could" be applied to `BCollection`, we could even go one better and have your `ClassA` be the root model.
___

```
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
```
___

**Inspecting the Origin Model**

Your question (as I understand it) isn’t just about making it work, but also about how to debug it when it doesn’t. After adding the first `ClassB` to `BCollection` simply call `A.OriginModel.ToString()` to view the result.

___
```xml
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
</model>"
```

___

**Example Code for a `WithNotifyOnDescendants` Extension**

There's more code than will fit here, but it's really not that much longer than the "first pass enumerator" in my previous answer [(see: WithNotifyOnDescendants.MainEntry.cs on GitHub](https://github.com/IVSoftware/WithNotifyOnDescendants/blob/master/WithNotifyOnDescendants/WithNotifyOnDescendants.MainEntry.cs)). The scheme in your original post is somewhat limited by only performing discovery when `BCollection` changes. This goes beyond that, and responds to `INotifyPropertyChanged` sources that come and go as _member properties of nested classes._ It also takes into account some of the edge cases that could result from doing this.

##### Edge Cases

- What if `ClassB` does not implement `INotifyPropertyChanged` but some of its member properties _do_?
- What if `ClassB` has singleton or `Lazy<T>` properties? How do you run discovery without activating them prematurely?
- What if `ClassB` (which in this case _does_ implement INPC) has a bindable property of type `object?` that is default `null`. When something comes along and sets this property to an instance that implements INPC how do the property changes of the new member get added to that of the parent class?
- And then, if that same property is set back to `null` then how to detect the necessary unsubscription of the outgoing instance?
- What if an arbitrary `ClassB` has a member that is another `ObservableCollection<T>`?

___
**Unit Testing**

JonasH's answer contains an excellent comment.

> Regardless of the approach I would recommend writing a fair bit of unit tests to confirm the desired behavior in various kinds of circumstances, since it is quite easy to make mistakes when writing code like this.

I agree. If you want to look into doing that, there are several unit tests in the repo. One of the highlights is a `TestMethod` designed to test your scenario specifically. Please refer to the online source code for details.


```
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
}
```