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

            // Use for-loop to allow multi-line array parsing
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
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
                        var value = parts[1].Trim().TrimEnd(';').Trim();

                        // Handle multi-line array values
                        if (value.StartsWith("{"))
                        {
                            var arrayItems = new List<string>();

                            // If the array ends on the same line
                            if (value.EndsWith("}"))
                            {
                                var arrayContent = value.Substring(1, value.Length - 2).Trim();
                                arrayItems.AddRange(arrayContent
                                    .Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                                    .Select(s => s.Trim().Trim('"'))
                                    .Where(s => !string.IsNullOrWhiteSpace(s)));
                            }
                            else
                            {
                                // Start collecting lines until we find the closing '}'
                                // Remove opening brace if present
                                if (value == "{")
                                {
                                    value = "";
                                }
                                else
                                {
                                    value = value.Substring(1).Trim();
                                    if (!string.IsNullOrEmpty(value))
                                    {
                                        arrayItems.AddRange(value
                                            .Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                                            .Select(s => s.Trim().Trim('"'))
                                            .Where(s => !string.IsNullOrWhiteSpace(s)));
                                    }
                                }

                                // Read subsequent lines for array items
                                while (++i < lines.Length)
                                {
                                    var arrayLine = lines[i].Trim().TrimEnd(';').Trim();
                                    if (arrayLine.EndsWith("}"))
                                    {
                                        arrayLine = arrayLine.Substring(0, arrayLine.Length - 1).Trim();
                                        if (!string.IsNullOrEmpty(arrayLine))
                                        {
                                            arrayItems.AddRange(arrayLine
                                                .Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                                                .Select(s => s.Trim().Trim('"'))
                                                .Where(s => !string.IsNullOrWhiteSpace(s)));
                                        }
                                        break;
                                    }
                                    else if (!string.IsNullOrEmpty(arrayLine) && arrayLine != "{")
                                    {
                                        arrayItems.Add(arrayLine.Trim().Trim('"', ','));
                                    }
                                }
                            }
                            value = string.Join(", ", arrayItems);
                        }
                        else
                        {
                            value = value.Trim('"');
                        }

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
                                Category = ExtractCategory(resourceId),
                                Details = new Dictionary<string, string>()
                            };
                        }

                        // Add all properties from this instance to Details
                        foreach (var kvp in instance.Properties)
                        {
                            // Avoid overwriting if property already exists
                            if (!ruleMap[ruleId].Details.ContainsKey(kvp.Key))
                            {
                                ruleMap[ruleId].Details[kvp.Key] = kvp.Value;
                            }
                        }

                        // Optionally store raw text for fallback diff
                        ruleMap[ruleId].RawText = string.Join(Environment.NewLine,
                            instance.Properties.Select(p => $"{p.Key} = {p.Value}"));
                    }
                }
            }

            Rules = ruleMap.Values.OrderBy(r => r.RuleId).ToList();

        }

        private string ExtractCategory(string resourceId)
        {
            var match = Regex.Match(resourceId, @"\[(.*?)\]");
            return match.Success ? match.Groups[1].Value : "Unknown";
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