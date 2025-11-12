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

                var rulesRoot = new TreeViewItem { Header = $"Rules ({mof.Rules.Count})" };

                foreach (var rule in mof.Rules.OrderBy(r => r.RuleId))
                {
                    var ruleNode = new TreeViewItem
                    {
                        Header = rule.RuleId,
                        Foreground = rule.IsSkipped ? Brushes.Red : Brushes.Green,
                        Tag = rule // store rule object for selection
                    };

                    rulesRoot.Items.Add(ruleNode);
                }

                RuleTree.Items.Add(rulesRoot);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading MOF: {ex.Message}");
            }
        }

        private void FilterRules(object sender, RoutedEventArgs e)
        {
            if (mof == null) return;

            RuleTree.Items.Clear();

            var filteredRules = ShowSkippedCheckBox.IsChecked == true
                ? mof.Rules.Where(r => r.IsSkipped).OrderBy(r => r.RuleId)
                : mof.Rules.OrderBy(r => r.RuleId);

            var rulesRoot = new TreeViewItem { Header = $"Rules ({filteredRules.Count()})" };

            foreach (var rule in filteredRules)
            {
                var ruleNode = new TreeViewItem
                {
                    Header = rule.RuleId,
                    Foreground = rule.IsSkipped ? Brushes.Red : Brushes.Black,
                    Tag = rule
                };

                rulesRoot.Items.Add(ruleNode);
            }

            RuleTree.Items.Add(rulesRoot);
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
                // Do not add instance properties, since they are the same
            }
        }
    }
}
