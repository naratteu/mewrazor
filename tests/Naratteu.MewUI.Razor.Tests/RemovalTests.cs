using Aprillz.MewUI.Controls;

namespace Naratteu.MewUI.Razor.Tests;

/// <summary>
/// MewUI severs a control's bindings only when that control itself is disposed: removing it
/// from its parent does not, and disposing the parent does not reach it. A removed subtree that
/// is not disposed keeps pushing values into state that no longer displays it.
/// </summary>
public class RemovalTests
{
    private static void Type(TextBox input, string text)
    {
        input.SelectAll();
        input.ReplaceSelection(text);
    }

    [Fact]
    public void RemovedControlStopsFeedingState()
    {
        var panel = new MewRazorRenderer().Mount<RemovalHarness, StackPanel>();
        var input = panel.Children.OfType<TextBox>().Single();

        Type(input, "one");
        Assert.Equal("one", RemovalHarness.Current());

        RemovalHarness.Hide();
        Assert.Empty(panel.Children);

        Type(input, "two");

        Assert.Equal("one", RemovalHarness.Current());
    }
}
