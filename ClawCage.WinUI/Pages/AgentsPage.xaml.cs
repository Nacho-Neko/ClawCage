using ClawCage.WinUI.Components.Agents;
using ClawCage.WinUI.Services.Agents;
using ClawCage.WinUI.Services.OpenClaw;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ClawCage.WinUI.Pages
{
    public sealed partial class AgentsPage : Page
    {
        private readonly AgentsConfigService _agentsService = Ioc.Default.GetRequiredService<AgentsConfigService>();
        private readonly OpenClawConfigService _openClawService = Ioc.Default.GetRequiredService<OpenClawConfigService>();
        private List<string>? _agentIds;

        public AgentsPage()
        {
            InitializeComponent();
            Loaded += AgentsPage_Loaded;
        }

        private async void AgentsPage_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadAgentsAsync();
        }

        private async void ReloadButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadAgentsAsync();
        }

        private async Task LoadAgentsAsync()
        {
            StatusText.Text = "读取中...";
            AgentListPanel.Children.Clear();

            _agentIds = await _agentsService.ListAgentIdsAsync();

            if (_agentIds.Count == 0)
            {
                StatusText.Text = "暂无代理";
                EmptyHint.Visibility = Visibility.Visible;
                AgentListPanel.Children.Add(EmptyHint);
                return;
            }

            EmptyHint.Visibility = Visibility.Collapsed;

            foreach (var agentId in _agentIds)
            {
                var config = await _agentsService.LoadModelsAsync(agentId);
                AgentListPanel.Children.Add(
                    AgentCardBuilder.CreateAgentCard(agentId, config,
                        OnAddProviderClick, OnDeleteAgentClick,
                        OnEditProviderClick, OnDeleteProviderClick));
            }

            StatusText.Text = $"已加载 {_agentIds.Count} 个代理";
        }

        // ── Add agent ──

        private async void AddAgentButton_Click(object sender, RoutedEventArgs e)
        {
            var nameBox = new TextBox
            {
                Header = "代理 ID *",
                PlaceholderText = "例如: ops, dev, test"
            };

            var dialog = new ContentDialog
            {
                Title = "新增代理",
                PrimaryButtonText = "创建",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot,
                Content = nameBox
            };

            dialog.PrimaryButtonClick += (_, args) =>
            {
                if (string.IsNullOrWhiteSpace(nameBox.Text))
                {
                    nameBox.Focus(FocusState.Programmatic);
                    args.Cancel = true;
                }
            };

            if (await AgentDialogs.ShowAsync(dialog) != ContentDialogResult.Primary)
                return;

            var agentId = nameBox.Text.Trim();
            await _agentsService.CreateAgentAsync(agentId);
            await LoadAgentsAsync();
        }

        // ── Event handlers ──

        private async void OnDeleteAgentClick(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string agentId) return;
            if (await AgentActions.DeleteAgentAsync(XamlRoot, _agentsService, agentId))
                await LoadAgentsAsync();
        }

        private async void OnAddProviderClick(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string agentId) return;
            if (await AgentActions.AddProviderAsync(XamlRoot, _agentsService, _openClawService, agentId))
                await LoadAgentsAsync();
        }

        private async void OnEditProviderClick(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string tag) return;
            var parts = tag.Split('\n');
            if (parts.Length < 2) return;
            if (await AgentActions.EditProviderModelsAsync(XamlRoot, _agentsService, _openClawService, parts[0], parts[1]))
                await LoadAgentsAsync();
        }

        private async void OnDeleteProviderClick(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string tag) return;
            var parts = tag.Split('\n');
            if (parts.Length < 2) return;
            if (await AgentActions.DeleteProviderAsync(XamlRoot, _agentsService, parts[0], parts[1]))
                await LoadAgentsAsync();
        }
    }
}
