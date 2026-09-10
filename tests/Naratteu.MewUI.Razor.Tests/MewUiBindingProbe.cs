using Aprillz.MewUI;
using Aprillz.MewUI.Controls;

namespace Naratteu.MewUI.Razor.Tests;

/// <summary>
/// Pins the MewUI contract MewTextBox depends on: a two-way binding writes back only from
/// the editing pipeline. Assigning the property, or calling SetCurrentValue, does not.
/// </summary>
public class MewUiBindingProbe
{
    private static (TextBox Box, ObservableValue<string> Source) Bound()
    {
        var box = new TextBox();
        var source = new ObservableValue<string>("start");
        box.SetBinding(TextBox.TextProperty, source, BindingMode.TwoWay);
        return (box, source);
    }

    [Fact]
    public void EditingWritesBackToTheSource()
    {
        var (box, source) = Bound();

        box.SelectAll();
        box.ReplaceSelection("edited");

        Assert.Equal("edited", box.Text);
        Assert.Equal("edited", source.Value);
    }

    [Fact]
    public void SourceChangeReachesTheControl()
    {
        var (box, source) = Bound();

        source.Value = "fromSource";

        Assert.Equal("fromSource", box.Text);
    }

    [Fact]
    public void ProgrammaticWritesDoNotWriteBack()
    {
        var (box, source) = Bound();

        box.SetCurrentValue(TextBox.TextProperty, "fromUser");

        Assert.Equal("fromUser", box.Text);
        Assert.Equal("start", source.Value);
    }
}
