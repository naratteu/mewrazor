using Aprillz.MewUI.Controls;

namespace Naratteu.MewUI.Razor.Tests;

/// <summary>
/// Editing the native control must travel back through the component to the owning state,
/// and the resulting re-render must reach the rest of the tree.
/// </summary>
public class BindingTests
{
    private static (TextBox Input, TextBlock Echo) Parts(StackPanel panel)
        => (panel.Children.OfType<TextBox>().Single(), panel.Children.OfType<TextBlock>().Single());

    /// <summary>
    /// Stands in for typing. Only MewUI's editing pipeline writes a two-way binding back to
    /// its source; assigning <c>Text</c> sets a local value and leaves the source untouched.
    /// </summary>
    private static void Type(TextBox input, string text)
    {
        input.SelectAll();
        input.ReplaceSelection(text);
    }

    [Fact]
    public void HookStyleBindingRoundTrips()
    {
        var panel = new MewRazorRenderer().Mount<BindHarness, StackPanel>();
        var (input, echo) = Parts(panel);
        Assert.Equal("hello ", echo.Text);

        Type(input, "world");

        Assert.Equal("hello world", echo.Text);
    }

    [Fact]
    public void BindValueDirectiveRoundTrips()
    {
        var panel = new MewRazorRenderer().Mount<ClassicBindHarness, StackPanel>();
        var (input, echo) = Parts(panel);

        Type(input, "razor");

        Assert.Equal("hello razor", echo.Text);
    }

    [Fact]
    public void ParameterWriteReachesTheControl()
    {
        var panel = new MewRazorRenderer().Mount<BindHarness, StackPanel>();
        var (input, _) = Parts(panel);

        Type(input, "typed");

        Assert.Equal("typed", input.Text);
    }
}
