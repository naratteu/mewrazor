namespace Naratteu.MewUI.Razor;

/// <summary>
/// A box whose identity survives re-renders. Writing to it does not render anything, which is
/// the point: it carries what a long-lived closure needs to read later — the latest value of
/// something, a handle to cancel, a count that no control displays.
/// </summary>
public sealed class Ref<T>(T value)
{
    public T Value { get; set; } = value;
}
