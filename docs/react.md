# MewRazor for React developers

[한국어](react.ko.md)

Blazor's own style reads like Svelte: fields, lifecycle overrides, `@bind`. MewRazor keeps
Blazor's compiler and diff but puts the React model on top of them — a component body that re-runs
from the top, state identified by hook call order, and a diff that decides what actually changes.
The tags are native MewUI controls rather than DOM nodes, so there is no HTML anywhere, but the
shape of a component is the one you already know.

## The same counter, twice

```jsx
function Counter() {
  const [count, setCount] = useState(0);

  return (
    <div>
      <button onClick={() => setCount(count + 1)}>Increment</button>
      <span>count = {count}</span>
      {count >= 3 && <small>threshold reached</small>}
    </div>
  );
}
```

```razor
@inherits HookComponent
@{
    var (count, setCount) = UseState(0);
}

<MewStackPanel Spacing="8">
    <MewButton Text="Increment" OnClick="@(() => setCount(count + 1))" />
    <MewTextBlock Text="@($"count = {count}")" />

    @if (count >= 3)
    {
        <MewTextBlock Text="threshold reached" FontSize="12" />
    }
</MewStackPanel>
```

`setCount` re-runs the whole body. Nothing inspects what a closure captured, and a `TextBlock`
whose text changed keeps its identity and only has `Text` reassigned — the native control is not
rebuilt.

## What maps to what

| React | MewRazor |
| --- | --- |
| `function C() { … }` | a `.razor` file with `@inherits HookComponent` |
| JSX element | a MewUI component tag (`<MewStackPanel>`); HTML is a compile error |
| props | `[Parameter]` properties in `@code` |
| `children` | `ChildContent`, a `RenderFragment` |
| render prop | `RenderFragment<T>` |
| `useState` | `UseState` |
| `useEffect` | `UseEffect` |
| `useMemo` | `UseMemo` |
| `useCallback` | `UseCallback` |
| `useRef` (a value) | `UseRef` |
| `useRef` (a node) | `@ref` on the tag, then `NativeElement` |
| `useContext` | `<CascadingValue>` and `[CascadingParameter]` |
| `key` | `@key` |
| `onClick={fn}` | `OnClick="fn"` |
| `{cond && …}` | `@if (cond) { … }` |
| `items.map(…)` | `@foreach (var item in items) { … }` |
| `<>…</>` | nothing — a component may render several tags |

## Children and render props

`ChildContent` is `children`, and a typed fragment is a render prop — the child owns the loop, the
parent owns what a row looks like:

```razor
<TodoList Items="items">
    <Row Context="item">
        <MewTextBlock Text="@item.Text" />
    </Row>
</TodoList>
```

```razor
@foreach (var item in Items)
{
    @Row(item)
}

@code {
    [Parameter] public Item[] Items { get; set; } = [];

    [Parameter] public RenderFragment<Item> Row { get; set; } = default!;
}
```

## Hooks

`UseState` returns the value and its setter, as a tuple you deconstruct:

```razor
var (name, setName) = UseState("world");
```

The setter can be called from any thread — after an `await` that resumed on the pool, from a
process's output callback, from an R3 subscription. It hands the update to the UI thread itself, so
nothing has to marshal first.

`UseEffect` runs after the render reaches the control tree, and the action it returns is the
cleanup. No dependencies means once on mount; dependencies re-run it when they change, cleaning up
first:

```razor
UseEffect(() =>
{
    var timer = new DispatcherTimer()
        .IntervalMs(1000)
        .OnTick(() => setClock(DateTime.Now.ToString("HH:mm:ss")));

    timer.Start();
    return () => timer.Stop();
});
```

`UseMemo` keeps a value until its dependencies change, `UseCallback` does the same for a delegate,
and `UseRef` is a box that survives renders and never causes one:

```razor
var view = UseMemo(() => ItemsView.Create(items), items);
var onPick = UseCallback((int id) => setSelected(id), items);
var nextId = UseRef(1);
```

Context is spelled the Blazor way, but it is the same idea — a value handed down the tree instead
of through every component in between:

```razor
<CascadingValue Value="theme">
    <Toolbar />
</CascadingValue>
```

```razor
@code {
    [CascadingParameter] public Theme? Theme { get; set; }
}
```

## Six rules that are not React's

**1. A string attribute is a literal.** `Text="hello"` passes the word *hello*. To pass code, use
`@`: `Text="@name"`, `Text="@($"count = {count}")"`. Attributes of any other type are always C#,
which is why `Width="240"` and `Margin="20"` work without one.

**2. There is no DOM, so there is no CSS.** Spacing, colour and size are parameters on the control
(`Spacing`, `Foreground`, `FontSize`), and the rest comes from the MewUI theme. An HTML tag in a
view is a hard error, not a silent fallback.

**3. A prop is a property, so a cleanup sees the new value.** In React the body's locals are what a
closure captured; here an effect that reads `Channel` reads it off the component, and by cleanup
time it already holds the next value. Copy what you need into a local first:

```razor
@{
    var channel = Channel;

    UseEffect(() => Subscribe(channel), channel);
}
```

**4. Children do not re-render by default.** React re-renders the subtree and asks you to reach for
`memo`; here a child renders again only when a parameter actually changed. A handler written in the
markup captures state, so it *is* a new value every render — that, not render cost, is what
`UseCallback` is for.

**5. A misspelled attribute fails the build.** `Bold="true"` on a component that has no `Bold`
parameter is `MEW001`, reported on the `.razor` line. In React that is a silent no-op; in Blazor
alone it is an exception on the first frame.

**6. There is no functional update form.** `setCount(c => c + 1)` does not exist, and in a handler
you do not need it: the body re-runs on every change, so the handler you are looking at was built
with the current value. For a closure that outlives its render — a timer, a subscription — use a
ref, exactly as you would in React:

```razor
@{
    var (count, setCount) = UseState(0);

    var latest = UseRef(count);
    latest.Value = count;

    UseEffect(() =>
    {
        var timer = new DispatcherTimer().IntervalMs(1000).OnTick(() => setCount(latest.Value + 1));
        timer.Start();
        return () => timer.Stop();
    });
}
```

## A whole app

[`samples/todo`](../samples/todo) is the canonical first React app, written here. It is four files
and runs with `dotnet run todo.cs`.

The list lives in one place, and rows own nothing:

```razor
@inherits HookComponent
@{
    var (items, setItems) = UseState<Item[]>([]);
    var (draft, setDraft) = UseState("");

    var nextId = UseRef(1);
    var left = items.Count(item => !item.Done);

    void Add()
    {
        if (string.IsNullOrWhiteSpace(draft)) return;

        setItems([.. items, new Item(nextId.Value++, draft.Trim(), false)]);
        setDraft("");
    }

    void Toggle(int id) =>
        setItems([.. items.Select(item => item.Id == id ? item with { Done = !item.Done } : item)]);
}

<MewStackPanel Spacing="10" Margin="20">
    <MewStackPanel Orientation="Orientation.Horizontal" Spacing="8">
        <MewTextBox Value="@draft" ValueChanged="setDraft" Width="260" />
        <MewButton Text="Add" OnClick="Add" />
    </MewStackPanel>

    @foreach (var item in items)
    {
        <TodoRow @key="item.Id" Item="item" OnToggle="Toggle" OnRemove="Remove" />
    }

    <MewTextBlock Text="@($"{left} left")" FontSize="12" Foreground="Color.Gray" />
</MewStackPanel>
```

A row takes props and reports clicks upward, the way a presentational component does:

```razor
<MewStackPanel Orientation="Orientation.Horizontal" Spacing="8">
    <MewCheckBox IsChecked="Item.Done" OnCheckedChanged="@(_ => OnToggle.InvokeAsync(Item.Id))" />
    <MewTextBlock Text="@Item.Text" Width="280" />
    <MewButton Text="remove" OnClick="@(() => OnRemove.InvokeAsync(Item.Id))" />
</MewStackPanel>

@code {
    [Parameter] public Item Item { get; set; } = default!;

    [Parameter] public EventCallback<int> OnToggle { get; set; }

    [Parameter] public EventCallback<int> OnRemove { get; set; }
}
```

`@key` does what `key` does: toggling an item rewrites the array, and the keyed row keeps the
control it already had instead of being rebuilt.

## What is not here

- No `useReducer`, `useTransition`, `Suspense`, or portals.
- Long lists want a collection control rather than `@foreach`. `ListBox`, `ComboBox`, `GridView`
  and `TreeView` are data driven: they take an items view, and MewUI builds and recycles the rows.
  See [Collections](../README.md#collections).
- `TabControl` and `NavigationView` cannot be filled from a view yet
  ([#7](https://github.com/naratteu/mewrazor/issues/7)).
