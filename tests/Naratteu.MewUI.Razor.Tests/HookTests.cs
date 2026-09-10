using Aprillz.MewUI.Controls;

namespace Naratteu.MewUI.Razor.Tests;

/// <summary>
/// Verifies the hook model end to end: a closure created during one render updates state,
/// the body re-runs, and the control tree follows.
/// </summary>
public class HookTests
{
    private static string[] Labels(StackPanel panel) => panel.Children
        .Select(child => child switch
        {
            TextBlock text => text.Text,
            Button { Content: TextBlock label } => $"[{label.Text}]",
            _ => child.GetType().Name,
        })
        .ToArray();

    [Fact]
    public void ClosureOverStateDrivesRerender()
    {
        var panel = new MewRazorRenderer().Mount<HookHarness, StackPanel>();
        Assert.Equal(["count=0", "idle"], Labels(panel));

        HookHarness.Probe();
        Assert.Equal(["count=1", "clicked 1", "[btn0]"], Labels(panel));

        HookHarness.Probe();
        Assert.Equal(["count=2", "clicked 2", "[btn0]", "[btn1]"], Labels(panel));
    }

    [Fact]
    public void StateSurvivesAcrossRenders()
    {
        var panel = new MewRazorRenderer().Mount<HookHarness, StackPanel>();
        var firstTextBlock = panel.Children[0];

        HookHarness.Probe();
        HookHarness.Probe();
        HookHarness.Probe();

        Assert.Equal("count=3", ((TextBlock)panel.Children[0]).Text);
        Assert.Same(firstTextBlock, panel.Children[0]);
    }
}

public class FragmentTests
{
    [Fact]
    public void InlineTemplateExpressionRenders()
    {
        var panel = new MewRazorRenderer().Mount<FragmentHarness, StackPanel>();

        Assert.Equal(
            ["n=0", "static"],
            panel.Children.OfType<TextBlock>().Select(t => t.Text));
    }
}
