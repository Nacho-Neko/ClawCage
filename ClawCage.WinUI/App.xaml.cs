using Microsoft.UI.Xaml;

namespace ClawCage.WinUI
{
    public partial class App : Application
    {
        public static Window? MainWindow { get; private set; }

        public App()
        {
            InitializeComponent();
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            MainWindow = new MainWindow();
            MainWindow.Activate();
        }
    }
}
