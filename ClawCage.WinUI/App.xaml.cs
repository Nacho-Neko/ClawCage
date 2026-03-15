using ClawCage.WinUI.Services.OpenClaw;
using ClawCage.WinUI.ViewModels;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;

namespace ClawCage.WinUI
{
    public partial class App : Application
    {
        public static Window? MainWindow { get; private set; }

        public IHost Host { get; }

        public App()
        {
            InitializeComponent();

            Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    // Services — singleton
                    services.AddSingleton<OpenClawConfigService>();
                    services.AddSingleton<OpenClawPluginService>();

                    // ViewModels
                    services.AddTransient(sp =>
                        new OverviewPageViewModel(sp.GetRequiredService<OpenClawConfigService>()));

                })
                .Build();

            Ioc.Default.ConfigureServices(Host.Services);
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            MainWindow = new MainWindow();
            MainWindow.Activate();
        }
    }
}
