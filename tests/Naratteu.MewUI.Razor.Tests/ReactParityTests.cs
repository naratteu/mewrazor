using Aprillz.MewUI.Controls;

namespace Naratteu.MewUI.Razor.Tests;

/// <summary>
/// The hooks a React developer reaches for when a closure has to outlive the render that made
/// it. A ref is the escape hatch from the closure the effect captured; a callback is the way to
/// hand the same delegate down twice.
/// </summary>
public class RefTests
{
    private static string Text(StackPanel panel) => ((TextBlock)panel.Children[0]).Text;

    [Fact]
    public void AClosureMadeOnMountReadsTheCurrentValueThroughARef()
    {
        var panel = new MewRazorRenderer().Mount<RefHarness, StackPanel>();

        RefHarness.Tick();
        RefHarness.Tick();
        RefHarness.Tick();

        Assert.Equal("count=3", Text(panel));
    }

    [Fact]
    public void TheSameClosureWithoutARefKeepsTheValueItCaptured()
    {
        var panel = new MewRazorRenderer().Mount<RefHarness, StackPanel>();

        RefHarness.Stale();
        RefHarness.Stale();
        RefHarness.Stale();

        Assert.Equal("count=1", Text(panel));
    }

    [Fact]
    public void WritingToARefRendersNothing()
    {
        new MewRazorRenderer().Mount<RefHarness, StackPanel>();
        Assert.Equal(1, RefHarness.Renders());

        RefHarness.Tick();

        // One more render for the state change, and none for the two ref writes in the body.
        Assert.Equal(2, RefHarness.Renders());
    }
}

public class CallbackTests
{
    [Fact]
    public void ACallbackKeepsItsIdentityUntilItsDependenciesChange()
    {
        new MewRazorRenderer().Mount<CallbackHarness, StackPanel>();
        var forever = CallbackHarness.Forever();
        var perTopic = CallbackHarness.PerTopic();

        CallbackHarness.Bump();

        Assert.Same(forever, CallbackHarness.Forever());
        Assert.Same(perTopic, CallbackHarness.PerTopic());

        CallbackHarness.Switch("b");

        Assert.Same(forever, CallbackHarness.Forever());
        Assert.NotSame(perTopic, CallbackHarness.PerTopic());
    }

    [Fact]
    public void ACallbackWithNoDependenciesKeepsTheStateItCaptured()
    {
        var panel = new MewRazorRenderer().Mount<CallbackHarness, StackPanel>();

        CallbackHarness.Bump();
        CallbackHarness.Forever()();

        // The callback was built on the first render, where count was 0, so it still sets 1.
        Assert.Equal("a:1", ((TextBlock)panel.Children[0]).Text);
    }
}

/// <summary>
/// React's context, spelled the way Blazor spells it: a value cascaded down the tree and read by
/// name rather than threaded through every component in between.
/// </summary>
public class ContextTests
{
    [Fact]
    public void ACascadedValueReachesADescendantAndUpdates()
    {
        var panel = new MewRazorRenderer().Mount<ContextHarness, StackPanel>();
        Assert.Equal("theme=dark", ((TextBlock)panel.Children[0]).Text);

        ContextHarness.SwitchTheme("light");

        Assert.Equal("theme=light", ((TextBlock)panel.Children[0]).Text);
    }
}

/// <summary>
/// A render prop. The child owns the loop and the parent owns what a row looks like, which is
/// React's children-as-a-function with a Razor spelling.
/// </summary>
public class TemplateTests
{
    [Fact]
    public void AChildRendersTheTemplateItsParentPassed()
    {
        var panel = new MewRazorRenderer().Mount<TemplateHarness, StackPanel>();

        Assert.Equal(["row a", "row b"], panel.Children.OfType<TextBlock>().Select(text => text.Text));
    }
}
