using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;

namespace Haihv.Vbdlis.Tools.Desktop.ViewModels
{
    /// <summary>
    /// ViewModel for the Playwright browser installation progress window
    /// </summary>
    public partial class PlaywrightInstallationViewModel : ViewModelBase
    {
        [ObservableProperty]
        private string _statusMessage = "Đang kiểm tra Playwright browsers...";

        [ObservableProperty]
        private string _operatingSystem = string.Empty;

        [ObservableProperty]
        private bool _isInstalling = false;

        [ObservableProperty]
        private bool _isCompleted = false;

        [ObservableProperty]
        private bool _hasError = false;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private int _progressValue = 0;

        [ObservableProperty]
        private bool _isIndeterminate = true;

        /// <summary>
        /// Event raised when user clicks Retry button
        /// </summary>
        public event EventHandler? RetryRequested;

        /// <summary>
        /// Event raised when user clicks Exit button
        /// </summary>
        public event EventHandler? ExitRequested;

        /// <summary>
        /// Updates the status message and optionally the progress
        /// </summary>
        public void UpdateStatus(string message, int? progress = null, bool? isIndeterminate = null)
        {
            StatusMessage = message;

            if (progress.HasValue)
            {
                ProgressValue = progress.Value;
            }

            if (isIndeterminate.HasValue)
            {
                IsIndeterminate = isIndeterminate.Value;
            }

            // Auto-detect progress percentage from message
            if (message.Contains("%"))
            {
                var match = System.Text.RegularExpressions.Regex.Match(message, @"(\d+)%");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int percent))
                {
                    ProgressValue = percent;
                    IsIndeterminate = false;
                }
            }
        }

        /// <summary>
        /// Marks the installation as started
        /// </summary>
        public void StartInstallation()
        {
            IsInstalling = true;
            HasError = false;
            IsCompleted = false;
            StatusMessage = "Đang tải xuống và cài đặt Chromium browser...";
        }

        /// <summary>
        /// Marks the installation as completed successfully
        /// </summary>
        public void CompleteInstallation()
        {
            IsInstalling = false;
            IsCompleted = true;
            HasError = false;
            IsIndeterminate = false;
            ProgressValue = 100;
            StatusMessage = "Cài đặt Playwright hoàn tất!";
        }

        /// <summary>
        /// Marks the installation as failed with an error message
        /// </summary>
        public void SetError(string errorMessage)
        {
            IsInstalling = false;
            IsCompleted = false;
            HasError = true;
            ErrorMessage = errorMessage;
            StatusMessage = "Lỗi khi cài đặt Playwright";
        }

        /// <summary>
        /// Marks that browsers are already installed
        /// </summary>
        public void SetAlreadyInstalled()
        {
            IsInstalling = false;
            IsCompleted = true;
            HasError = false;
            IsIndeterminate = false;
            ProgressValue = 100;
            StatusMessage = "Playwright browsers đã được cài đặt sẵn";
        }

        /// <summary>
        /// Command to retry installation
        /// </summary>
        [RelayCommand]
        private void Retry()
        {
            RetryRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Command to exit application
        /// </summary>
        [RelayCommand]
        private void Exit()
        {
            ExitRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
