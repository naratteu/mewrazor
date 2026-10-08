using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using Microsoft.AspNetCore.Components;

namespace Naratteu.MewUI.Razor.Components;

/// <summary>Row and column tracks, written the way MewUI writes them: <c>"120,*"</c>, <c>"Auto,2*,*"</c>.</summary>
public partial class MewGrid
{
    private string? _columns;
    private string? _rows;

    [Parameter] public string? Columns { get; set; }

    [Parameter] public string? Rows { get; set; }

    partial void ApplyCustomParameters()
    {
        // The tracks are rebuilt only when the text changes, not on every parent render.
        if (Columns is not null && Columns != _columns) Control.Columns(_columns = Columns);
        if (Rows is not null && Rows != _rows) Control.Rows(_rows = Rows);
    }
}
