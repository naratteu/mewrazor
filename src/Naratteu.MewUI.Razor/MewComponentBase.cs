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

    public virtual Task SetParametersAsync(ParameterView parameters)
    {
        parameters.SetParameterProperties(this);
        ApplyParameters();
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
