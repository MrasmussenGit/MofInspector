
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace MofInspector
{
    public partial class CompareDetailWindow : Window
    {
        private readonly string ruleId;
        private readonly MofRule rule1;
        private readonly MofRule rule2;
        private readonly List<MofInstance> instances1;
        private readonly List<MofInstance> instances2;

        public CompareDetailWindow(
            string ruleId,
            MofRule rule1,
            MofRule rule2,
            List<MofInstance> instances1,
            List<MofInstance> instances2)
        {
            InitializeComponent();

            this.ruleId = ruleId;
            this.rule1 = rule1;
            this.rule2 = rule2;

            // If you target .NET Framework or older C# language versions, avoid target-typed 'new()'
            this.instances1 = instances1 ?? new List<MofInstance>();
            this.instances2 = instances2 ?? new List<MofInstance>();

            RuleIdText.Text = ruleId ?? "—";

            // Populate rule properties (show "—" for empty)
            RuleProps1.ItemsSource = ToKvp(rule1?.Details);
            RuleProps2.ItemsSource = ToKvp(rule2?.Details);

            // Populate instance lists with a small preview
            Instances1.ItemsSource = this.instances1
                .Select(i => new
                {
                    i.ClassName,
                    i.InstanceName,
                    Preview = PreviewFromProps(i.Properties)
                })
                .ToList();

            Instances2.ItemsSource = this.instances2
                .Select(i => new
                {
                    i.ClassName,
                    i.InstanceName,
                    Preview = PreviewFromProps(i.Properties)
                })
                .ToList();
        }

        private static IEnumerable<KeyValuePair<string, string>> ToKvp(Dictionary<string, string> dict)
        {
            if (dict == null || dict.Count == 0)
                return new[] { new KeyValuePair<string, string>("(no properties)", "—") };

            // Move commonly-compared fields to the top (in the order listed), then alpha for the rest
            var commonFirst = new[] { "ResourceID", "Key", "Value", "Ensure", "Type", "Path" };

            var head = commonFirst
                .Select(k => dict
                    .FirstOrDefault(kv => string.Equals(kv.Key, k, StringComparison.OrdinalIgnoreCase)))
                .Where(kv => !string.IsNullOrEmpty(kv.Key)); // keep only those that exist

            var tail = dict
                .Where(kv => !commonFirst.Contains(kv.Key, StringComparer.OrdinalIgnoreCase))
                .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase);

            return head.Concat(tail);
        }

        private static string PreviewFromProps(Dictionary<string, string> props, int maxLen = 120)
        {
            if (props == null || props.Count == 0) return "—";

            var picks = new[] { "ResourceID", "Key", "Value", "Name", "Path" };

            var picked = props
                .Where(kv => picks.Contains(kv.Key, StringComparer.OrdinalIgnoreCase))
                .Select(kv => $"{kv.Key}={kv.Value}");

            // If none of the picks exist, fall back to the first few properties
            var parts = picked.Any()
                ? picked
                : props.Take(3).Select(kv => $"{kv.Key}={kv.Value}");

            var s = string.Join(" | ", parts);
            return s.Length <= maxLen ? s : s.Substring(0, maxLen) + "...";
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }

}
