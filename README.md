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
    <MewStackPanel Spacing="12" Margin="20">
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

### Effects

Anything that outlives a render — a timer, a subscription, a file watcher — is declared with
`UseEffect`. It runs once the render has reached the control tree, and the action it returns is
the cleanup:

```razor
@{
    var (clock, setClock) = UseState("--:--:--");

    UseEffect(() =>
    {
        var timer = new DispatcherTimer()
            .IntervalMs(1000)
            .OnTick(() => setClock(DateTime.Now.ToString("HH:mm:ss")));

        timer.Start();
        return () => timer.Stop();
    });
}
```

With no dependencies the effect runs once on mount, and its cleanup runs when the component
leaves the tree. Pass dependencies to re-run it when they change — the previous cleanup runs
first:

```razor
UseEffect(() =>
{
    var subscription = feed.Subscribe(channel, setMessage);
    return subscription.Dispose;
}, channel);
```

An effect may set state; that render happens normally. And like `UseState`, the slot is call
order, so an effect must not be declared inside a conditional or a loop.

One difference from React is worth knowing. A parameter read inside the effect is read off the
component, so by the time the cleanup runs it already holds the *new* value. Copy what the
cleanup needs into a local in the body — `var channel = Channel;` — and close over that.

### Collections

MewUI's collection controls are data driven: a `ListBox`, `ComboBox`, `GridView` or `TreeView`
takes an items view, not child controls. The view is a parameter like any other:

```razor
@{
    var (items, setItems) = UseState<string[]>(["alpha", "beta", "gamma"]);
    var (selected, setSelected) = UseState("(none)");

    var view = UseMemo(() => ItemsView.Create(items), items);
}

<MewListBox ItemsSource="view"
            OnSelectionChanged="@(item => setSelected(item?.ToString() ?? "(none)"))" />
```

`UseMemo` is what keeps that correct. Built inline, the view would be a new object on every
render, and replacing a control's source resets what the user had selected — so it is rebuilt
only when the items change.

Rows are built by MewUI's `IDataTemplate`, which is also a parameter (`ItemTemplate="template"`).
A `RenderFragment` is not a data template: what a template builds is bound and recycled by the
control, not diffed by Blazor.

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

A parameter takes a C# expression, not a string, so MewUI's own conversions apply: `Margin="20"`
is a uniform `Thickness`, because `Thickness` converts from a `double`. Sides that differ still
need the constructor — `Padding="new Thickness(4, 8)"`.

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

**`TabControl` and `NavigationView` take no items.** Every other collection control has an
assignable `ItemsSource` and is covered above, but tabs and navigation panes are exposed as
read-only collections (`Tabs`, `Pane`) that are filled by mutation, so there is nothing a view can
hand them. Modelling those as child components is a design question, not a filter tweak — see
[#7](https://github.com/naratteu/mewrazor/issues/7).

One smaller gap: an attribute matching no parameter compiles, and only fails when the component
renders. That is Blazor's behaviour rather than something this library adds
([#4](https://github.com/naratteu/mewrazor/issues/4)).

This library builds on `Microsoft.AspNetCore.Components.RenderTree`, which Microsoft marks with
`BL0006`: not recommended outside Blazor, and subject to change between releases. That is the
design, so the warning is suppressed once at the project level — but it is why the package pins
its `Microsoft.AspNetCore.Components` version rather than floating.

## License

[VibeCoded AI-Slop License v1.0](LICENSE). This code was written by an AI from prompts, and the
license is honest about what that means.
