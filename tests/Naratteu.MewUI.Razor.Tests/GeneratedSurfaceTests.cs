using System.Reflection;
using Aprillz.MewUI.Controls;
using Naratteu.MewUI.Razor.Components;

namespace Naratteu.MewUI.Razor.Tests;

/// <summary>Guards the breadth of generated components, not just the few used by the samples.</summary>
public class GeneratedSurfaceTests
{
    private static IReadOnlyList<Type> ConcreteComponents => typeof(MewComponentBase).Assembly
        .GetTypes()
        .Where(t => t is { IsAbstract: false, IsGenericTypeDefinition: false }
                    && typeof(MewComponentBase).IsAssignableFrom(t))
        .OrderBy(t => t.Name)
        .ToList();

    [Fact]
    public void CoversMostOfTheControlLibrary()
        => Assert.True(ConcreteComponents.Count >= 60, $"only {ConcreteComponents.Count} components generated");

    [Fact]
    public void EveryComponentCreatesItsControl()
    {
        var failures = new List<string>();

        foreach (var type in ConcreteComponents)
        {
            try
            {
                var component = (MewComponentBase)Activator.CreateInstance(type)!;
                if (component.NativeElement is null) failures.Add($"{type.Name}: null NativeElement");
            }
            catch (Exception ex)
            {
                failures.Add($"{type.Name}: {(ex as TargetInvocationException)?.InnerException?.Message ?? ex.Message}");
            }
        }

        Assert.Empty(failures);
    }
}

public class GeneratedMarkupTests
{
    [Fact]
    public void ComponentsNeverWrittenByHandWorkInRazor()
    {
        var grid = new MewRazorRenderer().Mount<GeneratedControlsHarness, Grid>();

        Assert.True(grid.ShowGridLine);
        Assert.Equal(6, grid.Spacing);

        var check = grid.Children.OfType<CheckBox>().Single();
        var label = grid.Children.OfType<Label>().Single();
        var border = grid.Children.OfType<Border>().Single();

        Assert.False(check.IsChecked);
        Assert.Equal("checked = False", label.Text);
        Assert.Equal(4, border.CornerRadius);
    }

    [Fact]
    public void GeneratedEventReachesState()
    {
        var grid = new MewRazorRenderer().Mount<GeneratedControlsHarness, Grid>();
        var check = grid.Children.OfType<CheckBox>().Single();
        var label = grid.Children.OfType<Label>().Single();

        check.IsChecked = true;

        Assert.Equal("checked = True", label.Text);
    }
}
