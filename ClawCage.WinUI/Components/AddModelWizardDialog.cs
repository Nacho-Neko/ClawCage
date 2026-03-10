using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using ClawCage.WinUI.Components.Providers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Foundation;
using OpenClawModel = ClawCage.WinUI.Model.Model;

namespace ClawCage.WinUI.Components
{
    internal sealed class AddModelWizardDialog
    {
        internal sealed class ProviderTemplate
        {
            public string Key { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string? DefaultBaseUrl { get; set; }
            public string? DefaultApi { get; set; }
            public bool IsCustom { get; set; }
            public IProviderWizardComponent? Component { get; set; }
        }

        internal sealed class Result
        {
            public bool Succeeded { get; set; }
            public bool IsNewProvider { get; set; }
            public bool IsCustomProvider { get; set; }
            public string ProviderKey { get; set; } = string.Empty;
            public string ApiKey { get; set; } = string.Empty;
            public string BaseUrl { get; set; } = string.Empty;
            public string Api { get; set; } = string.Empty;
            public string ModelId { get; set; } = string.Empty;
            public List<OpenClawModel>? PresetModels { get; set; }
        }

        internal static async Task<Result?> ShowAsync(XamlRoot xamlRoot, IReadOnlyList<string> existingProviderKeys)
        {
            ProviderComponentRegistry.EnsureInitialized();

            var existingProviderItems = existingProviderKeys
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(x => new ProviderTemplate
                {
                    Key = x,
                    Title = x,
                    Description = "现有供应商"
                })
                .ToList();

            var newProviderItems = ProviderComponentRegistry.GetAll()
                .Select(c => new ProviderTemplate
                {
                    Key = c.Key,
                    Title = c.Title,
                    Description = c.Description,
                    DefaultApi = c.DefaultApi,
                    DefaultBaseUrl = c.DefaultBaseUrl,
                    IsCustom = c.IsCustom,
                    Component = c
                })
                .OrderBy(x => x.IsCustom ? 1 : 0)
                .ThenBy(x => x.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var useExistingProviderToggle = new RadioButton { Content = "使用现有供应商", IsChecked = true };
            var useNewProviderToggle = new RadioButton { Content = "新增供应商" };

            var existingProviderGrid = CreateProviderGrid(existingProviderItems);
            var newProviderGrid = CreateProviderGrid(newProviderItems);
            newProviderGrid.Visibility = Visibility.Collapsed;

            useExistingProviderToggle.Checked += (_, _) =>
            {
                existingProviderGrid.Visibility = Visibility.Visible;
                newProviderGrid.Visibility = Visibility.Collapsed;
            };

            useNewProviderToggle.Checked += (_, _) =>
            {
                existingProviderGrid.Visibility = Visibility.Collapsed;
                newProviderGrid.Visibility = Visibility.Visible;
            };

            var chooseDialog = new ContentDialog
            {
                Title = "选择供应商",
                PrimaryButtonText = "下一步",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = xamlRoot,
                Content = new ScrollViewer
                {
                    MaxHeight = 620,
                    Content = new StackPanel
                    {
                        MinWidth = 780,
                        Spacing = 12,
                        Children =
                        {
                            new StackPanel
                            {
                                Orientation = Orientation.Horizontal,
                                Spacing = 16,
                                Children =
                                {
                                    useExistingProviderToggle,
                                    useNewProviderToggle
                                }
                            },
                            existingProviderGrid,
                            newProviderGrid
                        }
                    }
                }
            };

            var chooseResult = await ShowDialogAsync(chooseDialog);
            if (chooseResult != ContentDialogResult.Primary)
                return null;

            var useExisting = useExistingProviderToggle.IsChecked == true;
            var selectedProvider = useExisting
                ? existingProviderGrid.SelectedItem as ProviderTemplate
                : newProviderGrid.SelectedItem as ProviderTemplate;

            if (selectedProvider is null)
                return null;

            var result = new Result
            {
                Succeeded = true,
                IsNewProvider = !useExisting,
                IsCustomProvider = selectedProvider.IsCustom,
                ProviderKey = selectedProvider.Key,
                Api = selectedProvider.DefaultApi ?? string.Empty,
                BaseUrl = selectedProvider.DefaultBaseUrl ?? string.Empty
            };

            if (!useExisting)
            {
                var draft = new ProviderDraft
                {
                    ProviderKey = selectedProvider.IsCustom ? string.Empty : selectedProvider.Key,
                    Api = selectedProvider.DefaultApi ?? string.Empty,
                    BaseUrl = selectedProvider.DefaultBaseUrl ?? string.Empty
                };

                if (selectedProvider.Component is null)
                    return null;

                var configured = await selectedProvider.Component.ConfigureNewProviderAsync(xamlRoot, draft);
                if (!configured)
                    return null;

                result.ProviderKey = draft.ProviderKey;
                result.ApiKey = draft.ApiKey;
                result.BaseUrl = draft.BaseUrl;
                result.Api = draft.Api;

                if (draft.SkipModelStep)
                {
                    result.PresetModels = draft.PresetModels;
                    return result;
                }
            }

            var modelIdBox = new TextBox { Header = "Model ID" };
            var testText = new TextBlock { Opacity = 0.7 };
            var tested = false;
            var testButton = new Button { Content = "测试", MinWidth = 82, HorizontalAlignment = HorizontalAlignment.Left };
            testButton.Click += (_, _) =>
            {
                if (string.IsNullOrWhiteSpace(modelIdBox.Text))
                {
                    tested = false;
                    testText.Text = "测试失败：Model ID 不能为空。";
                    return;
                }

                tested = true;
                testText.Text = "测试通过。";
            };

            var modelDialog = new ContentDialog
            {
                Title = "添加模型",
                PrimaryButtonText = "完成",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = xamlRoot,
                Content = new StackPanel
                {
                    MinWidth = 580,
                    Spacing = 10,
                    Children =
                    {
                        modelIdBox,
                        testButton,
                        testText
                    }
                }
            };

            modelDialog.PrimaryButtonClick += (_, args) =>
            {
                if (!tested)
                {
                    testText.Text = "请先测试通过。";
                    args.Cancel = true;
                }
            };

            var modelResult = await ShowDialogAsync(modelDialog);
            if (modelResult != ContentDialogResult.Primary)
                return null;

            result.ModelId = modelIdBox.Text.Trim();
            return result;
        }

        private static GridView CreateProviderGrid(IReadOnlyList<ProviderTemplate> items)
        {
            var grid = new GridView
            {
                SelectionMode = ListViewSelectionMode.Single,
                IsItemClickEnabled = false,
                MinHeight = 380,
                ItemsSource = items,
                ItemTemplate = (DataTemplate)XamlReader.Load("""
                    <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                        <Border Width="238" Height="96" CornerRadius="8" Padding="12,10"
                                Margin="0,0,10,10"
                                BorderThickness="1"
                                BorderBrush="{ThemeResource CardStrokeColorDefaultBrush}"
                                Background="{ThemeResource CardBackgroundFillColorSecondaryBrush}">
                            <StackPanel Spacing="5">
                                <TextBlock Text="{Binding Title}" FontSize="14" FontWeight="SemiBold"/>
                                <TextBlock Text="{Binding Description}" FontSize="12" Opacity="0.7" TextWrapping="WrapWholeWords"/>
                            </StackPanel>
                        </Border>
                    </DataTemplate>
                    """)
            };

            grid.ItemsPanel = (ItemsPanelTemplate)XamlReader.Load("""
                <ItemsPanelTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                    <ItemsWrapGrid Orientation="Horizontal"/>
                </ItemsPanelTemplate>
                """);

            return grid;
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
