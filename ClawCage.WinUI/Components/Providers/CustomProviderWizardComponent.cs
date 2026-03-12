using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Threading.Tasks;

namespace ClawCage.WinUI.Components.Providers
{
    internal sealed class CustomProviderWizardComponent : IProviderWizardComponent
    {
        public string Key => "custom";
        public string Title => "自定义 Provider";
        public string Description => "自定义 API URL 与 Endpoint compatibility";
        public bool IsCustom => true;
        public string? DefaultBaseUrl => null;
        public string? DefaultApi => null;


        private const string OpenAiCompatibleApi = "openai-completions";
        private const string AnthropicCompatibleApi = "anthropic-completions";


        public async Task<bool> ConfigureNewProviderAsync(XamlRoot xamlRoot, ProviderDraft draft)
        {
            var keyBox = new TextBox { Header = "Provider Key", Text = string.Empty };
            var apiKeyBox = new PasswordBox { Header = "API Key", PasswordRevealMode = PasswordRevealMode.Peek };
            var apiUrlBox = new TextBox { Header = "API URL" };
            var endpointCombo = new ComboBox
            {
                Header = "Endpoint compatibility",
                ItemsSource = new[] { OpenAiCompatibleApi, AnthropicCompatibleApi },
                SelectedItem = string.IsNullOrWhiteSpace(draft.Api) ? OpenAiCompatibleApi : draft.Api
            };

            var providerDialog = new ContentDialog
            {
                Title = "供应商信息",
                PrimaryButtonText = "继续",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = xamlRoot,
                Content = new StackPanel
                {
                    MinWidth = 580,
                    Spacing = 10,
                    Children =
                    {
                        keyBox,
                        apiKeyBox,
                        apiUrlBox,
                        endpointCombo
                    }
                }
            };

            var providerResult = await ProviderDialogHelper.ShowDialogAsync(providerDialog);
            if (providerResult != ContentDialogResult.Primary)
                return false;

            draft.ProviderKey = keyBox.Text.Trim();
            draft.ApiKey = apiKeyBox.Password;
            draft.BaseUrl = apiUrlBox.Text.Trim();
            draft.Api = endpointCombo.SelectedItem?.ToString() ?? OpenAiCompatibleApi;
            return true;
        }
    }
}
