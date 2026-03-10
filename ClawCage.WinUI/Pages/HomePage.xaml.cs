using ClawCage.WinUI.Components;
using Microsoft.UI.Xaml.Controls;

namespace ClawCage.WinUI.Pages
{
    public sealed partial class HomePage : Page
    {
        public HomePage()
        {
            InitializeComponent();
            InitializeNavigationMenu();
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            var tag = args.SelectedItemContainer?.Tag?.ToString();
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

            if (NavView.MenuItems.Count > 0)
                NavView.SelectedItem = NavView.MenuItems[0];
        }
    }
}
