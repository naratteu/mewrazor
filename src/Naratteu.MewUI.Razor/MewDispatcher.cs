using Microsoft.AspNetCore.Components;
using MewIDispatcher = Aprillz.MewUI.IDispatcher;

namespace Naratteu.MewUI.Razor;

/// <summary>Bridges Blazor's <see cref="Dispatcher"/> onto the MewUI UI thread.</summary>
/// <remarks>
/// The MewUI dispatcher is resolved lazily: the main-window factory runs before
/// <c>Application.Current</c> exists, and that call is already on the UI thread.
/// </remarks>
internal sealed class MewDispatcher(Func<MewIDispatcher?> resolve) : Dispatcher
{
    public override bool CheckAccess() => resolve() is not { } inner || inner.IsOnUIThread;

    public override Task InvokeAsync(Action workItem) => Post(() =>
    {
        workItem();
        return Task.CompletedTask;
    });

    public override Task InvokeAsync(Func<Task> workItem) => Post(workItem);

    public override Task<TResult> InvokeAsync<TResult>(Func<TResult> workItem)
        => Post(() => Task.FromResult(workItem()));

    public override Task<TResult> InvokeAsync<TResult>(Func<Task<TResult>> workItem) => Post(workItem);

    private Task Post(Func<Task> work) => Post<object?>(async () =>
    {
        await work().ConfigureAwait(false);
        return null;
    });

    private Task<T> Post<T>(Func<Task<T>> work)
    {
        if (CheckAccess())
        {
            try { return work(); }
            catch (Exception ex) { return Task.FromException<T>(ex); }
        }

        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        resolve()!.BeginInvoke(async void () =>
        {
            try { completion.SetResult(await work().ConfigureAwait(true)); }
            catch (Exception ex) { completion.SetException(ex); }
        });
        return completion.Task;
    }
}
