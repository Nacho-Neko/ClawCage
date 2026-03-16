using ClawCage.WinUI.Model.ScheduledTasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;

namespace ClawCage.WinUI.Pages
{
    public sealed partial class CronJobRunRecordsPage : Page
    {
        public CronJobRunRecordsPage()
        {
            InitializeComponent();
        }

        public void LoadRecords(string jobName, string jobId, List<CronJobRunRecord> records)
        {
            TitleText.Text = $"执行记录 - {jobName}";
            JobInfoText.Text = $"任务 ID: {jobId}";
            RunRecordsPanel.Children.Clear();

            if (records.Count == 0)
            {
                RunRecordsPanel.Children.Add(new TextBlock
                {
                    Text = "暂无执行记录",
                    FontSize = 13,
                    Opacity = 0.5,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 48, 0, 0)
                });
                return;
            }

            for (var i = records.Count - 1; i >= 0; i--)
            {
                RunRecordsPanel.Children.Add(CreateRecordCard(records[i]));
            }
        }

        private static Border CreateRecordCard(CronJobRunRecord rec)
        {
            var runTime = DateTimeOffset.FromUnixTimeMilliseconds(rec.RunAtMs).LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss");

            var card = new Border
            {
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(14, 8, 14, 8),
                Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["LayerFillColorDefaultBrush"],
                BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            };

            var grid = new Grid { ColumnSpacing = 12 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 2 };
            info.Children.Add(new TextBlock
            {
                Text = $"{runTime}  ·  {rec.Status}",
                FontSize = 13,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });
            if (!string.IsNullOrWhiteSpace(rec.Summary))
            {
                info.Children.Add(new TextBlock
                {
                    Text = rec.Summary,
                    FontSize = 11,
                    Opacity = 0.6,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxLines = 2
                });
            }
            info.Children.Add(new TextBlock
            {
                Text = $"用时 {rec.DurationMs}ms · 投递: {rec.DeliveryStatus}",
                FontSize = 11,
                Opacity = 0.5,
                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Cascadia Mono")
            });

            var actionBadge = new TextBlock
            {
                Text = rec.Action,
                FontSize = 11,
                Opacity = 0.55,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(actionBadge, 1);

            grid.Children.Add(info);
            grid.Children.Add(actionBadge);
            card.Child = grid;
            return card;
        }
    }
}
