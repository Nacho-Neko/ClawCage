using ClawCage.WinUI.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;

namespace ClawCage.WinUI.Pages
{
    public sealed partial class InitialDeployPage : Page
    {
        public InitialDeployViewModel ViewModel { get; } = new();

        public InitialDeployPage()
        {
            InitializeComponent();
            InitializeViewModel();
        }

        private void InitializeViewModel()
        {
            ViewModel.NavigateToSetup = () => Frame.Navigate(typeof(SetupPage));
            ViewModel.NavigateToDeploy = () => Frame.Navigate(typeof(DeployPage));
            ViewModel.ShowContentDialog = async (title, content) =>
            {
                var dialog = new ContentDialog
                {
                    Title = title,
                    Content = content,
                    CloseButtonText = "确定",
                    XamlRoot = XamlRoot
                };
                await dialog.ShowAsync();
            };
            ViewModel.RequestDeleteConfirmation = async (content, title) =>
            {
                var confirm = new ContentDialog
                {
                    Title = title,
                    Content = content,
                    PrimaryButtonText = "删除",
                    CloseButtonText = "取消",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = XamlRoot
                };
                return await confirm.ShowAsync() == ContentDialogResult.Primary;
            };
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _ = ViewModel.InitializeAsync();
        }

        private void NodeVersionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ViewModel.OnNodeVersionSelectionChanged();
        }
    }
}

