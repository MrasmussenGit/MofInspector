using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MofInspector
{
    public partial class StigDetailsWindow : Window
    {
        private List<MofRule> _allRules = new List<MofRule>();
        private List<MofRule> _filteredRules = new List<MofRule>();
        private string _stigName;

        public StigDetailsWindow(string stigName, List<MofRule> rules)
        {
            InitializeComponent();
            _stigName = stigName;
            _allRules = rules;
            _filteredRules = new List<MofRule>(_allRules);

            LoadData();
        }

        private void LoadData()
        {
            StigNameText.Text = _stigName;

            int totalRules = _allRules.Count;
            int skippedRules = _allRules.Count(r => r.IsSkipped);
            int activeRules = totalRules - skippedRules;

            TotalRulesText.Text = $"Total Rules: {totalRules}";
            SkippedRulesText.Text = $"Skipped: {skippedRules}";
            ActiveRulesText.Text = $"Active: {activeRules}";

            RefreshRulesList();
        }

        private void RefreshRulesList()
        {
            RulesListBox.ItemsSource = null;
            RulesListBox.ItemsSource = _filteredRules;
            ResultCountText.Text = $"Showing {_filteredRules.Count} rules";

            // Auto-select the first item if available
            if (_filteredRules.Count > 0)
            {
                RulesListBox.SelectedIndex = 0;
                RulesListBox.ScrollIntoView(RulesListBox.SelectedItem);
            }
            else
            {
                // No rules to show, hide details
                HideRuleDetails();
            }
        }

        private void FilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void ApplyFilters()
        {
            if (FilterComboBox == null || SearchTextBox == null)
                return;

            _filteredRules = new List<MofRule>(_allRules);

            // Apply filter combo
            var selectedFilter = (FilterComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
            if (selectedFilter == "Skipped Only")
            {
                _filteredRules = _filteredRules.Where(r => r.IsSkipped).ToList();
            }
            else if (selectedFilter == "Active Only")
            {
                _filteredRules = _filteredRules.Where(r => !r.IsSkipped).ToList();
            }

            // Apply search
            string searchText = SearchTextBox.Text?.Trim();
            if (!string.IsNullOrEmpty(searchText))
            {
                _filteredRules = _filteredRules.Where(r =>
                    r.RuleId.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    r.Category.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    r.Details.Any(kvp => kvp.Key.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                                         kvp.Value.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                ).ToList();
            }

            RefreshRulesList();
        }

        private void RulesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RulesListBox.SelectedItem is MofRule rule)
            {
                ShowRuleDetails(rule);
            }
            else
            {
                HideRuleDetails();
            }
        }

        private void ShowRuleDetails(MofRule rule)
        {
            DetailsPlaceholder.Visibility = Visibility.Collapsed;
            DetailsContent.Visibility = Visibility.Visible;

            DetailRuleId.Text = rule.RuleId;
            DetailRuleStatus.Text = rule.IsSkipped ? "SKIPPED" : "ACTIVE";
            DetailStatusBadge.Background = rule.IsSkipped
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C"))
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27AE60"));

            DetailPropertiesDisplay.ItemsSource = rule.Details;
        }

        private void HideRuleDetails()
        {
            DetailsPlaceholder.Visibility = Visibility.Visible;
            DetailsContent.Visibility = Visibility.Collapsed;
        }

        private void TotalRulesBorder_Click(object sender, MouseButtonEventArgs e)
        {
            FilterComboBox.SelectedIndex = 0; // All Rules
        }

        private void SkippedRulesBorder_Click(object sender, MouseButtonEventArgs e)
        {
            FilterComboBox.SelectedIndex = 1; // Skipped Only
        }

        private void ActiveRulesBorder_Click(object sender, MouseButtonEventArgs e)
        {
            FilterComboBox.SelectedIndex = 2; // Active Only
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
