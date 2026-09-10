using Aprillz.MewUI.Controls;

namespace Naratteu.MewUI.Razor.Tests;

/// <summary>
/// Documents a real gap: an attribute that matches no parameter compiles cleanly and only
/// fails when the component renders.
/// </summary>
public class UnknownAttributeTests
{
    [Fact]
    public void UnknownAttributeFailsAtRuntimeNotCompileTime()
    {
        var error = Assert.ThrowsAny<Exception>(
            () => new MewRazorRenderer().Mount<UnknownAttributeHarness, StackPanel>());

        Assert.Contains("Bold", Flatten(error));
    }

    private static string Flatten(Exception? error)
        => error is null ? "" : error.Message + "\n" + Flatten(error.InnerException);
}
