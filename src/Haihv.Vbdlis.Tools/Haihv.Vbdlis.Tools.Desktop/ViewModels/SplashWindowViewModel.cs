using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Reflection;

namespace Haihv.Vbdlis.Tools.Desktop.ViewModels
{
    /// <summary>
    /// ViewModel for the application splash screen
    /// </summary>
    public partial class SplashWindowViewModel : ViewModelBase
    {
        [ObservableProperty]
        private string _statusMessage = "Đang khởi động ứng dụng...";

        [ObservableProperty]
        private string _version = string.Empty;
        [ObservableProperty]
        private string _copyright = "© 2025 vpdkbacninh.vn | haihv.vn";

        [ObservableProperty]
        private double _progressWidth = 0;

        private const double MaxProgressWidth = 480; // Max width for progress bar (600 - 60*2 margins)

        public SplashWindowViewModel()
        {
            // Get application version
            var version = MainWindowViewModel.AppVersion;
            Version = string.IsNullOrWhiteSpace(version) ? "Phiên bản 1.0.0" : $"Phiên bản {version}";
            // Get copyright
            var copyright = MainWindowViewModel.CopyrightText;
            Copyright = string.IsNullOrWhiteSpace(copyright) ? "© 2025 vpdkbacninh.vn | haihv.vn" : copyright;
        }

        /// <summary>
        /// Updates the status message and progress
        /// </summary>
        /// <param name="message">Status message to display</param>
        /// <param name="progress">Progress percentage (0-100)</param>
        public void UpdateStatus(string message, int progress)
        {
            StatusMessage = message;
            ProgressWidth = Math.Min(MaxProgressWidth, progress / 100.0 * MaxProgressWidth);
        }

        /// <summary>
        /// Sets status to checking for updates
        /// </summary>
        public void SetCheckingUpdates()
        {
            UpdateStatus("Đang kiểm tra cập nhật...", 10);
        }

        /// <summary>
        /// Sets status to downloading update
        /// </summary>
        public void SetDownloadingUpdate(int downloadProgress)
        {
            UpdateStatus($"Đang tải bản cập nhật... {downloadProgress}%", 10 + (downloadProgress / 2));
        }

        /// <summary>
        /// Sets status to checking Playwright
        /// </summary>
        public void SetCheckingPlaywright()
        {
            UpdateStatus("Đang kiểm tra Playwright browsers...", 60);
        }

        /// <summary>
        /// Sets status to installing Playwright
        /// </summary>
        public void SetInstallingPlaywright(int installProgress)
        {
            UpdateStatus($"Đang cài đặt Playwright browsers... {installProgress}%", 60 + (installProgress / 4));
        }

        /// <summary>
        /// Sets status to initializing main window
        /// </summary>
        public void SetInitializingMainWindow()
        {
            UpdateStatus("Đang khởi tạo cửa sổ chính...", 85);
        }

        /// <summary>
        /// Sets status to loading data
        /// </summary>
        public void SetLoadingData()
        {
            UpdateStatus("Đang tải dữ liệu...", 95);
        }

        /// <summary>
        /// Sets status to complete
        /// </summary>
        public void SetComplete()
        {
            UpdateStatus("Hoàn tất!", 100);
        }
    }
}
