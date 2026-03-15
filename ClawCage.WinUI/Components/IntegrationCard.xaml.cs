using ClawCage.WinUI.Pages;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;

namespace ClawCage.WinUI.Components
{
    public sealed partial class IntegrationCard : UserControl
    {
        public static readonly DependencyProperty ItemProperty = DependencyProperty.Register(
            nameof(Item),
            typeof(object),
            typeof(IntegrationCard),
            new PropertyMetadata(null, OnItemChanged));

        public object? Item
        {
            get => GetValue(ItemProperty);
            set => SetValue(ItemProperty, value);
        }

        private static void OnItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is IntegrationCard card)
                card.UpdateIntegrationIcon();
        }

        private void UpdateIntegrationIcon()
        {
            var iconUrl = (Item as IntegrationAccessPage.IntegrationViewItem)?.IconUrl;
            if (!string.IsNullOrEmpty(iconUrl))
            {
                IntegrationIconImage.Source = new BitmapImage(new Uri(iconUrl));
                IntegrationIconBorder.Visibility = Visibility.Visible;
                DefaultIconBorder.Visibility = Visibility.Collapsed;
            }
            else
            {
                IntegrationIconImage.Source = null;
                IntegrationIconBorder.Visibility = Visibility.Collapsed;
                DefaultIconBorder.Visibility = Visibility.Visible;
            }
        }

        public event EventHandler<object?>? EditRequested;
        public event EventHandler<object?>? DeleteRequested;

        public IntegrationCard()
        {
            InitializeComponent();
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            EditRequested?.Invoke(this, Item);
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            DeleteRequested?.Invoke(this, Item);
        }
    }
}
