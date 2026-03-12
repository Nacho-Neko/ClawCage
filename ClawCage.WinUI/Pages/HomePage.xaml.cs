using ClawCage.WinUI.Components;
using ClawCage.WinUI.Services.OpenClaw;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;

namespace ClawCage.WinUI.Pages
{
    public sealed partial class HomePage : Page
    {
        public HomePage()
        {
            InitializeComponent();
            Loaded += HomePage_Loaded;
            Unloaded += HomePage_Unloaded;
            InitializeNavigationMenu();
        }

        private void HomePage_Loaded(object sender, RoutedEventArgs e)
        {
            OpenClawConfigService.ConfigChanged -= OnConfigChanged;
            OpenClawConfigService.ConfigChanged += OnConfigChanged;
            UpdateModelNavigationEnabled();
        }

        private void HomePage_Unloaded(object sender, RoutedEventArgs e)
        {
            OpenClawConfigService.ConfigChanged -= OnConfigChanged;
        }

        private void OnConfigChanged(object? sender, EventArgs e)
        {
            _ = DispatcherQueue.TryEnqueue(UpdateModelNavigationEnabled);
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            var tag = args.SelectedItemContainer?.Tag?.ToString();

            if (tag == HomeNavigationMenu.ModelAccessTag && !OpenClawConfigService.IsInitialized())
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

            UpdateModelNavigationEnabled();

            if (NavView.MenuItems.Count > 0)
                NavView.SelectedItem = NavView.MenuItems[0];
        }

        private void UpdateModelNavigationEnabled()
        {
            var modelItem = NavView.MenuItems
                .OfType<NavigationViewItem>()
                .FirstOrDefault(item => string.Equals(item.Tag?.ToString(), HomeNavigationMenu.ModelAccessTag, StringComparison.Ordinal));

            if (modelItem is null)
                return;

            var isInitialized = OpenClawConfigService.IsInitialized();
            modelItem.IsEnabled = isInitialized;

            if (!isInitialized && ReferenceEquals(NavView.SelectedItem, modelItem))
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
