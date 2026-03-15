using ClawCage.WinUI.Services.OpenClaw;
using ClawCage.WinUI.ViewModels;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace ClawCage.WinUI.Pages
{
    public sealed partial class OverviewPage : Page
    {
        public OverviewPageViewModel ViewModel { get; } = Ioc.Default.GetRequiredService<OverviewPageViewModel>();

        private readonly OpenClawConfigService _configService = Ioc.Default.GetRequiredService<OpenClawConfigService>();

        public OverviewPage()
        {
            InitializeComponent();
            Loaded += OverviewPage_Loaded;
            Unloaded += OverviewPage_Unloaded;
        }

        private async void OverviewPage_Loaded(object sender, RoutedEventArgs e)
        {
            ViewModel.Initialize(XamlRoot);
            _configService.ConfigChanged -= OnConfigChanged;
            _configService.ConfigChanged += OnConfigChanged;
            OpenClawWatcher.RunningStateChanged -= OnRunningStateChanged;
            OpenClawWatcher.RunningStateChanged += OnRunningStateChanged;
            await ViewModel.RefreshCommand.ExecuteAsync(null);
        }

        private void OverviewPage_Unloaded(object sender, RoutedEventArgs e)
        {
            _configService.ConfigChanged -= OnConfigChanged;
            OpenClawWatcher.RunningStateChanged -= OnRunningStateChanged;
        }

        private void OnConfigChanged(object? sender, EventArgs e) =>
            _ = DispatcherQueue.TryEnqueue(async () => await ViewModel.RefreshCommand.ExecuteAsync(null));

        private void OnRunningStateChanged(object? sender, EventArgs e) =>
            _ = DispatcherQueue.TryEnqueue(async () => await ViewModel.RefreshCommand.ExecuteAsync(null));
    }
}
