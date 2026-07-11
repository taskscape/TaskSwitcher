using System;
using System.Threading;
using System.Threading.Tasks;

namespace TaskSwitcher
{
    public class WindowCloser
    {
        private static readonly TimeSpan CheckInterval = TimeSpan.FromMilliseconds(125);

        public async Task<bool> TryCloseAsync(
            AppWindowViewModel window,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(window);
            if (timeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(timeout), "The close timeout must be greater than zero.");
            }

            var appWindow = window.AppWindow;

            using CancellationTokenSource timeoutSource =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(timeout);

            try
            {
                appWindow.Close();

                while (!appWindow.IsClosedOrHidden)
                {
                    await Task.Delay(CheckInterval, timeoutSource.Token);
                }

                return true;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // The target ignored WM_CLOSE (for example, while showing an unsaved-work prompt).
                return false;
            }

        }
    }
}
