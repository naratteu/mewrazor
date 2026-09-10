using Aprillz.MewUI.Controls;
using Naratteu.MewUI.Razor;

namespace Naratteu.MewUI.Razor.Tests;

/// <summary>
/// Drives a Razor component through the renderer and asserts the resulting MewUI control
/// tree, which is what the render-tree diff is translated into.
/// </summary>
public class RenderTests
{
    private static (StackPanel Panel, Harness Component) Mount()
    {
        var panel = new MewRazorRenderer().Mount<Harness, StackPanel>();
        return (panel, Harness.Current!);
    }

    private static string[] Labels(StackPanel panel) => panel.Children
        .Select(child => child switch
        {
            TextBlock text => text.Text,
            Button { Content: TextBlock label } => $"[{label.Text}]",
            _ => child.GetType().Name,
        })
        .ToArray();

    [Fact]
    public void RendersInitialTree()
    {
        var (panel, _) = Mount();

        Assert.Equal(["n=0"], Labels(panel));
    }

    [Fact]
    public void UpdatesTextInPlace()
    {
        var (panel, harness) = Mount();
        var before = panel.Children[0];

        harness.SetN(1);

        Assert.Equal(["n=1", "[btn0]"], Labels(panel));
        Assert.Same(before, panel.Children[0]);
    }

    [Fact]
    public void InsertsAndRemovesConditionalBranch()
    {
        var (panel, harness) = Mount();

        harness.SetN(2);
        Assert.Equal(["n=2", "conditional", "[btn0]", "[btn1]"], Labels(panel));

        harness.SetN(1);
        Assert.Equal(["n=1", "[btn0]"], Labels(panel));
    }

    [Fact]
    public void GrowsAndShrinksList()
    {
        var (panel, harness) = Mount();

        harness.SetN(4);
        Assert.Equal(["n=4", "conditional", "[btn0]", "[btn1]", "[btn2]", "[btn3]"], Labels(panel));

        harness.SetN(0);
        Assert.Equal(["n=0"], Labels(panel));
    }
}
