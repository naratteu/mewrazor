using Aprillz.MewUI.Controls;

namespace Naratteu.MewUI.Razor.Tests;

/// <summary>
/// What a parent's render costs its children. Blazor stops at a child whose parameters it can
/// prove unchanged — which a handler written in the markup is not, because a lambda that captures
/// anything is a new delegate every render. That is the gap <c>UseCallback</c> closes.
/// </summary>
public class RerenderTests
{
    [Fact]
    public void OnlyChildrenWhoseParametersChangedRenderAgain()
    {
        PlainChild.Renders = 0;
        ChangingChild.Renders = 0;
        CallbackChild.Renders = 0;
        StableCallbackChild.Renders = 0;

        new MewRazorRenderer().Mount<RerenderHarness, StackPanel>();
        Assert.Equal(1, PlainChild.Renders);
        Assert.Equal(1, ChangingChild.Renders);
        Assert.Equal(1, CallbackChild.Renders);
        Assert.Equal(1, StableCallbackChild.Renders);

        RerenderHarness.Rerender();
        RerenderHarness.Rerender();

        Assert.Equal(1, PlainChild.Renders);
        Assert.Equal(3, ChangingChild.Renders);
        Assert.Equal(3, CallbackChild.Renders);
        Assert.Equal(1, StableCallbackChild.Renders);
    }
}
