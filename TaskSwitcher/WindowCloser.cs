using System;
using System.Threading.Tasks;
using TaskSwitcher.Core;

namespace TaskSwitcher
{
    public class WindowCloser : IDisposable
    {
        private bool _isDisposed;

        private static readonly TimeSpan CheckInterval = TimeSpan.FromMilliseconds(125);

        public async Task<bool> TryCloseAsync(AppWindowViewModel window)
        {
            window.IsBeingClosed = true;
            window.AppWindow.SendClose();

            while (!_isDisposed && !IsClosedOrHidden(window.AppWindow))
                await Task.Delay(CheckInterval).ConfigureAwait(false);

            return IsClosedOrHidden(window.AppWindow);
        }

        private bool IsClosedOrHidden(AppWindow appWindow)
        {
            // Assuming IsClosed and IsHidden are properties or methods of AppWindow
            return appWindow.IsValid() || !appWindow.Visible;
        }

        public void Dispose()
        {
            _isDisposed = true;
        }
    }
}