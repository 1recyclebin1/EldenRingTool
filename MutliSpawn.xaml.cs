using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Policy;
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
        public ObservableCollection<Lookup> Infusions { get; private set; }
        public ObservableCollection<Lookup> Ashes { get; private set; }


        private Point startPoint;
        private const string SaveFileName = "savedLists.csv";
        ERProcess _process;

        public class Item
        {
            public string Name { get; set; }
            public uint Id { get; set; }
            public int Level { get; set; }
            public uint InfusionId { get; set; }
            public string InfusionName { get; set; } 

            public uint AshOfWarId { get; set; }
            public string AshOfWarName { get; set; } 
            public int Quantity { get; set; }
        }

        public class Lookup
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

            Infusions = new ObservableCollection<Lookup>(
                ItemDB.Infusions.Select(item => new Lookup
                {
                    Name = item.Item1,
                    Id = item.Item2
                })
            );

            Ashes = new ObservableCollection<Lookup>(
                ItemDB.Ashes.Select(item => new Lookup
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
            uint infusionId = comboInfusion.SelectedValue is uint iv ? iv : 0;
            uint ashId = comboAsh.SelectedValue is uint av ? av : 0;

            var selectedInfusion = Infusions.FirstOrDefault(i => i.Id == infusionId);
            var selectedAsh = Ashes.FirstOrDefault(a => a.Id == ashId);

            foreach (var item in AvailableList.SelectedItems.Cast<Item>())
            {
                SelectedItems.Add(new Item
                {
                    Name = item.Name,
                    Id = item.Id,
                    Level = int.TryParse(txtLevel.Text, out var lvl) ? lvl : 0,
                    InfusionId = selectedInfusion?.Id ?? 0,
                    InfusionName = selectedInfusion?.Name ?? "Normal",
                    AshOfWarId = selectedAsh?.Id ?? 0,
                    AshOfWarName = selectedAsh?.Name ?? "Default",
                    Quantity = int.TryParse(txtQuantity.Text, out var qty) ? qty : 1
                });
            }
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            var toMove = SelectedList.SelectedItems.Cast<Item>().ToList();
            foreach (var item in toMove)
            {
                SelectedItems.Remove(item);
            }
        }
    }
}
