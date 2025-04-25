using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using TaskSwitcher.Core;

namespace TaskSwitcher
{
    public class AppWindowViewModel : INotifyPropertyChanged, IWindowText, IDisposable
    {
        public AppWindowViewModel(AppWindow appWindow)
        {
            AppWindow = appWindow;
        }

        public AppWindow AppWindow { get; private set; }

        #region IWindowText Members

        public string WindowTitle => AppWindow.Title;

        public string ProcessTitle => AppWindow.ProcessTitle;

        #endregion

        #region Bindable properties

        public IntPtr HWnd => AppWindow.HWnd;

        private string _formattedTitle;

        public string FormattedTitle
        {
            get => _formattedTitle;
            set
            {
                _formattedTitle = value;
                NotifyOfPropertyChange(() => FormattedTitle);
            }
        }

        private string _formattedProcessTitle;

        public string FormattedProcessTitle
        {
            get => _formattedProcessTitle;
            set
            {
                _formattedProcessTitle = value;
                NotifyOfPropertyChange(() => FormattedProcessTitle);
            }
        }

        private bool _isBeingClosed;

        public bool IsBeingClosed
        {
            get => _isBeingClosed;
            set
            {
                _isBeingClosed = value;
                NotifyOfPropertyChange(() => IsBeingClosed);
            }
        }

        #endregion

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyOfPropertyChange<T>(Expression<Func<T>> property)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(GetPropertyName(property)));
        }

        private string GetPropertyName<T>(Expression<Func<T>> property)
        {
            LambdaExpression lambda = (LambdaExpression) property;

            MemberExpression memberExpression;
            if (lambda.Body is UnaryExpression)
            {
                UnaryExpression unaryExpression = (UnaryExpression) lambda.Body;
                memberExpression = (MemberExpression) unaryExpression.Operand;
            }
            else
            {
                memberExpression = (MemberExpression) lambda.Body;
            }

            return memberExpression.Member.Name;
        }

        #endregion
        
        #region IDisposable Implementation

        private bool _disposed;

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

        ~AppWindowViewModel()
        {
            Dispose(false);
        }

        #endregion
    }
}