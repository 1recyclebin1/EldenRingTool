using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace EldenRingTool
{
    public partial class MutliSpawn : Window
    {
        public ObservableCollection<Item> AvailableItems { get; private set; }
        public ICollectionView AvailableItemsView { get; set; } // For filtering
        public ObservableCollection<Item> SelectedItems { get; set; }

        private Point startPoint;
        private const string SaveFileName = "savedLists.csv";
        ERProcess _process;

        public class Item
        {
            public string Name { get; set; }
            public uint Id { get; set; }
        }

        // Holds all saved lists by name
        private Dictionary<string, List<string>> savedLists = new Dictionary<string, List<string>>();

        public MutliSpawn(ERProcess process)
        {
            _process = process;
            InitializeComponent();
            populate();
        }

        private void populate()
        {
            AvailableItems = new ObservableCollection<Item>(
                ItemDB.Items.Select(item => new Item
                {
                    Name = item.Item1,
                    Id = item.Item2
                })
            );

            SelectedItems = new ObservableCollection<Item>();

            AvailableItemsView = CollectionViewSource.GetDefaultView(AvailableItems);
            AvailableItemsView.Filter = FilterAvailableItems;

            DataContext = this;
        }

        private bool FilterAvailableItems(object obj)
        {
            if (obj is Item item)
            {
                // Remove items that are already in SelectedItems
                if (SelectedItems.Contains(item))
                    return false;

                // Filter by TextBox input
                string filter = FilterBox?.Text?.ToLower() ?? "";
                return string.IsNullOrEmpty(filter) || item.Name.ToLower().Contains(filter);
            }
            return false;
        }

        private void FilterBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filter = FilterBox.Text.ToLower();

            AvailableItemsView.Filter = item =>
            {
                if (item is Item i)
                {
                    return i.Name.ToLower().Contains(filter);
                }
                return false;
            };
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var toMove = AvailableList.SelectedItems.Cast<Item>().ToList();
            foreach (var item in toMove)
            {
                SelectedItems.Add(new Item { Name = item.Name, Id = item.Id });
            }

            AvailableItemsView.Refresh();
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            var toMove = SelectedList.SelectedItems.Cast<Item>().ToList();
            foreach (var item in toMove)
            {
                SelectedItems.Remove(item);
                // No need to add back to AvailableItems if the original collection remains unchanged
            }

            AvailableItemsView.Refresh();
        }
    }
}
