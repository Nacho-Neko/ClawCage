using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Windows.Foundation;

namespace ClawCage.WinUI.Components.Integrations
{
    /// <summary>
    /// Shows a configuration dialog whose form fields are generated automatically
    /// from <see cref="IIntegrationWizardComponent.ConfigFields"/>.
    /// </summary>
    internal static class ChannelConfigDialog
    {
        /// <summary>
        /// Shows the config dialog and returns the edited <see cref="JsonObject"/>,
        /// or <c>null</c> if the user cancelled.
        /// </summary>
        internal static async Task<JsonObject?> ShowAsync(
            XamlRoot xamlRoot,
            IIntegrationWizardComponent component,
            JsonObject? existingData)
        {
            var isNew = existingData is null;
            var data = existingData ?? BuildDefaultData(component);

            var fieldControls = new List<(ChannelConfigField Field, FrameworkElement Control)>();
            // Map field name → list of dependent controls for conditional visibility
            var dependentControls = new Dictionary<string, List<(ChannelConfigField Field, FrameworkElement Control)>>(StringComparer.OrdinalIgnoreCase);
            var configPanel = new StackPanel { Spacing = 10 };

            foreach (var field in component.ConfigFields)
            {
                var currentValue = data.TryGetPropertyValue(field.Name, out var node) ? node : null;
                FrameworkElement control;

                switch (field.FieldType)
                {
                    case ChannelConfigFieldType.Bool:
                        {
                            var val = currentValue is JsonValue jv && jv.TryGetValue<bool>(out var bv) ? bv
                                : field.DefaultValue is bool db && db;
                            control = new ToggleSwitch
                            {
                                IsOn = val,
                                Header = BuildHeader(field),
                                Tag = field.Name
                            };
                            break;
                        }
                    case ChannelConfigFieldType.Int:
                        {
                            var val = currentValue is JsonValue jv && jv.TryGetValue<int>(out var iv) ? iv.ToString()
                                : field.DefaultValue?.ToString() ?? "";
                            var box = CreateTextBox(field, val);
                            box.BeforeTextChanging += (s, args) =>
                            {
                                args.Cancel = !string.IsNullOrEmpty(args.NewText) && !int.TryParse(args.NewText, out _);
                            };
                            control = box;
                            break;
                        }
                    case ChannelConfigFieldType.Combo:
                        {
                            var val = currentValue?.ToString() ?? field.DefaultValue?.ToString() ?? "";
                            var combo = new ComboBox
                            {
                                Header = BuildHeader(field),
                                HorizontalAlignment = HorizontalAlignment.Stretch,
                                Tag = field.Name
                            };
                            if (field.ComboOptions is not null)
                            {
                                foreach (var option in field.ComboOptions)
                                    combo.Items.Add(option);
                                combo.SelectedItem = field.ComboOptions.Contains(val) ? val : field.ComboOptions.FirstOrDefault();
                            }
                            if (!string.IsNullOrEmpty(field.Hint))
                            {
                                var wrapper = new StackPanel { Spacing = 2 };
                                wrapper.Children.Add(combo);
                                wrapper.Children.Add(new TextBlock
                                {
                                    Text = field.Hint,
                                    FontSize = 12,
                                    Opacity = 0.6
                                });
                                fieldControls.Add((field, combo));
                                configPanel.Children.Add(wrapper);
                                continue; // skip the default Add below
                            }
                            control = combo;
                            break;
                        }
                    case ChannelConfigFieldType.StringArray:
                        {
                            var items = currentValue is JsonArray ja
                                ? ja.Select(n => n?.ToString() ?? "").ToArray()
                                : field.DefaultValue is string[] sa ? sa : [];
                            var box = new TextBox
                            {
                                Header = BuildHeader(field),
                                PlaceholderText = field.Hint,
                                Text = string.Join("\n", items),
                                AcceptsReturn = true,
                                TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
                                MinHeight = 72,
                                HorizontalAlignment = HorizontalAlignment.Stretch,
                                Tag = field.Name
                            };
                            control = box;
                            break;
                        }
                    default: // String
                        {
                            var val = currentValue?.ToString() ?? field.DefaultValue?.ToString() ?? "";
                            control = CreateTextBox(field, val);
                            break;
                        }
                }

                fieldControls.Add((field, control));
                configPanel.Children.Add(control);
            }

            // Wire up conditional visibility
            foreach (var (field, control) in fieldControls)
            {
                if (string.IsNullOrEmpty(field.VisibleWhen))
                    continue;

                if (!dependentControls.ContainsKey(field.VisibleWhen))
                    dependentControls[field.VisibleWhen] = [];
                dependentControls[field.VisibleWhen].Add((field, control));

                // Also hide the wrapper StackPanel if it exists
                var target = control.Parent is StackPanel sp && sp.Parent == configPanel ? (FrameworkElement)sp : control;
                target.Tag ??= field.Name; // ensure tag is set on wrapper too
            }

            // Apply initial visibility and hook up change events
            foreach (var (driverName, deps) in dependentControls)
            {
                var driverEntry = fieldControls.FirstOrDefault(fc => string.Equals(fc.Field.Name, driverName, StringComparison.OrdinalIgnoreCase));
                if (driverEntry.Control is null)
                    continue;

                // Apply initial state
                var currentDriverValue = GetControlValue(driverEntry);
                foreach (var (depField, depControl) in deps)
                    SetVisibility(depControl, currentDriverValue, depField.VisibleWhenValue);

                // Hook change event
                if (driverEntry.Control is ComboBox driverCombo)
                {
                    driverCombo.SelectionChanged += (_, _) =>
                    {
                        var val = driverCombo.SelectedItem?.ToString() ?? "";
                        foreach (var (df, dc) in deps)
                            SetVisibility(dc, val, df.VisibleWhenValue);
                    };
                }
                else if (driverEntry.Control is TextBox driverBox)
                {
                    driverBox.TextChanged += (_, _) =>
                    {
                        foreach (var (df, dc) in deps)
                            SetVisibility(dc, driverBox.Text, df.VisibleWhenValue);
                    };
                }
                else if (driverEntry.Control is ToggleSwitch driverToggle)
                {
                    driverToggle.Toggled += (_, _) =>
                    {
                        var val = driverToggle.IsOn.ToString();
                        foreach (var (df, dc) in deps)
                            SetVisibility(dc, val, df.VisibleWhenValue);
                    };
                }
            }

            var content = new StackPanel { Spacing = 12 };
            if (configPanel.Children.Count > 0)
                content.Children.Add(configPanel);

            var dialog = new ContentDialog
            {
                Title = isNew ? $"配置接入 - {component.Title}" : $"编辑接入 - {component.Title}",
                PrimaryButtonText = "保存",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = xamlRoot,
                Content = new ScrollViewer
                {
                    MaxHeight = 500,
                    Content = content
                }
            };

            var dialogResult = await ShowDialogCoreAsync(dialog);
            if (dialogResult != ContentDialogResult.Primary)
                return null;

            // Write values back to data (preserve enabled as-is)
            foreach (var (field, control) in fieldControls)
            {
                switch (field.FieldType)
                {
                    case ChannelConfigFieldType.Bool:
                        data[field.Name] = ((ToggleSwitch)control).IsOn;
                        break;
                    case ChannelConfigFieldType.Int:
                        data[field.Name] = int.TryParse(((TextBox)control).Text, out var iv) ? iv : 0;
                        break;
                    case ChannelConfigFieldType.Combo:
                        data[field.Name] = ((ComboBox)control).SelectedItem?.ToString() ?? "";
                        break;
                    case ChannelConfigFieldType.StringArray:
                        {
                            var arr = new JsonArray();
                            foreach (var line in ((TextBox)control).Text
                                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                                .Select(l => l.Trim())
                                .Where(l => l.Length > 0))
                                arr.Add(JsonValue.Create(line));
                            data[field.Name] = arr;
                            break;
                        }
                    default:
                        data[field.Name] = ((TextBox)control).Text;
                        break;
                }
            }

            return data;
        }

        /// <summary>
        /// Builds a <see cref="JsonObject"/> with default values from the component's field definitions.
        /// </summary>
        internal static JsonObject BuildDefaultData(IIntegrationWizardComponent component)
        {
            var data = new JsonObject { ["enabled"] = true };
            foreach (var field in component.ConfigFields)
            {
                switch (field.DefaultValue)
                {
                    case bool b:
                        data[field.Name] = b;
                        break;
                    case int i:
                        data[field.Name] = i;
                        break;
                    case double d:
                        data[field.Name] = d;
                        break;
                    case string s:
                        data[field.Name] = s;
                        break;
                    case string[] sa:
                        {
                            var arr = new JsonArray();
                            foreach (var item in sa)
                                arr.Add(JsonValue.Create(item));
                            data[field.Name] = arr;
                            break;
                        }
                    default:
                        data[field.Name] = "";
                        break;
                }
            }
            return data;
        }

        private static string GetControlValue((ChannelConfigField Field, FrameworkElement Control) entry)
        {
            return entry.Control switch
            {
                ComboBox cb => cb.SelectedItem?.ToString() ?? "",
                TextBox tb => tb.Text,
                ToggleSwitch ts => ts.IsOn.ToString(),
                _ => ""
            };
        }

        private static void SetVisibility(FrameworkElement control, string driverValue, string? requiredValue)
        {
            var visible = string.Equals(driverValue, requiredValue, StringComparison.OrdinalIgnoreCase);
            // If the control is inside a wrapper StackPanel, toggle the wrapper instead
            var target = control.Parent is StackPanel sp && sp.Parent is StackPanel ? sp : control;
            target.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        private static TextBox CreateTextBox(ChannelConfigField field, string value)
        {
            return new TextBox
            {
                Header = BuildHeader(field),
                PlaceholderText = field.Hint,
                Text = value,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Tag = field.Name
            };
        }

        private static string BuildHeader(ChannelConfigField field)
        {
            return field.Required ? $"{field.Label} *" : field.Label;
        }

        private static Task<ContentDialogResult> ShowDialogCoreAsync(ContentDialog dialog)
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
