using Microsoft.Maui.Platform;
using Microsoft.Playwright;
using System.Diagnostics;

namespace Haihv.Vbdlis.Tools.Desktop
{
    public partial class MainPage : ContentPage
    {
        private IPlaywright? playwright;
        int count = 0;

        public MainPage()
        {
            InitializeComponent();
        }

        private void OnCounterClicked(object sender, EventArgs e)
        {
            count++;

            if (count == 1)
                CounterBtn.Text = $"Clicked {count} time";
            else
                CounterBtn.Text = $"Clicked {count} times";

            SemanticScreenReader.Announce(CounterBtn.Text);
        }
        public async Task AutomateWeb()
        {
            playwright = await Playwright.CreateAsync();
            var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,
            });
            var page = await browser.NewPageAsync();
            await page.GotoAsync("https://bgi.mplis.gov.vn/dc/home/index"); ;

            if (page.Url.Contains("authen.mplis.gov.vn/account/login"))
            {
                // Điền thông tin đăng nhập (thay thế bằng thông tin thực tế của bạn)
                await page.FillAsync("input[name='username']", "haihv849");
                await page.FillAsync("input[name='password']", "A@Rht5f6PYJFtmx");
                await page.ClickAsync("button[type='submit']");
                await Task.Delay(1000); // Chờ một chút để trang tải sau khi đăng nhập
            }
            await page.WaitForRequestFinishedAsync();

            // Hiển thị lại trình duyệt:
            browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = false,
            });

            var title = await page.TitleAsync();
            Debug.WriteLine($"Title: {title}");
            // Dừng trình duyệt để người dùng có thể xem kết quả
            // await Task.Delay(5000);
            // await browser.CloseAsync();
        }
        private async void OnStartWebClicked(object sender, EventArgs e)
        {
            await AutomateWeb();
        }
    }

}
