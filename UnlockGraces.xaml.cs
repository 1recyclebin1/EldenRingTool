using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace EldenRingTool
{
    public partial class UnlockGraces : Window
    {
        public ObservableCollection<AreaGroup> AvailableGracesGrouped { get; set; } = new ObservableCollection<AreaGroup>();
        public ObservableCollection<Grace> SelectedGraces { get; set; } = new ObservableCollection<Grace>();

        public UnlockGraces()
        {
            InitializeComponent();
            DataContext = this;

            // Example: populate AvailableGracesGrouped from GraceDB
            var grouped = GraceDB.Graces
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
        }

        private void AvailableGracesTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            // Optional: handle selection if needed
        }

        private void AvailableGraces_DoubleClick(object sender, RoutedEventArgs e)
        {
            // Add selected grace from tree to SelectedGraces
            if (AvailableGracesTree.SelectedItem is Grace g && !SelectedGraces.Contains(g))
            {
                SelectedGraces.Add(g);
                ActivateSelectedButton.IsEnabled = SelectedGraces.Any();
            }
        }

        private void AddGrace_Click(object sender, RoutedEventArgs e)
        {
            if (AvailableGracesTree.SelectedItem is Grace g && !SelectedGraces.Contains(g))
            {
                SelectedGraces.Add(g);
                ActivateSelectedButton.IsEnabled = SelectedGraces.Any();
            }
        }

        private void RemoveGrace_Click(object sender, RoutedEventArgs e)
        {
            foreach (var g in SelectedGracesList.SelectedItems.Cast<Grace>().ToList())
            {
                SelectedGraces.Remove(g);
            }
            ActivateSelectedButton.IsEnabled = SelectedGraces.Any();
        }

        private void ActivateSelected_Click(object sender, RoutedEventArgs e)
        {
            // Your activation logic here
            MessageBox.Show($"Activating {SelectedGraces.Count} graces!");
        }

        private void FilterBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            // Optional: implement filtering
        }

        private void ClearFilter_Click(object sender, RoutedEventArgs e)
        {
            FilterBox.Text = "";
        }
    }
}
