# MewRazor

[한국어](README.ko.md)

Write [MewUI](https://www.nuget.org/packages?q=Aprillz.MewUI) desktop applications as Razor
components.

```razor
@inherits HookComponent
@{
    var (name, setName) = UseState("world");
}

<MewWindow Title="Hello MewRazor" Width="420" Height="240">
    <MewStackPanel Spacing="12" Margin="new Thickness(20)">
        <MewTextBlock Text="@($"Hello, {name}!")" FontSize="22" FontWeight="FontWeight.Bold" />
        <MewTextBox Value="@name" ValueChanged="setName" />
    </MewStackPanel>
</MewWindow>
```

That is a real native window. There is no HTML, no browser, and no web server anywhere in the
process — `<MewStackPanel>` compiles to a `StackPanel` control, and Blazor's diffing engine
applies changes straight to the MewUI element tree.

## Run it in one command

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download). No project file needed —
this is a [file-based app](https://learn.microsoft.com/dotnet/core/whats-new/dotnet-10/sdk#file-based-apps).

```bash
git clone https://github.com/naratteu/mewrazor
cd mewrazor/samples/hello
dotnet run hello.cs
```

Three files, and the third one is imports:

```csharp
// hello.cs
#:sdk Microsoft.NET.Sdk.Razor
#:property RootNamespace=
#:package Aprillz.MewUI.MacOS@0.21.1
#:project ../../src/Naratteu.MewUI.Razor/Naratteu.MewUI.Razor.csproj

using Aprillz.MewUI;
using Naratteu.MewUI.Razor;

Application
    .Create()
    .UseMacOS()
    .UseMewVGMetal()
    .BuildMainWindow<App>()
    .Run();
```

```razor
@* _Imports.razor *@
@using Microsoft.AspNetCore.Components
@using Aprillz.MewUI
@using Naratteu.MewUI.Razor
@using Naratteu.MewUI.Razor.Components
```

`App.razor` is the snippet at the top of this page.

> `#:property RootNamespace=` matters. Without it the SDK derives a root namespace from the
> entry file's name, the generated component class lands in it, and the top-level statements
> cannot see `App`.

Swap the platform lines for your OS:

| Target | `#:package` | Registration |
| --- | --- | --- |
| Windows (Direct2D) | `Aprillz.MewUI.Windows` | `.UseWin32().UseDirect2D()` |
| Windows (GDI) | `Aprillz.MewUI.Windows` | `.UseWin32().UseGdi()` |
| Linux (X11) | `Aprillz.MewUI.Linux` | `.UseX11().UseMewVGX11()` |
| macOS (Metal) | `Aprillz.MewUI.MacOS` | `.UseMacOS().UseMewVGMetal()` |

For a normal project instead of a single file, see [`samples/Playground`](samples/Playground) —
the only difference is `<Project Sdk="Microsoft.NET.Sdk.Razor">` with
`AddRazorSupportForMvc=false` and `StaticWebAssetsEnabled=false`.

## State

State is a plain local. Nothing is registered, bound, or observed:

```razor
@{
    var (count, setCount) = UseState(0);
}

<MewButton Text="Increment" OnClick="@(() => setCount(count + 1))" />

@if (count >= 3)
{
    <MewTextBlock Text="threshold reached" />
}
```

`setCount` re-runs the whole component body and the diff decides what actually changes — a
`TextBlock` whose text changed keeps its identity and only has `Text` reassigned. This is the
React model: state is identified by hook call order, so hooks must not run inside a conditional
or a loop, and nothing ever inspects what a closure captured.

Classic Blazor components work too. Inherit `ComponentBase` instead of `HookComponent` and use
fields with `@bind-Value`:

```razor
<MewTextBox @bind-Value="_name" />

@code {
    private string _name = "";
}
```

## Controls

Components are generated from MewUI's own metadata, so the control library is covered rather
than hand-picked: 69 components across the MewUI hierarchy, from `MewBorder` to `MewTreeView`.

The generated components mirror MewUI's class hierarchy, so each level declares only the
members it introduces:

```
MewButtonBase<T> : MewCommandSourceControlBase<T> : MewContentControlBase<T> : MewControlBase<T>
  : MewTextElementBase<T> : MewFrameworkElementBase<T> : MewUIElementBase<T> : MewElementBase<T>
```

Public settable properties become parameters, and `Action` / `Action<T>` events become
`EventCallback` parameters — `Button.Click` is reachable as `OnClick` without anyone writing it.
Every parameter is nullable and an unset one is never assigned, so a component you did not
configure does not stamp defaults over the theme.

Each generated component is `partial` with `OnControlCreated` and `ApplyCustomParameters` hooks,
which is how the three hand-written extensions add what metadata cannot express: window sizing,
`@bind-Value` on `MewTextBox`, and a `Text` shorthand on `MewButton`.

## How it works

The Razor SDK compiles `.razor` into `RenderTreeBuilder` calls. Those calls are not
HTML-specific: when a tag resolves to an `IComponent` type it becomes `OpenComponent<T>()` with
typed parameters, which is why a MewRazor view contains no HTML at all — every tag is a MewUI
control wrapper. An HTML element in a view is a hard error, not a silent fallback.

`MewRazorRenderer` is a `Microsoft.AspNetCore.Components.RenderTree.Renderer` that turns each
`RenderBatch` into inserts and removes on the MewUI element tree. Everything above it —
component lifecycle, parameters, `RenderFragment`, `EventCallback`, and the diffing itself — is
stock Blazor, on the supported `Microsoft.AspNetCore.Components` package. Nothing depends on
ASP.NET Core at runtime.

The design follows [BlazorBindings.Maui](https://github.com/Dreamescaper/BlazorBindings.Maui),
which does the same thing for .NET MAUI.

## Not done yet

**Collection controls take no items.** `ListBox`, `GridView`, `ComboBox`, `TreeView` and
`NavigationView` are generated and will happily appear in completion, but their item properties
are `IReadOnlyList<T>`, which is outside the generated parameter surface. They work as
containers and nothing else. Modelling items as child components is a design question, not a
filter tweak — see [#3](https://github.com/naratteu/mewrazor/issues/3).

Smaller gaps:

- No `UseEffect`, so a component that subscribes to something has no place to unsubscribe
  ([#2](https://github.com/naratteu/mewrazor/issues/2)).
- An attribute matching no parameter compiles and only fails when the component renders. That
  is Blazor's behaviour rather than something this library adds
  ([#4](https://github.com/naratteu/mewrazor/issues/4)).
- `Margin="new Thickness(20)"` is wordy; there is no shorthand
  ([#5](https://github.com/naratteu/mewrazor/issues/5)).

This library builds on `Microsoft.AspNetCore.Components.RenderTree`, which Microsoft marks with
`BL0006`: not recommended outside Blazor, and subject to change between releases. That is the
design, so the warning is suppressed once at the project level — but it is why the package pins
its `Microsoft.AspNetCore.Components` version rather than floating.

## License

[VibeCoded AI-Slop License v1.0](LICENSE). This code was written by an AI from prompts, and the
license is honest about what that means.
