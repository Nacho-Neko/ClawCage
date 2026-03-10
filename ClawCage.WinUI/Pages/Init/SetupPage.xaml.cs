using ClawCage.WinUI.Services;
using ClawCage.WinUI.Services.Tools.Helper;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.IO;

namespace ClawCage.WinUI.Pages
{
    public sealed partial class SetupPage : Page
    {
        public SetupPage()
        {
            InitializeComponent();
            Loaded += SetupPage_Loaded;
        }

        private void SetupPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (AppRuntimeState.DatabasePath is string savedPath
                && !string.IsNullOrWhiteSpace(savedPath))
            {
                PathTextBox.Text = savedPath;
                StartButton.Focus(FocusState.Programmatic);
            }
        }

        private void PathTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var path = PathTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(path))
            {
                StartButton.IsEnabled = false;
                PathHintText.Visibility = Visibility.Collapsed;
                return;
            }

            if (!IsValidPath(path))
            {
                StartButton.IsEnabled = false;
                PathHintText.Text = "路径格式无效";
                PathHintText.Visibility = Visibility.Visible;
                return;
            }

            StartButton.IsEnabled = true;
            PathHintText.Text = Directory.Exists(path) ? "" : "路径不存在，将在确认后自动创建";
            PathHintText.Visibility = string.IsNullOrEmpty(PathHintText.Text)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            var selected = FolderPickerHelper.PickFolder(hwnd, PathTextBox.Text.Trim());
            if (selected is not null)
                PathTextBox.Text = selected;
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            var path = PathTextBox.Text.Trim();
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            AppRuntimeState.SetDatabasePath(path);
            SecureConfigStore.AddPathEntry(path, path);

            Frame.Navigate(typeof(InitialDeployPage));
        }

        private static bool IsValidPath(string path)
        {
            try { _ = Path.GetFullPath(path); return true; }
            catch { return false; }
        }
    }
}
