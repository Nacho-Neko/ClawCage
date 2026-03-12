using ClawCage.WinUI.Services.OpenClaw;
using ClawCage.WinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace ClawCage.WinUI.Pages
{
    public sealed partial class OverviewPage : Page
    {
        public OverviewPageViewModel ViewModel { get; } = new();

        public OverviewPage()
        {
            InitializeComponent();
            Loaded += OverviewPage_Loaded;
            Unloaded += OverviewPage_Unloaded;
        }

        private async void OverviewPage_Loaded(object sender, RoutedEventArgs e)
        {
            ViewModel.Initialize(XamlRoot);
            OpenClawConfigService.ConfigChanged -= OnConfigChanged;
            OpenClawConfigService.ConfigChanged += OnConfigChanged;
            OpenClawWatcher.RunningStateChanged -= OnRunningStateChanged;
            OpenClawWatcher.RunningStateChanged += OnRunningStateChanged;
            await ViewModel.RefreshCommand.ExecuteAsync(null);
        }

        private void OverviewPage_Unloaded(object sender, RoutedEventArgs e)
        {
            OpenClawConfigService.ConfigChanged -= OnConfigChanged;
            OpenClawWatcher.RunningStateChanged -= OnRunningStateChanged;
        }

        private void OnConfigChanged(object? sender, EventArgs e) =>
            _ = DispatcherQueue.TryEnqueue(async () => await ViewModel.RefreshCommand.ExecuteAsync(null));

        private void OnRunningStateChanged(object? sender, EventArgs e) =>
            _ = DispatcherQueue.TryEnqueue(async () => await ViewModel.RefreshCommand.ExecuteAsync(null));
    }
}
