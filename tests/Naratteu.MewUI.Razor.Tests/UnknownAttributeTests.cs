using Aprillz.MewUI.Controls;

namespace Naratteu.MewUI.Razor.Tests;

/// <summary>
/// The behaviour MEW001 exists to prevent. This project does not reference the analyzer, so the
/// harness still compiles and the runtime failure it produces stays visible: without the build-time
/// check, an attribute that matches no parameter throws when the component renders.
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
