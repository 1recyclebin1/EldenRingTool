using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace EldenRingTool
{
    public partial class SaveProfileDialog : Window
    {
        public string SelectedProfileName { get; private set; }
        private readonly HashSet<string> _existingProfiles;

        public SaveProfileDialog(IEnumerable<string> existingProfiles, string windowName, string currentProfile = "")
        {
            this.Title = windowName;

            InitializeComponent();
            _existingProfiles = new HashSet<string>(existingProfiles);
            ProfileNameComboBox.ItemsSource = _existingProfiles.ToList();

            if (string.Equals(currentProfile, UnlockGraces.EMPTY_PROFILE_NAME, StringComparison.OrdinalIgnoreCase))
                ProfileNameComboBox.Text = string.Empty;
            else
                ProfileNameComboBox.Text = currentProfile;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            string name = ProfileNameComboBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(name) ||
                string.Equals(name, UnlockGraces.EMPTY_PROFILE_NAME, StringComparison.OrdinalIgnoreCase))
            {
                string message = string.IsNullOrWhiteSpace(name)
                    ? "Please enter a profile name."
                    : $"'{UnlockGraces.EMPTY_PROFILE_NAME}' is reserved. Please choose a different name.";

                MessageBox.Show(message, "Invalid Name", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_existingProfiles.Contains(name))
            {
                var result = MessageBox.Show(
                    $"A profile named '{name}' already exists. Do you want to overwrite it?",
                    "Confirm Overwrite",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                    return; // stay in dialog
            }

            SelectedProfileName = name;
            DialogResult = true;
        }
    }
}
