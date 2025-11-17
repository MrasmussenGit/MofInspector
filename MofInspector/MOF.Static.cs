using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MofInspector
{
    public partial class Mof
    {
        // Convenience static loader used by CompareWindow
        public static Mof LoadFromFile(string filePath) => new Mof(filePath);
    }
}