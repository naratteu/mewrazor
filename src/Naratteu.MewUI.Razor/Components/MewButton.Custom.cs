using Aprillz.MewUI.Controls;
using Microsoft.AspNetCore.Components;

namespace Naratteu.MewUI.Razor.Components;

public partial class MewButton
{
    /// <summary>Shorthand for a button whose content is a single line of text.</summary>
    [Parameter] public string? Text { get; set; }

    partial void ApplyCustomParameters()
    {
        if (Text is not null) Control.Content = new TextBlock { Text = Text };
    }
}
