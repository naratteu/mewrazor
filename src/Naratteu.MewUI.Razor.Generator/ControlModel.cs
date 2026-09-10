namespace Naratteu.MewUI.Razor.Generator;

/// <summary>One MewUI control type and the members worth exposing as component parameters.</summary>
/// <param name="BaseComponentName">
/// Nearest ancestor that also has a generated component, or null to sit directly on
/// <c>MewComponentBase&lt;TControl&gt;</c>.
/// </param>
internal sealed record ControlModel(
    string ControlType,
    string ComponentName,
    string? BaseComponentName,
    bool IsAbstract,
    bool IsSealed,
    IReadOnlyList<PropertyModel> Properties,
    IReadOnlyList<EventModel> Events);

/// <param name="ParameterType">Always nullable, so an unset parameter leaves the control alone.</param>
internal sealed record PropertyModel(string Name, string ParameterType, bool IsValueType);

/// <param name="ArgumentType">Null for a plain <c>Action</c>.</param>
internal sealed record EventModel(string Name, string ParameterName, string? ArgumentType);
