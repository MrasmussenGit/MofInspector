using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MofInspector
{
    public partial class InspectWindow : Window
    {
        private Mof mof;

        public InspectWindow()
        {
            InitializeComponent();
            DetailsList.KeyDown += DetailsList_KeyDown;
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "MOF Files (*.mof)|*.mof|All Files (*.*)|*.*",
                Title = "Select a MOF File"
            };

            if (dialog.ShowDialog() == true)
            {
                FilePathBox.Text = dialog.FileName;
                LoadMofFile(dialog.FileName);
            }
        }

        private void Load_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(FilePathBox.Text))
            {
                LoadMofFile(FilePathBox.Text);
            }
            else
            {
                MessageBox.Show("Please enter or select a file path.");
            }
        }

        private void LoadMofFile(string filePath)
        {
            try
            {
                mof = new Mof(filePath);
                RuleTree.Items.Clear();

                RuleCountLabel.Content = $"Total Rules: {mof.Rules.Count}";

                PopulateRulesGroupedByClass(onlySkipped: false);
                ApplyExpandState();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading MOF: {ex.Message}");
            }
        }

        /// <summary>
        /// Group rules by instance type (ClassName). Each top-level node is a ClassName,
        /// expanded it shows distinct rules from all instances of that type.
        /// </summary>
        private void PopulateRulesGroupedByClass(bool onlySkipped)
        {
            RuleTree.Items.Clear();
            if (mof == null) return;

            var ruleProducingInstances = mof.Instances
                .Where(i => i.Properties.ContainsKey("ResourceID"))
                .ToList();

            var groups = ruleProducingInstances
                .GroupBy(i => i.ClassName)
                .OrderBy(g => g.Key);

            foreach (var classGroup in groups)
            {
                var distinctRuleIds = classGroup
                    .SelectMany(inst =>
                    {
                        var resourceId = inst.Properties["ResourceID"];
                        return mof.ExtractRuleIds(resourceId);
                    })
                    .Distinct()
                    .Select(id => mof.Rules.FirstOrDefault(r => r.RuleId == id))
                    .Where(r => r != null);

                if (onlySkipped)
                    distinctRuleIds = distinctRuleIds.Where(r => r.IsSkipped);

                var ruleList = distinctRuleIds
                    .OrderBy(r => r.RuleId)
                    .ToList();

                if (ruleList.Count == 0)
                    continue;

                var classNode = new TreeViewItem
                {
                    Header = $"{classGroup.Key} (rules: {ruleList.Count})",
                    FontWeight = FontWeights.Bold
                };

                foreach (var rule in ruleList)
                {
                    var ruleNode = new TreeViewItem
                    {
                        Header = rule.RuleId,
                        Foreground = rule.IsSkipped ? Brushes.Red : Brushes.Green,
                        Tag = rule
                    };
                    classNode.Items.Add(ruleNode);
                }

                RuleTree.Items.Add(classNode);
            }
        }

        private void FilterRules(object sender, RoutedEventArgs e)
        {
            if (mof == null) return;
            bool onlySkipped = ShowSkippedCheckBox.IsChecked == true;
            PopulateRulesGroupedByClass(onlySkipped);
            ApplyExpandState();
        }

        private void ExpandAllCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            ApplyExpandState();
        }

        private void ApplyExpandState()
        {
            bool expand = ExpandAllCheckBox.IsChecked == true;
            foreach (var item in RuleTree.Items)
            {
                if (item is TreeViewItem tvi)
                    tvi.IsExpanded = expand;
            }
        }

        private void DetailsList_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.C && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                var selectedItems = DetailsList.SelectedItems.Cast<KeyValuePair<string, string>>();
                var text = string.Join(Environment.NewLine, selectedItems.Select(kvp => $"{kvp.Key}: {kvp.Value}"));
                Clipboard.SetText(text);
            }
        }

        private void RuleTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            DetailsList.Items.Clear();

            if (e.NewValue is TreeViewItem selectedNode && selectedNode.Tag is MofRule selectedRule)
            {
                foreach (var detail in selectedRule.Details)
                {
                    DetailsList.Items.Add(new KeyValuePair<string, string>(detail.Key, detail.Value));
                }
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}