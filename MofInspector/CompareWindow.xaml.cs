using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MofInspector
{
    public partial class CompareWindow : Window
    {
        private Mof? mof1;
        private Mof? mof2;
        private string? filePath1;
        private string? filePath2;
        private List<CompareRow> allRows = new();

        private class CompareRow
        {
            public string RuleId { get; set; } = "";
            public string Status { get; set; } = "";
            public string Details { get; set; } = "";
            public string Category { get; set; } = "";
            public bool IsVersionOnlyDifference { get; set; }
        }

        public CompareWindow()
        {
            InitializeComponent();
        }

        // Regex patterns (same as detail window, extended)
        private static readonly Regex PowerStigPathSegment = new(@"(?i)(PowerStig)[\\/](v?\d+(?:\.\d+)*(?:[a-z])?)(?=[\\/])", RegexOptions.Compiled);
        private static readonly Regex PowerStigInline = new(@"(?i)\bPowerStig[-_](v?\d+(?:\.\d+)*(?:[a-z])?)\b", RegexOptions.Compiled);
        private static readonly Regex PowerStigGenericMixed = new(@"(?i)\b(PowerStig)(?:[-_/\\])(v?\d+(?:\.\d+)*(?:[a-z])?)(?=[-_/\\]|$)", RegexOptions.Compiled);
        private static readonly Regex MultiWhitespace = new(@"\s+", RegexOptions.Compiled);

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

        private static string Preprocess(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            var s = value.Trim();

            // Strip outer quotes
            if (s.Length > 1 && ((s[0] == '"' && s[^1] == '"') || (s[0] == '\'' && s[^1] == '\'')))
                s = s.Substring(1, s.Length - 2);

            // Remove trailing semicolon if any
            if (s.EndsWith(";"))
                s = s.Substring(0, s.Length - 1).Trim();

            // Collapse multiple whitespace
            s = MultiWhitespace.Replace(s, " ");

            // Unescape double backslashes
            s = s.Replace(@"\\", @"\");

            return NormalizePowerStigVersion(s);
        }

        private static bool EquivalentIgnoringPowerStigVersion(string? a, string? b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            var pa = Preprocess(a);
            var pb = Preprocess(b);
            return string.Equals(pa, pb, StringComparison.OrdinalIgnoreCase);
        }

        private void Browse1_Click(object sender, RoutedEventArgs e) => LoadFile1_Click(sender, e);
        private void Browse2_Click(object sender, RoutedEventArgs e) => LoadFile2_Click(sender, e);

        private void Compare_Click(object sender, RoutedEventArgs e)
        {
            if (mof1 == null || mof2 == null)
            {
                MessageBox.Show("Load both MOF files before comparing.", "Compare", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            BuildDiffs();
        }

        private void DifferencesList_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.C && Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && DifferencesList.SelectedItems.Count > 0)
            {
                var rows = DifferencesList.SelectedItems.Cast<CompareRow>();
                var text = string.Join(Environment.NewLine,
                    rows.Select(r => $"{r.RuleId}\t{r.Status}\t{r.Details}"));
                Clipboard.SetText(text);
            }
        }

        private void LoadFile1_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "MOF Files (*.mof)|*.mof|All Files (*.*)|*.*" };
            if (dlg.ShowDialog() == true)
            {
                filePath1 = dlg.FileName;
                File1PathText.Text = System.IO.Path.GetFileName(filePath1);
                mof1 = Mof.LoadFromFile(filePath1);
                BuildDiffs();
            }
        }

        private void LoadFile2_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "MOF Files (*.mof)|*.mof|All Files (*.*)|*.*" };
            if (dlg.ShowDialog() == true)
            {
                filePath2 = dlg.FileName;
                File2PathText.Text = System.IO.Path.GetFileName(filePath2);
                mof2 = Mof.LoadFromFile(filePath2);
                BuildDiffs();
            }
        }

        private void BuildDiffs()
        {
            allRows.Clear();
            DifferencesList.ItemsSource = null;
            if (mof1 == null && mof2 == null) return;

            var rules1 = mof1?.Rules ?? new List<MofRule>();
            var rules2 = mof2?.Rules ?? new List<MofRule>();
            var allRuleIds = new HashSet<string>(rules1.Select(r => r.RuleId), StringComparer.OrdinalIgnoreCase);
            allRuleIds.UnionWith(rules2.Select(r => r.RuleId));

            foreach (var ruleId in allRuleIds.OrderBy(r => r))
            {
                var r1 = rules1.FirstOrDefault(r => string.Equals(r.RuleId, ruleId, StringComparison.OrdinalIgnoreCase));
                var r2 = rules2.FirstOrDefault(r => string.Equals(r.RuleId, ruleId, StringComparison.OrdinalIgnoreCase));

                string status;
                bool versionOnly = false;
                string category = r1?.Category ?? r2?.Category ?? "";

                if (r1 == null) status = "Missing in File 1";
                else if (r2 == null) status = "Missing in File 2";
                else if (r1.Details == null || r2.Details == null) status = "Parsing Error";
                else
                {
                    bool sameFully =
                        DictionariesFullyEqual(r1.Details, r2.Details) &&
                        string.Equals(r1.RawText, r2.RawText, StringComparison.Ordinal);

                    if (sameFully)
                    {
                        status = "Match";
                    }
                    else
                    {
                        bool sameIgnoringVersionDetails = RuleDetailsEquivalentIgnoringVersion(r1.Details, r2.Details);
                        bool rawVersionOnly = RawTextDiffIsVersionOnly(r1.RawText, r2.RawText);

                        if (sameIgnoringVersionDetails && rawVersionOnly)
                        {
                            status = "VersionOnly";
                            versionOnly = true;
                        }
                        else
                        {
                            status = "Different";
                        }
                    }
                }

                allRows.Add(new CompareRow
                {
                    RuleId = ruleId,
                    Status = status,
                    Category = category,
                    Details = status == "VersionOnly" ? "(Only PowerSTIG version differs)" : "",
                    IsVersionOnlyDifference = versionOnly
                });
            }

            ApplyRowFilter();
        }

        private static bool DictionariesFullyEqual(Dictionary<string, string> d1, Dictionary<string, string> d2)
        {
            if (d1.Count != d2.Count) return false;
            foreach (var kvp in d1)
            {
                if (!d2.TryGetValue(kvp.Key, out var v2)) return false;
                if (!string.Equals(kvp.Value?.Trim(), v2?.Trim(), StringComparison.Ordinal))
                    return false;
            }
            return true;
        }

        private void ApplyRowFilter()
        {
            bool includeVersionOnly = IncludeVersionOnlyDiffsCheckBox.IsChecked == true;

            var view = allRows.Where(r =>
                r.Status != "Match" &&                   // hide pure matches
                (r.Status != "VersionOnly" || includeVersionOnly) // hide version-only unless toggled
            ).ToList();

            DifferencesList.ItemsSource = view;
        }

        private static bool RuleDetailsEquivalentIgnoringVersion(Dictionary<string, string> d1, Dictionary<string, string> d2)
        {
            if (d1.Count != d2.Count) return false;
            foreach (var kvp in d1)
            {
                if (!d2.TryGetValue(kvp.Key, out var v2)) return false;
                if (!EquivalentIgnoringPowerStigVersion(kvp.Value, v2)) return false;
            }
            return true;
        }

        private static bool RuleDetailsDifferenceIsOnlyVersion(Dictionary<string, string> d1, Dictionary<string, string> d2)
        {
            var keys = new HashSet<string>(d1.Keys, StringComparer.OrdinalIgnoreCase);
            keys.UnionWith(d2.Keys);
            foreach (var k in keys)
            {
                var v1 = d1.TryGetValue(k, out var tv1) ? tv1 : null;
                var v2 = d2.TryGetValue(k, out var tv2) ? tv2 : null;
                if (v1 == null || v2 == null) return false; // missing property is real difference
                if (!EquivalentIgnoringPowerStigVersion(v1, v2)) return false;
            }
            return true;
        }

        private static bool RawTextDiffIsVersionOnly(string? raw1, string? raw2)
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

        private void FilterDiffsChanged(object sender, RoutedEventArgs e) => ApplyRowFilter();
        private void DifferencesList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => OpenSelectedRuleDetail();

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

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}