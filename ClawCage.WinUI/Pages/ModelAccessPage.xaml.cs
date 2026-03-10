using ClawCage.WinUI.Components;
using ClawCage.WinUI.Model;
using ClawCage.WinUI.Services.OpenClaw;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Windows.Foundation;
using OpenClawModel = ClawCage.WinUI.Model.Model;

namespace ClawCage.WinUI.Pages
{
    public sealed partial class ModelAccessPage : Page
    {
        public ObservableCollection<ProviderViewItem> ProviderItems { get; } = [];

        private JsonObject? _rootConfigNode;
        private Models? _modelsConfig;
        private string? _configPath;

        public ModelAccessPage()
        {
            InitializeComponent();
            Loaded += ModelAccessPage_Loaded;
            Unloaded += ModelAccessPage_Unloaded;
        }

        private async void ModelAccessPage_Loaded(object sender, RoutedEventArgs e)
        {
            OpenClawConfigService.ConfigChanged -= OpenClawConfigService_ConfigChanged;
            OpenClawConfigService.ConfigChanged += OpenClawConfigService_ConfigChanged;
            await LoadModelsAsync();
        }

        private void ModelAccessPage_Unloaded(object sender, RoutedEventArgs e)
        {
            OpenClawConfigService.ConfigChanged -= OpenClawConfigService_ConfigChanged;
        }

        private void OpenClawConfigService_ConfigChanged(object? sender, EventArgs e)
        {
            _ = DispatcherQueue.TryEnqueue(async () => await LoadModelsAsync());
        }

        private async void ReloadButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadModelsAsync();
        }

        private async Task LoadModelsAsync()
        {
            ProviderItems.Clear();
            StatusText.Text = "读取中...";

            _configPath = OpenClawConfigService.GetConfigPath();
            var modelsConfigResult = await OpenClawConfigService.LoadModelsConfigAsync();
            if (modelsConfigResult is null)
            {
                ModeText.Text = "Mode: -";
                StatusText.Text = $"配置文件不存在: {_configPath}";
                return;
            }

            try
            {
                _rootConfigNode = modelsConfigResult.Value.Root;
                _modelsConfig = modelsConfigResult.Value.Models;

                ModeText.Text = $"Mode: {_modelsConfig.Mode ?? "-"}";
                foreach (var entry in _modelsConfig.Providers ?? [])
                {
                    var provider = entry.Value;
                    if (provider is null)
                        continue;

                    ProviderItems.Add(BuildProviderViewItem(entry.Key, provider));
                }

                StatusText.Text = $"已加载 {ProviderItems.Count} 个供应商。";
            }
            catch (Exception ex)
            {
                ModeText.Text = "Mode: -";
                StatusText.Text = $"读取失败: {ex.Message}";
            }
        }

        private async void AddModelButton_Click(object sender, RoutedEventArgs e)
        {
            await AddModelWorkflowAsync();
        }

        private async Task AddModelWorkflowAsync()
        {
            if (_modelsConfig is null)
                await LoadModelsAsync();

            if (_modelsConfig is null)
                return;

            var applyResult = await AddModelProviderComponent.ShowAndApplyAsync(XamlRoot, _modelsConfig);
            if (!string.IsNullOrWhiteSpace(applyResult.ErrorMessage))
            {
                StatusText.Text = applyResult.ErrorMessage;
                return;
            }

            if (!applyResult.Applied)
                return;

            if (!await SaveModelsConfigAsync())
                return;

            await LoadModelsAsync();
            StatusText.Text = applyResult.SuccessMessage ?? "已完成添加模型。";
        }

        private ProviderViewItem BuildProviderViewItem(string providerKey, Provider provider)
        {
            return new ProviderViewItem
            {
                Name = providerKey,
                ModelsCountText = $"模型数: {(provider.Models ?? []).Count}",
                BaseUrlText = provider.BaseUrl ?? string.Empty,
                ApiText = provider.Api ?? string.Empty,
                ApiKeyPlain = provider.ApiKey ?? string.Empty,
                ApiKeyMasked = MaskApiKey(provider.ApiKey),
                SourceProvider = provider,
                Models = (provider.Models ?? [])
                    .Select(m => BuildModelViewItem(providerKey, m))
                    .ToList()
            };
        }

        private static ModelViewItem BuildModelViewItem(string providerKey, OpenClawModel model)
        {
            return new ModelViewItem
            {
                ProviderKey = providerKey,
                SourceModel = model,
                Title = string.IsNullOrWhiteSpace(model.Name) ? model.Id : model.Name,
                IdText = $"Id: {model.Id}",
                InputText = $"Input: {string.Join(", ", model.Input ?? [])}",
                ReasoningText = $"Reasoning: {model.Reasoning}",
                ContextText = $"ContextWindow: {model.ContextWindow}",
                MaxTokensText = $"MaxTokens: {model.MaxTokens}",
                CostText = $"Cost(In/Out/Read/Write): {model.Cost?.Input ?? 0}/{model.Cost?.Output ?? 0}/{model.Cost?.CacheRead ?? 0}/{model.Cost?.CacheWrite ?? 0}"
            };
        }

        private async void EditProviderButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: ProviderViewItem providerItem })
                return;

            await EditProviderAsync(providerItem);
        }

        private async void ProviderCard_EditProviderRequested(object sender, object? e)
        {
            if (e is not ProviderViewItem providerItem)
                return;

            await EditProviderAsync(providerItem);
        }

        private async Task EditProviderAsync(ProviderViewItem providerItem)
        {
            if (providerItem.SourceProvider is null)
                return;

            var baseUrlBox = new TextBox { Text = providerItem.SourceProvider.BaseUrl ?? string.Empty, Header = "BaseUrl" };
            var apiBox = new TextBox { Text = providerItem.SourceProvider.Api ?? string.Empty, Header = "Api" };
            var apiKeyBox = new PasswordBox { Password = providerItem.SourceProvider.ApiKey ?? string.Empty, Header = "ApiKey", PasswordRevealMode = PasswordRevealMode.Peek };

            var dialog = new ContentDialog
            {
                Title = $"编辑 Provider - {providerItem.Name}",
                PrimaryButtonText = "保存",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot,
                Content = new StackPanel
                {
                    Spacing = 10,
                    Children =
                    {
                        baseUrlBox,
                        apiBox,
                        apiKeyBox
                    }
                }
            };

            var result = await ShowDialogAsync(dialog);
            if (result != ContentDialogResult.Primary)
                return;

            providerItem.SourceProvider.BaseUrl = baseUrlBox.Text.Trim();
            providerItem.SourceProvider.Api = apiBox.Text.Trim();
            providerItem.SourceProvider.ApiKey = apiKeyBox.Password;

            if (!await SaveModelsConfigAsync())
                return;

            await LoadModelsAsync();
        }

        private async void EditModelButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: ModelViewItem modelItem })
                return;

            await EditModelAsync(modelItem);
        }

        private async void ProviderCard_EditModelRequested(object sender, object? e)
        {
            if (e is not ModelViewItem modelItem)
                return;

            await EditModelAsync(modelItem);
        }

        private async Task EditModelAsync(ModelViewItem modelItem)
        {
            if (modelItem.SourceModel is null)
                return;

            var model = modelItem.SourceModel;
            model.Cost ??= new Cost();
            model.Input ??= [];

            var idBox = new TextBox { Header = "Id", Text = model.Id ?? string.Empty };
            var nameBox = new TextBox { Header = "Name", Text = model.Name ?? string.Empty };
            var textInputTag = new ToggleButton { Content = "text", IsChecked = model.Input.Contains("text", StringComparer.OrdinalIgnoreCase), MinWidth = 80 };
            var imageInputTag = new ToggleButton { Content = "image", IsChecked = model.Input.Contains("image", StringComparer.OrdinalIgnoreCase), MinWidth = 80 };

            var contextNumber = new NumberBox { Header = "ContextWindow", SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact, Value = model.ContextWindow, HorizontalAlignment = HorizontalAlignment.Stretch };
            var maxTokensNumber = new NumberBox { Header = "MaxTokens", SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact, Value = model.MaxTokens, HorizontalAlignment = HorizontalAlignment.Stretch };

            var costInputNumber = new NumberBox { Header = "Cost Input", SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact, Value = model.Cost.Input, HorizontalAlignment = HorizontalAlignment.Stretch };
            var costOutputNumber = new NumberBox { Header = "Cost Output", SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact, Value = model.Cost.Output, HorizontalAlignment = HorizontalAlignment.Stretch };
            var costReadNumber = new NumberBox { Header = "Cost CacheRead", SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact, Value = model.Cost.CacheRead, HorizontalAlignment = HorizontalAlignment.Stretch };
            var costWriteNumber = new NumberBox { Header = "Cost CacheWrite", SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact, Value = model.Cost.CacheWrite, HorizontalAlignment = HorizontalAlignment.Stretch };

            var advancedPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Spacing = 8,
                Children =
                {
                    contextNumber,
                    maxTokensNumber,
                    costInputNumber,
                    costOutputNumber,
                    costReadNumber,
                    costWriteNumber
                }
            };

            var advancedExpander = new Expander
            {
                Header = "高级设置",
                IsExpanded = false,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Content = advancedPanel
            };

            var contentPanel = new StackPanel
            {
                MinWidth = 520,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Spacing = 8,
                Children =
                {
                    idBox,
                    nameBox,
                    new TextBlock { Text = "InputTags（可选一个或两个）", Opacity = 0.8 },
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { textInputTag, imageInputTag } },
                    advancedExpander
                }
            };

            var dialog = new ContentDialog
            {
                Title = $"编辑模型 - {modelItem.Title}",
                PrimaryButtonText = "保存",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot,
                Content = new ScrollViewer
                {
                    MaxHeight = 520,
                    Content = contentPanel
                }
            };

            var result = await ShowDialogAsync(dialog);
            if (result != ContentDialogResult.Primary)
                return;

            var selectedInputs = new List<string>();
            if (textInputTag.IsChecked == true) selectedInputs.Add("text");
            if (imageInputTag.IsChecked == true) selectedInputs.Add("image");

            if (selectedInputs.Count == 0)
            {
                StatusText.Text = "Input 至少选择一个标签。";
                return;
            }

            model.Id = idBox.Text.Trim();
            model.Name = nameBox.Text.Trim();
            model.Input = selectedInputs;
            model.ContextWindow = Convert.ToInt32(contextNumber.Value);
            model.MaxTokens = Convert.ToInt32(maxTokensNumber.Value);
            model.Cost.Input = Convert.ToInt32(costInputNumber.Value);
            model.Cost.Output = Convert.ToInt32(costOutputNumber.Value);
            model.Cost.CacheRead = Convert.ToInt32(costReadNumber.Value);
            model.Cost.CacheWrite = Convert.ToInt32(costWriteNumber.Value);

            if (!await SaveModelsConfigAsync())
                return;

            await LoadModelsAsync();
        }

        private async Task<bool> SaveModelsConfigAsync()
        {
            if (_rootConfigNode is null || _modelsConfig is null)
                return false;

            try
            {
                await OpenClawConfigService.SaveModelsConfigAsync(_rootConfigNode, _modelsConfig);
                StatusText.Text = "保存成功。";
                return true;
            }
            catch (Exception ex)
            {
                StatusText.Text = $"保存失败: {ex.Message}";
                return false;
            }
        }

        private static Task<ContentDialogResult> ShowDialogAsync(ContentDialog dialog)
        {
            var tcs = new TaskCompletionSource<ContentDialogResult>();
            var operation = dialog.ShowAsync();
            operation.Completed = (op, status) =>
            {
                switch (status)
                {
                    case AsyncStatus.Completed:
                        tcs.TrySetResult(op.GetResults());
                        break;
                    case AsyncStatus.Canceled:
                        tcs.TrySetResult(ContentDialogResult.None);
                        break;
                    default:
                        tcs.TrySetException(new InvalidOperationException("对话框执行失败。"));
                        break;
                }
            };

            return tcs.Task;
        }

        private static string MaskApiKey(string? apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                return "(empty)";

            if (apiKey.Length <= 8)
                return new string('*', apiKey.Length);

            return $"{apiKey[..4]}****{apiKey[^4..]}";
        }

        public sealed class ProviderViewItem
        {
            public string Name { get; set; } = string.Empty;
            public string ModelsCountText { get; set; } = string.Empty;
            public string BaseUrlText { get; set; } = string.Empty;
            public string ApiText { get; set; } = string.Empty;
            public string ApiKeyPlain { get; set; } = string.Empty;
            public string ApiKeyMasked { get; set; } = string.Empty;
            public Provider? SourceProvider { get; set; }
            public List<ModelViewItem> Models { get; set; } = [];
        }

        public sealed class ModelViewItem
        {
            public string ProviderKey { get; set; } = string.Empty;
            public OpenClawModel? SourceModel { get; set; }
            public string Title { get; set; } = string.Empty;
            public string IdText { get; set; } = string.Empty;
            public string InputText { get; set; } = string.Empty;
            public string ReasoningText { get; set; } = string.Empty;
            public string ContextText { get; set; } = string.Empty;
            public string MaxTokensText { get; set; } = string.Empty;
            public string CostText { get; set; } = string.Empty;
        }

    }
}
