
using System.Windows.Threading;

namespace RfLab.ViewModels;

/// <summary>
/// Repeats the panel's periodic detector read.
/// </summary>
/// <remarks>
/// The abstraction keeps the view model testable without a dispatcher.
/// </remarks>
public interface IRfLabPanelScheduler
{
    IDisposable Schedule(TimeSpan interval, Func<Task> operation);

    /// <summary>
    /// Waits for the detector to settle between two analysed steps.
    /// </summary>
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default);
}

/// <summary>
/// Repeats the periodic read on the user-interface thread.
/// </summary>
public sealed class RfLabPanelScheduler : IRfLabPanelScheduler
{
    /// <inheritdoc />
    public Task DelayAsync(
        TimeSpan delay,
        CancellationToken cancellationToken = default) =>
        Task.Delay(delay, cancellationToken);

    public IDisposable Schedule(TimeSpan interval, Func<Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var timer = new DispatcherTimer
        {
            Interval = interval
        };
        var subscription = new TimerSubscription(timer);

        timer.Tick += async (_, _) =>
        {
            if (subscription.IsBusy)
            {
                return;
            }

            subscription.IsBusy = true;
            try
            {
                await operation().ConfigureAwait(true);
            }
            finally
            {
                subscription.IsBusy = false;
            }
        };
        timer.Start();

        return subscription;
    }

    private sealed class TimerSubscription(DispatcherTimer timer) : IDisposable
    {
        public bool IsBusy { get; set; }

        public void Dispose() => timer.Stop();
    }
}
