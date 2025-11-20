using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Media;

namespace MofInspector
{
    public partial class CompareWindow : Window
    {
        private Mof? mof1;
        private Mof? mof2;
        private readonly List<dynamic> allResults = new();

        // Regex patterns for PowerStig version normalization
        private static readonly Regex PowerStigPathSegment = new(@"(?i)(PowerStig)[\\/](v?\d+(?:\.\d+)*(?:[a-z])?)(?=[\\/])", RegexOptions.Compiled);
        private static readonly Regex PowerStigInline = new(@"(?i)\bPowerStig[-_](v?\d+(?:\.\d+)*(?:[a-z])?)\b", RegexOptions.Compiled);
        private static readonly Regex PowerStigGenericMixed = new(@"(?i)\b(PowerStig)(?:[-_/\\])(v?\d+(?:\.\d+)*(?:[a-z])?)(?=[-_/\\]|$)", RegexOptions.Compiled);
        private static readonly Regex MultiWhitespace = new(@"\s+", RegexOptions.Compiled);

        public CompareWindow()
        {
            InitializeComponent();
        }

        private void LoadFile1_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "MOF Files (*.mof)|*.mof|All Files (*.*)|*.*"
            };
            if (dlg.ShowDialog() == true)
            {
                File1PathText.Text = dlg.FileName;
                TryRunComparison();
            }
        }

        private void LoadFile2_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "MOF Files (*.mof)|*.mof|All Files (*.*)|*.*"
            };
            if (dlg.ShowDialog() == true)
            {
                File2PathText.Text = dlg.FileName;
                TryRunComparison();
            }
        }

        private async void TryRunComparison()
        {
            var path1 = File1PathText.Text;
            var path2 = File2PathText.Text;

            if (string.IsNullOrWhiteSpace(path1) || string.IsNullOrWhiteSpace(path2))
                return;
            if (!File.Exists(path1) || !File.Exists(path2))
                return;

            DifferencesList.ItemsSource = null;

            await Task.Run(() =>
            {
                mof1 = new Mof(path1);
                mof2 = new Mof(path2);
            });

            BuildComparisonResults();
            ApplyFilters();
        }

        private void DifferencesList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => OpenSelectedRuleDetail();

        private void DifferencesList_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                OpenSelectedRuleDetail();
                e.Handled = true;
            }
            else if (e.Key == Key.C && Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && DifferencesList.SelectedItems.Count > 0)
            {
                var rows = DifferencesList.SelectedItems.Cast<object>();
                var text = string.Join(Environment.NewLine,
                    rows.Select(r =>
                    {
                        var t = r.GetType();
                        return $"{t.GetProperty("RuleId")?.GetValue(r)}\t{t.GetProperty("Status")?.GetValue(r)}\t{t.GetProperty("Details")?.GetValue(r)}";
                    }));
                Clipboard.SetText(text);
            }
        }

        private void OpenSelectedRuleDetail()
        {
            if (DifferencesList.SelectedItem == null) return;

            var selected = DifferencesList.SelectedItem;
            var ruleIdProp = selected.GetType().GetProperty("RuleId");
            var ruleId = ruleIdProp?.GetValue(selected) as string;
            if (string.IsNullOrWhiteSpace(ruleId)) return;

            var rule1 = mof1?.Rules.FirstOrDefault(r => r.RuleId == ruleId);
            var rule2 = mof2?.Rules.FirstOrDefault(r => r.RuleId == ruleId);

            var instances1 = mof1?.Instances
                .Where(inst => inst.Properties.TryGetValue("ResourceID", out var rid) &&
                               mof1.ExtractRuleIds(rid).Contains(ruleId))
                .ToList() ?? new List<MofInstance>();

            var instances2 = mof2?.Instances
                .Where(inst => inst.Properties.TryGetValue("ResourceID", out var rid) &&
                               mof2.ExtractRuleIds(rid).Contains(ruleId))
                .ToList() ?? new List<MofInstance>();

            var details = new CompareDetailWindow(ruleId, rule1, rule2, instances1, instances2)
            {
                Owner = this
            };
            details.ShowDialog();
        }

        private void BuildComparisonResults()
        {
            if (mof1 == null || mof2 == null) return;

            allResults.Clear();

            var allRuleIds = mof1.Rules.Select(r => r.RuleId)
                .Union(mof2.Rules.Select(r => r.RuleId))
                .Distinct()
                .OrderBy(r => r);

            foreach (var ruleId in allRuleIds)
            {
                var rule1 = mof1.Rules.FirstOrDefault(r => r.RuleId == ruleId);
                var rule2 = mof2.Rules.FirstOrDefault(r => r.RuleId == ruleId);
                string category = rule1?.Category ?? rule2?.Category ?? "Unknown";

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

                if (rule1.Details == null || rule2.Details == null || rule1.Details.Count == 0 || rule2.Details.Count == 0)
                {
                    allResults.Add(new
                    {
                        RuleId = ruleId,
                        Status = "Parsing Error",
                        Details = "One or both rules have no parsed details.",
                        Category = category
                    });
                    continue;
                }

                bool exactEqual = DictionariesExact(rule1.Details, rule2.Details);
                if (exactEqual)
                {
                    allResults.Add(new { RuleId = ruleId, Status = "Match", Details = "All properties identical", Category = category });
                    continue;
                }

                bool detailsIgnoringVersion = DictionariesIgnoringVersion(rule1.Details, rule2.Details);
                bool rawVersionOnly = RawTextDiffIsVersionOnly(rule1.RawText, rule2.RawText);

                if (detailsIgnoringVersion && rawVersionOnly)
                {
                    allResults.Add(new
                    {
                        RuleId = ruleId,
                        Status = "VersionOnly",
                        Details = "Only PowerStig version differs",
                        Category = category
                    });
                    continue;
                }

                var diffs = new List<string>();

                foreach (var kvp in rule1.Details)
                {
                    if (!rule2.Details.TryGetValue(kvp.Key, out var v2))
                    {
                        diffs.Add($"{kvp.Key}: {kvp.Value} vs (missing)");
                    }
                    else if (!string.Equals(kvp.Value?.Trim(), v2?.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        diffs.Add($"{kvp.Key}: {kvp.Value} vs {v2}");
                    }
                }
                foreach (var kvp in rule2.Details)
                {
                    if (!rule1.Details.ContainsKey(kvp.Key))
                        diffs.Add($"{kvp.Key}: (missing) vs {kvp.Value}");
                }

                allResults.Add(new
                {
                    RuleId = ruleId,
                    Status = "Different",
                    Details = string.Join("; ", diffs),
                    Category = category
                });
            }
        }

        private static bool DictionariesExact(Dictionary<string, string> a, Dictionary<string, string> b)
        {
            if (a.Count != b.Count) return false;
            foreach (var kvp in a)
            {
                if (!b.TryGetValue(kvp.Key, out var v2)) return false;
                if (!string.Equals(kvp.Value?.Trim(), v2?.Trim(), StringComparison.Ordinal))
                    return false;
            }
            return true;
        }

        private static bool DictionariesIgnoringVersion(Dictionary<string, string> a, Dictionary<string, string> b)
        {
            if (a.Count != b.Count) return false;
            foreach (var kvp in a)
            {
                if (!b.TryGetValue(kvp.Key, out var v2)) return false;
                if (!EquivalentIgnoringPowerStigVersion(kvp.Value, v2)) return false;
            }
            return true;
        }

        private static bool RawTextDiffIsVersionOnly(string raw1, string raw2)
        {
            if (raw1 == null || raw2 == null) return false;
            var lines1 = raw1.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var lines2 = raw2.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            if (lines1.Length != lines2.Length) return false;
            for (int i = 0; i < lines1.Length; i++)
            {
                if (!EquivalentIgnoringPowerStigVersion(lines1[i], lines2[i])) return false;
            }
            return true;
        }

        private static string Preprocess(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            var s = value.Trim();

            if (s.Length > 1 && ((s[0] == '"' && s[^1] == '"') || (s[0] == '\'' && s[^1] == '\'')))
                s = s.Substring(1, s.Length - 2);

            if (s.EndsWith(";"))
                s = s.Substring(0, s.Length - 1).Trim();

            s = MultiWhitespace.Replace(s, " ");
            s = s.Replace(@"\\", @"\");
            return NormalizePowerStigVersion(s);
        }

        private static bool EquivalentIgnoringPowerStigVersion(string a, string b)
        {
            var pa = Preprocess(a);
            var pb = Preprocess(b);
            return string.Equals(pa, pb, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizePowerStigVersion(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            string norm = input.Replace('\\', '/');
            norm = PowerStigPathSegment.Replace(norm, m => $"{m.Groups[1].Value}/__POWERSTIG_VERSION__");
            norm = PowerStigInline.Replace(norm, "PowerStig__POWERSTIG_VERSION__");
            norm = PowerStigGenericMixed.Replace(norm, m => $"{m.Groups[1].Value}__POWERSTIG_VERSION__");
            norm = Regex.Replace(norm, @"(__POWERSTIG_VERSION__)+", "__POWERSTIG_VERSION__");
            return norm;
        }

        private void FilterDiffsChanged(object sender, RoutedEventArgs e) => ApplyFilters();

        private void ApplyFilters()
        {
            // Checked now hides VersionOnly; unchecked shows everything (default).
            bool hideVersionOnly = IncludeVersionOnlyDiffsCheckBox.IsChecked == true;
            var filtered = allResults.Where(x => !hideVersionOnly || x.Status != "VersionOnly").ToList();

            var view = new ListCollectionView(filtered);
            view.GroupDescriptions.Add(new PropertyGroupDescription("Category"));
            DifferencesList.ItemsSource = view;
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}