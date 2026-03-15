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
        private Dictionary<string, ChannelEntry>? _channelsConfig;
        private PluginsConfig? _pluginsConfig;
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

            IntegrationComponentRegistry.EnsureInitialized();

            // Fetch installed plugins from singleton cache
            var plugins = _pluginService.Plugins;

            // Load plugins config (enabled + allow list)
            var pluginsConfigResult = await _configService.LoadPluginsConfigAsync();
            if (pluginsConfigResult is not null)
            {
                _pluginsConfig = pluginsConfigResult.Value.Plugins;
            }
            else
            {
                _pluginsConfig = new PluginsConfig { Enabled = false, Allow = [] };
            }

            // Collect keys already present in config so we don't duplicate
            var configKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var channelsResult = await _configService.LoadChannelsConfigAsync();
            if (channelsResult is not null)
            {
                _rootConfigNode = channelsResult.Value.Root;
                _channelsConfig = channelsResult.Value.Channels;
            }

            try
            {
                // 1. Show config-based channels
                if (_channelsConfig is not null)
                {
                    foreach (var channel in _channelsConfig.Values)
                    {
                        configKeys.Add(channel.Key);
                        IntegrationItems.Add(BuildChannelViewItem(channel));
                    }
                }

                // 2. Show installed plugins that match registered IIntegrationWizardComponent keys
                foreach (var component in IntegrationComponentRegistry.GetAll())
                {
                    if (configKeys.Contains(component.ConfigKey))
                        continue;

                    if (!plugins.TryGetValue(component.Key, out var pluginInfo))
                        continue;

                    IntegrationItems.Add(BuildPluginViewItem(component, pluginInfo));
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
            OpenClawPluginService.PluginInfo pluginInfo)
        {
            var isLoaded = string.Equals(pluginInfo.Status, "loaded", StringComparison.OrdinalIgnoreCase);
            var statusColor = isLoaded
                ? Windows.UI.Color.FromArgb(255, 34, 177, 76)
                : Windows.UI.Color.FromArgb(255, 244, 67, 54);

            return new IntegrationViewItem
            {
                Key = component.ConfigKey,
                Name = component.Title,
                VersionText = !string.IsNullOrWhiteSpace(pluginInfo.Version) ? pluginInfo.Version : "-",
                DescriptionText = component.Description,
                EnabledText = isLoaded ? "已加载" : pluginInfo.Status,
                StatusColor = new Microsoft.UI.Xaml.Media.SolidColorBrush(statusColor),
                SourceChannelData = null,
                IsAllowed = false,
                IconUrl = !string.IsNullOrEmpty(component.IconResourceName)
                    ? $"ms-appx:///Asset/Integration/{component.IconResourceName}.png"
                    : null
            };
        }

        private static IntegrationViewItem BuildChannelViewItem(ChannelEntry channel)
        {
            IntegrationComponentRegistry.TryGet(channel.Key, out var component);

            var statusColor = channel.Enabled
                ? Windows.UI.Color.FromArgb(255, 34, 177, 76)
                : Windows.UI.Color.FromArgb(255, 244, 67, 54);

            return new IntegrationViewItem
            {
                Key = channel.Key,
                Name = component?.Title ?? channel.Key,
                VersionText = "-",
                DescriptionText = component?.Description ?? string.Empty,
                EnabledText = channel.Enabled ? "启用" : "禁用",
                StatusColor = new Microsoft.UI.Xaml.Media.SolidColorBrush(statusColor),
                SourceChannelData = channel.Data,
                IsAllowed = channel.Enabled,
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

            // Look up the component to get the npm package name
            if (!IntegrationComponentRegistry.TryGet(result.IntegrationKey, out var component) || component is null)
                return;

            StatusText.Text = $"正在安装插件「{component.Title}」，请在弹出的终端中查看进度…";
            var installResult = await _pluginService.InstallPluginAsync(component.NpmPackageName);
            if (!installResult.Success)
            {
                StatusText.Text = $"插件安装失败: {installResult.Error}";
                return;
            }

            // Add to plugins allow list
            _pluginsConfig ??= new PluginsConfig { Enabled = true, Allow = [] };
            _pluginsConfig.Allow ??= [];
            if (!_pluginsConfig.Allow.Contains(component.Key, StringComparer.OrdinalIgnoreCase))
                _pluginsConfig.Allow.Add(component.Key);
            await SavePluginsConfigInternalAsync();

            // Refresh plugin cache after install
            await _pluginService.LoadAsync();
            await LoadIntegrationsAsync();
            StatusText.Text = $"插件「{component.Title}」已安装。";
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
            if (_suppressToggleEvents || _channelsConfig is null)
                return;

            if (sender is not ToggleSwitch { Tag: IntegrationViewItem item })
                return;

            var isOn = ((ToggleSwitch)sender).IsOn;

            if (!_channelsConfig.TryGetValue(item.Key, out var channelEntry))
            {
                IntegrationComponentRegistry.TryGet(item.Key, out var component);
                var defaultData = component is not null
                    ? ChannelConfigDialog.BuildDefaultData(component)
                    : new JsonObject();
                channelEntry = new ChannelEntry { Key = item.Key, Data = defaultData, Enabled = isOn };
                _channelsConfig[item.Key] = channelEntry;
            }
            else
            {
                channelEntry.Enabled = isOn;
            }

            item.IsAllowed = isOn;
            await SaveChannelsConfigAsync();
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
            if (!IntegrationComponentRegistry.TryGet(integrationItem.Key, out var component) || component is null)
                return;

            ChannelEntry? channelEntry = null;
            _channelsConfig?.TryGetValue(integrationItem.Key, out channelEntry);

            var editedData = await ChannelConfigDialog.ShowAsync(XamlRoot, component, channelEntry?.Data);
            if (editedData is null)
                return;

            if (channelEntry is not null)
            {
                channelEntry.Data = editedData;
            }
            else if (_channelsConfig is not null)
            {
                _channelsConfig[integrationItem.Key] = new ChannelEntry { Key = integrationItem.Key, Data = editedData };
            }

            if (!await SaveChannelsConfigAsync())
                return;

            await LoadIntegrationsAsync();
        }

        private async Task DeleteIntegrationAsync(IntegrationViewItem integrationItem)
        {
            var dialog = new ContentDialog
            {
                Title = "确认卸载",
                Content = $"确认卸载接入「{integrationItem.Name}」？此操作不可撤销。",
                PrimaryButtonText = "卸载",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.None,
                XamlRoot = XamlRoot
            };

            if (await ShowDialogAsync(dialog) != ContentDialogResult.Primary)
                return;

            // Uninstall plugin via CLI if it has a registered component
            if (IntegrationComponentRegistry.TryGet(integrationItem.Key, out var component) && component is not null)
            {
                StatusText.Text = $"正在卸载插件「{integrationItem.Name}」，请在弹出的终端中查看进度…";
                var uninstallResult = await _pluginService.UninstallPluginAsync(component.NpmPackageName);
                if (!uninstallResult.Success)
                {
                    StatusText.Text = $"插件卸载失败: {uninstallResult.Error}";
                    return;
                }
            }

            _channelsConfig?.Remove(integrationItem.Key);

            if (!await SaveChannelsConfigAsync())
                return;

            // Remove from plugins allow list
            if (_pluginsConfig is not null && component is not null)
            {
                _pluginsConfig.Allow?.RemoveAll(k => string.Equals(k, component.Key, StringComparison.OrdinalIgnoreCase));
                await SavePluginsConfigInternalAsync();
            }

            // Refresh plugin cache after uninstall
            await _pluginService.LoadAsync();
            await LoadIntegrationsAsync();
            StatusText.Text = $"已卸载接入「{integrationItem.Name}」。";
        }

        private async Task<bool> SaveChannelsConfigAsync()
        {
            if (_rootConfigNode is null || _channelsConfig is null)
                return false;

            try
            {
                await _configService.SaveChannelsConfigAsync(_rootConfigNode, _channelsConfig);
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
            public JsonObject? SourceChannelData { get; set; }
            public string? IconUrl { get; set; }
            public bool HasIcon => !string.IsNullOrEmpty(IconUrl);
            public Microsoft.UI.Xaml.Media.Imaging.BitmapImage? IconSource =>
                HasIcon ? new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(IconUrl!)) : null;
        }
    }
}
