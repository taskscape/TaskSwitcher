﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using TaskSwitcher.Core;
using TaskSwitcher.Core.Browsers;

namespace TaskSwitcher
{
    public class AppWindowViewModel : INotifyPropertyChanged, IWindowText, IDisposable
    {
        public AppWindowViewModel(AppWindow appWindow)
        {
            AppWindow = appWindow;
            IsChromeTab = appWindow is ChromeTabWindow;
        }

        public AppWindow AppWindow { get; private set; }
        
        /// <summary>
        /// Whether this window represents a Chrome tab
        /// </summary>
        public bool IsChromeTab { get; }

        #region IWindowText Members

        public string WindowTitle
        {
            get 
            {
                // If this is a Chrome tab, use its DisplayTitle property instead of Title
                if (IsChromeTab && AppWindow is ChromeTabWindow chromeTab)
                {
                    return chromeTab.DisplayTitle;
                }
                return AppWindow.Title;
            }
        }

        public string ProcessTitle
        {
            get { return AppWindow.ProcessTitle; }
        }

        #endregion

        #region Bindable properties

        public IntPtr HWnd
        {
            get { return AppWindow.HWnd; }
        }

        private string _formattedTitle;

        public string FormattedTitle
        {
            get { return _formattedTitle; }
            set
            {
                _formattedTitle = value;
                NotifyOfPropertyChange(() => FormattedTitle);
            }
        }

        private string _formattedProcessTitle;

        public string FormattedProcessTitle
        {
            get { return _formattedProcessTitle; }
            set
            {
                _formattedProcessTitle = value;
                NotifyOfPropertyChange(() => FormattedProcessTitle);
            }
        }

        private bool _isBeingClosed = false;

        public bool IsBeingClosed
        {
            get { return _isBeingClosed; }
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

        ~AppWindowViewModel()
        {
            Dispose(false);
        }

        #endregion
    }
}