using ClawCage.WinUI.Model.ScheduledTasks;
using ClawCage.WinUI.Services.Cron;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;

namespace ClawCage.WinUI.Components.ScheduledTasks
{
    internal static class CronJobRunRecordsDialog
    {
        private const int PageSize = 20;

        internal static async Task ShowAsync(XamlRoot xamlRoot, CronConfigService cronService, string jobId, string jobName)
        {
            var allRecords = await cronService.LoadRunRecordsAsync(jobId);
            allRecords.Reverse();

            var totalCount = allRecords.Count;
            var totalPages = Math.Max(1, (int)Math.Ceiling((double)totalCount / PageSize));
            var currentPage = 0;

            var recordsPanel = new StackPanel { Spacing = 6 };
            var pageInfoText = new TextBlock
            {
                FontSize = 12,
                Opacity = 0.55,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var prevBtn = new Button
            {
                Content = new FontIcon
                {
                    FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets"),
                    Glyph = "\uE76B",
                    FontSize = 12
                },
                Width = 28,
                Height = 28,
                Padding = new Thickness(0),
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderThickness = new Thickness(0)
            };
            var nextBtn = new Button
            {
                Content = new FontIcon
                {
                    FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets"),
                    Glyph = "\uE76C",
                    FontSize = 12
                },
                Width = 28,
                Height = 28,
                Padding = new Thickness(0),
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderThickness = new Thickness(0)
            };

            var pagerRow = new Grid { Margin = new Thickness(0, 4, 0, 0) };
            pagerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            pagerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            pagerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(pageInfoText, 1);
            Grid.SetColumn(nextBtn, 2);
            pagerRow.Children.Add(prevBtn);
            pagerRow.Children.Add(pageInfoText);
            pagerRow.Children.Add(nextBtn);

            void RenderPage()
            {
                recordsPanel.Children.Clear();

                if (totalCount == 0)
                {
                    recordsPanel.Children.Add(new TextBlock
                    {
                        Text = "暂无执行记录",
                        FontSize = 13,
                        Opacity = 0.5,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 32, 0, 0)
                    });
                    pageInfoText.Text = "0 / 0";
                    prevBtn.IsEnabled = false;
                    nextBtn.IsEnabled = false;
                    return;
                }

                var start = currentPage * PageSize;
                var end = Math.Min(start + PageSize, totalCount);

                for (var i = start; i < end; i++)
                {
                    recordsPanel.Children.Add(CreateRecordCard(allRecords[i], i + 1));
                }

                pageInfoText.Text = $"第 {currentPage + 1} / {totalPages} 页  （共 {totalCount} 条）";
                prevBtn.IsEnabled = currentPage > 0;
                nextBtn.IsEnabled = currentPage < totalPages - 1;
            }

            prevBtn.Click += (_, _) => { if (currentPage > 0) { currentPage--; RenderPage(); } };
            nextBtn.Click += (_, _) => { if (currentPage < totalPages - 1) { currentPage++; RenderPage(); } };

            RenderPage();

            var root = new StackPanel { Spacing = 12 };

            // ── Summary tags ──
            var summaryRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            summaryRow.Children.Add(CreateSummaryPill("总记录", totalCount.ToString()));

            if (totalCount > 0)
            {
                var okCount = 0;
                var errCount = 0;
                foreach (var r in allRecords)
                {
                    if (r.Status == "ok") okCount++; else errCount++;
                }
                summaryRow.Children.Add(CreateSummaryPill("成功", okCount.ToString(),
                    (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorSuccessBackgroundBrush"]));
                if (errCount > 0)
                    summaryRow.Children.Add(CreateSummaryPill("失败", errCount.ToString(),
                        (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorCautionBackgroundBrush"]));
            }
            root.Children.Add(summaryRow);

            // ── Divider ──
            root.Children.Add(new Border
            {
                Height = 1,
                Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"]
            });

            root.Children.Add(recordsPanel);
            root.Children.Add(pagerRow);

            var windowWidth = xamlRoot.Size.Width;
            var dialogWidth = windowWidth > 0 ? windowWidth * 0.8 : 820;

            var scrollViewer = new ScrollViewer
            {
                Content = root,
                MaxHeight = 520,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };

            var dialog = new ContentDialog
            {
                Title = $"执行记录 — {jobName}",
                CloseButtonText = "关闭",
                XamlRoot = xamlRoot,
                Content = scrollViewer
            };
            dialog.Resources["ContentDialogMinWidth"] = dialogWidth;
            dialog.Resources["ContentDialogMaxWidth"] = dialogWidth;

            var tcs = new TaskCompletionSource<ContentDialogResult>();
            var op = dialog.ShowAsync();
            op.Completed = (o, s) => tcs.TrySetResult(ContentDialogResult.None);
            await tcs.Task;
        }

        private static Border CreateRecordCard(CronJobRunRecord rec, int index)
        {
            var runTime = DateTimeOffset.FromUnixTimeMilliseconds(rec.RunAtMs).LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss");
            var isOk = rec.Status == "ok";

            var card = new Border
            {
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(14, 12, 14, 12),
                Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["LayerFillColorDefaultBrush"],
                BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            };

            var grid = new Grid { ColumnSpacing = 12 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Column 0: status indicator (dot + index)
            var indicatorPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 4, HorizontalAlignment = HorizontalAlignment.Center };
            var dot = new Border
            {
                Width = 10,
                Height = 10,
                CornerRadius = new CornerRadius(5),
                HorizontalAlignment = HorizontalAlignment.Center,
                Background = isOk
                    ? (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorSuccessBackgroundBrush"]
                    : (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorCautionBackgroundBrush"]
            };
            indicatorPanel.Children.Add(dot);
            indicatorPanel.Children.Add(new TextBlock
            {
                Text = $"#{index}",
                FontSize = 10,
                Opacity = 0.35,
                HorizontalTextAlignment = TextAlignment.Center
            });
            grid.Children.Add(indicatorPanel);

            // Column 1: content
            var content = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 6 };
            Grid.SetColumn(content, 1);

            // Title row: time + action
            var titleRow = new Grid();
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            titleRow.Children.Add(new TextBlock
            {
                Text = runTime,
                FontSize = 13,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            });

            var actionTag = new Border
            {
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(7, 2, 7, 2),
                VerticalAlignment = VerticalAlignment.Center,
                Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SubtleFillColorTertiaryBrush"],
                Child = new TextBlock
                {
                    Text = rec.Action,
                    FontSize = 10,
                    Opacity = 0.65
                }
            };
            Grid.SetColumn(actionTag, 1);
            titleRow.Children.Add(actionTag);

            content.Children.Add(titleRow);

            // Summary
            if (!string.IsNullOrWhiteSpace(rec.Summary))
            {
                content.Children.Add(new TextBlock
                {
                    Text = rec.Summary,
                    FontSize = 12,
                    Opacity = 0.55,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxLines = 2
                });
            }

            // Detail tags
            var tagsRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            tagsRow.Children.Add(CreatePillTag(rec.Status, isOk
                ? (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorSuccessBackgroundBrush"]
                : (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorCautionBackgroundBrush"]));
            tagsRow.Children.Add(CreatePillTag($"{rec.DurationMs}ms"));
            tagsRow.Children.Add(CreatePillTag(rec.DeliveryStatus));
            content.Children.Add(tagsRow);

            grid.Children.Add(content);

            card.Child = grid;
            return card;
        }

        private static Border CreatePillTag(string text, Microsoft.UI.Xaml.Media.Brush? bg = null) => new()
        {
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(7, 2, 7, 2),
            Background = bg ?? (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SubtleFillColorTertiaryBrush"],
            Child = new TextBlock
            {
                Text = text,
                FontSize = 10,
                Opacity = 0.65,
                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Cascadia Mono")
            }
        };

        private static Border CreateSummaryPill(string label, string value, Microsoft.UI.Xaml.Media.Brush? bg = null)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            panel.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 12,
                Opacity = 0.5,
                VerticalAlignment = VerticalAlignment.Center
            });
            panel.Children.Add(new TextBlock
            {
                Text = value,
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            });

            return new Border
            {
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10, 4, 10, 4),
                Background = bg ?? (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SubtleFillColorTertiaryBrush"],
                Child = panel
            };
        }
    }
}
