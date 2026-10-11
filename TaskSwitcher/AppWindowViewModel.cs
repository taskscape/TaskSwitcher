using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using TaskSwitcher.Core;

namespace TaskSwitcher
{
    public class AppWindowViewModel(AppWindow appWindow) : INotifyPropertyChanged, IWindowText, IDisposable
    {
        public AppWindow AppWindow { get; private set; } = appWindow;

        #region IWindowText Members

        // The texts are read once, when the view model is created on the loading thread.
        // Filtering then works on plain strings instead of querying a window that may be
        // busy or already gone for every keystroke.

        public string WindowTitle { get; } = appWindow.Title ?? string.Empty;

        public string ProcessTitle { get; } = appWindow.ProcessTitle ?? string.Empty;

        #endregion

        #region Bindable properties

        public IntPtr HWnd { get; } = appWindow.HWnd;

        private string _formattedTitle;

        public string FormattedTitle
        {
            get { return _formattedTitle; }
            set
            {
                // Every notification makes the row parse and lay out the text again.
                if (_formattedTitle == value) return;

                _formattedTitle = value;
                NotifyOfPropertyChange();
            }
        }

        private string _formattedProcessTitle;

        public string FormattedProcessTitle
        {
            get { return _formattedProcessTitle; }
            set
            {
                if (_formattedProcessTitle == value) return;

                _formattedProcessTitle = value;
                NotifyOfPropertyChange();
            }
        }

        private ImageSource _iconImage;

        /// <summary>
        /// The icon of the window, or null while it has not been loaded yet. The value must be
        /// frozen because it is assigned from a background thread.
        /// </summary>
        public ImageSource IconImage
        {
            get { return _iconImage; }
            set
            {
                _iconImage = value;
                NotifyOfPropertyChange();
            }
        }

        private bool _isBeingClosed = false;

        public bool IsBeingClosed
        {
            get { return _isBeingClosed; }
            set
            {
                _isBeingClosed = value;
                NotifyOfPropertyChange();
            }
        }

        #endregion

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyOfPropertyChange([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
        
        #region IDisposable Implementation

        private bool _disposed = false;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                // Dispose managed resources
                PropertyChanged = null;
                
                // Clean up references that might contain circular dependencies
                AppWindow = null;
            }

            _disposed = true;
        }

        #endregion
    }
}
