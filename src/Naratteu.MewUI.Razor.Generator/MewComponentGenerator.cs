using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Naratteu.MewUI.Razor.Generator;

/// <summary>
/// Emits one Razor-usable component per MewUI control, mirroring MewUI's own class hierarchy
/// so each level declares only the members it introduces.
/// </summary>
[Generator]
public sealed class MewComponentGenerator : IIncrementalGenerator
{
    private const string ElementTypeName = "Aprillz.MewUI.Controls.Element";

    /// <summary>Property types that map cleanly onto a Razor attribute.</summary>
    private static readonly ImmutableHashSet<SpecialType> SupportedSpecialTypes =
    [
        SpecialType.System_Boolean, SpecialType.System_String, SpecialType.System_Double,
        SpecialType.System_Single, SpecialType.System_Int32, SpecialType.System_Int64,
    ];

    private static readonly ImmutableHashSet<string> SupportedValueTypes =
    [
        "Aprillz.MewUI.Thickness", "Aprillz.MewUI.Color", "Aprillz.MewUI.Size", "Aprillz.MewUI.Point",
    ];

    /// <summary>Names owned by the component base classes.</summary>
    private static readonly ImmutableHashSet<string> ReservedNames =
        ["Control", "NativeElement", "ChildContent"];

    public void Initialize(IncrementalGeneratorInitializationContext context)
        => context.RegisterSourceOutput(context.CompilationProvider, Emit);

    private static void Emit(SourceProductionContext context, Compilation compilation)
    {
        var element = compilation.GetTypeByMetadataName(ElementTypeName);
        if (element is null) return;

        foreach (var model in Collect(element))
        {
            context.AddSource($"{model.ComponentName}.g.cs", SourceText(model));
        }
    }

    private static IEnumerable<ControlModel> Collect(INamedTypeSymbol element)
    {
        var candidates = Walk(element.ContainingAssembly.GlobalNamespace)
            .Where(type => IsCandidate(type, element))
            .ToList();

        var known = new HashSet<INamedTypeSymbol>(candidates, SymbolEqualityComparer.Default);

        foreach (var type in candidates)
        {
            yield return new ControlModel(
                ControlType: type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                ComponentName: $"Mew{type.Name}",
                BaseComponentName: NearestGeneratedBase(type, known),
                IsAbstract: type.IsAbstract,
                IsSealed: type.IsSealed,
                Properties: [.. Properties(type)],
                Events: [.. Events(type)]);
        }
    }

    private static bool IsCandidate(INamedTypeSymbol type, INamedTypeSymbol element)
    {
        if (type.TypeKind != TypeKind.Class || type.IsStatic) return false;
        if (type.DeclaredAccessibility != Accessibility.Public) return false;
        if (type.IsGenericType) return false;
        if (!DerivesFrom(type, element) && !SymbolEqualityComparer.Default.Equals(type, element)) return false;
        return type.IsAbstract || HasParameterlessConstructor(type);
    }

    /// <summary>
    /// A sealed control cannot be a type constraint, so it never becomes a generic base and is
    /// skipped when looking for one.
    /// </summary>
    private static string? NearestGeneratedBase(INamedTypeSymbol type, HashSet<INamedTypeSymbol> known)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (!current.IsSealed && known.Contains(current)) return $"Mew{current.Name}Base";
        }

        return null;
    }

    private static IEnumerable<INamedTypeSymbol> Walk(INamespaceSymbol ns)
    {
        foreach (var member in ns.GetMembers())
        {
            switch (member)
            {
                case INamespaceSymbol nested:
                    foreach (var type in Walk(nested)) yield return type;
                    break;

                case INamedTypeSymbol type:
                    yield return type;
                    break;
            }
        }
    }

    private static bool DerivesFrom(INamedTypeSymbol type, INamedTypeSymbol target)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, target)) return true;
        }

        return false;
    }

    private static bool HasParameterlessConstructor(INamedTypeSymbol type) => type.InstanceConstructors
        .Any(c => c.Parameters.Length == 0 && c.DeclaredAccessibility == Accessibility.Public);

    private static IEnumerable<PropertyModel> Properties(INamedTypeSymbol type)
    {
        foreach (var property in type.GetMembers().OfType<IPropertySymbol>())
        {
            if (property.DeclaredAccessibility != Accessibility.Public) continue;
            if (property.IsStatic || property.IsIndexer) continue;
            if (property.SetMethod is not { DeclaredAccessibility: Accessibility.Public }) continue;
            if (property.GetMethod is not { DeclaredAccessibility: Accessibility.Public }) continue;
            if (ReservedNames.Contains(property.Name)) continue;
            if (property.GetAttributes().Any(a => a.AttributeClass?.Name == "ObsoleteAttribute")) continue;

            // A property already declared by a base type is generated on that base's component.
            if (property.IsOverride || IsShadowing(type, property)) continue;

            if (Unwrap(property.Type) is not { } underlying) continue;

            yield return new PropertyModel(
                property.Name,
                $"{underlying.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}?",
                underlying.IsValueType);
        }
    }

    private static bool IsShadowing(INamedTypeSymbol type, IPropertySymbol property)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.GetMembers(property.Name).OfType<IPropertySymbol>().Any()) return true;
        }

        return false;
    }

    /// <summary>Returns the value the parameter carries, or null when the type is not expressible.</summary>
    private static ITypeSymbol? Unwrap(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
        {
            type = nullable.TypeArguments[0];
        }

        if (type.TypeKind == TypeKind.Enum) return type;
        if (SupportedSpecialTypes.Contains(type.SpecialType)) return type;
        if (SupportedValueTypes.Contains(type.ToDisplayString())) return type;
        return null;
    }

    private static IEnumerable<EventModel> Events(INamedTypeSymbol type)
    {
        foreach (var member in type.GetMembers().OfType<IEventSymbol>())
        {
            if (member.DeclaredAccessibility != Accessibility.Public || member.IsStatic) continue;
            if (member.Type is not INamedTypeSymbol handler) continue;

            var name = handler.OriginalDefinition.ToDisplayString();
            string? argument = name switch
            {
                "System.Action" => null,
                "System.Action<T>" => handler.TypeArguments[0]
                    .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                _ => "?",
            };

            if (argument == "?") continue;

            yield return new EventModel(member.Name, $"On{member.Name}", argument);
        }
    }

    private static string SourceText(ControlModel model)
    {
        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated/>");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();
        builder.AppendLine("namespace Naratteu.MewUI.Razor.Components;");
        builder.AppendLine();

        if (model.IsSealed)
        {
            AppendConcrete(builder, model, ownMembers: true);
            return builder.ToString();
        }

        var baseType = model.BaseComponentName is null
            ? "global::Naratteu.MewUI.Razor.MewComponentBase<TControl>"
            : $"global::Naratteu.MewUI.Razor.Components.{model.BaseComponentName}<TControl>";

        builder.AppendLine($"/// <summary>Members introduced by <see cref=\"{model.ControlType}\"/>.</summary>");
        builder.AppendLine($"public abstract partial class {model.ComponentName}Base<TControl> : {baseType}");
        builder.AppendLine($"    where TControl : {model.ControlType}, new()");
        builder.AppendLine("{");
        AppendEventSubscriptions(builder, model, $"{model.ComponentName}Base", "protected");
        AppendParameters(builder, model);
        AppendApply(builder, model, customHook: false);
        builder.AppendLine("}");

        if (!model.IsAbstract)
        {
            builder.AppendLine();
            AppendConcrete(builder, model, ownMembers: false);
        }

        return builder.ToString();
    }

    private static void AppendConcrete(StringBuilder builder, ControlModel model, bool ownMembers)
    {
        var baseType = ownMembers
            ? model.BaseComponentName is null
                ? $"global::Naratteu.MewUI.Razor.MewComponentBase<{model.ControlType}>"
                : $"global::Naratteu.MewUI.Razor.Components.{model.BaseComponentName}<{model.ControlType}>"
            : $"{model.ComponentName}Base<{model.ControlType}>";

        builder.AppendLine($"/// <summary>Razor component for <see cref=\"{model.ControlType}\"/>.</summary>");
        builder.AppendLine($"public partial class {model.ComponentName} : {baseType}");
        builder.AppendLine("{");

        if (ownMembers)
        {
            AppendEventSubscriptions(builder, model, model.ComponentName, "public", callsHook: true);
        }
        else
        {
            builder.AppendLine($"    public {model.ComponentName}() => OnControlCreated();");
            builder.AppendLine();
        }

        builder.AppendLine("    /// <summary>Hand-written partials hook control setup here.</summary>");
        builder.AppendLine("    partial void OnControlCreated();");
        builder.AppendLine();

        if (ownMembers) AppendParameters(builder, model);

        AppendApply(builder, model, customHook: true, declaredMembers: ownMembers);

        builder.AppendLine();
        builder.AppendLine("    /// <summary>Hand-written partials apply extra parameters here.</summary>");
        builder.AppendLine("    partial void ApplyCustomParameters();");
        builder.AppendLine("}");
    }

    private static void AppendEventSubscriptions(
        StringBuilder builder, ControlModel model, string typeName, string accessibility, bool callsHook = false)
    {
        if (model.Events.Count == 0 && !callsHook) return;

        builder.AppendLine($"    {accessibility} {typeName}()");
        builder.AppendLine("    {");
        foreach (var e in model.Events)
        {
            // Subscribe once; the handler reads whichever callback the latest render supplied.
            builder.AppendLine(e.ArgumentType is null
                ? $"        Control.{e.Name} += () => _ = {e.ParameterName}.InvokeAsync();"
                : $"        Control.{e.Name} += __arg => _ = {e.ParameterName}.InvokeAsync(__arg);");
        }

        if (callsHook) builder.AppendLine("        OnControlCreated();");
        builder.AppendLine("    }");
        builder.AppendLine();
    }

    private static void AppendParameters(StringBuilder builder, ControlModel model)
    {
        foreach (var p in model.Properties)
        {
            builder.AppendLine("    [global::Microsoft.AspNetCore.Components.Parameter]");
            builder.AppendLine($"    public {p.ParameterType} {p.Name} {{ get; set; }}");
            builder.AppendLine();
        }

        foreach (var e in model.Events)
        {
            var callback = e.ArgumentType is null
                ? "global::Microsoft.AspNetCore.Components.EventCallback"
                : $"global::Microsoft.AspNetCore.Components.EventCallback<{e.ArgumentType}>";

            builder.AppendLine("    [global::Microsoft.AspNetCore.Components.Parameter]");
            builder.AppendLine($"    public {callback} {e.ParameterName} {{ get; set; }}");
            builder.AppendLine();
        }
    }

    private static void AppendApply(
        StringBuilder builder, ControlModel model, bool customHook, bool declaredMembers = true)
    {
        builder.AppendLine("    protected override void ApplyParameters()");
        builder.AppendLine("    {");
        builder.AppendLine("        base.ApplyParameters();");

        foreach (var p in declaredMembers ? model.Properties : [])
        {
            builder.AppendLine(p.IsValueType
                ? $"        if ({p.Name} is {{ }} __{p.Name}) Control.{p.Name} = __{p.Name};"
                : $"        if ({p.Name} is not null) Control.{p.Name} = {p.Name};");
        }

        if (customHook) builder.AppendLine("        ApplyCustomParameters();");
        builder.AppendLine("    }");
    }
}
