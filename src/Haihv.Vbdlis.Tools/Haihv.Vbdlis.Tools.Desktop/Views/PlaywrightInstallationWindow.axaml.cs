using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Haihv.Vbdlis.Tools.Desktop.ViewModels;
using System;
using System.Threading.Tasks;

namespace Haihv.Vbdlis.Tools.Desktop.Views
{
    public partial class PlaywrightInstallationWindow : Window
    {
        /// <summary>
        /// Event raised when user requests retry
        /// </summary>
        public event EventHandler? RetryRequested;

        /// <summary>
        /// Event raised when user requests to exit application
        /// </summary>
        public event EventHandler? ExitRequested;

        public PlaywrightInstallationWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            // Subscribe to ViewModel events when DataContext changes
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object? sender, EventArgs e)
        {
            // Unsubscribe from old ViewModel if any
            if (sender is PlaywrightInstallationWindow window && window.ViewModel != null)
            {
                window.ViewModel.RetryRequested += OnViewModelRetryRequested;
                window.ViewModel.ExitRequested += OnViewModelExitRequested;
            }
        }

        private void OnViewModelRetryRequested(object? sender, EventArgs e)
        {
            RetryRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnViewModelExitRequested(object? sender, EventArgs e)
        {
            ExitRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Gets the ViewModel for this window
        /// </summary>
        public PlaywrightInstallationViewModel? ViewModel => DataContext as PlaywrightInstallationViewModel;

        /// <summary>
        /// Auto-closes the window after a delay when installation is complete
        /// </summary>
        public async Task AutoCloseAfterDelayAsync(int delayMilliseconds = 3000)
        {
            await Task.Delay(delayMilliseconds);

            // Use dispatcher to close on UI thread
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Close();
            });
        }

        /// <summary>
        /// Updates the status message on the UI thread
        /// </summary>
        public void UpdateStatus(string message, int? progress = null, bool? isIndeterminate = null)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                ViewModel?.UpdateStatus(message, progress, isIndeterminate);
            });
        }

        /// <summary>
        /// Marks installation as started on the UI thread
        /// </summary>
        public void StartInstallation()
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                ViewModel?.StartInstallation();
            });
        }

        /// <summary>
        /// Marks installation as completed on the UI thread
        /// </summary>
        public void CompleteInstallation()
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                ViewModel?.CompleteInstallation();
            });
        }

        /// <summary>
        /// Sets error state on the UI thread
        /// </summary>
        public void SetError(string errorMessage)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                ViewModel?.SetError(errorMessage);
            });
        }

        /// <summary>
        /// Marks that browsers are already installed on the UI thread
        /// </summary>
        public void SetAlreadyInstalled()
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                ViewModel?.SetAlreadyInstalled();
            });
        }
    }
}
