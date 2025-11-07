using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MofInspector
{
    public class MofRule
    {
        public string RuleId { get; set; }
        public bool IsSkipped { get; set; }
        public string Category { get; set; } // e.g., DotNetFramework or InternetExplorer
        public Dictionary<string, string> Details { get; set; } = new Dictionary<string, string>();
        public string RawText { get; set; } // Original MOF text for fallback diff
    }

}
