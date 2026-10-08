# React 개발자를 위한 MewRazor

[English](react.md)

Blazor의 기본 스타일은 스벨트에 가깝습니다 — 필드, 라이프사이클 오버라이드, `@bind`. MewRazor는
Blazor의 컴파일러와 diff는 그대로 쓰되 그 위에 React 모델을 올립니다. 컴포넌트 본문이 위에서부터
다시 실행되고, 상태는 훅 호출 순서로 식별되며, 무엇이 실제로 바뀔지는 diff가 정합니다. 태그는 DOM
노드가 아니라 네이티브 MewUI 컨트롤이라 HTML은 어디에도 없지만, 컴포넌트의 생김새는 이미 아는 그
모양입니다.

## 같은 카운터, 두 가지 언어

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

`setCount`는 본문을 통째로 다시 실행합니다. 클로저가 무엇을 캡쳐했는지는 아무도 들여다보지 않고,
텍스트만 바뀐 `TextBlock`은 인스턴스를 유지한 채 `Text`만 재대입됩니다 — 네이티브 컨트롤이 다시
만들어지지 않습니다.

## 무엇이 무엇에 대응하는가

| React | MewRazor |
| --- | --- |
| `function C() { … }` | `@inherits HookComponent`를 단 `.razor` 파일 |
| JSX 엘리먼트 | MewUI 컴포넌트 태그(`<MewStackPanel>`). HTML은 컴파일 에러 |
| props | `@code`의 `[Parameter]` 프로퍼티 |
| `children` | `RenderFragment`인 `ChildContent` |
| render prop | `RenderFragment<T>` |
| `useState` | `UseState` |
| `useEffect` | `UseEffect` |
| `useMemo` | `UseMemo` |
| `useCallback` | `UseCallback` |
| `useRef` (값) | `UseRef` |
| `useRef` (노드) | 태그에 `@ref`, 그리고 `NativeElement` |
| `useContext` | `<CascadingValue>` + `[CascadingParameter]` |
| `key` | `@key` |
| `onClick={fn}` | `OnClick="fn"` |
| `{cond && …}` | `@if (cond) { … }` |
| `items.map(…)` | `@foreach (var item in items) { … }` |
| `<>…</>` | 필요 없음 — 컴포넌트는 태그를 여러 개 렌더해도 됩니다 |

## children과 render prop

`ChildContent`가 곧 `children`이고, 타입이 있는 fragment가 render prop입니다 — 반복은 자식이 돌고,
행의 생김새는 부모가 정합니다:

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

## 훅

`UseState`는 값과 세터를 튜플로 돌려주고, 구조 분해로 받습니다:

```razor
var (name, setName) = UseState("world");
```

세터는 어느 스레드에서 불러도 됩니다. 풀 스레드에서 재개된 `await` 뒤든, 프로세스 출력 콜백이든,
R3 구독이든 세터가 알아서 UI 스레드로 넘기므로 호출하는 쪽이 따로 마샬링할 필요가 없습니다.

`UseEffect`는 렌더가 컨트롤 트리에 반영된 뒤 실행되고, 반환한 액션이 정리입니다. 의존성을 주지
않으면 마운트 때 한 번, 주면 값이 바뀔 때마다 이전 정리를 먼저 돌리고 다시 실행합니다:

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

`UseMemo`는 의존성이 바뀔 때까지 값을 유지하고, `UseCallback`은 델리게이트에 같은 일을 하며,
`UseRef`는 렌더를 넘어 살아남되 바뀌어도 아무것도 렌더하지 않는 상자입니다:

```razor
var view = UseMemo(() => ItemsView.Create(items), items);
var onPick = UseCallback((int id) => setSelected(id), items);
var nextId = UseRef(1);
```

context는 Blazor식 철자를 쓰지만 같은 개념입니다 — 중간의 모든 컴포넌트를 거치지 않고 트리 아래로
값을 내려보냅니다:

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

## React와 다른 규칙 여섯 가지

**1. 문자열 속성은 리터럴입니다.** `Text="hello"`는 *hello*라는 단어를 넘깁니다. 코드를 넘기려면
`@`가 필요합니다: `Text="@name"`, `Text="@($"count = {count}")"`. 문자열이 아닌 타입의 속성은
언제나 C#이라, `Width="240"`이나 `Margin="20"`은 `@` 없이 그대로 됩니다.

**2. DOM이 없으니 CSS도 없습니다.** 간격·색·크기는 컨트롤의 파라미터(`Spacing`, `Foreground`,
`FontSize`)이고 나머지는 MewUI 테마가 정합니다. 뷰 안의 HTML 태그는 조용한 폴백이 아니라
바로 에러입니다.

**3. prop은 프로퍼티라서, 정리는 새 값을 봅니다.** React에서는 본문의 지역 변수가 곧 클로저가
캡쳐한 것이지만, 여기서 이펙트가 읽는 `Channel`은 컴포넌트에서 읽는 값이고 정리가 돌 시점엔 이미
다음 값입니다. 필요한 값은 먼저 지역 변수로 받으세요:

```razor
@{
    var channel = Channel;

    UseEffect(() => Subscribe(channel), channel);
}
```

**4. 자식은 기본적으로 다시 렌더되지 않습니다.** React는 서브트리를 다시 렌더하고 `memo`를 쓰라고
하지만, 여기서는 파라미터가 실제로 바뀐 자식만 다시 렌더됩니다. 다만 마크업에 쓴 핸들러는 상태를
캡쳐하므로 **매 렌더마다 새 값**입니다 — 렌더 비용이 아니라 이것이 `UseCallback`이 있는 이유입니다.

**5. 오타 난 속성은 빌드를 실패시킵니다.** `Bold` 파라미터가 없는 컴포넌트에 `Bold="true"`를 쓰면
`MEW001`이 `.razor`의 해당 줄에 찍힙니다. React에서는 조용히 무시되고, Blazor만으로는 첫 프레임에서
예외가 납니다.

**6. 함수형 업데이트 문법은 없습니다.** `setCount(c => c + 1)`은 존재하지 않고, 핸들러에서는
필요하지도 않습니다. 상태가 바뀔 때마다 본문이 다시 실행되므로 지금 보고 있는 핸들러는 현재 값으로
만들어진 것입니다. 자기 렌더보다 오래 사는 클로저(타이머, 구독)에서는 React에서와 똑같이 ref를
쓰면 됩니다:

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

## 앱 하나 통째로

[`samples/todo`](../samples/todo)는 React의 그 첫 예제를 여기서 쓴 것입니다. 파일 네 개이고
`dotnet run todo.cs`로 실행됩니다.

목록은 한 곳에 있고, 행(row)은 아무것도 소유하지 않습니다:

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

행은 props를 받아 클릭을 위로 보고합니다. 프레젠테이셔널 컴포넌트 그대로입니다:

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

`@key`는 `key`가 하는 일을 그대로 합니다. 항목을 토글하면 배열이 새로 쓰이지만, 키가 붙은 행은
다시 만들어지지 않고 쓰던 컨트롤을 그대로 유지합니다.

## 여기 없는 것

- `useReducer`, `useTransition`, `Suspense`, 포털은 없습니다.
- 긴 목록은 `@foreach`가 아니라 컬렉션 컨트롤이 맞습니다. `ListBox`, `ComboBox`, `GridView`,
  `TreeView`는 데이터 기반이라 items view를 받고, 행은 MewUI가 만들고 재활용합니다.
  [컬렉션](../README.ko.md#컬렉션)을 보세요.
- `TabControl`과 `NavigationView`에는 아직 view를 건넬 수 없습니다
  ([#7](https://github.com/naratteu/mewrazor/issues/7)).
