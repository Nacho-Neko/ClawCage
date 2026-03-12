using ClawCage.WinUI.Model;
using ClawCage.WinUI.Services.OpenClaw;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.Threading.Tasks;
using OpenClawModel = ClawCage.WinUI.Model.Model;

namespace ClawCage.WinUI.Components.Providers
{
    internal sealed class AliCloudProviderWizardComponent : IProviderWizardComponent
    {
        private const string OpenAiCompatibleApi = "openai-completions";
        private const string AnthropicCompatibleApi = "anthropic-completions";

        public string Key => "AliCloud-Coding";
        public string Title => "AliCloud Coding Plan";
        public string Description => "阿里云 Coding Plan 平台";
        public string? IconResourceName => "alicloud";
        public bool IsCustom => false;
        public string? DefaultBaseUrl => "https://coding.dashscope.aliyuncs.com/v1";
        public string? DefaultApi => OpenAiCompatibleApi;

        public async Task<bool> ConfigureNewProviderAsync(XamlRoot xamlRoot, ProviderDraft draft)
        {
            var apiKeyBox = new PasswordBox { Header = "API Key", PasswordRevealMode = PasswordRevealMode.Peek };
            var apiCombo = new ComboBox
            {
                Header = "API 方式",
                ItemsSource = new[] { OpenAiCompatibleApi, AnthropicCompatibleApi },
                SelectedItem = string.IsNullOrWhiteSpace(draft.Api) ? OpenAiCompatibleApi : draft.Api
            };
            var testText = new TextBlock { Opacity = 0.7 };
            var tested = false;
            ContentDialog? dialog = null;

            apiKeyBox.PasswordChanged += (_, _) =>
            {
                tested = false;
                if (dialog is not null)
                    dialog.PrimaryButtonText = "测试";
                testText.Text = string.Empty;
            };
            apiCombo.SelectionChanged += (_, _) =>
            {
                tested = false;
                if (dialog is not null)
                    dialog.PrimaryButtonText = "测试";
                testText.Text = string.Empty;
            };

            dialog = new ContentDialog
            {
                Title = "AliCloud Coding Plan 配置",
                PrimaryButtonText = "测试",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = xamlRoot,
                Content = new StackPanel
                {
                    MinWidth = 580,
                    Spacing = 10,
                    Children =
                    {
                        apiKeyBox,
                        apiCombo,
                        testText
                    }
                }
            };

            dialog.PrimaryButtonClick += async (_, args) =>
            {
                if (tested)
                    return;

                args.Cancel = true;
                var deferral = args.GetDeferral();
                try
                {
                    if (string.IsNullOrWhiteSpace(apiKeyBox.Password))
                    {
                        testText.Text = "测试失败：API Key 不能为空。";
                        return;
                    }

                    dialog.IsPrimaryButtonEnabled = false;
                    testText.Text = "测试中...";
                    var selectedApi = apiCombo.SelectedItem?.ToString() ?? OpenAiCompatibleApi;
                    var testResult = await TestAliCloudApiAsync(apiKeyBox.Password, selectedApi);
                    tested = testResult.Success;
                    testText.Text = tested ? string.Empty : testResult.Message;
                    dialog.PrimaryButtonText = tested ? "完成" : "测试";
                }
                finally
                {
                    dialog.IsPrimaryButtonEnabled = true;
                    deferral.Complete();
                }
            };

            var result = await ProviderDialogHelper.ShowDialogAsync(dialog);
            if (result != ContentDialogResult.Primary)
                return false;

            draft.ProviderKey = "AliCloud-Coding";
            draft.BaseUrl = "https://coding.dashscope.aliyuncs.com/v1";
            draft.ApiKey = apiKeyBox.Password;
            draft.Api = apiCombo.SelectedItem?.ToString() ?? OpenAiCompatibleApi;
            draft.SkipModelStep = true;
            draft.PresetModels = BuildAliCloudPresetModels();
            return true;
        }

        private static async Task<(bool Success, string Message)> TestAliCloudApiAsync(string apiKey, string selectedApi)
        {
            return selectedApi == AnthropicCompatibleApi
                ? await ProviderApiTestService.TestCompatibleAsync(
                    apiKey,
                    "https://coding.dashscope.aliyuncs.com/apps/anthropic/v1/messages",
                    "qwen3.5-plus")
                : await ProviderApiTestService.TestCompatibleAsync(
                    apiKey,
                    "https://coding.dashscope.aliyuncs.com/v1/chat/completions",
                    "qwen3.5-plus");
        }

        private static List<OpenClawModel> BuildAliCloudPresetModels()
        {
            return
            [
                CreateModel("qwen3.5-plus", ["text", "image"], 1000000, 65536),
                CreateModel("qwen3-max-2026-01-23", ["text"], 262144, 65536),
                CreateModel("qwen3-coder-next", ["text"], 262144, 65536),
                CreateModel("qwen3-coder-plus", ["text"], 1000000, 65536),
                CreateModel("MiniMax-M2.5", ["text"], 196608, 32768),
                CreateModel("glm-5", ["text"], 202752, 16384),
                CreateModel("glm-4.7", ["text"], 202752, 16384),
                CreateModel("kimi-k2.5", ["text", "image"], 262144, 32768)
            ];
        }

        private static OpenClawModel CreateModel(string id, List<string> input, int contextWindow, int maxTokens)
        {
            return new OpenClawModel
            {
                Id = id,
                Name = id,
                Reasoning = false,
                Input = input,
                Cost = new Cost { Input = 0, Output = 0, CacheRead = 0, CacheWrite = 0 },
                ContextWindow = contextWindow,
                MaxTokens = maxTokens
            };
        }
    }
}
