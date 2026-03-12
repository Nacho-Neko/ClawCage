using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;

namespace ClawCage.WinUI.Components
{
    public sealed partial class AddModelWizardChooseStep : UserControl
    {
        public AddModelWizardChooseStep()
        {
            InitializeComponent();
        }

        internal void ShowOnlyNewProviders()
        {
            ToggleRow.Visibility = Visibility.Collapsed;
            UseNewToggle.IsChecked = true;
        }

        internal void SetExistingProviders(IReadOnlyList<AddModelWizardDialog.ProviderTemplate> items)
        {
            ExistingProviderGrid.ItemsSource = items;
        }

        internal void SetNewProviders(IReadOnlyList<AddModelWizardDialog.ProviderTemplate> items)
        {
            NewProviderGrid.ItemsSource = items;
        }

        internal bool IsUsingExisting => UseExistingToggle.IsChecked == true;

        internal AddModelWizardDialog.ProviderTemplate? GetSelectedProvider()
        {
            return IsUsingExisting
                ? ExistingProviderGrid.SelectedItem as AddModelWizardDialog.ProviderTemplate
                : NewProviderGrid.SelectedItem as AddModelWizardDialog.ProviderTemplate;
        }

        private void UseExistingToggle_Checked(object sender, RoutedEventArgs e)
        {
            if (ExistingProviderGrid is null || NewProviderGrid is null) return;
            ExistingProviderGrid.Visibility = Visibility.Visible;
            NewProviderGrid.Visibility = Visibility.Collapsed;
        }

        private void UseNewToggle_Checked(object sender, RoutedEventArgs e)
        {
            if (ExistingProviderGrid is null || NewProviderGrid is null) return;
            ExistingProviderGrid.Visibility = Visibility.Collapsed;
            NewProviderGrid.Visibility = Visibility.Visible;
        }
    }
}
