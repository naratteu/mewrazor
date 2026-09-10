using Aprillz.MewUI.Controls;

namespace Naratteu.MewUI.Razor.Components;

public partial class MewWindow
{
    partial void ApplyCustomParameters()
    {
        // Window sizing is not element sizing: it goes through WindowSize. Applied here, after
        // the inherited Width/Height parameters, so the window rule wins.
        Control.Resizable(Width ?? 640, Height ?? 420, MinWidth ?? 320, MinHeight ?? 240);
    }
}
