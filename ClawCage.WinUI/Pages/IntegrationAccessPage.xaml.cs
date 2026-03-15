using ClawCage.WinUI.Components.Integrations;
using ClawCage.WinUI.Model;
using ClawCage.WinUI.Services.OpenClaw;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Windows.Foundation;

namespace ClawCage.WinUI.Pages
{
    public sealed partial class IntegrationAccessPage : Page
    {
        private readonly OpenClawConfigService _configService = Ioc.Default.GetRequiredService<OpenClawConfigService>();
        private readonly OpenClawPluginService _pluginService = Ioc.Default.GetRequiredService<OpenClawPluginService>();

        public ObservableCollection<IntegrationViewItem> IntegrationItems { get; } = [];

        private JsonObject? _rootConfigNode;
        private Integrations? _integrationConfig;
        private JsonObject? _pluginsRootNode;
        private PluginsConfig? _pluginsConfig;
        private string? _configPath;
        private bool _suppressToggleEvents;

        public IntegrationAccessPage()
        {
            InitializeComponent();
            Loaded += IntegrationAccessPage_Loaded;
            Unloaded += IntegrationAccessPage_Unloaded;
        }

        private async void IntegrationAccessPage_Loaded(object sender, RoutedEventArgs e)
        {
            _configService.ConfigChanged -= OpenClawConfigService_ConfigChanged;
            _configService.ConfigChanged += OpenClawConfigService_ConfigChanged;
            await LoadIntegrationsAsync();
        }

        private void IntegrationAccessPage_Unloaded(object sender, RoutedEventArgs e)
        {
            _configService.ConfigChanged -= OpenClawConfigService_ConfigChanged;
        }

        private void OpenClawConfigService_ConfigChanged(object? sender, EventArgs e)
        {
            _ = DispatcherQueue.TryEnqueue(async () => await LoadIntegrationsAsync());
        }

        private async void ReloadButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadIntegrationsAsync();
        }

        private async Task LoadIntegrationsAsync()
        {
            _suppressToggleEvents = true;
            IntegrationItems.Clear();
            StatusText.Text = "读取中...";

            _configPath = _configService.GetConfigPath();
            IntegrationComponentRegistry.EnsureInitialized();

            // Fetch installed plugins from singleton cache
            var plugins = _pluginService.Plugins;

            // Load plugins config (enabled + allow list)
            var pluginsConfigResult = await _configService.LoadPluginsConfigAsync();
            if (pluginsConfigResult is not null)
            {
                _pluginsRootNode = pluginsConfigResult.Value.Root;
                _pluginsConfig = pluginsConfigResult.Value.Plugins;
            }
            else
            {
                _pluginsRootNode = null;
                _pluginsConfig = new PluginsConfig { Enabled = false, Allow = [] };
            }

            var allowSet = new HashSet<string>(_pluginsConfig.Allow ?? [], StringComparer.OrdinalIgnoreCase);

            // Collect keys already present in config so we don't duplicate
            var configKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var integrationConfigResult = await _configService.LoadIntegrationConfigAsync();
            if (integrationConfigResult is not null)
            {
                _rootConfigNode = integrationConfigResult.Value.Root;
                _integrationConfig = integrationConfigResult.Value.Integrations;
            }

            try
            {
                // 1. Show config-based integrations
                if (_integrationConfig?.Providers is not null)
                {
                    foreach (var entry in _integrationConfig.Providers)
                    {
                        var integration = entry.Value;
                        if (integration is null)
                            continue;

                        configKeys.Add(entry.Key);
                        IntegrationItems.Add(BuildIntegrationViewItem(entry.Key, integration, allowSet));
                    }
                }

                // 2. Show installed plugins that match registered IIntegrationWizardComponent keys
                foreach (var component in IntegrationComponentRegistry.GetAll())
                {
                    if (configKeys.Contains(component.Key))
                        continue;

                    if (!plugins.TryGetValue(component.Key, out var pluginInfo))
                        continue;

                    IntegrationItems.Add(BuildPluginViewItem(component, pluginInfo, allowSet));
                }

                StatusText.Text = $"已加载 {IntegrationItems.Count} 个接入。";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"读取失败: {ex.Message}";
            }
            finally
            {
                _suppressToggleEvents = false;
            }
        }

        private static IntegrationViewItem BuildPluginViewItem(
            IIntegrationWizardComponent component,
            OpenClawPluginService.PluginInfo pluginInfo,
            HashSet<string> allowSet)
        {
            var isLoaded = string.Equals(pluginInfo.Status, "loaded", StringComparison.OrdinalIgnoreCase);
            var statusColor = isLoaded
                ? Windows.UI.Color.FromArgb(255, 34, 177, 76)
                : Windows.UI.Color.FromArgb(255, 244, 67, 54);

            return new IntegrationViewItem
            {
                Key = component.Key,
                Name = component.Title,
                VersionText = !string.IsNullOrWhiteSpace(pluginInfo.Version) ? pluginInfo.Version : "-",
                DescriptionText = component.Description,
                EnabledText = isLoaded ? "已加载" : pluginInfo.Status,
                StatusColor = new Microsoft.UI.Xaml.Media.SolidColorBrush(statusColor),
                SourceIntegration = null,
                IsAllowed = allowSet.Contains(component.Key),
                IconUrl = !string.IsNullOrEmpty(component.IconResourceName)
                    ? $"ms-appx:///Asset/Integration/{component.IconResourceName}.png"
                    : null
            };
        }

        private static IntegrationViewItem BuildIntegrationViewItem(string integrationKey, Integration integration, HashSet<string> allowSet)
        {
            IntegrationComponentRegistry.TryGet(integrationKey, out var component);

            var statusColor = integration.Enabled
                ? Windows.UI.Color.FromArgb(255, 34, 177, 76)
                : Windows.UI.Color.FromArgb(255, 244, 67, 54);

            return new IntegrationViewItem
            {
                Key = integrationKey,
                Name = component?.Title ?? integration.Name ?? integrationKey,
                VersionText = "-",
                DescriptionText = integration.Description ?? string.Empty,
                EnabledText = integration.Enabled ? "启用" : "禁用",
                StatusColor = new Microsoft.UI.Xaml.Media.SolidColorBrush(statusColor),
                SourceIntegration = integration,
                IsAllowed = allowSet.Contains(integrationKey),
                IconUrl = component is not null && !string.IsNullOrEmpty(component.IconResourceName)
                    ? $"ms-appx:///Asset/Integration/{component.IconResourceName}.png"
                    : null
            };
        }

        private async void AddIntegrationButton_Click(object sender, RoutedEventArgs e)
        {
            var result = await AddIntegrationDialog.ShowAddIntegrationAsync(XamlRoot);
            if (result is null)
                return;

            if (_integrationConfig is null)
                await LoadIntegrationsAsync();

            if (_integrationConfig is null)
                return;

            var newIntegration = new Integration
            {
                Name = result.Name,
                Type = result.Type,
                Description = result.Description,
                Enabled = result.Enabled,
                Config = result.Config
            };

            var key = result.IntegrationKey.ToLower();
            _integrationConfig.Providers ??= new();
            _integrationConfig.Providers[key] = newIntegration;

            if (!await SaveIntegrationConfigAsync())
                return;

            await LoadIntegrationsAsync();
            StatusText.Text = "接入已添加。";
        }

        private async void ConfigureButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { Tag: IntegrationViewItem item })
                return;

            await EditIntegrationAsync(item);
        }

        private async void UninstallButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { Tag: IntegrationViewItem item })
                return;

            await DeleteIntegrationAsync(item);
        }

        private void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { Tag: IntegrationViewItem item })
                return;

            StatusText.Text = $"检查「{item.Name}」更新中...";
            // TODO: implement update check logic
        }

        private async void CardToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_suppressToggleEvents || _pluginsConfig is null)
                return;

            if (sender is not ToggleSwitch { Tag: IntegrationViewItem item })
                return;

            _pluginsConfig.Allow ??= [];

            if (item.IsAllowed && !_pluginsConfig.Allow.Contains(item.Key, StringComparer.OrdinalIgnoreCase))
            {
                // Was toggled ON but somehow not in list — skip to avoid duplicate
            }

            if (((ToggleSwitch)sender).IsOn)
            {
                if (!_pluginsConfig.Allow.Contains(item.Key, StringComparer.OrdinalIgnoreCase))
                    _pluginsConfig.Allow.Add(item.Key);
                item.IsAllowed = true;
            }
            else
            {
                _pluginsConfig.Allow.RemoveAll(k => string.Equals(k, item.Key, StringComparison.OrdinalIgnoreCase));
                item.IsAllowed = false;
            }

            await SavePluginsConfigInternalAsync();
        }

        private async Task<bool> SavePluginsConfigInternalAsync()
        {
            if (_pluginsConfig is null)
                return false;

            try
            {
                // Always reload root to avoid stale state
                var root = await _configService.LoadRootAsync() ?? new JsonObject();
                await _configService.SavePluginsConfigAsync(root, _pluginsConfig);
                StatusText.Text = "插件配置已保存。";
                return true;
            }
            catch (Exception ex)
            {
                StatusText.Text = $"保存失败: {ex.Message}";
                return false;
            }
        }

        private async Task EditIntegrationAsync(IntegrationViewItem integrationItem)
        {
            if (integrationItem.SourceIntegration is null)
                return;

            var integration = integrationItem.SourceIntegration;
            var nameBox = new TextBox { Text = integration.Name ?? string.Empty, Header = "名称", HorizontalAlignment = HorizontalAlignment.Stretch };
            var descBox = new TextBox { Text = integration.Description ?? string.Empty, Header = "描述", AcceptsReturn = true, Height = 80, HorizontalAlignment = HorizontalAlignment.Stretch };
            var enabledToggle = new ToggleSwitch { IsOn = integration.Enabled, Header = "启用此接入" };

            var configPanel = new StackPanel { Spacing = 12 };
            if (integration.Config != null && integration.Config.Count > 0)
            {
                configPanel.Children.Add(new TextBlock { Text = "配置信息", Style = Application.Current.Resources["BodyStrongTextBlockStyle"] as Style });
                foreach (var kvp in integration.Config)
                {
                    var box = new TextBox
                    {
                        Header = kvp.Key,
                        Text = kvp.Value?.ToString() ?? string.Empty,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        Tag = kvp.Key
                    };
                    configPanel.Children.Add(box);
                }
            }

            var content = new StackPanel { Spacing = 12 };
            content.Children.Add(nameBox);
            content.Children.Add(descBox);
            content.Children.Add(enabledToggle);
            content.Children.Add(configPanel);

            var dialog = new ContentDialog
            {
                Title = $"编辑接入 - {integrationItem.Name}",
                PrimaryButtonText = "保存",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot,
                Content = new ScrollViewer
                {
                    MaxHeight = 500,
                    Content = content
                }
            };

            var result = await ShowDialogAsync(dialog);
            if (result != ContentDialogResult.Primary)
                return;

            integration.Name = nameBox.Text.Trim();
            integration.Description = descBox.Text.Trim();
            integration.Enabled = enabledToggle.IsOn;

            // 更新配置项
            if (integration.Config != null)
            {
                foreach (var child in configPanel.Children.OfType<TextBox>())
                {
                    var key = child.Tag as string;
                    if (!string.IsNullOrEmpty(key))
                        integration.Config[key] = child.Text;
                }
            }

            if (!await SaveIntegrationConfigAsync())
                return;

            await LoadIntegrationsAsync();
        }

        private async Task DeleteIntegrationAsync(IntegrationViewItem integrationItem)
        {
            var dialog = new ContentDialog
            {
                Title = "确认删除",
                Content = $"确认删除接入「{integrationItem.Name}」？此操作不可撤销。",
                PrimaryButtonText = "删除",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.None,
                XamlRoot = XamlRoot
            };

            if (await ShowDialogAsync(dialog) != ContentDialogResult.Primary)
                return;

            _integrationConfig?.Providers?.Remove(integrationItem.Key);

            if (!await SaveIntegrationConfigAsync())
                return;

            await LoadIntegrationsAsync();
            StatusText.Text = $"已删除接入「{integrationItem.Name}」。";
        }

        private async Task<bool> SaveIntegrationConfigAsync()
        {
            if (_rootConfigNode is null || _integrationConfig is null)
                return false;

            try
            {
                await _configService.SaveIntegrationConfigAsync(_rootConfigNode, _integrationConfig);
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

        public sealed class IntegrationViewItem
        {
            public string Key { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string VersionText { get; set; } = string.Empty;
            public string DescriptionText { get; set; } = string.Empty;
            public string EnabledText { get; set; } = string.Empty;
            public bool IsAllowed { get; set; }
            public Microsoft.UI.Xaml.Media.SolidColorBrush StatusColor { get; set; }
            public Integration? SourceIntegration { get; set; }
            public string? IconUrl { get; set; }
            public bool HasIcon => !string.IsNullOrEmpty(IconUrl);
            public Microsoft.UI.Xaml.Media.Imaging.BitmapImage? IconSource =>
                HasIcon ? new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(IconUrl!)) : null;
        }
    }
}
