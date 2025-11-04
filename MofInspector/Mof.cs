
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace MofInspector
{
    public class Mof
    {
        public List<MofInstance> Instances { get; private set; } = new List<MofInstance>();
        public List<MofRule> Rules { get; private set; } = new List<MofRule>();

        public Mof(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("MOF file not found.", filePath);

            ParseMofFile(filePath);
        }

        private void ParseMofFile(string filePath)
        {
            var lines = File.ReadAllLines(filePath);
            MofInstance currentInstance = null;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                // Detect start of instance
                if (trimmed.StartsWith("instance of"))
                {
                    currentInstance = new MofInstance
                    {
                        ClassName = trimmed.Split(' ')[2]
                    };
                    continue;
                }

                // Detect end of instance
                if (trimmed.StartsWith("};"))
                {
                    if (currentInstance != null)
                    {
                        Instances.Add(currentInstance);
                        currentInstance = null;
                    }
                    continue;
                }

                // Parse properties inside instance
                if (currentInstance != null && trimmed.Contains("="))
                {
                    var parts = trimmed.Split(new[] { '=' }, 2);
                    if (parts.Length == 2)
                    {
                        var key = parts[0].Trim();
                        var value = parts[1].Trim().TrimEnd(';').Trim('"');
                        currentInstance.Properties[key] = value;
                    }
                }
            }

            BuildRulesFromInstances();
        }

        private void BuildRulesFromInstances()
        {
            var ruleMap = new Dictionary<string, MofRule>();

            foreach (var instance in Instances)
            {
                if (instance.Properties.TryGetValue("ResourceID", out var resourceId))
                {
                    var ruleIds = ExtractRuleIds(resourceId);
                    foreach (var ruleId in ruleIds)
                    {
                        if (!ruleMap.ContainsKey(ruleId))
                        {
                            ruleMap[ruleId] = new MofRule
                            {
                                RuleId = ruleId,
                                IsSkipped = resourceId.Contains("[Skip]"),
                                Details = new Dictionary<string, string>()
                            };
                        }
                    }
                }
            }

            Rules = ruleMap.Values.OrderBy(r => r.RuleId).ToList();
        }

        public List<string> ExtractRuleIds(string resourceId)
        {
            var ruleIds = new List<string>();
            if (string.IsNullOrEmpty(resourceId)) return ruleIds;

            // Matches V-218743 or V-218743.a or V-218743.b etc.
            var matches = Regex.Matches(resourceId, @"V-\d+(\.\w+)?");
            foreach (Match match in matches)
            {
                ruleIds.Add(match.Value);
            }
            return ruleIds;
        }
    }
}
