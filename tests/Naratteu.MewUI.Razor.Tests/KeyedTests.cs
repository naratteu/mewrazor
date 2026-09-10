using Aprillz.MewUI.Controls;

namespace Naratteu.MewUI.Razor.Tests;

/// <summary>
/// The point of <c>@key</c> is that a reordered item keeps its control instance instead of
/// being rebuilt in place.
/// </summary>
public class KeyedTests
{
    private static string[] Texts(StackPanel panel)
        => panel.Children.OfType<TextBlock>().Select(t => t.Text).ToArray();

    [Fact]
    public void ReorderMovesControlsInsteadOfRebuildingThem()
    {
        var panel = new MewRazorRenderer().Mount<KeyedHarness, StackPanel>();
        Assert.Equal(["a", "b", "c"], Texts(panel));

        var first = panel.Children[0];

        KeyedHarness.Reorder(["c", "a", "b"]);

        Assert.Equal(["c", "a", "b"], Texts(panel));
        Assert.Same(first, panel.Children[1]);
    }

    [Fact]
    public void ReorderCombinedWithRemovalKeepsSurvivingControls()
    {
        var panel = new MewRazorRenderer().Mount<KeyedHarness, StackPanel>();
        var a = panel.Children[0];
        var c = panel.Children[2];

        KeyedHarness.Reorder(["c", "a"]);

        Assert.Equal(["c", "a"], Texts(panel));
        Assert.Same(c, panel.Children[0]);
        Assert.Same(a, panel.Children[1]);
    }

    [Fact]
    public void GrowingAKeyedListKeepsExistingControls()
    {
        var panel = new MewRazorRenderer().Mount<KeyedHarness, StackPanel>();
        var b = panel.Children[1];

        KeyedHarness.Reorder(["b", "a", "c", "d"]);

        Assert.Equal(["b", "a", "c", "d"], Texts(panel));
        Assert.Same(b, panel.Children[0]);
    }
}
