namespace ClinicApp.Behaviors;

// Attach to a ScrollView; increment the bound Trigger property (e.g. after
// adding an item to a list above the fold) to animate the ScrollView back
// to the top so the user sees the change without losing their scroll flow.
public class ScrollToTopBehavior : Behavior<ScrollView>
{
    public static readonly BindableProperty TriggerProperty =
        BindableProperty.Create(
            nameof(Trigger),
            typeof(int),
            typeof(ScrollToTopBehavior),
            0,
            propertyChanged: OnTriggerChanged);

    public int Trigger
    {
        get => (int)GetValue(TriggerProperty);
        set => SetValue(TriggerProperty, value);
    }

    ScrollView? _scrollView;

    protected override void OnAttachedTo(ScrollView bindable)
    {
        base.OnAttachedTo(bindable);
        _scrollView = bindable;

        // Behaviors don't auto-inherit BindingContext from the control
        // they're attached to — without this, Trigger="{Binding ...}"
        // silently never resolves.
        BindingContext = bindable.BindingContext;
        bindable.BindingContextChanged += OnBindableContextChanged;
    }

    void OnBindableContextChanged(object? sender, EventArgs e)
    {
        BindingContext = _scrollView?.BindingContext;
    }

    protected override void OnDetachingFrom(ScrollView bindable)
    {
        base.OnDetachingFrom(bindable);
        bindable.BindingContextChanged -= OnBindableContextChanged;
        _scrollView = null;
    }

    static async void OnTriggerChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not ScrollToTopBehavior behavior || behavior._scrollView == null)
            return;

        // Give the CollectionView a moment to measure the newly added row
        // before scrolling, or the offset can land short.
        await Task.Delay(80);
        await behavior._scrollView.ScrollToAsync(0, 0, true);
    }
}