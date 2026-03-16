using ClawCage.WinUI.Model.ScheduledTasks;
using ClawCage.WinUI.Services.Agents;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using Windows.Foundation;

namespace ClawCage.WinUI.Components.ScheduledTasks
{
    internal static class AddScheduledTaskDialog
    {
        internal static Task<CronJobConfig?> ShowAsync(XamlRoot xamlRoot)
            => ShowCoreAsync(xamlRoot, null);

        internal static Task<CronJobConfig?> ShowEditAsync(XamlRoot xamlRoot, CronJobConfig existing)
            => ShowCoreAsync(xamlRoot, existing);

        private static async Task<CronJobConfig?> ShowCoreAsync(XamlRoot xamlRoot, CronJobConfig? existing)
        {
            var isEdit = existing is not null;

            // ── 基本信息 ──
            var nameBox = new TextBox
            {
                Header = "名称 *",
                PlaceholderText = "定时任务",
                Text = existing?.Name ?? ""
            };
            var descBox = new TextBox
            {
                Header = "描述",
                PlaceholderText = "此任务的说明(可选)",
                Text = existing?.Description ?? ""
            };
            var agentCombo = new ComboBox
            {
                Header = "代理 ID",
                PlaceholderText = "选择代理…",
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var agentsService = Ioc.Default.GetRequiredService<AgentsConfigService>();
            var agentIds = await agentsService.ListAgentIdsAsync();
            foreach (var id in agentIds)
                agentCombo.Items.Add(id);

            var currentAgentId = existing?.AgentId ?? "main";
            if (agentCombo.Items.Contains(currentAgentId))
                agentCombo.SelectedItem = currentAgentId;
            else
            {
                agentCombo.Items.Add(currentAgentId);
                agentCombo.SelectedItem = currentAgentId;
            }

            var basicSection = CreateSection("基本信息", "命名并选择助手。",
                nameBox, descBox, agentCombo);

            // ── 调度 ──
            var everyMs = existing?.Schedule?.EveryMs ?? 1800000;
            long everyValue;
            int unitIndex;
            if (everyMs % 86400000 == 0) { everyValue = everyMs / 86400000; unitIndex = 2; }
            else if (everyMs % 3600000 == 0) { everyValue = everyMs / 3600000; unitIndex = 1; }
            else { everyValue = everyMs / 60000; unitIndex = 0; }

            var everyBox = new NumberBox
            {
                Header = "每隔 *",
                PlaceholderText = "30",
                Value = everyValue,
                Minimum = 1,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact
            };
            var unitCombo = new ComboBox
            {
                Header = "单位",
                Items = { "分钟", "小时", "天" },
                SelectedIndex = unitIndex,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var scheduleRow = new Grid { ColumnSpacing = 12 };
            scheduleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            scheduleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(unitCombo, 1);
            scheduleRow.Children.Add(everyBox);
            scheduleRow.Children.Add(unitCombo);

            var scheduleSection = CreateSection("调度", "控制任务运行时间。", scheduleRow);

            // ── 执行 ──
            var sessionCombo = new ComboBox
            {
                Header = "会话目标",
                Items = { "main", "isolated" },
                SelectedIndex = (existing?.SessionTarget == "isolated") ? 1 : 0,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            var sessionHint = CreateHint("主会话发布系统事件。隔离会话运行独立的代理轮次。");

            // 当代理为 main 时，会话目标只能为 main
            void ApplyAgentSessionConstraint()
            {
                var isMain = agentCombo.SelectedItem?.ToString() == "main";
                if (isMain)
                {
                    sessionCombo.SelectedIndex = 0;
                    sessionCombo.IsEnabled = false;
                }
                else
                {
                    sessionCombo.IsEnabled = true;
                }
            }

            agentCombo.SelectionChanged += (_, _) => ApplyAgentSessionConstraint();
            ApplyAgentSessionConstraint();

            var wakeCombo = new ComboBox
            {
                Header = "唤醒模式",
                Items = { "next-heartbeat", "immediate" },
                SelectedIndex = (existing?.WakeMode == "immediate") ? 1 : 0,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            var wakeHint = CreateHint("立即模式立即触发。下次心跳等待下一个周期。");

            var payloadKindCombo = new ComboBox
            {
                Header = "负载类型",
                Items = { "systemEvent", "runAgent" },
                SelectedIndex = (existing?.Payload?.Kind == "runAgent") ? 1 : 0,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var promptBox = new TextBox
            {
                Header = "负载文本 *",
                PlaceholderText = "请输入任务内容…",
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                MinHeight = 72,
                Text = existing?.Payload?.Text ?? ""
            };

            var execSection = CreateSection("执行", "选择唤醒时机和任务执行内容。",
                sessionCombo, sessionHint,
                wakeCombo, wakeHint,
                payloadKindCombo,
                promptBox);

            // ── 投递 ──
            var deliveryCombo = new ComboBox
            {
                Header = "结果投递",
                Items = { "none", "announce" },
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            var existingMode = existing?.Delivery?.Mode ?? "none";
            if (existingMode == "announce") deliveryCombo.SelectedIndex = 1;
            else deliveryCombo.SelectedIndex = 0;
            var deliveryHint = CreateHint("announce 将摘要发送到聊天。none 仅内部执行。");

            var channelCombo = new ComboBox
            {
                Header = "频道",
                Items = { "last" },
                HorizontalAlignment = HorizontalAlignment.Stretch,
                IsEditable = true
            };
            channelCombo.Text = existing?.Delivery?.Channel ?? "last";

            var accountIdBox = new TextBox
            {
                Header = "Account ID",
                PlaceholderText = "default",
                Text = existing?.Delivery?.AccountId ?? ""
            };
            var accountIdHint = CreateHint("多账户环境下的可选频道账户 ID。");

            var bestEffortToggle = new ToggleSwitch
            {
                Header = "尽力投递",
                OnContent = "",
                OffContent = "",
                IsOn = existing?.Delivery?.BestEffort ?? true
            };
            var bestEffortHint = CreateHint("投递失败时不使任务失败。");

            var deliverySection = CreateSection("投递", "选择运行摘要的发送位置。",
                deliveryCombo, deliveryHint,
                channelCombo,
                accountIdBox, accountIdHint);

            // ── 高级（折叠） ──
            var deleteAfterRunToggle = new ToggleSwitch
            {
                Header = "运行后删除",
                OnContent = "",
                OffContent = "",
                IsOn = existing?.DeleteAfterRun ?? false
            };
            var deleteAfterRunHint = CreateHint("适用于应自动清理的一次性提醒。");

            var clearAgentOverrideToggle = new ToggleSwitch
            {
                Header = "清除代理覆盖",
                OnContent = "",
                OffContent = "",
                IsOn = existing?.ClearAgentOverride ?? false
            };
            var clearAgentOverrideHint = CreateHint("强制此任务使用网关默认助手。");

            var sessionKeyBox = new TextBox
            {
                Header = "Session key",
                PlaceholderText = "agent:main:main",
                Text = existing?.SessionKey ?? ""
            };
            var sessionKeyHint = CreateHint("可选的路由 key，用于任务投递和唤醒路由。");

            var lightContextToggle = new ToggleSwitch
            {
                Header = "Light context",
                OnContent = "",
                OffContent = "",
                IsOn = existing?.Payload?.LightContext ?? false
            };
            var lightContextHint = CreateHint("使用轻量级引导上下文运行此代理任务。");

            var modelBox = new TextBox
            {
                Header = "模型",
                PlaceholderText = "openai/gpt-5.2",
                Text = existing?.Payload?.Model ?? ""
            };
            var modelHint = CreateHint("输入以选择已知模型，或输入自定义模型标识。");

            var thinkingBox = new TextBox
            {
                Header = "思考",
                PlaceholderText = "low",
                Text = existing?.Payload?.Thinking ?? ""
            };
            var thinkingHint = CreateHint("使用建议级别或输入提供商特定值。");

            // ── 失败告警 ──
            // 0=继承全局设置, 1=禁用, 2=自定义设置
            int failureAlertIndex;
            if (existing?.FailureAlert is not null && existing.FailureAlert.Mode == "none")
                failureAlertIndex = 1;
            else if (existing?.FailureAlert is not null)
                failureAlertIndex = 2;
            else
                failureAlertIndex = 0;

            var failureAlertCombo = new ComboBox
            {
                Header = "Failure alerts",
                Items = { "继承全局设置", "禁用", "自定义设置" },
                SelectedIndex = failureAlertIndex,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            var failureAlertHint = CreateHint("控制此任务在连续失败时的告警行为。");

            var failureAfterBox = new NumberBox
            {
                Header = "连续失败次数",
                PlaceholderText = "2",
                Value = existing?.FailureAlert?.After ?? 2,
                Minimum = 1,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact
            };

            var failureChannelCombo = new ComboBox
            {
                Header = "告警频道",
                Items = { "last" },
                HorizontalAlignment = HorizontalAlignment.Stretch,
                IsEditable = true
            };
            failureChannelCombo.Text = existing?.FailureAlert?.Channel ?? "last";

            var failureToBox = new TextBox
            {
                Header = "告警接收者",
                PlaceholderText = "用户 ID 或名称",
                Text = existing?.FailureAlert?.To ?? ""
            };

            var failureCooldownBox = new NumberBox
            {
                Header = "冷却时间 (分钟)",
                PlaceholderText = "60",
                Value = (existing?.FailureAlert?.CooldownMs ?? 3600000) / 60000.0,
                Minimum = 1,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact
            };

            var failureModeCombo = new ComboBox
            {
                Header = "告警模式",
                Items = { "announce" },
                HorizontalAlignment = HorizontalAlignment.Stretch,
                IsEditable = true
            };
            failureModeCombo.Text = existing?.FailureAlert?.Mode ?? "announce";

            var failurePanel = new StackPanel { Spacing = 10 };
            AddChildren(failurePanel, failureAfterBox, failureChannelCombo, failureToBox, failureCooldownBox, failureModeCombo);
            failurePanel.Visibility = failureAlertCombo.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
            failureAlertCombo.SelectionChanged += (_, _) =>
            {
                failurePanel.Visibility = failureAlertCombo.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
            };

            var advancedContent = new StackPanel { Spacing = 10 };
            AddChildren(advancedContent,
                deleteAfterRunToggle, deleteAfterRunHint,
                clearAgentOverrideToggle, clearAgentOverrideHint,
                sessionKeyBox, sessionKeyHint,
                bestEffortToggle, bestEffortHint,
                lightContextToggle, lightContextHint,
                modelBox, modelHint,
                thinkingBox, thinkingHint,
                failureAlertCombo, failureAlertHint,
                failurePanel);

            var advancedExpander = new Expander
            {
                Header = "高级",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Content = advancedContent
            };

            // ── Assemble ──
            var root = new StackPanel { Spacing = 20, MinWidth = 520 };
            root.Children.Add(basicSection);
            root.Children.Add(scheduleSection);
            root.Children.Add(execSection);
            root.Children.Add(deliverySection);
            root.Children.Add(advancedExpander);

            var scrollViewer = new ScrollViewer
            {
                Content = root,
                MaxHeight = 520,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };

            var dialog = new ContentDialog
            {
                Title = isEdit ? "修改定时任务" : "新增定时任务",
                PrimaryButtonText = isEdit ? "保存" : "创建",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = xamlRoot,
                Content = scrollViewer
            };

            dialog.PrimaryButtonClick += (_, args) =>
            {
                if (string.IsNullOrWhiteSpace(nameBox.Text))
                {
                    nameBox.Focus(FocusState.Programmatic);
                    args.Cancel = true;
                    return;
                }
                if (string.IsNullOrWhiteSpace(promptBox.Text))
                {
                    promptBox.Focus(FocusState.Programmatic);
                    args.Cancel = true;
                    return;
                }
            };

            var result = await ShowDialogAsync(dialog);
            if (result != ContentDialogResult.Primary)
                return null;

            long unitMultiplier = unitCombo.SelectedIndex switch
            {
                1 => 3600000,
                2 => 86400000,
                _ => 60000
            };
            var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var job = existing ?? new CronJobConfig();
            if (!isEdit)
            {
                job.Id = Guid.NewGuid().ToString();
                job.CreatedAtMs = nowMs;
            }
            job.UpdatedAtMs = nowMs;
            job.Name = nameBox.Text.Trim();
            job.Description = string.IsNullOrWhiteSpace(descBox.Text) ? null : descBox.Text.Trim();
            job.AgentId = agentCombo.SelectedItem?.ToString() ?? "main";
            job.Enabled = existing?.Enabled ?? true;
            job.Schedule = new CronJobSchedule
            {
                Kind = "every",
                EveryMs = (long)(double.IsNaN(everyBox.Value) ? 30 : everyBox.Value) * unitMultiplier,
                AnchorMs = existing?.Schedule?.AnchorMs ?? nowMs
            };
            job.SessionTarget = sessionCombo.SelectedItem?.ToString() ?? "main";
            job.WakeMode = wakeCombo.SelectedItem?.ToString() ?? "next-heartbeat";
            job.Payload = new CronJobPayload
            {
                Kind = payloadKindCombo.SelectedItem?.ToString() ?? "systemEvent",
                Text = promptBox.Text.Trim(),
                Message = string.IsNullOrWhiteSpace(promptBox.Text) ? null : promptBox.Text.Trim(),
                Model = string.IsNullOrWhiteSpace(modelBox.Text) ? null : modelBox.Text.Trim(),
                Thinking = string.IsNullOrWhiteSpace(thinkingBox.Text) ? null : thinkingBox.Text.Trim(),
                LightContext = lightContextToggle.IsOn
            };
            job.Delivery = new CronJobDelivery
            {
                Mode = deliveryCombo.SelectedItem?.ToString() ?? "none",
                Channel = string.IsNullOrWhiteSpace(channelCombo.Text) ? null : channelCombo.Text.Trim(),
                AccountId = string.IsNullOrWhiteSpace(accountIdBox.Text) ? null : accountIdBox.Text.Trim(),
                BestEffort = bestEffortToggle.IsOn
            };
            job.DeleteAfterRun = deleteAfterRunToggle.IsOn;
            job.ClearAgentOverride = clearAgentOverrideToggle.IsOn;
            job.SessionKey = string.IsNullOrWhiteSpace(sessionKeyBox.Text) ? null : sessionKeyBox.Text.Trim();

            if (failureAlertCombo.SelectedIndex == 1)
            {
                // 禁用 → mode=none
                job.FailureAlert = new CronJobFailureAlert { Mode = "none" };
            }
            else if (failureAlertCombo.SelectedIndex == 2)
            {
                // 自定义设置
                job.FailureAlert = new CronJobFailureAlert
                {
                    After = (int)(double.IsNaN(failureAfterBox.Value) ? 2 : failureAfterBox.Value),
                    Channel = string.IsNullOrWhiteSpace(failureChannelCombo.Text) ? null : failureChannelCombo.Text.Trim(),
                    To = string.IsNullOrWhiteSpace(failureToBox.Text) ? null : failureToBox.Text.Trim(),
                    CooldownMs = (long)(double.IsNaN(failureCooldownBox.Value) ? 60 : failureCooldownBox.Value) * 60000,
                    Mode = string.IsNullOrWhiteSpace(failureModeCombo.Text) ? "announce" : failureModeCombo.Text.Trim()
                };
            }
            else
            {
                // 继承全局设置 → null
                job.FailureAlert = null;
            }

            return job;
        }

        private static StackPanel CreateSection(string title, string subtitle, params UIElement[] children)
        {
            var panel = new StackPanel { Spacing = 10 };

            panel.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 16,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });
            panel.Children.Add(new TextBlock
            {
                Text = subtitle,
                FontSize = 12,
                Opacity = 0.6,
                Margin = new Thickness(0, -6, 0, 0)
            });

            AddChildren(panel, children);
            return panel;
        }

        private static TextBlock CreateHint(string text) =>
            new()
            {
                Text = text,
                FontSize = 11,
                Opacity = 0.5,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, -6, 0, 0)
            };

        private static void AddChildren(StackPanel panel, params UIElement[] children)
        {
            foreach (var child in children)
                panel.Children.Add(child);
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
    }
}
