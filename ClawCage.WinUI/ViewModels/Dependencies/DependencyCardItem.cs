using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace ClawCage.WinUI.ViewModels
{
    public sealed partial class DependencyCardItem : ObservableObject
    {
        [ObservableProperty] private string _kind = string.Empty;
        [ObservableProperty] private string _name = string.Empty;
        [ObservableProperty] private string _description = string.Empty;
        [ObservableProperty] private string _iconGlyph = "\uE946";
        [ObservableProperty] private BitmapImage? _iconImage;
        [ObservableProperty] private Visibility _iconImageVisibility = Visibility.Collapsed;
        [ObservableProperty] private Visibility _iconGlyphVisibility = Visibility.Visible;
        [ObservableProperty] private string _currentVersionText = "待检测";
        [ObservableProperty] private Visibility _detectRingVisibility = Visibility.Collapsed;
        [ObservableProperty] private Visibility _statusIconVisibility = Visibility.Collapsed;
        [ObservableProperty] private string _statusGlyph = "\uE711";
        [ObservableProperty] private Brush _statusForeground = new SolidColorBrush(Colors.Red);
        [ObservableProperty] private Visibility _installButtonVisibility = Visibility.Collapsed;
        [ObservableProperty] private Visibility _uninstallButtonVisibility = Visibility.Collapsed;
        [ObservableProperty] private Visibility _downloadPanelVisibility = Visibility.Collapsed;
        [ObservableProperty] private bool _downloadIndeterminate = true;
        [ObservableProperty] private double _downloadValue;
        [ObservableProperty] private string _downloadPercentText = string.Empty;
        [ObservableProperty] private string _downloadSizeText = string.Empty;
        [ObservableProperty] private string _downloadPhaseText = string.Empty;
        [ObservableProperty] private Visibility _targetVersionSelectorVisibility = Visibility.Collapsed;
        [ObservableProperty] private Visibility _targetVersionTextVisibility = Visibility.Visible;
        [ObservableProperty] private string _targetVersionText = string.Empty;
    }
}
