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
            set
            {
                _hasUnsavedChanges = value;
                SaveProfileButton.IsEnabled = value;
            }
        }

        private List<AreaGroup> _allGraces = new List<AreaGroup>();
        private bool _suspendChangeTracking = false;

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
            {
                AvailableGracesGrouped.Add(area);
            }

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
                if (!_suspendChangeTracking)
                    HasUnsavedChanges = true;

                ActivateSelectedButton.IsEnabled = SelectedGraces.Any();
            };

            LoadProfilesIntoComboBox();
            _process = process;
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
                _process.getSetEventFlag(g.ID, true);
                if (g.Area.ToLower() == "Ainsel River" ||
                    g.Area.ToLower() == "Nokron, Eternal City" ||
                    g.Area.ToLower() == "Deeproot Depths")
                {
                    _process.getSetEventFlag(82001, true); // underground map -- force
                }
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
            if (!SelectedGraces.Any()) return;

            string profileName = ProfileComboBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(profileName))
            {
                MessageBox.Show("Please enter a profile name.");
                return;
            }

            _suspendChangeTracking = true;

            var line = profileName + ":" + string.Join(",", SelectedGraces.Select(g => g.ID));
            var lines = File.Exists(ProfilesFile) ? File.ReadAllLines(ProfilesFile).ToList() : new List<string>();

            // Overwrite existing profile if present
            lines.RemoveAll(l => l.StartsWith(profileName + ":"));
            lines.Add(line);

            File.WriteAllLines(ProfilesFile, lines);

            LoadProfilesIntoComboBox();
            ProfileComboBox.SelectedItem = profileName;

            _suspendChangeTracking = false;
            HasUnsavedChanges = false;

            MessageBox.Show($"Profile '{profileName}' saved/updated!");
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

            if (HasUnsavedChanges)
            {
                var result = MessageBox.Show(
                    "You have unsaved changes. Do you want to discard them and load the selected profile?",
                    "Unsaved Changes",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.No)
                {
                    if (e.RemovedItems.Count > 0)
                        ProfileComboBox.SelectedItem = e.RemovedItems[0];
                    return;
                }
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

            if (!File.Exists(ProfilesFile)) return;

            foreach (var line in File.ReadAllLines(ProfilesFile))
                ProfileComboBox.Items.Add(line.Split(':')[0]);
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
