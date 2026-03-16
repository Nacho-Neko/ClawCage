using ClawCage.WinUI.Model.Agents;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ClawCage.WinUI.Components.Agents
{
    internal static class AgentCardBuilder
    {
        internal const string MainAgentId = "main";

        internal static UIElement CreateAgentCard(
            string agentId,
            AgentModelsConfig? config,
            RoutedEventHandler onAddProvider,
            RoutedEventHandler onDeleteAgent,
            RoutedEventHandler onEditProvider,
            RoutedEventHandler onDeleteProvider)
        {
            var providerCount = config?.Providers.Count ?? 0;
            var modelCount = 0;
            if (config is not null)
            {
                foreach (var provider in config.Providers.Values)
                    modelCount += provider.Models.Count;
            }

            // Header
            var header = new Grid { ColumnSpacing = 12 };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var iconBorder = new Border
            {
                Width = 36,
                Height = 36,
                CornerRadius = new CornerRadius(8),
                Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["AccentFillColorDefaultBrush"],
                VerticalAlignment = VerticalAlignment.Center,
                Child = new FontIcon
                {
                    FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets"),
                    Glyph = "\uE716",
                    FontSize = 15,
                    Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextOnAccentFillColorPrimaryBrush"],
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            header.Children.Add(iconBorder);

            var titlePanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 1 };
            Grid.SetColumn(titlePanel, 1);
            titlePanel.Children.Add(new TextBlock
            {
                Text = agentId,
                FontSize = 15,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });
            titlePanel.Children.Add(new TextBlock
            {
                Text = $"{providerCount} 个提供商 · {modelCount} 个模型",
                FontSize = 12,
                Opacity = 0.55
            });
            header.Children.Add(titlePanel);

            // Action buttons
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(actions, 2);

            var addProviderBtn = CreateIconButton("\uE710", "添加提供商", agentId);
            addProviderBtn.Click += onAddProvider;
            actions.Children.Add(addProviderBtn);

            var isMain = string.Equals(agentId, MainAgentId, System.StringComparison.OrdinalIgnoreCase);
            var deleteBtn = CreateIconButton("\uE74D", isMain ? "主代理不可删除" : "删除代理", agentId, true);
            deleteBtn.IsEnabled = !isMain;
            deleteBtn.Click += onDeleteAgent;
            actions.Children.Add(deleteBtn);

            header.Children.Add(actions);

            // Expander content: providers & models
            var contentPanel = new StackPanel { Spacing = 8 };

            if (config is not null)
            {
                foreach (var (providerName, provider) in config.Providers)
                {
                    contentPanel.Children.Add(CreateProviderSection(agentId, providerName, provider, onEditProvider, onDeleteProvider));
                }
            }

            if (contentPanel.Children.Count == 0)
            {
                contentPanel.Children.Add(new TextBlock
                {
                    Text = "暂无提供商配置",
                    FontSize = 12,
                    Opacity = 0.45,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 8, 0, 8)
                });
            }

            return new Expander
            {
                Header = header,
                Content = contentPanel,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };
        }

        internal static Border CreateProviderSection(
            string agentId,
            string providerName,
            AgentProvider provider,
            RoutedEventHandler onEditProvider,
            RoutedEventHandler onDeleteProvider)
        {
            var border = new Border
            {
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(14, 10, 14, 10),
                Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"]
            };

            var root = new StackPanel { Spacing = 8 };

            // Provider header
            var provHeader = new Grid { ColumnSpacing = 8 };
            provHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            provHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var provTitle = new StackPanel { Spacing = 1 };
            provTitle.Children.Add(new TextBlock
            {
                Text = providerName,
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });
            provTitle.Children.Add(new TextBlock
            {
                Text = $"{provider.Api}  ·  {provider.Models.Count} 个模型",
                FontSize = 11,
                Opacity = 0.5
            });
            provHeader.Children.Add(provTitle);

            var provActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(provActions, 1);

            var editProvBtn = CreateIconButton("\uE70F", "管理模型", $"{agentId}\n{providerName}");
            editProvBtn.Click += onEditProvider;
            provActions.Children.Add(editProvBtn);

            var delProvBtn = CreateIconButton("\uE74D", "删除提供商", $"{agentId}\n{providerName}", true);
            delProvBtn.Click += onDeleteProvider;
            provActions.Children.Add(delProvBtn);

            provHeader.Children.Add(provActions);
            root.Children.Add(provHeader);

            // Provider info grid
            var infoGrid = new Grid { RowSpacing = 4, ColumnSpacing = 12 };
            infoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
            infoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var r = 0;
            AddDetailRow(infoGrid, r++, "BaseUrl", provider.BaseUrl);
            AddDetailRow(infoGrid, r++, "ApiKey", MaskApiKey(provider.ApiKey));

            for (var i = 0; i < r; i++)
                infoGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            root.Children.Add(infoGrid);

            // Models
            if (provider.Models.Count > 0)
            {
                var modelsWrap = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                foreach (var model in provider.Models)
                {
                    modelsWrap.Children.Add(CreateModelTag(model));
                }
                root.Children.Add(modelsWrap);
            }

            border.Child = root;
            return border;
        }

        internal static Border CreateModelTag(AgentModel model)
        {
            return new Border
            {
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(8, 3, 8, 3),
                Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SubtleFillColorTertiaryBrush"],
                Child = new TextBlock
                {
                    Text = model.Name,
                    FontSize = 11,
                    Opacity = 0.75,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
        }

        internal static Button CreateIconButton(string glyph, string tooltip, string tag, bool isDanger = false)
        {
            var icon = new FontIcon
            {
                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets"),
                Glyph = glyph,
                FontSize = 11
            };
            if (isDanger)
                icon.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorCriticalBrush"];

            var btn = new Button
            {
                Width = 28,
                Height = 28,
                Padding = new Thickness(0),
                Tag = tag,
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderThickness = new Thickness(0),
                Content = icon
            };
            ToolTipService.SetToolTip(btn, tooltip);
            return btn;
        }

        private static void AddDetailRow(Grid grid, int row, string label, string value)
        {
            var labelBlock = new TextBlock
            {
                Text = label,
                FontSize = 11,
                Opacity = 0.45,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(labelBlock, row);
            grid.Children.Add(labelBlock);

            var valueBlock = new TextBlock
            {
                Text = value,
                FontSize = 12,
                Opacity = 0.75,
                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Cascadia Mono"),
                TextTrimming = TextTrimming.CharacterEllipsis,
                IsTextSelectionEnabled = true,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(valueBlock, row);
            Grid.SetColumn(valueBlock, 1);
            grid.Children.Add(valueBlock);
        }

        private static string MaskApiKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return "-";
            if (key.Length <= 8) return "••••••••";
            return key[..4] + "••••" + key[^4..];
        }
    }
}
