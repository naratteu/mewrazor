using Aprillz.MewUI.Controls;

namespace Naratteu.MewUI.Razor.Tests;

/// <summary>
/// The shape of a React app, end to end: a list held as state, rows that own nothing and report
/// clicks upward, and identity carried by <c>@key</c> rather than by position.
/// </summary>
public class TodoTests
{
    private static StackPanel[] Rows(StackPanel panel) => [.. panel.Children.OfType<StackPanel>()];

    private static string Text(StackPanel row) => row.Children.OfType<TextBlock>().Single().Text;

    private static bool? Done(StackPanel row) => row.Children.OfType<CheckBox>().Single().IsChecked;

    private static string Footer(StackPanel panel) => panel.Children.OfType<TextBlock>().Last().Text;

    [Fact]
    public void RowsFollowTheListAndReportBackToIt()
    {
        var panel = new MewRazorRenderer().Mount<TodoHarness, StackPanel>();
        Assert.Empty(Rows(panel));
        Assert.Equal("0 left", Footer(panel));

        TodoHarness.Add("milk");
        TodoHarness.Add("eggs");

        Assert.Equal(["milk", "eggs"], Rows(panel).Select(Text));
        Assert.Equal("2 left", Footer(panel));

        var milk = Rows(panel)[0];
        TodoHarness.Toggle(1);

        Assert.True(Done(Rows(panel)[0]));
        Assert.Equal("1 left", Footer(panel));

        // Toggling rewrote the list, and the keyed row kept the control it already had.
        Assert.Same(milk, Rows(panel)[0]);

        TodoHarness.Remove(1);

        Assert.Equal(["eggs"], Rows(panel).Select(Text));
        Assert.Equal("1 left", Footer(panel));
    }
}
