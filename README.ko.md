# MewRazor

[English](README.md)

[MewUI](https://www.nuget.org/packages?q=Aprillz.MewUI) 데스크톱 애플리케이션을 Razor 컴포넌트로
작성합니다.

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

이건 진짜 네이티브 창입니다. 프로세스 안에 HTML도, 브라우저도, 웹 서버도 없습니다 —
`<MewStackPanel>`은 `StackPanel` 컨트롤로 컴파일되고, Blazor의 diff 엔진이 변경분을 MewUI
엘리먼트 트리에 직접 적용합니다.

## 한 줄로 실행하기

[.NET 10 SDK](https://dotnet.microsoft.com/download)가 필요합니다. 프로젝트 파일은 없어도 됩니다 —
[파일 기반 앱](https://learn.microsoft.com/dotnet/core/whats-new/dotnet-10/sdk#file-based-apps)입니다.

```bash
git clone https://github.com/naratteu/mewrazor
cd mewrazor/samples/hello
dotnet run hello.cs
```

파일 세 개이고, 그중 하나는 using 모음입니다:

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

`App.razor`는 이 문서 맨 위의 코드입니다.

> `#:property RootNamespace=`가 중요합니다. 이게 없으면 SDK가 진입 파일 이름에서 루트
> 네임스페이스를 만들어내고, 생성된 컴포넌트 클래스가 그 안으로 들어가 최상위 문에서 `App`이
> 보이지 않습니다.

플랫폼에 맞게 두 줄만 바꾸면 됩니다:

| 대상 | `#:package` | 등록 |
| --- | --- | --- |
| Windows (Direct2D) | `Aprillz.MewUI.Windows` | `.UseWin32().UseDirect2D()` |
| Windows (GDI) | `Aprillz.MewUI.Windows` | `.UseWin32().UseGdi()` |
| Linux (X11) | `Aprillz.MewUI.Linux` | `.UseX11().UseMewVGX11()` |
| macOS (Metal) | `Aprillz.MewUI.MacOS` | `.UseMacOS().UseMewVGMetal()` |

단일 파일 대신 일반 프로젝트로 쓰려면 [`samples/Playground`](samples/Playground)를 보세요.
차이는 `<Project Sdk="Microsoft.NET.Sdk.Razor">`에 `AddRazorSupportForMvc=false`와
`StaticWebAssetsEnabled=false`를 붙이는 것뿐입니다.

## 상태

상태는 그냥 지역 변수입니다. 등록도, 바인딩도, 관측도 없습니다:

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

`setCount`는 컴포넌트 본문을 통째로 다시 실행하고, 실제로 무엇이 바뀔지는 diff가 정합니다 —
텍스트만 바뀐 `TextBlock`은 인스턴스를 유지한 채 `Text`만 재대입됩니다. React와 같은 모델이라,
상태는 훅 호출 순서로 식별되고(따라서 훅을 조건문이나 반복문 안에서 호출하면 안 됩니다) 클로저가
무엇을 캡쳐했는지는 아무도 들여다보지 않습니다.

기존 Blazor 스타일도 그대로 됩니다. `HookComponent` 대신 `ComponentBase`를 상속하고 필드에
`@bind-Value`를 쓰면 됩니다:

```razor
<MewTextBox @bind-Value="_name" />

@code {
    private string _name = "";
}
```

### 이펙트

타이머, 구독, 파일 감시처럼 한 번의 렌더보다 오래 사는 것은 `UseEffect`로 선언합니다. 렌더가
컨트롤 트리에 반영된 뒤에 실행되고, 반환한 액션이 정리(cleanup)입니다:

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

의존성을 주지 않으면 마운트 때 한 번만 실행되고, 컴포넌트가 트리에서 빠질 때 정리가 실행됩니다.
의존성을 주면 값이 바뀔 때 다시 실행되며, 그 전에 이전 정리가 먼저 돕니다:

```razor
UseEffect(() =>
{
    var subscription = feed.Subscribe(channel, setMessage);
    return subscription.Dispose;
}, channel);
```

이펙트 안에서 상태를 바꿔도 됩니다. 그 렌더는 평소대로 일어납니다. 그리고 `UseState`와 마찬가지로
슬롯이 호출 순서라, 이펙트도 조건문이나 반복문 안에서 선언하면 안 됩니다.

React와 다른 점이 하나 있습니다. 이펙트 안에서 파라미터를 읽으면 그건 컴포넌트에서 읽는 것이라,
정리가 실행될 시점에는 이미 **새 값**이 들어있습니다. 정리에 필요한 값은 본문에서 지역 변수로
받아두고(`var channel = Channel;`) 그걸 캡쳐하세요.

## 컨트롤

컴포넌트는 MewUI 메타데이터에서 생성되므로 골라 담은 게 아니라 라이브러리 전체가 덮입니다 —
`MewBorder`부터 `MewTreeView`까지 69개입니다.

생성된 컴포넌트는 MewUI의 클래스 계층을 그대로 미러링해서, 각 단계가 자기가 새로 도입한 멤버만
선언합니다:

```
MewButtonBase<T> : MewCommandSourceControlBase<T> : MewContentControlBase<T> : MewControlBase<T>
  : MewTextElementBase<T> : MewFrameworkElementBase<T> : MewUIElementBase<T> : MewElementBase<T>
```

public 설정 가능 프로퍼티는 파라미터가 되고, `Action` / `Action<T>` 이벤트는 `EventCallback`
파라미터가 됩니다 — `Button.Click`은 아무도 손으로 쓰지 않았는데 `OnClick`으로 쓸 수 있습니다.
모든 파라미터는 nullable이고 지정하지 않은 값은 대입하지 않으므로, 설정하지 않은 컴포넌트가 테마
위에 기본값을 덮어쓰지 않습니다.

생성된 컴포넌트는 전부 `partial`이고 `OnControlCreated`, `ApplyCustomParameters` 훅을 열어둡니다.
메타데이터로 표현할 수 없는 것 세 가지가 그 훅으로 들어가 있습니다 — 창 크기, `MewTextBox`의
`@bind-Value`, `MewButton`의 `Text` 축약.

## 동작 방식

Razor SDK는 `.razor`를 `RenderTreeBuilder` 호출로 컴파일합니다. 이 호출은 HTML 전용이 아닙니다.
태그가 `IComponent` 타입으로 해석되면 타입 있는 파라미터를 가진 `OpenComponent<T>()`가 되고,
그래서 MewRazor 뷰에는 HTML이 한 조각도 없습니다 — 모든 태그가 MewUI 컨트롤 래퍼입니다. 뷰에
HTML 엘리먼트를 쓰면 조용히 넘어가지 않고 명확한 에러가 납니다.

`MewRazorRenderer`는 `Microsoft.AspNetCore.Components.RenderTree.Renderer` 구현으로, 각
`RenderBatch`를 MewUI 엘리먼트 트리에 대한 삽입/제거로 옮깁니다. 그 위층 — 컴포넌트 수명주기,
파라미터, `RenderFragment`, `EventCallback`, diff 자체 — 는 전부 지원되는
`Microsoft.AspNetCore.Components` 패키지의 순정 Blazor입니다. 런타임에 ASP.NET Core에 의존하는
부분은 없습니다.

설계는 .NET MAUI에 같은 일을 하는
[BlazorBindings.Maui](https://github.com/Dreamescaper/BlazorBindings.Maui)를 따랐습니다.

## 아직 안 된 것

**컬렉션 컨트롤에 항목을 넣을 수 없습니다.** `ListBox`, `GridView`, `ComboBox`, `TreeView`,
`NavigationView`가 생성돼 있고 자동 완성에도 뜨지만, 항목 프로퍼티가 `IReadOnlyList<T>`라
생성 대상 파라미터 밖입니다. 컨테이너로만 동작합니다. 항목을 자식 컴포넌트로 표현하는 건 필터를
손보는 문제가 아니라 설계 문제입니다 — [#3](https://github.com/naratteu/mewrazor/issues/3).

그 외 작은 공백:

- 존재하지 않는 파라미터를 써도 컴파일은 통과하고 렌더링 시점에야 실패합니다. 이 라이브러리가
  더한 문제가 아니라 Blazor의 동작입니다
  ([#4](https://github.com/naratteu/mewrazor/issues/4)).
- `Margin="new Thickness(20)"`가 장황합니다. 축약형이 없습니다
  ([#5](https://github.com/naratteu/mewrazor/issues/5)).

이 라이브러리는 `Microsoft.AspNetCore.Components.RenderTree` 위에 서 있고, Microsoft는 여기에
`BL0006`을 붙여둡니다 — Blazor 바깥에서 쓰는 것을 권장하지 않으며 릴리스마다 바뀔 수 있다는
경고입니다. 그게 이 설계의 전제라 프로젝트 레벨에서 한 번 억제했지만, 패키지가
`Microsoft.AspNetCore.Components` 버전을 부동으로 두지 않고 고정하는 이유이기도 합니다.

## 라이선스

[VibeCoded AI-Slop License v1.0](LICENSE). 이 코드는 프롬프트로부터 AI가 작성했고, 라이선스는
그게 무슨 뜻인지 솔직하게 적고 있습니다.
