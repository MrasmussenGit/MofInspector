
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace MofInspector
{
    public partial class CompareWindow : Window
    {
        private Mof mof1;
        private Mof mof2;
        private List<dynamic> allResults = new List<dynamic>();

        public CompareWindow()
        {
            InitializeComponent();
        }

        private void Browse1_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "MOF Files (*.mof)|*.mof|All Files (*.*)|*.*"
            };
            if (dlg.ShowDialog() == true)
            {
                FilePath1.Text = dlg.FileName;
            }
        }

        private void Browse2_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "MOF Files (*.mof)|*.mof|All Files (*.*)|*.*"
            };
            if (dlg.ShowDialog() == true)
            {
                FilePath2.Text = dlg.FileName;
            }
        }

        private void DifferencesList_ItemDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (DifferencesList.SelectedItem is null) return;

            // The row items are anonymous objects; use dynamic to read RuleId
            dynamic row = DifferencesList.SelectedItem;
            string ruleId = row?.RuleId as string;
            if (string.IsNullOrWhiteSpace(ruleId)) return;

            // Resolve the rule from both files (may be null in Missing cases)
            var rule1 = mof1?.Rules.FirstOrDefault(r => r.RuleId == ruleId);
            var rule2 = mof2?.Rules.FirstOrDefault(r => r.RuleId == ruleId);

            // Resolve contributing instances for each file
            // (Instances where ResourceID contains this RuleId)
            List<MofInstance> inst1 = new();
            List<MofInstance> inst2 = new();

            if (mof1 != null)
            {
                inst1 = mof1.Instances
                    .Where(inst => inst.Properties.TryGetValue("ResourceID", out var rid)
                                   && mof1.ExtractRuleIds(rid).Contains(ruleId))
                    .ToList();
            }
            if (mof2 != null)
            {
                inst2 = mof2.Instances
                    .Where(inst => inst.Properties.TryGetValue("ResourceID", out var rid)
                                   && mof2.ExtractRuleIds(rid).Contains(ruleId))
                    .ToList();
            }

            // Open the details dialog
            var dlg = new CompareDetailWindow(ruleId, rule1, rule2, inst1, inst2)
            {
                Owner = this
            };
            dlg.ShowDialog();
        }


        private async void Compare_Click(object sender, RoutedEventArgs e)
        {
            if (!File.Exists(FilePath1.Text) || !File.Exists(FilePath2.Text))
            {
                MessageBox.Show("Please select both MOF files.");
                return;
            }

            // Capture file paths before async work
            string path1 = FilePath1.Text;
            string path2 = FilePath2.Text;

            // Show progress
            StatusText.Text = "Loading files...";
            DifferencesList.ItemsSource = null;

            // Parse MOF files on background thread
            await Task.Run(() =>
            {
                mof1 = new Mof(path1);
                mof2 = new Mof(path2);
            });

            StatusText.Text = "Building comparison...";
            BuildComparisonResults();
            ApplyFilters();

            StatusText.Text = "Comparison complete.";
        }
        private void BuildComparisonResults()
        {
            allResults.Clear();

            var allRuleIds = mof1.Rules.Select(r => r.RuleId)
                .Union(mof2.Rules.Select(r => r.RuleId))
                .Distinct();

            foreach (var ruleId in allRuleIds)
            {
                var rule1 = mof1.Rules.FirstOrDefault(r => r.RuleId == ruleId);
                var rule2 = mof2.Rules.FirstOrDefault(r => r.RuleId == ruleId);

                string category = rule1?.Category ?? rule2?.Category ?? "Unknown";

                // Handle missing rules
                if (rule1 == null)
                {
                    allResults.Add(new { RuleId = ruleId, Status = "Missing in File 1", Details = "", Category = category });
                    continue;
                }
                if (rule2 == null)
                {
                    allResults.Add(new { RuleId = ruleId, Status = "Missing in File 2", Details = "", Category = category });
                    continue;
                }

                // Handle null or empty Details
                if (rule1.Details == null || rule2.Details == null || rule1.Details.Count == 0 || rule2.Details.Count == 0)
                {
                    string rawDiff = GetRawTextDiff(rule1.RawText, rule2.RawText);
                    allResults.Add(new
                    {
                        RuleId = ruleId,
                        Status = "Parsing Error",
                        Details = $"One or both rules have no parsed details. Raw diff: {rawDiff}",
                        Category = category
                    });
                    continue;
                }

                // Compare Details dictionaries
                bool areEqual = rule1.Details.Count == rule2.Details.Count &&
                                rule1.Details.All(kvp =>
                                    rule2.Details.TryGetValue(kvp.Key, out var val) &&
                                    string.Equals(val?.Trim(), kvp.Value?.Trim(), StringComparison.OrdinalIgnoreCase));

                if (areEqual)
                {
                    allResults.Add(new { RuleId = ruleId, Status = "Match", Details = "All properties identical", Category = category });
                }
                else
                {
                    // Build detailed differences
                    var diffDetails = string.Join(", ",
                        rule1.Details.Where(kvp => !rule2.Details.ContainsKey(kvp.Key) || rule2.Details[kvp.Key] != kvp.Value)
                                     .Select(kvp => $"{kvp.Key}: {kvp.Value} vs {rule2.Details.GetValueOrDefault(kvp.Key, "N/A")}")
                        .Concat(rule2.Details.Where(kvp => !rule1.Details.ContainsKey(kvp.Key))
                                     .Select(kvp => $"{kvp.Key}: N/A vs {kvp.Value}")));

                    allResults.Add(new { RuleId = ruleId, Status = "Different", Details = diffDetails, Category = category });
                }
            }
        }

        // Simple raw text diff fallback
        private string GetRawTextDiff(string text1, string text2)
        {
            if (string.Equals(text1, text2, StringComparison.Ordinal))
                return "Raw text identical";

            return $"RawText differs. File1: {Truncate(text1)}, File2: {Truncate(text2)}";
        }

        private string Truncate(string input, int length = 100)
        {
            if (string.IsNullOrEmpty(input)) return "EMPTY";
            return input.Length <= length ? input : input.Substring(0, length) + "...";
        }


        private void CollapseAll_Checked(object sender, RoutedEventArgs e)
        {
            bool collapse = CollapseAllCheckBox.IsChecked == true;

            foreach (var expander in FindVisualChildren<Expander>(DifferencesList))
            {
                expander.IsExpanded = !collapse;
            }
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child is T t)
                    {
                        yield return t;
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
            }


        }


        private void FilterResults(object sender, RoutedEventArgs e)
        {
            ApplyFilters();
        }

        private void ApplyFilters()
        {
            var filtered = allResults.Where(x =>
                (ShowDifferencesCheckBox.IsChecked == true && x.Status == "Different") ||
                (ShowMissingCheckBox.IsChecked == true && x.Status.StartsWith("Missing")) ||
                (ShowMatchesCheckBox.IsChecked == true && x.Status == "Match") ||
                (ShowDifferencesCheckBox.IsChecked == false && ShowMissingCheckBox.IsChecked == false && ShowMatchesCheckBox.IsChecked == false)
            ).ToList();

            var view = new ListCollectionView(filtered);
            view.GroupDescriptions.Add(new PropertyGroupDescription("Category"));
            DifferencesList.ItemsSource = view;
        }

        private void ResetFilters_Click(object sender, RoutedEventArgs e)
        {
            ShowDifferencesCheckBox.IsChecked = false;
            ShowMissingCheckBox.IsChecked = false;
            ShowMatchesCheckBox.IsChecked = false;
            ApplyFilters();
        }
    }
}
