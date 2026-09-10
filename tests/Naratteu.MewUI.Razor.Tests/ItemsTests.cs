using Aprillz.MewUI;
using Aprillz.MewUI.Controls;

namespace Naratteu.MewUI.Razor.Tests;

/// <summary>
/// MewUI's collection controls are data driven: they take an items view, not child controls.
/// The view is a parameter like any other, which makes keeping it stable the view's own job.
/// </summary>
public class ItemsTests
{
    private static ListBox Mount() => new MewRazorRenderer()
        .Mount<ItemsHarness, StackPanel>()
        .Children.OfType<ListBox>()
        .Single();

    [Fact]
    public void ItemsReachTheControl()
    {
        var list = Mount();

        Assert.Equal(3, list.ItemsSource.Count);
        Assert.Equal(["alpha", "beta", "gamma"], Enumerable.Range(0, 3).Select(list.ItemsSource.GetText));
    }

    [Fact]
    public void TheItemTemplateReachesTheControl()
    {
        var list = Mount();

        Assert.IsType<DelegateTemplate<string>>(list.ItemTemplate);
    }

    [Fact]
    public void AMemoizedSourceSurvivesAnUnrelatedRender()
    {
        var list = Mount();
        var source = list.ItemsSource;
        list.SelectedIndex = 2;

        ItemsHarness.Rerender();

        Assert.Same(source, list.ItemsSource);
        Assert.Equal(2, list.SelectedIndex);
    }

    [Fact]
    public void ChangingTheItemsRebuildsTheSource()
    {
        var list = Mount();
        var source = list.ItemsSource;

        ItemsHarness.Append();

        Assert.NotSame(source, list.ItemsSource);
        Assert.Equal(4, list.ItemsSource.Count);
        Assert.Equal("delta", list.ItemsSource.GetText(3));
    }
}
