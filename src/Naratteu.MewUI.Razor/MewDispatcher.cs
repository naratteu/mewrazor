using System.Collections.Concurrent;
using Aprillz.MewUI;
using Microsoft.AspNetCore.Components;

namespace Naratteu.MewUI.Razor;

/// <summary>The UI thread, reduced to what the renderer needs from it.</summary>
internal interface IUiThread
{
    bool IsCurrent { get; }

    void Post(Action work);
}

internal sealed class MewUiThread(IDispatcher dispatcher) : IUiThread
{
    public bool IsCurrent => dispatcher.IsOnUIThread;

    public void Post(Action work) => dispatcher.BeginInvoke(work);

    /// <summary>MewUI's UI thread, once the application is running; until then, nothing.</summary>
    public static IUiThread? Resolve()
        => Application.IsRunning && Application.Current.Dispatcher is { } dispatcher
            ? new MewUiThread(dispatcher)
            : null;
}

/// <summary>Bridges Blazor's <see cref="Dispatcher"/> onto the MewUI UI thread.</summary>
/// <remarks>
/// The main-window factory builds the renderer and mounts the tree on the UI thread, but before
/// MewUI has a dispatcher -- so an effect's first background result can arrive with nothing to
/// post it to. Until the dispatcher exists, the thread that built the renderer stands for the UI
/// thread, and work from any other thread waits in order instead of running where it was posted.
/// </remarks>
internal sealed class MewDispatcher(Func<IUiThread?> resolve) : Dispatcher
{
    private readonly int _creatingThread = Environment.CurrentManagedThreadId;
    private readonly ConcurrentQueue<Action> _early = new();
    private IUiThread? _ui;
    private int _draining;

    private IUiThread? Ui => _ui ??= resolve();

    public override bool CheckAccess()
        => Ui is { } ui ? ui.IsCurrent : Environment.CurrentManagedThreadId == _creatingThread;

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
        PostToUi(async void () =>
        {
            try { completion.SetResult(await work().ConfigureAwait(true)); }
            catch (Exception ex) { completion.SetException(ex); }
        });
        return completion.Task;
    }

    private void PostToUi(Action work)
    {
        // While anything that arrived before the dispatcher is still waiting, later work queues
        // behind it, so two updates from one thread cannot land in the wrong order.
        if (Ui is { } ui && Volatile.Read(ref _draining) == 0 && _early.IsEmpty)
        {
            ui.Post(work);
            return;
        }

        _early.Enqueue(work);
        if (Interlocked.CompareExchange(ref _draining, 1, 0) == 0) _ = DrainWhenReadyAsync();
    }

    private async Task DrainWhenReadyAsync()
    {
        while (true)
        {
            IUiThread? ui;
            while ((ui = Ui) is null) await Task.Delay(5).ConfigureAwait(false);

            while (_early.TryDequeue(out var work)) ui.Post(work);
            Volatile.Write(ref _draining, 0);

            // Something may have queued between the last dequeue and the reset.
            if (_early.IsEmpty || Interlocked.CompareExchange(ref _draining, 1, 0) != 0) return;
        }
    }
}
