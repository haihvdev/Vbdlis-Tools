using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace Haihv.Vbdlis.Tools.Desktop.App
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow
                {
                    Title = "Các công cụ làm việc với VBDLIS",
                    MinHeight = 800,
                    MinWidth = 1200,
                    WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterScreen,
                };
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}