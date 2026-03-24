using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MofInspector
{
    public class StigGroup
    {
        public string StigName { get; set; }
        public int TotalRules { get; set; }
        public int SkippedRules { get; set; }
        public int ActiveRules { get; set; }
        public List<MofRule> Rules { get; set; }
    }

    public partial class InspectWindow : Window
    {
        private Mof _mof;
        private List<StigGroup> _stigGroups = new List<StigGroup>();
        private string _mofFilePath;

        public InspectWindow(string mofFilePath)
        {
            InitializeComponent();
            _mofFilePath = mofFilePath;
            LoadMofFile();
        }

        private void LoadMofFile()
        {
            try
            {
                _mof = new Mof(_mofFilePath);

                // Update header
                MofFileNameText.Text = Path.GetFileName(_mofFilePath);
                FilePathText.Text = $"Path: {_mofFilePath}";
                TotalRulesText.Text = $"Total Rules: {_mof.Rules.Count}";

                // Group rules by STIG/Product (ClassName)
                GroupRulesBySTIG();

                // Update summary
                StigSummaryText.Text = $"STIGs: {_stigGroups.Count}";

                // Populate DataGrid
                StigsDataGrid.ItemsSource = _stigGroups;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading MOF: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        private void GroupRulesBySTIG()
        {
            _stigGroups.Clear();

            // Try to extract STIG metadata from the MOF file
            var stigMetadata = ExtractStigMetadata();

            // Get all instances that produce rules (have ResourceID)
            var ruleProducingInstances = _mof.Instances
                .Where(i => i.Properties.ContainsKey("ResourceID"))
                .GroupBy(i => i.ClassName)
                .OrderBy(g => g.Key);

            foreach (var classGroup in ruleProducingInstances)
            {
                // Extract distinct rule IDs for this class
                var distinctRuleIds = classGroup
                    .SelectMany(inst =>
                    {
                        var resourceId = inst.Properties["ResourceID"];
                        return _mof.ExtractRuleIds(resourceId);
                    })
                    .Distinct();

                // Get the actual MofRule objects
                var rulesForThisStig = distinctRuleIds
                    .Select(id => _mof.Rules.FirstOrDefault(r => r.RuleId == id))
                    .Where(r => r != null)
                    .OrderBy(r => r.RuleId)
                    .ToList();

                if (rulesForThisStig.Count == 0)
                    continue;

                int skippedCount = rulesForThisStig.Count(r => r.IsSkipped);
                int activeCount = rulesForThisStig.Count - skippedCount;

                // Try to create a friendly STIG name from metadata
                string stigName = CreateStigName(classGroup.Key, stigMetadata);

                _stigGroups.Add(new StigGroup
                {
                    StigName = stigName,
                    TotalRules = rulesForThisStig.Count,
                    SkippedRules = skippedCount,
                    ActiveRules = activeCount,
                    Rules = rulesForThisStig
                });
            }
        }

        private Dictionary<string, string> ExtractStigMetadata()
        {
            var metadata = new Dictionary<string, string>();

            // Look for metadata instances (typically they don't have ResourceID but have STIG-related properties)
            var metadataInstances = _mof.Instances
                .Where(i => !i.Properties.ContainsKey("ResourceID") &&
                           (i.Properties.ContainsKey("TechnologyRole") ||
                            i.Properties.ContainsKey("TechnologyVersion") ||
                            i.Properties.ContainsKey("OsVersion") ||
                            i.Properties.ContainsKey("StigVersion")))
                .ToList();

            // Extract common metadata properties
            foreach (var instance in metadataInstances)
            {
                foreach (var prop in instance.Properties)
                {
                    if (!metadata.ContainsKey(prop.Key))
                    {
                        metadata[prop.Key] = prop.Value;
                    }
                }
            }

            return metadata;
        }

        private string CreateStigName(string className, Dictionary<string, string> metadata)
        {
            // If we have metadata, try to build a friendly name
            if (metadata.Count > 0)
            {
                var parts = new List<string>();

                if (metadata.TryGetValue("TechnologyRole", out var role) && !string.IsNullOrEmpty(role))
                {
                    parts.Add(role);
                }

                if (metadata.TryGetValue("TechnologyVersion", out var techVersion) && !string.IsNullOrEmpty(techVersion))
                {
                    parts.Add(techVersion);
                }

                if (metadata.TryGetValue("OsVersion", out var osVersion) && !string.IsNullOrEmpty(osVersion))
                {
                    parts.Add($"({osVersion})");
                }

                if (parts.Count > 0)
                {
                    return $"{string.Join(" ", parts)} - {className}";
                }
            }

            // Fallback to just the DSC Resource Type
            return className;
        }

        private void StigsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ViewDetailsButton.IsEnabled = StigsDataGrid.SelectedItems.Count > 0;

            // Update button text based on selection count
            if (StigsDataGrid.SelectedItems.Count > 1)
            {
                ViewDetailsButton.Content = $"View Selected ({StigsDataGrid.SelectedItems.Count})";
            }
            else if (StigsDataGrid.SelectedItems.Count == 1)
            {
                ViewDetailsButton.Content = "View Selected";
            }
        }

        private void StigsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (StigsDataGrid.SelectedItems.Count > 0)
            {
                OpenDetailsWindow();
            }
        }

        private void ViewDetailsButton_Click(object sender, RoutedEventArgs e)
        {
            if (StigsDataGrid.SelectedItems.Count > 0)
            {
                OpenDetailsWindow();
            }
        }

        private void ViewAllRulesButton_Click(object sender, RoutedEventArgs e)
        {
            if (_stigGroups.Count == 0)
            {
                MessageBox.Show("No STIGs loaded.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Combine all rules from all STIGs
            var allRules = _stigGroups.SelectMany(g => g.Rules).Distinct().OrderBy(r => r.RuleId).ToList();
            var detailsWindow = new StigDetailsWindow("All STIGs - Combined View", allRules);
            detailsWindow.ShowDialog();
        }

        private void OpenDetailsWindow()
        {
            var selectedGroups = StigsDataGrid.SelectedItems.Cast<StigGroup>().ToList();

            if (selectedGroups.Count == 0)
                return;

            if (selectedGroups.Count == 1)
            {
                // Single selection - show just that STIG
                var group = selectedGroups[0];
                var detailsWindow = new StigDetailsWindow(group.StigName, group.Rules);
                detailsWindow.ShowDialog();
            }
            else
            {
                // Multiple selections - combine rules from selected STIGs
                var combinedRules = selectedGroups
                    .SelectMany(g => g.Rules)
                    .Distinct()
                    .OrderBy(r => r.RuleId)
                    .ToList();

                var stigNames = string.Join(", ", selectedGroups.Select(g => g.StigName).Take(3));
                if (selectedGroups.Count > 3)
                    stigNames += $" (and {selectedGroups.Count - 3} more)";

                var detailsWindow = new StigDetailsWindow($"Combined View: {stigNames}", combinedRules);
                detailsWindow.ShowDialog();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
