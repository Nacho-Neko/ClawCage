using ClawCage.WinUI.Model;
using ClawCage.WinUI.Model.Agents;
using ClawCage.WinUI.Services.Agents;
using ClawCage.WinUI.Services.OpenClaw;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ClawCage.WinUI.Components.Agents
{
    internal static class AgentActions
    {
        internal static async Task<bool> DeleteAgentAsync(XamlRoot xamlRoot, AgentsConfigService agentsService, string agentId)
        {
            if (string.Equals(agentId, AgentCardBuilder.MainAgentId, System.StringComparison.OrdinalIgnoreCase))
                return false;

            if (await AgentDialogs.ShowConfirmDeleteAsync(xamlRoot,
                    $"确定要删除代理「{agentId}」及其所有配置？此操作不可恢复。") != ContentDialogResult.Primary)
                return false;

            await agentsService.DeleteAgentAsync(agentId);
            return true;
        }

        internal static async Task<bool> AddProviderAsync(
            XamlRoot xamlRoot,
            AgentsConfigService agentsService,
            OpenClawConfigService openClawService,
            string agentId)
        {
            var result = await openClawService.LoadModelsConfigAsync();
            if (result is null)
            {
                await AgentDialogs.ShowInfoAsync(xamlRoot, "提示", "无法读取全局配置，请确认 OpenClaw 已初始化。");
                return false;
            }

            var globalProviders = result.Value.Models.Providers;
            if (globalProviders is null || globalProviders.Count == 0)
            {
                await AgentDialogs.ShowInfoAsync(xamlRoot, "提示", "全局配置中暂无供应商，请先在模型页面配置供应商。");
                return false;
            }

            // Filter out providers already added to this agent
            var agentConfig = await agentsService.LoadModelsAsync(agentId);
            var existingNames = agentConfig?.Providers.Keys.ToHashSet(System.StringComparer.OrdinalIgnoreCase) ?? [];
            var available = globalProviders
                .Where(kv => !existingNames.Contains(kv.Key))
                .ToList();

            if (available.Count == 0)
            {
                await AgentDialogs.ShowInfoAsync(xamlRoot, "提示", "所有供应商已添加到该代理中。");
                return false;
            }

            var providerCombo = new ComboBox
            {
                Header = "选择供应商 *",
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            foreach (var kv in available)
                providerCombo.Items.Add(kv.Key);
            providerCombo.SelectedIndex = 0;

            var infoText = new TextBlock
            {
                FontSize = 12,
                Opacity = 0.55,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0)
            };

            void UpdateInfo()
            {
                var idx = providerCombo.SelectedIndex;
                if (idx >= 0 && idx < available.Count)
                {
                    var provider = available[idx].Value;
                    var modelNames = provider.Models?.Select(m => m.Name ?? m.Id) ?? [];
                    infoText.Text = $"API: {provider.Api}\n模型 ({provider.Models?.Count ?? 0}): {string.Join(", ", modelNames)}";
                }
            }

            providerCombo.SelectionChanged += (_, _) => UpdateInfo();
            UpdateInfo();

            var panel = new StackPanel { Spacing = 10, MinWidth = 400 };
            panel.Children.Add(providerCombo);
            panel.Children.Add(infoText);

            var dialog = new ContentDialog
            {
                Title = "添加供应商",
                PrimaryButtonText = "添加",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = xamlRoot,
                Content = panel
            };

            if (await AgentDialogs.ShowAsync(dialog) != ContentDialogResult.Primary)
                return false;

            var selectedIdx = providerCombo.SelectedIndex;
            if (selectedIdx < 0 || selectedIdx >= available.Count)
                return false;

            var selected = available[selectedIdx];
            var globalProvider = selected.Value;
            var agentProvider = new AgentProvider
            {
                BaseUrl = globalProvider.BaseUrl ?? string.Empty,
                ApiKey = globalProvider.ApiKey ?? string.Empty,
                Api = globalProvider.Api ?? "openai-completions",
                Models = globalProvider.Models?.Select(m => new AgentModel
                {
                    Id = m.Id ?? string.Empty,
                    Name = m.Name ?? string.Empty,
                    Reasoning = m.Reasoning,
                    Input = m.Input != null ? new List<string>(m.Input) : [],
                    Cost = new AgentModelCost
                    {
                        Input = m.Cost?.Input ?? 0,
                        Output = m.Cost?.Output ?? 0,
                        CacheRead = m.Cost?.CacheRead ?? 0,
                        CacheWrite = m.Cost?.CacheWrite ?? 0
                    },
                    ContextWindow = m.ContextWindow,
                    MaxTokens = m.MaxTokens
                }).ToList() ?? []
            };

            await agentsService.SaveProviderAsync(agentId, selected.Key, agentProvider);
            return true;
        }

        internal static async Task<bool> EditProviderModelsAsync(
            XamlRoot xamlRoot,
            AgentsConfigService agentsService,
            OpenClawConfigService openClawService,
            string agentId,
            string providerName)
        {
            // Load global provider models
            var globalResult = await openClawService.LoadModelsConfigAsync();
            Provider? globalProvider = null;
            if (globalResult is not null &&
                globalResult.Value.Models.Providers is not null)
            {
                globalResult.Value.Models.Providers.TryGetValue(providerName, out globalProvider);
            }

            if (globalProvider is null || globalProvider.Models is null || globalProvider.Models.Count == 0)
            {
                await AgentDialogs.ShowInfoAsync(xamlRoot, "提示", $"全局配置中未找到供应商「{providerName}」的模型。");
                return false;
            }

            // Load agent's enabled models
            var agentConfig = await agentsService.LoadModelsAsync(agentId);
            AgentProvider? agentProvider = null;
            agentConfig?.Providers.TryGetValue(providerName, out agentProvider);
            var enabledIds = agentProvider?.Models
                .Select(m => m.Id)
                .ToHashSet(System.StringComparer.OrdinalIgnoreCase) ?? [];

            // Build toggle list
            var toggles = new List<(ToggleSwitch Toggle, ClawCage.WinUI.Model.Model GlobalModel)>();
            var listPanel = new StackPanel { Spacing = 6 };

            foreach (var model in globalProvider.Models)
            {
                var row = new Grid { ColumnSpacing = 12, Padding = new Thickness(4, 6, 4, 6) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var infoPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 1 };
                infoPanel.Children.Add(new TextBlock
                {
                    Text = model.Name ?? model.Id,
                    FontSize = 13,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                });
                infoPanel.Children.Add(new TextBlock
                {
                    Text = model.Id,
                    FontSize = 11,
                    Opacity = 0.5,
                    FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Cascadia Mono")
                });
                row.Children.Add(infoPanel);

                var toggle = new ToggleSwitch
                {
                    IsOn = enabledIds.Contains(model.Id ?? string.Empty),
                    OnContent = "启用",
                    OffContent = "禁用",
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(toggle, 1);
                row.Children.Add(toggle);

                toggles.Add((toggle, model));
                listPanel.Children.Add(row);

                // Separator
                listPanel.Children.Add(new Border
                {
                    Height = 1,
                    Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                    Opacity = 0.3
                });
            }

            var scrollViewer = new ScrollViewer
            {
                Content = listPanel,
                MaxHeight = 400,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };

            var headerText = new TextBlock
            {
                Text = $"供应商「{providerName}」下共 {globalProvider.Models.Count} 个模型，切换开关以启用或禁用：",
                FontSize = 12,
                Opacity = 0.7,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };

            var contentPanel = new StackPanel { MinWidth = 450 };
            contentPanel.Children.Add(headerText);
            contentPanel.Children.Add(scrollViewer);

            var dialog = new ContentDialog
            {
                Title = $"管理模型 — {providerName}",
                PrimaryButtonText = "保存",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = xamlRoot,
                Content = contentPanel
            };

            if (await AgentDialogs.ShowAsync(dialog) != ContentDialogResult.Primary)
                return false;

            // Build the new models list from toggles
            var newModels = new List<AgentModel>();
            foreach (var (toggle, gm) in toggles)
            {
                if (!toggle.IsOn) continue;
                newModels.Add(new AgentModel
                {
                    Id = gm.Id ?? string.Empty,
                    Name = gm.Name ?? string.Empty,
                    Reasoning = gm.Reasoning,
                    Input = gm.Input != null ? new List<string>(gm.Input) : [],
                    Cost = new AgentModelCost
                    {
                        Input = gm.Cost?.Input ?? 0,
                        Output = gm.Cost?.Output ?? 0,
                        CacheRead = gm.Cost?.CacheRead ?? 0,
                        CacheWrite = gm.Cost?.CacheWrite ?? 0
                    },
                    ContextWindow = gm.ContextWindow,
                    MaxTokens = gm.MaxTokens
                });
            }

            // Preserve agent provider's connection info, update only models
            var updatedProvider = agentProvider ?? new AgentProvider
            {
                BaseUrl = globalProvider.BaseUrl ?? string.Empty,
                ApiKey = globalProvider.ApiKey ?? string.Empty,
                Api = globalProvider.Api ?? "openai-completions"
            };
            updatedProvider.Models = newModels;

            await agentsService.SaveProviderAsync(agentId, providerName, updatedProvider);
            return true;
        }

        internal static async Task<bool> DeleteProviderAsync(
            XamlRoot xamlRoot,
            AgentsConfigService agentsService,
            string agentId,
            string providerName)
        {
            if (await AgentDialogs.ShowConfirmDeleteAsync(xamlRoot,
                    $"确定要删除提供商「{providerName}」？") != ContentDialogResult.Primary)
                return false;

            await agentsService.RemoveProviderAsync(agentId, providerName);
            return true;
        }
    }
}
