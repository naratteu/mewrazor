using Aprillz.MewUI.Controls;

namespace Naratteu.MewUI.Razor.Tests;

/// <summary>Records what a component's effect did, in the order it happened.</summary>
public static class EffectLog
{
    private static readonly List<string> Entries = [];

    public static void Add(string entry) => Entries.Add(entry);

    public static string[] Drain()
    {
        var entries = Entries.ToArray();
        Entries.Clear();
        return entries;
    }
}

/// <summary>
/// A component that subscribes to something needs a place to unsubscribe. These tests pin the
/// three moments that matters at: after the control exists, when the dependency changes, and
/// when the component leaves the tree.
/// </summary>
public class EffectTests
{
    private static StackPanel Mount()
    {
        EffectLog.Drain();
        return new MewRazorRenderer().Mount<EffectHarness, StackPanel>();
    }

    [Fact]
    public void EffectRunsOnceAfterTheControlIsInTheTree()
    {
        Mount();

        Assert.Equal(["render a", "subscribe a (children=1)"], EffectLog.Drain());
    }

    [Fact]
    public void UnchangedDependenciesDoNotRerunTheEffect()
    {
        Mount();
        EffectLog.Drain();

        EffectHarness.Rerender();

        Assert.Equal(["render a"], EffectLog.Drain());
    }

    [Fact]
    public void ChangedDependencyCleansUpTheOldEffectFirst()
    {
        Mount();
        EffectLog.Drain();

        EffectHarness.Switch("b");

        Assert.Equal(["render b", "unsubscribe a", "subscribe b (children=1)"], EffectLog.Drain());
    }

    [Fact]
    public void LeavingTheTreeRunsTheCleanup()
    {
        var panel = Mount();
        EffectLog.Drain();

        EffectHarness.Unmount();

        Assert.Empty(panel.Children);
        Assert.Equal(["unsubscribe a"], EffectLog.Drain());
    }

    [Fact]
    public void EffectCanSetStateOnMount()
    {
        var panel = new MewRazorRenderer().Mount<MountEffectHarness, StackPanel>();

        Assert.Equal("loaded", ((TextBlock)panel.Children[0]).Text);
    }
}
