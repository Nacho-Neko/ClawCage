using ClawCage.WinUI.Components.Integrations;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;

namespace ClawCage.WinUI.Components
{
    public sealed partial class AddIntegrationWizardChooseStep : UserControl
    {
        public AddIntegrationWizardChooseStep()
        {
            InitializeComponent();
        }

        internal void SetIntegrations(IReadOnlyList<AddIntegrationDialog.IntegrationTemplate> items)
        {
            IntegrationGrid.ItemsSource = items;
        }

        internal AddIntegrationDialog.IntegrationTemplate? GetSelected()
        {
            return IntegrationGrid.SelectedItem as AddIntegrationDialog.IntegrationTemplate;
        }
    }
}
