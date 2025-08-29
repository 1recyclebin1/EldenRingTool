using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace EldenRingTool
{
    public partial class UnlockGraces : Window
    {
        public ObservableCollection<AreaGroup> AvailableGracesGrouped { get; set; } = new ObservableCollection<AreaGroup>();
        public ObservableCollection<Grace> SelectedGraces { get; set; } = new ObservableCollection<Grace>();

        ERProcess _process;

        private string ProfilesFile => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Profiles.txt");

        private bool _hasUnsavedChanges;
        private bool HasUnsavedChanges
        {
            get => _hasUnsavedChanges;
            set => _hasUnsavedChanges = value;
        }

        private List<AreaGroup> _allGraces = new List<AreaGroup>();
        private bool _suspendChangeTracking = false;

        public const string EMPTY_PROFILE_NAME = "<New Profile>";

        public UnlockGraces(ERProcess process)
        {
            InitializeComponent();
            DataContext = this;

            var excludedGraces = new List<string> { "Show underground", "Table of Lost Grace / Roundtable Hold" };

            var grouped = GraceDB.Graces
                .Where(g => !excludedGraces.Contains(g.Area))
                .GroupBy(g => g.Area)
                .Select(g => new AreaGroup
                {
                    Area = g.Key,
                    SubAreas = g.GroupBy(s => s.SubArea)
                                .Select(sa => new SubAreaGroup
                                {
                                    SubArea = sa.Key,
                                    Graces = sa.ToList()
                                }).ToList()
                });

            foreach (var area in grouped)
                AvailableGracesGrouped.Add(area);

            _allGraces = AvailableGracesGrouped.Select(a => new AreaGroup
            {
                Area = a.Area,
                SubAreas = a.SubAreas.Select(sa => new SubAreaGroup
                {
                    SubArea = sa.SubArea,
                    Graces = new List<Grace>(sa.Graces)
                }).ToList(),
                GracesWithoutSubArea = new List<Grace>(a.GracesWithoutSubArea)
            }).ToList();

            SelectedGraces.CollectionChanged += (s, e) =>
            {
                ActivateSelectedButton.IsEnabled = SelectedGraces.Any();
                SaveProfileButton.IsEnabled = SelectedGraces.Any(); 
            };

            LoadProfilesIntoComboBox(); 

            _process = process;

            SaveProfileButton.IsEnabled = SelectedGraces.Any();
            ActivateSelectedButton.IsEnabled = SelectedGraces.Any();
            DeleteProfileButton.IsEnabled = ProfileComboBox.SelectedItem != null
                                           && ProfileComboBox.SelectedItem.ToString() != EMPTY_PROFILE_NAME;
        }

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

        private void AvailableGraces_SelectedItemChanged(object sender, RoutedEventArgs e)
        {
            ActivateSelectedButton.IsEnabled = (AvailableGracesTree.SelectedItem is Grace g);
        }

        private void RemoveGraceFromSelected()
        {
            if (SelectedGracesList.SelectedItem is Grace g)
            {
                SelectedGraces.Remove(g);
                ActivateSelectedButton.IsEnabled = SelectedGraces.Any();
                _hasUnsavedChanges = true; // mark unsaved changes
            }
        }

        private void AddGrace_Click(object sender, RoutedEventArgs e)
        {
            if (AvailableGracesTree.SelectedItem is Grace g && !SelectedGraces.Contains(g))
                SelectedGraces.Add(g);
        }

        private void RemoveGrace_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedGracesList.SelectedItem is Grace g)
            {
                SelectedGraces.Remove(g);
                ActivateSelectedButton.IsEnabled = SelectedGraces.Any();
                _hasUnsavedChanges = true; // mark unsaved changes
            }
        }

        private void ActivateSelected_Click(object sender, RoutedEventArgs e)
        {
            if (AvailableGracesTree.SelectedItem is Grace g)
            {
                UnlockGrace(g);
            }
        }

        private void ActivateGroup_Click(object sender, RoutedEventArgs e)
        {
            foreach (var selected in SelectedGraces)
            {
                if (selected is Grace)
                {
                    UnlockGrace(selected);            
                }
            }
        }

        private void UnlockGrace(Grace g)
        {
            _process.getSetEventFlag(g.ID, true);
            if (g.Area == "Ainsel River" ||
                g.Area == "Nokron, Eternal City" ||
                g.Area == "Deeproot Depths")
            {
                _process.getSetEventFlag(82001, true); // underground map -- force
            }
        }

        #endregion

        #region Filter

        private void FilterBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filter = FilterBox.Text.Trim().ToLower();
            AvailableGracesGrouped.Clear();

            foreach (var area in _allGraces)
            {
                var filteredArea = new AreaGroup { Area = area.Area };

                filteredArea.GracesWithoutSubArea = area.GracesWithoutSubArea
                    .Where(g => g.Name.ToLower().Contains(filter) || area.Area.ToLower().Contains(filter))
                    .ToList();

                filteredArea.SubAreas = area.SubAreas
                    .Select(sa => new SubAreaGroup
                    {
                        SubArea = sa.SubArea,
                        Graces = sa.Graces
                            .Where(g =>
                                g.Name.ToLower().Contains(filter) ||
                                sa.SubArea.ToLower().Contains(filter) ||
                                area.Area.ToLower().Contains(filter))
                            .ToList()
                    })
                    .Where(sa => sa.Graces.Any())
                    .ToList();

                if (filteredArea.GracesWithoutSubArea.Any() || filteredArea.SubAreas.Any())
                    AvailableGracesGrouped.Add(filteredArea);
            }
        }

        private void ClearFilter_Click(object sender, RoutedEventArgs e)
        {
            FilterBox.Text = "";
        }

        #endregion

        #region Profile management

        private void SaveProfile_Click(object sender, RoutedEventArgs e)
        {
            if (!SelectedGraces.Any())
            {
                MessageBox.Show("Cannot save an empty profile. Please select at least one grace.");
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

                MessageBox.Show($"Profile '{profileName}' saved/updated!");
            }
        }

        private void DeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            if (ProfileComboBox.SelectedItem == null) return;

            var profileName = ProfileComboBox.SelectedItem.ToString();
            if (!File.Exists(ProfilesFile)) return;

            var lines = File.ReadAllLines(ProfilesFile).ToList();
            var removed = lines.RemoveAll(l => l.StartsWith(profileName + ":"));

            if (removed > 0)
            {
                File.WriteAllLines(ProfilesFile, lines);
                LoadProfilesIntoComboBox();

                _suspendChangeTracking = true;
                SelectedGraces.Clear();
                _suspendChangeTracking = false;

                HasUnsavedChanges = false;
                MessageBox.Show($"Profile '{profileName}' deleted.");
            }
        }

        private void ProfileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProfileComboBox.SelectedItem == null) return;

            string selectedProfile = ProfileComboBox.SelectedItem.ToString();

            // Disable delete for empty profile
            DeleteProfileButton.IsEnabled = selectedProfile != EMPTY_PROFILE_NAME;

            // Clear selection if Empty Profile chosen
            if (selectedProfile == EMPTY_PROFILE_NAME)
            {
                _suspendChangeTracking = true;
                SelectedGraces.Clear();
                _suspendChangeTracking = false;
                HasUnsavedChanges = false;
                return;
            }

            // Load saved profile
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

            _suspendChangeTracking = true;
            SelectedGraces.Clear();

            foreach (var area in AvailableGracesGrouped)
            {
                foreach (var g in area.GracesWithoutSubArea)
                    if (ids.Contains(g.ID))
                        SelectedGraces.Add(g);

                foreach (var sub in area.SubAreas)
                    foreach (var g in sub.Graces)
                        if (ids.Contains(g.ID))
                            SelectedGraces.Add(g);
            }

            _suspendChangeTracking = false;
        }

        private void LoadProfilesIntoComboBox()
        {
            ProfileComboBox.Items.Clear();

            ProfileComboBox.Items.Add(EMPTY_PROFILE_NAME);

            if (File.Exists(ProfilesFile))
            {
                foreach (var line in File.ReadAllLines(ProfilesFile))
                {
                    var profileName = line.Split(':')[0];
                    ProfileComboBox.Items.Add(profileName);
                }
            }

            ProfileComboBox.SelectedIndex = 0;

            DeleteProfileButton.IsEnabled = false; // can't delete the empty profile
        }

        #endregion

        #region Unsaved Changes on Close

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (HasUnsavedChanges)
            {
                var result = MessageBox.Show(
                    "You have unsaved changes. Are you sure you want to close?",
                    "Unsaved Changes",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                    return;
                }
            }

            base.OnClosing(e);
        }

        #endregion
    }
}
