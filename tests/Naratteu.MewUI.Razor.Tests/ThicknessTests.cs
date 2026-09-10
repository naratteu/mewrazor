using Aprillz.MewUI.Controls;

namespace Naratteu.MewUI.Razor.Tests;

/// <summary>
/// A uniform thickness needs no constructor: MewUI's <c>Thickness</c> converts from a double,
/// and the conversion survives the nullable parameter the generator emits.
/// </summary>
public class ThicknessTests
{
    [Fact]
    public void NumberIsAUniformThickness()
    {
        var panel = new MewRazorRenderer().Mount<ThicknessHarness, StackPanel>();

        Assert.Equal(new Aprillz.MewUI.Thickness(20), panel.Margin);
        Assert.Equal(new Aprillz.MewUI.Thickness(4, 8), panel.Padding);
        Assert.Equal(new Aprillz.MewUI.Thickness(1.5), ((TextBlock)panel.Children[0]).Margin);
    }
}
