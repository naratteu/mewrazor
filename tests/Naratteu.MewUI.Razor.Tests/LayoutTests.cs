using Aprillz.MewUI.Controls;

namespace Naratteu.MewUI.Razor.Tests;

/// <summary>
/// Grid and dock placement are attached properties in MewUI -- set on the child, read by the
/// panel -- so they cannot come out of a control's own property list and need parameters of
/// their own on every component.
/// </summary>
public class LayoutTests
{
    private static TextBlock Find(Panel panel, string text) => panel.Children
        .OfType<TextBlock>()
        .Single(child => child.Text == text);

    [Fact]
    public void ChildrenCarryTheirGridAndDockPlacement()
    {
        var dock = new MewRazorRenderer().Mount<LayoutHarness, DockPanel>();
        var grid = dock.Children.OfType<Grid>().Single();

        Assert.Equal(Dock.Top, DockPanel.GetDock(Find(dock, "header")));

        Assert.Equal((0, 0), (Grid.GetRow(Find(grid, "corner")), Grid.GetColumn(Find(grid, "corner"))));
        Assert.Equal((1, 1), (Grid.GetRow(Find(grid, "cell")), Grid.GetColumn(Find(grid, "cell"))));
        Assert.Equal(3, Grid.GetRowSpan(Find(grid, "tall")));
        Assert.Equal((2, 2), (Grid.GetRow(Find(grid, "wide")), Grid.GetColumnSpan(Find(grid, "wide"))));
    }

    [Fact]
    public void AGridTakesItsTracksAsText()
    {
        var grid = new MewRazorRenderer().Mount<LayoutHarness, DockPanel>().Children.OfType<Grid>().Single();

        Assert.Equal(2, grid.ColumnDefinitions.Count);
        Assert.Equal(3, grid.RowDefinitions.Count);
    }
}
