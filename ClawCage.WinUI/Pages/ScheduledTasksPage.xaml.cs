using ClawCage.WinUI.Components.ScheduledTasks;
using ClawCage.WinUI.Model.ScheduledTasks;
using ClawCage.WinUI.Services.Cron;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace ClawCage.WinUI.Pages
{
    public sealed partial class ScheduledTasksPage : Page
    {
        private readonly CronConfigService _cronService = Ioc.Default.GetRequiredService<CronConfigService>();
        private List<CronJobConfig>? _jobs;

        private const double ColumnMinWidth = 380;
        private const double ColumnSpacing = 12;

        public ScheduledTasksPage()
        {
            InitializeComponent();
            Loaded += ScheduledTasksPage_Loaded;
            SizeChanged += (_, _) => RebuildWaterfallLayout();
        }

        private async void ScheduledTasksPage_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadJobsAsync();
        }

        private async void ReloadButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadJobsAsync();
        }

        private async Task LoadJobsAsync()
        {
            StatusText.Text = "读取中...";

            var result = await _cronService.LoadJobsAsync();
            if (result is null)
            {
                _jobs = null;
                StatusText.Text = "未找到计划任务配置";
                RebuildWaterfallLayout();
                return;
            }

            _jobs = result.Value.Jobs;
            StatusText.Text = _jobs.Count == 0
                ? "暂无定时任务"
                : $"已加载 {_jobs.Count} 个计划任务";

            RebuildWaterfallLayout();
        }

        // ── Waterfall layout ──

        private void RebuildWaterfallLayout()
        {
            TaskListPanel.Children.Clear();
            TaskListPanel.ColumnDefinitions.Clear();

            if (_jobs is null || _jobs.Count == 0)
            {
                EmptyHint.Visibility = Visibility.Visible;
                TaskListPanel.Children.Add(EmptyHint);
                return;
            }

            EmptyHint.Visibility = Visibility.Collapsed;

            var availableWidth = TaskListPanel.ActualWidth;
            if (availableWidth <= 0)
                availableWidth = ActualWidth - 48;
            if (availableWidth <= 0)
                availableWidth = 800;

            var colCount = Math.Max(1, (int)((availableWidth + ColumnSpacing) / (ColumnMinWidth + ColumnSpacing)));

            var columns = new StackPanel[colCount];
            for (var c = 0; c < colCount; c++)
            {
                TaskListPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                columns[c] = new StackPanel { Spacing = ColumnSpacing };
                if (c > 0)
                    columns[c].Margin = new Thickness(ColumnSpacing, 0, 0, 0);
                Grid.SetColumn(columns[c], c);
                TaskListPanel.Children.Add(columns[c]);
            }

            // Distribute cards round-robin into shortest column (simple waterfall)
            var heights = new int[colCount];
            foreach (var job in _jobs)
            {
                var shortest = 0;
                for (var c = 1; c < colCount; c++)
                {
                    if (heights[c] < heights[shortest])
                        shortest = c;
                }
                columns[shortest].Children.Add(CreateJobCard(job));
                heights[shortest]++;
            }
        }

        // ── Card ──

        private Border CreateJobCard(CronJobConfig job)
        {
            var card = new Border
            {
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(16, 14, 16, 14),
                Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["LayerFillColorDefaultBrush"],
                BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            };

            var root = new StackPanel { Spacing = 12 };

            // ── Header: icon + name/desc + toggle + actions ──
            var header = new Grid { ColumnSpacing = 12 };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Icon
            var stateLabel = job.State?.LastStatus ?? "-";
            var iconBg = stateLabel == "ok"
                ? (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorSuccessBackgroundBrush"]
                : job.Enabled
                    ? (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["AccentFillColorDefaultBrush"]
                    : (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SubtleFillColorTertiaryBrush"];
            var iconFg = job.Enabled && stateLabel == "ok"
                ? (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextOnAccentFillColorPrimaryBrush"]
                : null;
            var iconBorder = new Border
            {
                Width = 36,
                Height = 36,
                CornerRadius = new CornerRadius(8),
                Background = iconBg,
                VerticalAlignment = VerticalAlignment.Center,
                Child = new FontIcon
                {
                    FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets"),
                    Glyph = "\uE823",
                    FontSize = 15,
                    Foreground = iconFg,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            header.Children.Add(iconBorder);

            // Title + Description
            var titlePanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 1 };
            Grid.SetColumn(titlePanel, 1);

            titlePanel.Children.Add(new TextBlock
            {
                Text = job.Name,
                FontSize = 15,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            var descText = !string.IsNullOrWhiteSpace(job.Description) ? job.Description : job.Id;
            var descBlock = new TextBlock
            {
                Text = descText,
                FontSize = 12,
                Opacity = 0.55,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxLines = 1
            };
            ToolTipService.SetToolTip(descBlock, $"ID: {job.Id}  (点击复制)");
            descBlock.Tapped += (_, _) =>
            {
                var dp = new DataPackage();
                dp.SetText(job.Id);
                Clipboard.SetContent(dp);
            };
            titlePanel.Children.Add(descBlock);
            header.Children.Add(titlePanel);

            // Action buttons
            var headerActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(headerActions, 2);

            var toggle = new ToggleSwitch
            {
                IsOn = job.Enabled,
                OnContent = "",
                OffContent = "",
                MinWidth = 0,
                VerticalAlignment = VerticalAlignment.Center,
                Tag = job.Id
            };
            toggle.Toggled += OnJobEnabledToggled;
            headerActions.Children.Add(toggle);

            var editBtn = CreateIconButton("\uE70F", "修改", job.Id);
            editBtn.Click += OnEditJobClick;
            headerActions.Children.Add(editBtn);

            var recordsBtn = CreateIconButton("\uE81C", "执行记录", job.Id);
            recordsBtn.Click += OnViewRecordsClick;
            headerActions.Children.Add(recordsBtn);

            var deleteBtn = CreateIconButton("\uE74D", "删除", job.Id, true);
            deleteBtn.Click += OnDeleteJobClick;
            headerActions.Children.Add(deleteBtn);

            header.Children.Add(headerActions);
            root.Children.Add(header);

            // ── Details: key-value grid ──
            var details = new Border
            {
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(14, 10, 14, 10),
                Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"]
            };

            var detailGrid = new Grid { RowSpacing = 6, ColumnSpacing = 12 };
            detailGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(72) });
            detailGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var row = 0;

            // Schedule
            if (job.Schedule is not null)
            {
                AddDetailRow(detailGrid, row++, "调度", $"{FormatEveryMs(job.Schedule.EveryMs)}  ·  {job.Schedule.Kind}");
            }

            // Session / Wake / Agent
            AddDetailRow(detailGrid, row++, "会话", $"{job.SessionTarget}  ·  {job.WakeMode}");
            AddDetailRow(detailGrid, row++, "Agent", job.AgentId);

            // State info
            if (job.State is not null)
            {
                var lastRun = job.State.LastRunAtMs > 0
                    ? DateTimeOffset.FromUnixTimeMilliseconds(job.State.LastRunAtMs).LocalDateTime.ToString("MM-dd HH:mm:ss")
                    : "-";
                var nextRun = job.State.NextRunAtMs > 0
                    ? DateTimeOffset.FromUnixTimeMilliseconds(job.State.NextRunAtMs).LocalDateTime.ToString("MM-dd HH:mm:ss")
                    : "-";
                AddDetailRow(detailGrid, row++, "上次运行", $"{lastRun}  ({job.State.LastRunStatus}, {job.State.LastDurationMs}ms)");
                AddDetailRow(detailGrid, row++, "下次运行", nextRun);

                if (job.State.ConsecutiveErrors > 0)
                    AddDetailRow(detailGrid, row++, "连续错误", job.State.ConsecutiveErrors.ToString());
            }

            for (var i = 0; i < row; i++)
                detailGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            details.Child = detailGrid;
            root.Children.Add(details);

            // ── Tags row ──
            var tagsRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };

            tagsRow.Children.Add(CreateStatusPillTag(job.Enabled));
            tagsRow.Children.Add(CreatePillTag(stateLabel));

            if (job.DeleteAfterRun)
                tagsRow.Children.Add(CreatePillTag("运行后删除"));

            var created = job.CreatedAtMs > 0
                ? DateTimeOffset.FromUnixTimeMilliseconds(job.CreatedAtMs).LocalDateTime.ToString("yyyy-MM-dd HH:mm")
                : "-";
            tagsRow.Children.Add(CreatePillTag($"创建 {created}"));

            root.Children.Add(tagsRow);

            card.Child = root;
            return card;
        }

        private static void AddDetailRow(Grid grid, int row, string label, string value)
        {
            var labelBlock = new TextBlock
            {
                Text = label,
                FontSize = 12,
                Opacity = 0.5,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(labelBlock, row);
            grid.Children.Add(labelBlock);

            var valueBlock = new TextBlock
            {
                Text = value,
                FontSize = 13,
                Opacity = 0.85,
                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Cascadia Mono"),
                TextTrimming = TextTrimming.CharacterEllipsis,
                IsTextSelectionEnabled = true,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(valueBlock, row);
            Grid.SetColumn(valueBlock, 1);
            grid.Children.Add(valueBlock);
        }

        private static Border CreatePillTag(string text) => new()
        {
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(7, 2, 7, 2),
            Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SubtleFillColorTertiaryBrush"],
            Child = new TextBlock
            {
                Text = text,
                FontSize = 10,
                Opacity = 0.65
            }
        };

        private static Border CreateStatusPillTag(bool enabled)
        {
            var dot = new Border
            {
                Width = 8,
                Height = 8,
                CornerRadius = new CornerRadius(4),
                VerticalAlignment = VerticalAlignment.Center,
                Background = enabled
                    ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LimeGreen)
                    : new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Tomato)
            };
            var label = new TextBlock
            {
                Text = enabled ? "已启用" : "已禁用",
                FontSize = 10,
                Opacity = 0.65,
                VerticalAlignment = VerticalAlignment.Center
            };
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
            row.Children.Add(dot);
            row.Children.Add(label);
            return new Border
            {
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(7, 2, 7, 2),
                Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SubtleFillColorTertiaryBrush"],
                Child = row
            };
        }

        private static Button CreateIconButton(string glyph, string tooltip, string tag, bool isDanger = false)
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

        // ── Add ──

        private async void AddTaskButton_Click(object sender, RoutedEventArgs e)
        {
            var result = await AddScheduledTaskDialog.ShowAsync(XamlRoot);
            if (result is not null)
            {
                var jobs = _jobs ?? [];
                jobs.Add(result);
                await _cronService.SaveJobsAsync(jobs);
                await LoadJobsAsync();
            }
        }

        // ── Toggle ──

        private async void OnJobEnabledToggled(object sender, RoutedEventArgs e)
        {
            if (_jobs is null || sender is not ToggleSwitch ts || ts.Tag is not string id)
                return;

            if (await ScheduledTaskActions.ToggleEnabledAsync(_cronService, _jobs, id, ts.IsOn))
                await LoadJobsAsync();
        }

        // ── Edit ──

        private async void OnEditJobClick(object sender, RoutedEventArgs e)
        {
            if (_jobs is null || sender is not Button btn || btn.Tag is not string id) return;

            StatusText.Text = "保存中...";
            if (await ScheduledTaskActions.EditJobAsync(XamlRoot, _cronService, _jobs, id))
                await LoadJobsAsync();
            else
                StatusText.Text = "就绪";
        }

        // ── Delete ──

        private async void OnDeleteJobClick(object sender, RoutedEventArgs e)
        {
            if (_jobs is null || sender is not Button btn || btn.Tag is not string id) return;

            if (await ScheduledTaskActions.DeleteJobAsync(XamlRoot, _cronService, _jobs, id))
                await LoadJobsAsync();
        }

        // ── Records ──

        private async void OnViewRecordsClick(object sender, RoutedEventArgs e)
        {
            if (_jobs is null || sender is not Button btn || btn.Tag is not string id) return;
            var job = _jobs.FirstOrDefault(j => j.Id == id);
            if (job is null) return;

            await CronJobRunRecordsDialog.ShowAsync(XamlRoot, _cronService, id, job.Name);
        }

        // ── Helpers ──

        private static string FormatEveryMs(long ms)
        {
            if (ms >= 86400000 && ms % 86400000 == 0) return $"每 {ms / 86400000} 天";
            if (ms >= 3600000 && ms % 3600000 == 0) return $"每 {ms / 3600000} 小时";
            return $"每 {ms / 60000} 分钟";
        }
    }
}
