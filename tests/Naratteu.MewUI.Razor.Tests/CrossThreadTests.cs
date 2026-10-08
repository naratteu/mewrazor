using System.Collections.Concurrent;
using Aprillz.MewUI.Controls;

namespace Naratteu.MewUI.Razor.Tests;

/// <summary>
/// Stands in for MewUI's dispatcher: work posted to it waits in a queue until the test, acting as
/// the UI thread, pumps it.
/// </summary>
internal sealed class QueueDispatcher : IUiThread
{
    private readonly int _uiThread = Environment.CurrentManagedThreadId;
    private readonly ConcurrentQueue<Action> _queue = new();

    public bool IsCurrent => Environment.CurrentManagedThreadId == _uiThread;

    public int Pending => _queue.Count;

    public void Post(Action work) => _queue.Enqueue(work);

    public int Pump()
    {
        var ran = 0;
        while (_queue.TryDequeue(out var action))
        {
            action();
            ran++;
        }

        return ran;
    }
}

/// <summary>
/// A hook's setter is a closure, and a closure ends up wherever an await resumes -- often a pool
/// thread. React has one thread, so its setters never had to care; this one has to.
/// </summary>
public class CrossThreadTests
{
    private static string Text(StackPanel panel) => ((TextBlock)panel.Children[0]).Text;

    /// <summary>
    /// A dedicated thread, not Task.Run: waiting on a task that has not started yet may run it
    /// inline on the waiting thread, which here is the UI thread.
    /// </summary>
    private static void OnOtherThread(Action work)
    {
        var thread = new Thread(() => work());
        thread.Start();
        thread.Join();
    }

    [Fact]
    public void ASetterCalledOffTheUiThreadIsMarshaledNotRunInPlace()
    {
        var ui = new QueueDispatcher();
        var panel = new MewRazorRenderer(() => ui).Mount<CrossThreadHarness, StackPanel>();

        OnOtherThread(() => CrossThreadHarness.Set("from a pool thread"));

        Assert.Equal("start", Text(panel));
        Assert.Equal(1, ui.Pump());
        Assert.Equal("from a pool thread", Text(panel));
    }

    [Fact]
    public void WorkArrivingBeforeTheUiLoopExistsWaitsForIt()
    {
        // The main-window factory mounts the tree before MewUI has a dispatcher, so an effect's
        // first background result can arrive while there is nothing to post it to.
        QueueDispatcher? ui = null;
        var panel = new MewRazorRenderer(() => ui).Mount<CrossThreadHarness, StackPanel>();

        OnOtherThread(() => CrossThreadHarness.Set("early"));
        Thread.Sleep(50);
        Assert.Equal("start", Text(panel));

        ui = new QueueDispatcher();
        Assert.True(SpinWait.SpinUntil(() => ui.Pending > 0, TimeSpan.FromSeconds(2)));

        ui.Pump();
        Assert.Equal("early", Text(panel));
    }
}
