using Aprillz.MewUI.Controls;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Naratteu.MewUI.Razor;

/// <summary>
/// Base class for a component that owns one MewUI control. Parameters are applied to the
/// control directly; child content is rendered so the renderer can attach it as native children.
/// </summary>
public abstract class MewComponentBase : IComponent
{
    private RenderHandle _renderHandle;

    /// <summary>The control this component owns. Must be created once and never replaced.</summary>
    public abstract Element NativeElement { get; }

    [Parameter] public RenderFragment? ChildContent { get; set; }

    // Placement is an attached property in MewUI: set on the child, read by the panel around it.
    // It belongs to no control's own property list, so every component carries it here.

    /// <summary>The row of a <c>MewGrid</c> this component sits in.</summary>
    [Parameter] public int? Row { get; set; }

    /// <summary>The column of a <c>MewGrid</c> this component sits in.</summary>
    [Parameter] public int? Column { get; set; }

    [Parameter] public int? RowSpan { get; set; }

    [Parameter] public int? ColumnSpan { get; set; }

    /// <summary>The edge of a <c>MewDockPanel</c> this component docks to.</summary>
    [Parameter] public Dock? Dock { get; set; }

    void IComponent.Attach(RenderHandle renderHandle) => _renderHandle = renderHandle;

    /// <summary>
    /// True while parameters are being pushed onto the control. A MewUI control raises its change
    /// event whenever a property changes, including when this component assigns it, and reporting
    /// that back to the parent that just set the value is how a controlled value starts
    /// oscillating: the parent sets, the control reports, the callback sets it back.
    /// </summary>
    protected bool IsApplyingParameters { get; private set; }

    public virtual Task SetParametersAsync(ParameterView parameters)
    {
        parameters.SetParameterProperties(this);

        IsApplyingParameters = true;
        try
        {
            ApplyParameters();
            ApplyPlacement();
        }
        finally
        {
            IsApplyingParameters = false;
        }

        _renderHandle.Render(RenderChildContent);
        return Task.CompletedTask;
    }

    /// <summary>Pushes the current parameter values onto <see cref="NativeElement"/>.</summary>
    protected virtual void ApplyParameters()
    {
    }

    private void ApplyPlacement()
    {
        if (Row is { } row) Grid.SetRow(NativeElement, row);
        if (Column is { } column) Grid.SetColumn(NativeElement, column);
        if (RowSpan is { } rowSpan) Grid.SetRowSpan(NativeElement, rowSpan);
        if (ColumnSpan is { } columnSpan) Grid.SetColumnSpan(NativeElement, columnSpan);
        if (Dock is { } dock) DockPanel.SetDock(NativeElement, dock);
    }

    private void RenderChildContent(RenderTreeBuilder builder)
    {
        if (ChildContent is not null) builder.AddContent(0, ChildContent);
    }
}
