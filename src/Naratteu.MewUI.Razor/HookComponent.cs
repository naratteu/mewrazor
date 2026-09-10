using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Naratteu.MewUI.Razor;

/// <summary>
/// A component whose body re-runs on every render, with state kept in call-ordered slots.
/// This is the React hook model: nothing inspects what a closure captured, the whole body
/// simply runs again and the diff decides what actually changes.
/// </summary>
public abstract class HookComponent : IComponent, IHandleAfterRender, IDisposable
{
    private readonly List<object?> _slots = [];
    private readonly List<EffectSlot> _pending = [];
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
        if (_slots[slot] is HookSlot) throw OutOfOrder(slot, "UseState");

        return ((T)_slots[slot]!, next =>
        {
            if (Equals(_slots[slot], next)) return;
            _slots[slot] = next;
            Render();
        });
    }

    /// <summary>
    /// Keeps one value across renders and rebuilds it only when <paramref name="dependencies"/>
    /// change. A control that takes a data source needs this: handing it a freshly built items
    /// view on every render replaces the source, and with it whatever the user had selected.
    /// </summary>
    protected T UseMemo<T>(Func<T> create, params object?[] dependencies)
    {
        if (_cursor == _slots.Count) _slots.Add(new MemoSlot());
        var index = _cursor++;
        if (_slots[index] is not MemoSlot slot) throw OutOfOrder(index, "UseMemo");

        if (!slot.Created || !Unchanged(slot.Dependencies, dependencies))
        {
            slot.Dependencies = dependencies;
            slot.Value = create();
            slot.Created = true;
        }

        return (T)slot.Value!;
    }

    /// <summary>
    /// Runs <paramref name="effect"/> once the render that declared it has reached the control
    /// tree, and again whenever <paramref name="dependencies"/> change. The action it returns is
    /// the cleanup: it runs before the next run, and once more when the component leaves the tree.
    /// Passing no dependencies means the effect runs once, on mount.
    /// </summary>
    protected void UseEffect(Func<Action?> effect, params object?[] dependencies)
    {
        if (_cursor == _slots.Count) _slots.Add(new EffectSlot());
        var index = _cursor++;
        if (_slots[index] is not EffectSlot slot) throw OutOfOrder(index, "UseEffect");

        if (slot.Scheduled && Unchanged(slot.Dependencies, dependencies)) return;

        slot.Scheduled = true;
        slot.Dependencies = dependencies;
        slot.Next = effect;
        if (!_pending.Contains(slot)) _pending.Add(slot);
    }

    /// <summary>An effect with nothing to clean up.</summary>
    protected void UseEffect(Action effect, params object?[] dependencies)
        => UseEffect(
            () =>
            {
                effect();
                return null;
            },
            dependencies);

    /// <remarks>
    /// Effects run here rather than in the body so that the controls the render describes exist
    /// by the time an effect looks at them, and so state set by an effect renders normally.
    /// </remarks>
    Task IHandleAfterRender.OnAfterRenderAsync()
    {
        if (_pending.Count == 0) return Task.CompletedTask;

        var running = _pending.ToArray();
        _pending.Clear();

        foreach (var slot in running)
        {
            var effect = slot.Next;
            slot.Next = null;
            slot.Cleanup?.Invoke();
            slot.Cleanup = effect?.Invoke();
        }

        return Task.CompletedTask;
    }

    /// <summary>Runs every live cleanup. Called by the renderer when the component is removed.</summary>
    public virtual void Dispose()
    {
        _pending.Clear();

        foreach (var candidate in _slots)
        {
            if (candidate is not EffectSlot slot) continue;

            var cleanup = slot.Cleanup;
            slot.Cleanup = null;
            cleanup?.Invoke();
        }
    }

    private void Render() => _renderHandle.Render(builder =>
    {
        _cursor = 0;
        BuildRenderTree(builder);
    });

    private static bool Unchanged(object?[] previous, object?[] current)
    {
        if (previous.Length != current.Length) return false;

        for (var i = 0; i < previous.Length; i++)
        {
            if (!Equals(previous[i], current[i])) return false;
        }

        return true;
    }

    private static InvalidOperationException OutOfOrder(int slot, string hook) => new(
        $"Hook slot {slot} was claimed by a different hook on the previous render, and {hook} "
        + "reached it this time. Hooks are identified by call order, so they must not run inside "
        + "a conditional, a loop, or an early return.");

    /// <summary>A slot holding something other than a plain <see cref="UseState{T}"/> value.</summary>
    private abstract class HookSlot
    {
        public object?[] Dependencies { get; set; } = [];
    }

    private sealed class MemoSlot : HookSlot
    {
        public bool Created { get; set; }

        public object? Value { get; set; }
    }

    private sealed class EffectSlot : HookSlot
    {
        public bool Scheduled { get; set; }

        public Func<Action?>? Next { get; set; }

        public Action? Cleanup { get; set; }
    }
}
