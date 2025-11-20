using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MofInspector
{
    public partial class CompareDetailWindow : Window
    {
        private readonly string _ruleId;
        private readonly MofRule? _rule1;
        private readonly MofRule? _rule2;
        private readonly List<MofInstance> _instances1;
        private readonly List<MofInstance> _instances2;
        private GridViewColumnHeader? _lastHeaderClicked;
        private System.ComponentModel.ListSortDirection _lastHeaderDirection = System.ComponentModel.ListSortDirection.Ascending;

        private List<DiffRow> _allPropertyDiffRows = new();
        private List<RawLine> _rawLinesFull1 = new();
        private List<RawLine> _rawLinesFull2 = new();

        private static readonly Regex PowerStigPathSegment = new(@"(?i)(PowerStig)[\\/](v?\d+(?:\.\d+)*(?:[a-z])?)(?=[\\/])", RegexOptions.Compiled);
        private static readonly Regex PowerStigInline = new(@"(?i)\bPowerStig[-_](v?\d+(?:\.\d+)*(?:[a-z])?)\b", RegexOptions.Compiled);
        private static readonly Regex PowerStigGenericMixed = new(@"(?i)\b(PowerStig)(?:[-_/\\])(v?\d+(?:\.\d+)*(?:[a-z])?)(?=[-_/\\]|$)", RegexOptions.Compiled);
        private static readonly Regex MultiWhitespace = new(@"\s+", RegexOptions.Compiled);

        private static string StripOuterQuotes(string s) =>
            (s.Length >= 2 && ((s[0] == '"' && s[^1] == '"') || (s[0] == '\'' && s[^1] == '\'')))
            ? s.Substring(1, s.Length - 2)
            : s;

        public CompareDetailWindow(string ruleId, MofRule? rule1, MofRule? rule2, List<MofInstance> instances1, List<MofInstance> instances2)
        {
            InitializeComponent();
            _ruleId = ruleId;
            _rule1 = rule1;
            _rule2 = rule2;
            _instances1 = instances1;
            _instances2 = instances2;

            RuleIdText.Text = ruleId;
            BuildPropertyDiffs();
            BuildRawDiffLines();
            ShowRawDiffCheckBox.IsChecked = false;
            SetOverallStatus();
        }

        private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is not GridViewColumnHeader header || header.Column == null)
                return;

            var headerText = header.Column.Header?.ToString() ?? "";
            if (string.IsNullOrEmpty(headerText)) return;

            var rows = _allPropertyDiffRows;
            var direction = System.ComponentModel.ListSortDirection.Ascending;
            if (header == _lastHeaderClicked && _lastHeaderDirection == System.ComponentModel.ListSortDirection.Ascending)
                direction = System.ComponentModel.ListSortDirection.Descending;

            Func<DiffRow, object> selector = headerText switch
            {
                "Property" => r => r.Key,
                "File 1 Value" => r => r.Value1,
                "File 2 Value" => r => r.Value2,
                "Status" => r => r.Status,
                _ => r => r.Key
            };

            rows = direction == System.ComponentModel.ListSortDirection.Ascending
                ? rows.OrderBy(selector).ToList()
                : rows.OrderByDescending(selector).ToList();

            PropertyDiffList.ItemsSource = rows;

            _lastHeaderClicked = header;
            _lastHeaderDirection = direction;
        }

        private class DiffRow
        {
            public string Key { get; set; } = "";
            public string Value1 { get; set; } = "";
            public string Value2 { get; set; } = "";
            public string Status { get; set; } = "";
        }

        private class RawLine
        {
            public string Text { get; set; } = "";
            public bool IsChanged { get; set; }
            public Brush Bg => IsChanged ? new SolidColorBrush(Color.FromArgb(60, 255, 0, 0)) : Brushes.Transparent;
        }

        private void SetOverallStatus()
        {
            string status;
            if (_rule1 == null) status = "Missing in File 1";
            else if (_rule2 == null) status = "Missing in File 2";
            else if (_rule1.Details == null || _rule2.Details == null || _rule1.Details.Count == 0 || _rule2.Details.Count == 0)
                status = "Parsing Error";
            else
            {
                bool exactSame = _rule1.Details.Count == _rule2.Details.Count &&
                                 _rule1.Details.All(kvp => _rule2.Details.TryGetValue(kvp.Key, out var v2) &&
                                                           string.Equals(kvp.Value?.Trim(), v2?.Trim(), StringComparison.Ordinal));

                if (exactSame)
                    status = "Match";
                else
                {
                    bool ignoringVersionSame = _rule1.Details.Count == _rule2.Details.Count &&
                                               _rule1.Details.All(kvp => _rule2.Details.TryGetValue(kvp.Key, out var v2) &&
                                                                         EquivalentIgnoringPowerStigVersion(kvp.Value, v2));

                    bool rawVersionOnly = RawTextDiffIsVersionOnly(_rule1.RawText, _rule2.RawText);

                    status = (ignoringVersionSame && rawVersionOnly) ? "VersionOnly" : "Different";
                }
            }

            OverallStatusText.Text = status;
            OverallStatusText.Foreground = status switch
            {
                "Match" => Brushes.DarkGreen,
                "VersionOnly" => Brushes.DarkGoldenrod,
                "Different" => Brushes.DarkRed,
                "Missing in File 1" => Brushes.DarkOrange,
                "Missing in File 2" => Brushes.DarkOrange,
                "Parsing Error" => Brushes.Peru,
                _ => Brushes.Black
            };
        }

        private bool RawTextDiffIsVersionOnly(string? raw1, string? raw2)
        {
            if (raw1 == null || raw2 == null) return false;
            var lines1 = raw1.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var lines2 = raw2.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            if (lines1.Length != lines2.Length) return false;
            for (int i = 0; i < lines1.Length; i++)
                if (!EquivalentIgnoringPowerStigVersion(lines1[i], lines2[i])) return false;
            return true;
        }

        private void BuildPropertyDiffs()
        {
            var rows = new List<DiffRow>();
            var dict1 = _rule1?.Details ?? new Dictionary<string, string>();
            var dict2 = _rule2?.Details ?? new Dictionary<string, string>();

            var allKeys = new HashSet<string>(dict1.Keys, StringComparer.OrdinalIgnoreCase);
            allKeys.UnionWith(dict2.Keys);

            foreach (var key in allKeys.OrderBy(k => k))
            {
                bool in1 = dict1.TryGetValue(key, out var v1);
                bool in2 = dict2.TryGetValue(key, out var v2);
                string status = (in1, in2) switch
                {
                    (true, true) => string.Equals(v1?.Trim(), v2?.Trim(), StringComparison.Ordinal)
                        ? "Same"
                        : (EquivalentIgnoringPowerStigVersion(v1, v2) ? "VersionOnly" : "Different"),
                    (true, false) => "Missing in File 2",
                    (false, true) => "Missing in File 1",
                    _ => "Missing"
                };

                rows.Add(new DiffRow
                {
                    Key = key,
                    Value1 = v1 ?? "",
                    Value2 = v2 ?? "",
                    Status = status
                });
            }

            rows.Add(new DiffRow { Key = "(Instance Count File 1)", Value1 = _instances1.Count.ToString(), Value2 = "", Status = "Info" });
            rows.Add(new DiffRow { Key = "(Instance Count File 2)", Value1 = "", Value2 = _instances2.Count.ToString(), Status = "Info" });

            _allPropertyDiffRows = rows;
            PropertyDiffList.ItemsSource = _allPropertyDiffRows;
        }

        private bool EquivalentIgnoringPowerStigVersion(string? a, string? b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            return string.Equals(PreprocessAndNormalize(a), PreprocessAndNormalize(b), StringComparison.OrdinalIgnoreCase);
        }

        private string PreprocessAndNormalize(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "";
            var trimmed = input.Trim();
            if (trimmed.EndsWith(";"))
                trimmed = trimmed.Substring(0, trimmed.Length - 1).Trim();
            trimmed = StripOuterQuotes(trimmed);
            trimmed = MultiWhitespace.Replace(trimmed, " ");
            trimmed = trimmed.Replace(@"\\", @"\");
            return NormalizePowerStigVersion(trimmed);
        }

        private string NormalizePowerStigVersion(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            string norm = input.Replace('\\', '/');
            norm = PowerStigPathSegment.Replace(norm, m => $"{m.Groups[1].Value}/__POWERSTIG_VERSION__");
            norm = PowerStigInline.Replace(norm, "PowerStig__POWERSTIG_VERSION__");
            norm = PowerStigGenericMixed.Replace(norm, m => $"{m.Groups[1].Value}__POWERSTIG_VERSION__");
            norm = Regex.Replace(norm, @"(__POWERSTIG_VERSION__)+", "__POWERSTIG_VERSION__");
            return norm;
        }

        private void BuildRawDiffLines()
        {
            RawLines1.ItemsSource = null;
            RawLines2.ItemsSource = null;

            var lines1 = _rule1?.RawText != null ? SplitLinesLimited(_rule1.RawText) : new List<string>();
            var lines2 = _rule2?.RawText != null ? SplitLinesLimited(_rule2.RawText) : new List<string>();

            if (lines1.Count == 0 && _rule1 == null)
                lines1.Add("[No raw text: rule missing in File 1]");
            else if (lines1.Count == 0 && _rule1?.RawText == null)
                lines1.Add("[No raw text available for File 1]");

            if (lines2.Count == 0 && _rule2 == null)
                lines2.Add("[No raw text: rule missing in File 2]");
            else if (lines2.Count == 0 && _rule2?.RawText == null)
                lines2.Add("[No raw text available for File 2]");

            // Align lines using LCS to avoid marking all as changed when one insertion/deletion shifts indexes.
            var aligned = AlignLines(lines1, lines2);

            var out1 = new List<RawLine>(aligned.Count);
            var out2 = new List<RawLine>(aligned.Count);

            foreach (var (i1, i2) in aligned)
            {
                string l1 = i1.HasValue ? lines1[i1.Value] : "";
                string l2 = i2.HasValue ? lines2[i2.Value] : "";
                bool changed = !EquivalentIgnoringPowerStigVersion(l1, l2);
                out1.Add(new RawLine { Text = l1, IsChanged = changed });
                out2.Add(new RawLine { Text = l2, IsChanged = changed });
            }

            _rawLinesFull1 = out1;
            _rawLinesFull2 = out2;

            ToggleRawDiffVisibility(false);
            RefreshRawLineView();
        }

        private List<string> SplitLinesLimited(string text) =>
            text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                .Select(l => l.Length <= 500 ? l : l.Substring(0, 500) + "...")
                .ToList();

        // Longest Common Subsequence alignment of raw lines (exact string match).
        private List<(int? i1, int? i2)> AlignLines(IList<string> a, IList<string> b)
        {
            int n = a.Count, m = b.Count;
            var dp = new int[n + 1, m + 1];
            for (int i = n - 1; i >= 0; i--)
                for (int j = m - 1; j >= 0; j--)
                    dp[i, j] = a[i] == b[j] ? 1 + dp[i + 1, j + 1] : Math.Max(dp[i + 1, j], dp[i, j + 1]);

            var result = new List<(int? i1, int? i2)>();
            int x = 0, y = 0;
            while (x < n && y < m)
            {
                if (a[x] == b[y])
                {
                    result.Add((x, y));
                    x++; y++;
                }
                else if (dp[x + 1, y] >= dp[x, y + 1])
                {
                    result.Add((x, null));
                    x++;
                }
                else
                {
                    result.Add((null, y));
                    y++;
                }
            }
            while (x < n) { result.Add((x, null)); x++; }
            while (y < m) { result.Add((null, y)); y++; }

            return result;
        }

        private void ShowRawDiffChanged(object sender, RoutedEventArgs e)
        {
            ToggleRawDiffVisibility(ShowRawDiffCheckBox.IsChecked == true);
            if (ShowRawDiffCheckBox.IsChecked == true)
                RefreshRawLineView();
        }

        private void ShowOnlyChangedLinesChanged(object sender, RoutedEventArgs e) => RefreshRawLineView();

        private void RefreshRawLineView()
        {
            if (RawLines1 == null || RawLines2 == null) return;

            bool onlyChanged = ShowOnlyChangedLinesCheckBox.IsChecked == true;
            IEnumerable<RawLine> view1 = _rawLinesFull1;
            IEnumerable<RawLine> view2 = _rawLinesFull2;

            if (onlyChanged)
            {
                view1 = view1.Where(l => l.IsChanged);
                view2 = view2.Where(l => l.IsChanged);
            }

            RawLines1.ItemsSource = view1.ToList();
            RawLines2.ItemsSource = view2.ToList();
        }

        private void RawLines_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.C && Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && sender is ListBox lb)
            {
                var selected = lb.SelectedItems.Cast<RawLine>().Select(r => r.Text);
                if (!selected.Any()) return;
                Clipboard.SetText(string.Join(Environment.NewLine, selected));
            }
        }

        private void CopyRawLines_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi &&
                mi.Parent is ContextMenu cm &&
                cm.PlacementTarget is ListBox lb &&
                lb.ItemsSource is IEnumerable<RawLine> lines)
            {
                var selected = lb.SelectedItems.Cast<RawLine>().Select(r => r.Text);
                if (!selected.Any()) selected = lines.Select(r => r.Text);
                Clipboard.SetText(string.Join(Environment.NewLine, selected));
            }
        }

        private void CopyAllRawLines_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi &&
                mi.Parent is ContextMenu cm &&
                cm.PlacementTarget is ListBox lb &&
                lb.ItemsSource is IEnumerable<RawLine> lines)
            {
                Clipboard.SetText(string.Join(Environment.NewLine, lines.Select(r => r.Text)));
            }
        }

        private void ToggleRawDiffVisibility(bool visible)
        {
            if (RawLines1 == null || RawLines2 == null) return;
            RawLines1.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            RawLines2.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        private void PropertyDiffList_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.C && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                var selected = PropertyDiffList.SelectedItems.Cast<DiffRow>();
                var text = string.Join(Environment.NewLine, selected.Select(r => $"{r.Key}: [{r.Value1}] vs [{r.Value2}] ({r.Status})"));
                Clipboard.SetText(text);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}