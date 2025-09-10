using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace EldenRingTool
{
    public partial class UnlockGraces : Window, INotifyPropertyChanged
    {
        public const string EMPTY_PROFILE_NAME = "<New Profile>";

        public ObservableCollection<AreaGroup> AvailableGracesGrouped { get; set; } = new ObservableCollection<AreaGroup>();
        public ObservableCollection<Grace> SelectedGraces { get; set; } = new ObservableCollection<Grace>();

        private List<AreaGroup> _allGraces = new List<AreaGroup>(); // master copy for filtering
        private bool _hasUnsavedChanges;
        private string ProfilesFile => MainWindow.getGraceProfilesFileAppData();

        public bool HasUnsavedChanges
        {
            get { return _hasUnsavedChanges; }
            set { _hasUnsavedChanges = value; }
        }

        public UnlockGraces(ERProcess process)
        {
            InitializeComponent();
            DataContext = this;

            _process = process;

            var excludedGraces = new List<string> { "Show underground", "Table of Lost Grace / Roundtable Hold" };

            var grouped = GraceDB.Graces
                .Where(g => !excludedGraces.Contains(g.Area))
                .GroupBy(g => g.Area)
                .Select(a => new AreaGroup
                {
                    Area = a.Key,
                    SubAreas = a.GroupBy(s => s.SubArea)
                                .Select(sa => new SubAreaGroup
                                {
                                    SubArea = sa.Key,
                                    Graces = sa.ToList()
                                }).ToList()
                });

            foreach (var area in grouped)
                AvailableGracesGrouped.Add(area);

            foreach (var area in AvailableGracesGrouped)
            {
                AreaGroup copyArea = new AreaGroup
                {
                    Area = area.Area,
                    SubAreas = new List<SubAreaGroup>()
                };

                foreach (var sub in area.SubAreas)
                {
                    SubAreaGroup copySub = new SubAreaGroup
                    {
                        SubArea = sub.SubArea,
                        Graces = new List<Grace>(sub.Graces)
                    };
                    copyArea.SubAreas.Add(copySub);
                }

                _allGraces.Add(copyArea);
            }

            SelectedGraces.CollectionChanged += (s, e) =>
            {
                ActivateSelectedButton.IsEnabled = SelectedGraces.Any();
                SaveProfileButton.IsEnabled = SelectedGraces.Any();
            };

            LoadProfilesIntoComboBox();

            SaveProfileButton.IsEnabled = SelectedGraces.Any();
            ActivateSelectedButton.IsEnabled = SelectedGraces.Any();
            DeleteProfileButton.IsEnabled = ProfileComboBox.SelectedItem != null
                                           && ProfileComboBox.SelectedItem.ToString() != EMPTY_PROFILE_NAME;
        }

        private ERProcess _process;

        #region Tree/Listbox

        private void AvailableGraces_DoubleClick(object sender, RoutedEventArgs e)
        {
            if (AvailableGracesTree.SelectedItem is Grace g && !SelectedGraces.Contains(g))
                SelectedGraces.Add(g);
        }

        private void SelectedGracesList_DoubleClick(object sender, RoutedEventArgs e)
        {
            RemoveGraceFromSelected();
        }

        private void AvailableGraces_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            ActivateSelectedButton.IsEnabled = (AvailableGracesTree.SelectedItem is Grace);
        }

        private void RemoveGraceFromSelected()
        {
            if (SelectedGracesList.SelectedItem is Grace g)
            {
                SelectedGraces.Remove(g);
                ActivateSelectedButton.IsEnabled = SelectedGraces.Any();
                _hasUnsavedChanges = true;
            }
        }

        private void AddGrace_Click(object sender, RoutedEventArgs e)
        {
            if (AvailableGracesTree.SelectedItem is Grace g)
            {
                if (!SelectedGraces.Contains(g))
                    SelectedGraces.Add(g);
            }
            else if (AvailableGracesTree.SelectedItem is SubAreaGroup sub)
            {
                foreach (var grace in sub.Graces)
                {
                    if (!SelectedGraces.Contains(grace))
                        SelectedGraces.Add(grace);
                }
            }
            else if (AvailableGracesTree.SelectedItem is AreaGroup area)
            {
                foreach (var subarea in area.SubAreas)
                {
                    foreach (var grace in subarea.Graces)
                    {
                        if (!SelectedGraces.Contains(grace))
                            SelectedGraces.Add(grace);
                    }
                }
            }
        }

        private void RemoveGrace_Click(object sender, RoutedEventArgs e)
        {
            RemoveGraceFromSelected();
        }

        private void ActivateSelected_Click(object sender, RoutedEventArgs e)
        {
            if (AvailableGracesTree.SelectedItem is Grace g)
                UnlockGrace(g);
        }

        private void ActivateGroup_Click(object sender, RoutedEventArgs e)
        {
            foreach (var selected in SelectedGraces)
                UnlockGrace(selected);
        }

        private void UnlockGrace(Grace g)
        {
            _process.getSetEventFlag(g.ID, true);
            if (g.Area == "Ainsel River" || g.Area == "Nokron, Eternal City" || g.Area == "Deeproot Depths")
                _process.getSetEventFlag(82001, true); // underground map
            if (g.Area == "Land of the Tower" || 
                g.Area == "Gravesite Plain" || 
                g.Area == "Scadu Altus" || 
                g.Area == "Shadow Keep")
                _process.getSetEventFlag(82002, true); // dlc map
        }

        #endregion

        #region Filter

        private void FilterBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(FilterBox.Text))
            {
                CollapseAllTreeViewItems();
            }
            else
            {
                ApplyFilter(FilterBox.Text);
            }
        }

        private void ClearFilter_Click(object sender, RoutedEventArgs e)
        {
            FilterBox.Text = "";
            ApplyFilter(""); 

            AvailableGracesTree.Dispatcher.BeginInvoke(new Action(() =>
            {
                foreach (var areaItem in AvailableGracesTree.Items)
                {
                    TreeViewItem tviArea = AvailableGracesTree.ItemContainerGenerator.ContainerFromItem(areaItem) as TreeViewItem;
                    if (tviArea != null)
                    {
                        tviArea.IsExpanded = false;
                        tviArea.UpdateLayout();

                        foreach (var subItem in tviArea.Items)
                        {
                            TreeViewItem tviSub = tviArea.ItemContainerGenerator.ContainerFromItem(subItem) as TreeViewItem;
                            if (tviSub != null)
                                tviSub.IsExpanded = false;
                        }
                    }
                }
            }));
        }

        private void ApplyFilter(string filter)
        {
            if (filter == null) filter = "";
            filter = filter.ToLower();

            AvailableGracesGrouped.Clear();

            foreach (AreaGroup area in _allGraces)
            {
                AreaGroup filteredArea = new AreaGroup { Area = area.Area };

                foreach (SubAreaGroup sub in area.SubAreas)
                {
                    List<Grace> matchingGraces = sub.Graces
                        .Where(g => g.Name.ToLower().Contains(filter))
                        .ToList();

                    if (matchingGraces.Any())
                    {
                        SubAreaGroup filteredSub = new SubAreaGroup
                        {
                            SubArea = sub.SubArea,
                            Graces = matchingGraces
                        };
                        filteredArea.SubAreas.Add(filteredSub);
                    }
                }

                if (filteredArea.SubAreas.Any())
                    AvailableGracesGrouped.Add(filteredArea);
            }

            AvailableGracesTree.Dispatcher.BeginInvoke(new Action(() =>
            {
                foreach (object areaItem in AvailableGracesTree.Items)
                {
                    TreeViewItem tviArea = AvailableGracesTree.ItemContainerGenerator.ContainerFromItem(areaItem) as TreeViewItem;
                    if (tviArea != null)
                    {
                        tviArea.IsExpanded = true;
                        tviArea.UpdateLayout(); 

                        foreach (object subItem in tviArea.Items)
                        {
                            TreeViewItem tviSub = tviArea.ItemContainerGenerator.ContainerFromItem(subItem) as TreeViewItem;
                            if (tviSub != null)
                                tviSub.IsExpanded = true;
                        }
                    }
                }
            }));
        }

        private void CollapseAllTreeViewItems()
        {
            AvailableGracesTree.Dispatcher.BeginInvoke(new Action(() =>
            {
                foreach (var areaItem in AvailableGracesTree.Items)
                {
                    TreeViewItem tviArea = AvailableGracesTree.ItemContainerGenerator.ContainerFromItem(areaItem) as TreeViewItem;
                    if (tviArea != null)
                    {
                        tviArea.IsExpanded = false;
                        tviArea.UpdateLayout();

                        foreach (var subItem in tviArea.Items)
                        {
                            TreeViewItem tviSub = tviArea.ItemContainerGenerator.ContainerFromItem(subItem) as TreeViewItem;
                            if (tviSub != null)
                                tviSub.IsExpanded = false;
                        }
                    }
                }
            }));
        }


        #endregion

        #region Profile management

        private void SaveProfile_Click(object sender, RoutedEventArgs e)
        {
            if (!SelectedGraces.Any())
            {
                MessageBox.Show("Cannot save an empty profile.");
                return;
            }

            var existingProfiles = File.Exists(ProfilesFile)
                ? File.ReadAllLines(ProfilesFile).Select(l => l.Split(':')[0])
                : Enumerable.Empty<string>();

            var dialog = new SaveProfileDialog(existingProfiles, "Save Grace Profile", ProfileComboBox.Text)
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true)
            {
                string profileName = dialog.SelectedProfileName;

                var line = profileName + ":" + string.Join(",", SelectedGraces.Select(g => g.ID));
                var lines = File.Exists(ProfilesFile) ? File.ReadAllLines(ProfilesFile).ToList() : new List<string>();

                lines.RemoveAll(l => l.StartsWith(profileName + ":"));
                lines.Add(line);
                File.WriteAllLines(ProfilesFile, lines);

                LoadProfilesIntoComboBox();
                ProfileComboBox.SelectedItem = profileName;
                HasUnsavedChanges = false;

                MessageBox.Show($"Profile '{profileName}' saved!");
            }
        }

        private void DeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            if (ProfileComboBox.SelectedItem == null) return;

            string profileName = ProfileComboBox.SelectedItem.ToString();
            if (!File.Exists(ProfilesFile)) return;

            var result = MessageBox.Show(
                $"Are you sure you want to delete '{profileName}'?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                var lines = File.ReadAllLines(ProfilesFile).ToList();
                var removed = lines.RemoveAll(l => l.StartsWith(profileName + ":"));

                if (removed > 0)
                {
                    File.WriteAllLines(ProfilesFile, lines);
                    LoadProfilesIntoComboBox();

                    SelectedGraces.Clear();

                    HasUnsavedChanges = false;
                    MessageBox.Show($"Profile '{profileName}' deleted.");
                }
            }


        }

        private void ProfileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProfileComboBox.SelectedItem == null) return;

            string selectedProfile = ProfileComboBox.SelectedItem.ToString();
            DeleteProfileButton.IsEnabled = selectedProfile != EMPTY_PROFILE_NAME;

            if (selectedProfile == EMPTY_PROFILE_NAME)
            {
                SelectedGraces.Clear();
                HasUnsavedChanges = false;
                return;
            }

            LoadProfile(selectedProfile);
            HasUnsavedChanges = false;
        }

        private void LoadProfile(string profileName)
        {
            if (!File.Exists(ProfilesFile)) return;

            var line = File.ReadAllLines(ProfilesFile).FirstOrDefault(l => l.StartsWith(profileName + ":"));
            if (line == null) return;

            var parts = line.Split(':');
            if (parts.Length < 2) return;

            var ids = parts[1].Split(',').Select(int.Parse).ToHashSet();

            SelectedGraces.Clear();

            foreach (var area in AvailableGracesGrouped)
            {
                foreach (var sub in area.SubAreas)
                {
                    foreach (var g in sub.Graces)
                        if (ids.Contains(g.ID))
                            SelectedGraces.Add(g);
                }
            }

        }

        private void LoadProfilesIntoComboBox()
        {
            ProfileComboBox.Items.Clear();
            ProfileComboBox.Items.Add(EMPTY_PROFILE_NAME);

            if (File.Exists(ProfilesFile))
            {
                foreach (var line in File.ReadAllLines(ProfilesFile))
                {
                    string profileName = line.Split(':')[0];
                    ProfileComboBox.Items.Add(profileName);
                }
            }

            ProfileComboBox.SelectedIndex = 0;
            DeleteProfileButton.IsEnabled = false;
        }

        #endregion

        #region Unsaved Changes on Close

        protected override void OnClosing(CancelEventArgs e)
        {
            if (HasUnsavedChanges)
            {
                var result = MessageBox.Show(
                    "You have unsaved changes. Are you sure you want to close?",
                    "Unsaved Changes",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.No)
                    e.Cancel = true;
            }

            base.OnClosing(e);
        }

        #endregion

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(name));
        }
        #endregion
    }
}
