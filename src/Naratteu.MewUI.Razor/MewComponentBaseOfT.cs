using Aprillz.MewUI.Controls;

namespace Naratteu.MewUI.Razor;

/// <summary>
/// Component that owns one control of a known type. Generated components derive from this so
/// each level can assign to strongly typed control members.
/// </summary>
public abstract class MewComponentBase<TControl> : MewComponentBase
    where TControl : Element, new()
{
    protected TControl Control { get; } = new();

    public sealed override Element NativeElement => Control;
}
