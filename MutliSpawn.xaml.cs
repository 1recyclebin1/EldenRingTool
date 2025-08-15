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


        private Dictionary<string, List<string>> builds = new Dictionary<string, List<string>>();
        private string buildsFile = "builds.txt";
        private bool hasUnsavedChanges = false;

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

            LoadAllBuildsFromFile();

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

        private void AvailableItemsListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            AddButton_Click(sender, e);
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            if (AvailableList.SelectedItem is Item selected)
            {
                var newItem = new Item
                {
                    Name = selected.Name,
                    Id = selected.Id,
                    Level = int.TryParse(txtLevel.Text, out int lvl) ? lvl : 0,
                    Quantity = int.TryParse(txtQuantity.Text, out int qty) ? qty : 1,
                    InfusionName = comboInfusion.SelectedItem is Lookup inf ? inf.Name : "Normal",
                    AshOfWarName = comboAsh.SelectedItem is Lookup ash ? ash.Name : "Default"
                };

                SelectedItems.Add(newItem);

                // Reset inputs to defaults
                txtLevel.Text = "0";
                txtQuantity.Text = "1";
                comboInfusion.SelectedIndex = 0;
                comboAsh.SelectedIndex = 0;
            }

            hasUnsavedChanges = true;
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            var toMove = SelectedList.SelectedItems.Cast<Item>().ToList();
            foreach (var item in toMove)
            {
                SelectedItems.Remove(item);
            }

            hasUnsavedChanges = true;
        }

        private void btnSpawnAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var selected in SelectedItems)
            {
                var item = ItemDB.Items.FirstOrDefault(x => x.Item1 == selected.Name);
                if (item == default) continue;

                var infusion = ItemDB.Infusions
                    .Select(x => new Lookup { Name = x.Item1, Id = x.Item2 })
                    .FirstOrDefault(x => x.Name == selected.InfusionName) ?? new Lookup { Name = "Normal", Id = 0 };

                var ash = ItemDB.Ashes
                    .Select(x => new Lookup { Name = x.Item1, Id = x.Item2 })
                    .FirstOrDefault(x => x.Name == selected.AshOfWarName) ?? new Lookup { Name = "Default", Id = 0 };

                uint itemID = item.Item2 + (uint)selected.Level + infusion.Id;
                uint qty = (uint)selected.Quantity;

                _process.spawnItem(itemID, qty, ash.Id);
            }
        }

        private void SaveBuild_Click(object sender, RoutedEventArgs e)
        {
            string buildName = txtBuildName.Text.Trim();

            // If no name entered, check if a build is selected in the load dropdown
            if (string.IsNullOrEmpty(buildName))
            {
                var selectedBuild = comboLoadBuild.SelectedItem as string;
                if (!string.IsNullOrEmpty(selectedBuild))
                {
                    var result = MessageBox.Show(
                        $"No build name entered. Do you want to replace the selected build \"{selectedBuild}\"?",
                        "Confirm Overwrite",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question
                    );

                    if (result != MessageBoxResult.Yes) return;

                    buildName = selectedBuild; // Use the selected build name
                }
                else
                {
                    MessageBox.Show("Please enter a build name or select a build to overwrite.", "Missing Name", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            if (SelectedItems.Count == 0)
            {
                MessageBox.Show("No items selected to save.", "Empty Build", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Confirm overwrite if the build already exists
            if (builds.ContainsKey(buildName) && buildName != (comboLoadBuild.SelectedItem as string))
            {
                var result = MessageBox.Show(
                    $"A build named \"{buildName}\" already exists. Overwrite it?",
                    "Confirm Overwrite",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question
                );
                if (result != MessageBoxResult.Yes) return;
            }

            // Save selected items
            var itemsList = SelectedItems.Select(i =>
                $"{i.Name.Replace("|", "||")}|{i.InfusionName.Replace("|", "||")}|{i.AshOfWarName.Replace("|", "||")}|{i.Level}|{i.Quantity}"
            ).ToList();

            builds[buildName] = itemsList;
            SaveAllBuildsToFile();
            RefreshBuildList();
            comboLoadBuild.SelectedItem = buildName;

            hasUnsavedChanges = false;
            MessageBox.Show($"Build \"{buildName}\" saved successfully.", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SaveAllBuildsToFile()
        {
            var lines = builds.Select(kvp =>
                kvp.Key.Replace("|", "||") + "|" + string.Join(";", kvp.Value)
            );
            File.WriteAllLines(buildsFile, lines);
        }

        private void LoadAllBuildsFromFile()
        {
            builds.Clear();
            if (!File.Exists(buildsFile)) return;

            foreach (var line in File.ReadAllLines(buildsFile))
            {
                var parts = line.Split('|');
                if (parts.Length < 2) continue;

                string buildName = parts[0].Replace("||", "|");
                string itemsString = string.Join("|", parts.Skip(1));

                var items = itemsString.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s =>
                    {
                        var itemParts = s.Split(new[] { '|' }, StringSplitOptions.None);
                        return $"{itemParts[0].Replace("||", "|")}|{itemParts[1].Replace("||", "|")}|{itemParts[2].Replace("||", "|")}|{itemParts[3]}|{itemParts[4]}";
                    }).ToList();

                builds[buildName] = items;
            }

            RefreshBuildList();
        }

        private void ComboLoadBuild_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedBuild = comboLoadBuild.SelectedItem as string;
            if (selectedBuild == null) return;

            if (hasUnsavedChanges)
            {
                var result = MessageBox.Show(
                    "You have unsaved changes. Do you want to discard them and load the new build?",
                    "Unsaved Changes",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning
                );

                if (result != MessageBoxResult.Yes)
                {
                    // Cancel the selection change
                    comboLoadBuild.SelectionChanged -= ComboLoadBuild_SelectionChanged;
                    comboLoadBuild.SelectedItem = e.RemovedItems.Count > 0 ? e.RemovedItems[0] : null;
                    comboLoadBuild.SelectionChanged += ComboLoadBuild_SelectionChanged;
                    return;
                }
            }

            LoadBuild(selectedBuild);
            hasUnsavedChanges = false;
        }

        private void RefreshBuildList()
        {
            comboLoadBuild.ItemsSource = null;
            comboLoadBuild.ItemsSource = builds.Keys.ToList();
        }

        private void LoadBuild_Click(object sender, RoutedEventArgs e)
        {
            var selectedBuild = comboLoadBuild.SelectedItem as string;
            if (selectedBuild == null)
            {
                MessageBox.Show("Please select a valid build.");
                return;
            }

            LoadBuild(selectedBuild);
        }

        private void LoadBuild(string buildName)
        {
            if (!builds.ContainsKey(buildName)) return;

            SelectedItems.Clear();

            foreach (var itemString in builds[buildName])
            {
                var parts = itemString.Split('|');
                if (parts.Length < 5) continue;

                SelectedItems.Add(new Item
                {
                    Name = parts[0],
                    InfusionName = parts[1],
                    AshOfWarName = parts[2],
                    Level = int.Parse(parts[3]),
                    Quantity = int.Parse(parts[4])
                });
            }

            hasUnsavedChanges = false; // Reset unsaved flag after loading
        }

    }
}
