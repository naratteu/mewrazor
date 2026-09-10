using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Naratteu.MewUI.Razor;

/// <summary>
/// A component whose body re-runs on every render, with state kept in call-ordered slots.
/// This is the React hook model: nothing inspects what a closure captured, the whole body
/// simply runs again and the diff decides what actually changes.
/// </summary>
public abstract class HookComponent : IComponent
{
    private readonly List<object?> _slots = [];
    private RenderHandle _renderHandle;
    private int _cursor;

    void IComponent.Attach(RenderHandle renderHandle) => _renderHandle = renderHandle;

    public virtual Task SetParametersAsync(ParameterView parameters)
    {
        parameters.SetParameterProperties(this);
        Render();
        return Task.CompletedTask;
    }

    /// <summary>Overridden by the generated Razor class.</summary>
    protected virtual void BuildRenderTree(RenderTreeBuilder builder)
    {
    }

    /// <summary>
    /// Declares one piece of state. The slot is identified by call order, so hooks must not
    /// run inside a conditional or a loop.
    /// </summary>
    protected (T Value, Action<T> Set) UseState<T>(T initial)
    {
        if (_cursor == _slots.Count) _slots.Add(initial);
        var slot = _cursor++;

        return ((T)_slots[slot]!, next =>
        {
            if (Equals(_slots[slot], next)) return;
            _slots[slot] = next;
            Render();
        });
    }

    private void Render() => _renderHandle.Render(builder =>
    {
        _cursor = 0;
        BuildRenderTree(builder);
    });
}
