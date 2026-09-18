using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using Naratteu.MewUI.Razor;

// Runs as a published NativeAOT binary. Mounting needs no window and no display, so what this
// checks is exactly the part that trimming could break: Blazor instantiates components and sets
// their parameters by reflection, and every parameter here is a different shape of that.
var failures = 0;

void Check<T>(string what, T actual, T expected)
{
    var ok = EqualityComparer<T>.Default.Equals(actual, expected);
    if (!ok) failures++;
    Console.WriteLine($"{(ok ? "ok  " : "FAIL")} {what}: {actual}");
}

var panel = new MewRazorRenderer().Mount<Probe, StackPanel>();

var text = panel.Children.OfType<TextBlock>().First();
var check = panel.Children.OfType<CheckBox>().Single();
var input = panel.Children.OfType<TextBox>().Single();
var label = panel.Children.OfType<TextBlock>().Last();

Check("enum parameter", panel.Orientation, Orientation.Horizontal);
Check("double parameter", panel.Spacing, 6d);
Check("struct parameter", panel.Margin, new Thickness(4, 8));
Check("converted struct parameter", text.Margin, new Thickness(20));
Check("string parameter", text.Text, "hello");
Check("nullable double parameter", text.FontSize, 18d);
Check("enum parameter on a child", text.FontWeight, FontWeight.Bold);
Check("nullable bool parameter", check.IsChecked, true);
Check("parameter on a hand-written component", label.Text, "p42");

Probe.Rename("world");
Check("state change reaches the control", text.Text, "world");

input.SelectAll();
input.ReplaceSelection("typed");
Check("event callback reaches state", text.Text, "typed");

Console.WriteLine(failures == 0 ? "all checks passed" : $"{failures} checks failed");
return failures == 0 ? 0 : 1;
