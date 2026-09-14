using Aprillz.MewUI.Controls;

namespace Naratteu.MewUI.Razor.Tests;

/// <summary>
/// A generated component pushes parameters onto its control, and MewUI controls raise change
/// events when their properties change. Without a guard the two chase each other: the parent
/// sets the value, the control reports it, the callback sets it back.
/// </summary>
public class EchoTests
{
    [Fact]
    public void ApplyingAParameterDoesNotRaiseItsCallback()
    {
        EchoHarness.Callbacks = 0;

        var panel = new MewRazorRenderer().Mount<EchoHarness, StackPanel>();
        Assert.True(panel.Children.OfType<CheckBox>().Single().IsChecked);
        Assert.Equal(0, EchoHarness.Callbacks);

        EchoHarness.Flip();

        Assert.False(panel.Children.OfType<CheckBox>().Single().IsChecked);
        Assert.Equal(0, EchoHarness.Callbacks);
    }
}
