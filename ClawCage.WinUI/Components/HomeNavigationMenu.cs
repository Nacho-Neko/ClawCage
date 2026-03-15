using ClawCage.WinUI.Pages;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;

namespace ClawCage.WinUI.Components
{
    internal static class HomeNavigationMenu
    {
        internal const string OverviewTag = "overview";
        internal const string ModelAccessTag = "modelAccess";
        internal const string IntegrationAccessTag = "integrationAccess";
        internal const string SettingsTag = "settings";
        internal const string AboutTag = "about";

        internal static IReadOnlyList<NavigationViewItem> CreateMenuItems() =>
        [
            CreateMenuItem("概览", OverviewTag, "\uE80F"),
            CreateMenuItem("模型", ModelAccessTag, "\uE8D4"),
            CreateMenuItem("接入", IntegrationAccessTag, "\uE71B"),
            CreateMenuItem("设置", SettingsTag, "\uE713")
        ];

        internal static IReadOnlyList<NavigationViewItem> CreateFooterMenuItems() =>
        [
            CreateMenuItem("关于", AboutTag, "\uE946")
        ];

        internal static Type? ResolvePageType(string? tag) => tag switch
        {
            OverviewTag => typeof(OverviewPage),
            ModelAccessTag => typeof(ModelAccessPage),
            IntegrationAccessTag => typeof(IntegrationAccessPage),
            SettingsTag => typeof(SettingsPage),
            AboutTag => typeof(AboutPage),
            _ => null
        };

        private static NavigationViewItem CreateMenuItem(string content, string tag, string glyph) =>
            new()
            {
                Content = content,
                Tag = tag,
                Icon = new FontIcon
                {
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    Glyph = glyph
                }
            };
    }
}
