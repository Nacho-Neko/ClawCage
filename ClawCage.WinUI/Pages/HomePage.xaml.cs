using ClawCage.WinUI.Components;
using ClawCage.WinUI.Services.OpenClaw;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Linq;

namespace ClawCage.WinUI.Pages
{
    public sealed partial class HomePage : Page
    {
        private readonly OpenClawConfigService _configService = Ioc.Default.GetRequiredService<OpenClawConfigService>();
        private readonly OpenClawPluginService _pluginService = Ioc.Default.GetRequiredService<OpenClawPluginService>();

        public HomePage()
        {
            InitializeComponent();
            Loaded += HomePage_Loaded;
            Unloaded += HomePage_Unloaded;
            ContentFrame.Navigated += ContentFrame_Navigated;
            InitializeNavigationMenu();
        }

        private async void HomePage_Loaded(object sender, RoutedEventArgs e)
        {
            _configService.Initialize();

            _configService.ConfigChanged -= OnConfigChanged;
            _configService.ConfigChanged += OnConfigChanged;

            await _pluginService.LoadAsync();

            UpdateNavigationEnabled();
        }

        private void HomePage_Unloaded(object sender, RoutedEventArgs e)
        {
            _configService.ConfigChanged -= OnConfigChanged;
        }

        private void OnConfigChanged(object? sender, EventArgs e)
        {
            _ = DispatcherQueue.TryEnqueue(UpdateNavigationEnabled);
        }

        private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
        {
            UpdateNavigationEnabled();
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            var tag = args.SelectedItemContainer?.Tag?.ToString();

            if (tag is not null
                && HomeNavigationMenu.RequiresInitializationTags.Contains(tag)
                && !_configService.IsInitialized())
            {
                var overviewItem = NavView.MenuItems
                    .OfType<NavigationViewItem>()
                    .FirstOrDefault(item => string.Equals(item.Tag?.ToString(), HomeNavigationMenu.OverviewTag, StringComparison.Ordinal));

                if (overviewItem is not null)
                    NavView.SelectedItem = overviewItem;

                return;
            }

            var targetPage = HomeNavigationMenu.ResolvePageType(tag);
            if (targetPage is not null && ContentFrame.CurrentSourcePageType != targetPage)
                ContentFrame.Navigate(targetPage);
        }

        private void InitializeNavigationMenu()
        {
            NavView.MenuItems.Clear();
            foreach (var item in HomeNavigationMenu.CreateMenuItems())
                NavView.MenuItems.Add(item);

            NavView.FooterMenuItems.Clear();
            foreach (var item in HomeNavigationMenu.CreateFooterMenuItems())
                NavView.FooterMenuItems.Add(item);

            UpdateNavigationEnabled();

            if (NavView.MenuItems.Count > 0)
                NavView.SelectedItem = NavView.MenuItems[0];
        }

        private void UpdateNavigationEnabled()
        {
            var isInitialized = _configService.IsInitialized();

            var needsFallback = false;

            foreach (var item in NavView.MenuItems.OfType<NavigationViewItem>())
            {
                var tag = item.Tag?.ToString();
                if (tag is not null && HomeNavigationMenu.RequiresInitializationTags.Contains(tag))
                {
                    item.IsEnabled = isInitialized;

                    if (!isInitialized && ReferenceEquals(NavView.SelectedItem, item))
                        needsFallback = true;
                }
            }

            if (needsFallback)
            {
                var overviewItem = NavView.MenuItems
                    .OfType<NavigationViewItem>()
                    .FirstOrDefault(item => string.Equals(item.Tag?.ToString(), HomeNavigationMenu.OverviewTag, StringComparison.Ordinal));

                if (overviewItem is not null)
                    NavView.SelectedItem = overviewItem;
            }
        }
    }
}
