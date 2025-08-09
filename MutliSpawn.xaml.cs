using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace EldenRingTool
{
    public partial class MutliSpawn : Window
    {
        private List<string> allAvailableItems;
        private Point startPoint;
        private const string SaveFileName = "savedLists.csv";

        // Holds all saved lists by name
        private Dictionary<string, List<string>> savedLists = new Dictionary<string, List<string>>();

        private void RefreshAvailableList(string filter = "")
        {
            AvailableListBox.Items.Clear();
            foreach (var item in allAvailableItems.Where(i => i.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
                                                             && !SelectedListBox.Items.Contains(i)))
            {
                AvailableListBox.Items.Add(item);
            }
        }

        private void FilterBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            RefreshAvailableList(FilterBox.Text);
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var selected = AvailableListBox.SelectedItems.Cast<string>().ToList();
            foreach (var item in selected)
                SelectedListBox.Items.Add(item);

            RefreshAvailableList(FilterBox.Text);
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            var selected = SelectedListBox.SelectedItems.Cast<string>().ToList();
            foreach (var item in selected)
                SelectedListBox.Items.Remove(item);

            RefreshAvailableList(FilterBox.Text);
        }

        private void AddAllButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in AvailableListBox.Items.Cast<string>().ToList())
                SelectedListBox.Items.Add(item);

            RefreshAvailableList(FilterBox.Text);
        }

        private void RemoveAllButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedListBox.Items.Clear();
            RefreshAvailableList(FilterBox.Text);
        }

        // Drag start
        private void ListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            startPoint = e.GetPosition(null);
        }

        // Drag move
        private void ListBox_MouseMove(object sender, MouseEventArgs e)
        {
            Point mousePos = e.GetPosition(null);
            Vector diff = startPoint - mousePos;

            if (e.LeftButton == MouseButtonState.Pressed &&
                (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                 Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance))
            {
                ListBox listBox = sender as ListBox;
                if (listBox?.SelectedItem == null) return;

                string selectedItem = listBox.SelectedItem as string;
                DragDrop.DoDragDrop(listBox, selectedItem, DragDropEffects.Move);
            }
        }

        // Drop handlers
        private void SelectedListBox_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(string)))
            {
                var item = (string)e.Data.GetData(typeof(string));
                if (!SelectedListBox.Items.Contains(item))
                    SelectedListBox.Items.Add(item);

                RefreshAvailableList(FilterBox.Text);
            }
        }

        private void AvailableListBox_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(string)))
            {
                var item = (string)e.Data.GetData(typeof(string));
                SelectedListBox.Items.Remove(item);
                RefreshAvailableList(FilterBox.Text);
            }
        }

        // Save list by name
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            string listName = ListNameBox.Text.Trim();
            if (string.IsNullOrEmpty(listName))
            {
                MessageBox.Show("Please enter a list name.");
                return;
            }

            var items = SelectedListBox.Items.Cast<string>().ToList();
            savedLists[listName] = items;

            SaveAllListsToCsv();

            if (!SavedListsCombo.Items.Contains(listName))
                SavedListsCombo.Items.Add(listName);

            MessageBox.Show($"List '{listName}' saved.");
        }

        // Load selected list from combo
        private void SavedListsCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SavedListsCombo.SelectedItem is string selectedName && savedLists.ContainsKey(selectedName))
            {
                SelectedListBox.Items.Clear();
                foreach (var item in savedLists[selectedName])
                    SelectedListBox.Items.Add(item);

                RefreshAvailableList(FilterBox.Text);
                ListNameBox.Text = selectedName;
            }
        }

        // Load all lists from CSV file
        private void LoadSavedLists()
        {
            savedLists.Clear();
            SavedListsCombo.Items.Clear();

            if (!File.Exists(SaveFileName)) return;

            var lines = File.ReadAllLines(SaveFileName);
            foreach (var line in lines)
            {
                var parts = ParseCsvLine(line);
                if (parts.Count > 0)
                {
                    string listName = parts[0];
                    var items = parts.Skip(1).ToList();
                    savedLists[listName] = items;
                    SavedListsCombo.Items.Add(listName);
                }
            }
        }

        // Save all lists to CSV file
        private void SaveAllListsToCsv()
        {
            var lines = new List<string>();

            foreach (var kvp in savedLists)
            {
                var listName = EscapeCsvField(kvp.Key);
                var items = kvp.Value.Select(EscapeCsvField);
                string line = listName + "," + string.Join(",", items);
                lines.Add(line);
            }

            File.WriteAllLines(SaveFileName, lines);
        }

        // Helper: parse CSV line with quotes
        private List<string> ParseCsvLine(string line)
        {
            var values = new List<string>();
            bool inQuotes = false;
            var value = new StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        // Escaped quote
                        value.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    values.Add(value.ToString());
                    value.Clear();
                }
                else
                {
                    value.Append(c);
                }
            }
            values.Add(value.ToString());

            return values;
        }

        // Helper: escape fields for CSV
        private string EscapeCsvField(string field)
        {
            if (field.Contains('"'))
                field = field.Replace("\"", "\"\"");
            if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
                field = $"\"{field}\"";
            return field;
        }
    }
}
