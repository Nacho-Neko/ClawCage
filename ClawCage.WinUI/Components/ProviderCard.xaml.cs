using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;

namespace ClawCage.WinUI.Components
{
    public sealed partial class ProviderCard : UserControl
    {
        public static readonly DependencyProperty ItemProperty = DependencyProperty.Register(
            nameof(Item),
            typeof(object),
            typeof(ProviderCard),
            new PropertyMetadata(null, OnItemChanged));

        public object? Item
        {
            get => GetValue(ItemProperty);
            set => SetValue(ItemProperty, value);
        }

        private static void OnItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ProviderCard card)
                card.UpdateProviderIcon();
        }

        private void UpdateProviderIcon()
        {
            var iconUrl = (Item as Pages.ModelAccessPage.ProviderViewItem)?.IconUrl;
            if (!string.IsNullOrEmpty(iconUrl))
            {
                ProviderIconImage.Source = new BitmapImage(new Uri(iconUrl));
                ProviderIconBorder.Visibility = Visibility.Visible;
                DefaultIconBorder.Visibility = Visibility.Collapsed;
            }
            else
            {
                ProviderIconImage.Source = null;
                ProviderIconBorder.Visibility = Visibility.Collapsed;
                DefaultIconBorder.Visibility = Visibility.Visible;
            }
        }

        public event EventHandler<object?>? EditProviderRequested;
        public event EventHandler<object?>? EditModelRequested;
        public event EventHandler<object?>? AddModelRequested;
        public event EventHandler<object?>? DeleteProviderRequested;
        public event EventHandler<object?>? DeleteModelRequested;

        public ProviderCard()
        {
            InitializeComponent();
        }

        private void EditProviderButton_Click(object sender, RoutedEventArgs e)
        {
            EditProviderRequested?.Invoke(this, Item);
        }

        private void DeleteProviderButton_Click(object sender, RoutedEventArgs e)
        {
            DeleteProviderRequested?.Invoke(this, Item);
        }

        private void AddModelButton_Click(object sender, RoutedEventArgs e)
        {
            AddModelRequested?.Invoke(this, Item);
        }

        private void EditModelButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: var modelItem })
                EditModelRequested?.Invoke(this, modelItem);
        }

        private void DeleteModelButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: var modelItem })
                DeleteModelRequested?.Invoke(this, modelItem);
        }
    }
}
