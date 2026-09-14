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
        try { ApplyParameters(); }
        finally { IsApplyingParameters = false; }

        _renderHandle.Render(RenderChildContent);
        return Task.CompletedTask;
    }

    /// <summary>Pushes the current parameter values onto <see cref="NativeElement"/>.</summary>
    protected virtual void ApplyParameters()
    {
    }

    private void RenderChildContent(RenderTreeBuilder builder)
    {
        if (ChildContent is not null) builder.AddContent(0, ChildContent);
    }
}
