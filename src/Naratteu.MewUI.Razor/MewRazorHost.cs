using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using Microsoft.AspNetCore.Components;

namespace Naratteu.MewUI.Razor;

public static class MewRazorHost
{
    /// <summary>
    /// Uses a Razor component as the main window. The component must render a single
    /// <c>&lt;MewWindow&gt;</c> at its root.
    /// </summary>
    public static ApplicationBuilder BuildMainWindow<TRoot>(this ApplicationBuilder builder)
        where TRoot : IComponent
        => builder.BuildMainWindow(() => new MewRazorRenderer().Mount<TRoot, Window>());
}
