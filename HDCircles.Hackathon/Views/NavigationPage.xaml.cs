﻿namespace HDCircles.Hackathon.Views
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Windows.UI.Xaml.Controls;

    public sealed partial class NavigationPage : Page
    {
        private List<NavigationMenuItem> menuItems;

        public NavigationPage()
        {
            this.InitializeComponent();

            menuItems = new List<NavigationMenuItem>();
        }

        private void NavigationPage_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            menuItems.Add(new NavigationMenuItem
            {
                Name = "Activation",
            });
            menuItems.Add(new NavigationMenuItem
            {
                Name = "Activate DjiWIndowsSDK",
                Tag = nameof(ActivationPage),
                ViewType = typeof(ActivationPage)
            });

            menuItems.Add(new NavigationMenuItem
            {
                Name = "Test",
            });
            menuItems.Add(new NavigationMenuItem
            {
                Name = "Main",
                Tag = nameof(MainPage),
                ViewType = typeof(MainPage),
            });

            // initialize navigation menu items
            foreach (var item in menuItems)
            {
                if (null == item.Tag || null == item.ViewType)
                {
                    // considers as header item
                    NavView.MenuItems.Add(new NavigationViewItemHeader { Content = item.Name });
                }
                else
                {
                    // consider as nav item
                    NavView.MenuItems.Add(item.Name);
                }
            }
        }

        private void NavigationView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            var invokedName = args.InvokedItem as string;
            var targetItem = menuItems.FirstOrDefault(x => 0 == string.Compare(invokedName, x.Name, StringComparison.Ordinal));

            if (null != targetItem && null != targetItem.ViewType && ContentFrame.SourcePageType != targetItem.ViewType)
            {
                ContentFrame.Navigate(targetItem.ViewType);
            }
        }

        class NavigationMenuItem
        {
            public string Name { get; set; }
            public string Tag { get; set; }
            public Type ViewType { get; set; }
        }
    }
}
