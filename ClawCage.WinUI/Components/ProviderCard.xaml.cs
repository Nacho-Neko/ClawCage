using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace ClawCage.WinUI.Components
{
    public sealed partial class ProviderCard : UserControl
    {
        public static readonly DependencyProperty ItemProperty = DependencyProperty.Register(
            nameof(Item),
            typeof(object),
            typeof(ProviderCard),
            new PropertyMetadata(null));

        public object? Item
        {
            get => GetValue(ItemProperty);
            set => SetValue(ItemProperty, value);
        }

        public event EventHandler<object?>? EditProviderRequested;
        public event EventHandler<object?>? EditModelRequested;

        public ProviderCard()
        {
            InitializeComponent();
        }

        private void EditProviderButton_Click(object sender, RoutedEventArgs e)
        {
            EditProviderRequested?.Invoke(this, Item);
        }

        private void EditModelButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: var modelItem })
                EditModelRequested?.Invoke(this, modelItem);
        }
    }
}
