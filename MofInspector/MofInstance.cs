using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MofInspector
{

    public class MofInstance
    {
        public string ClassName { get; set; }
        public string InstanceName { get; set; }
        public Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();
    }

}
