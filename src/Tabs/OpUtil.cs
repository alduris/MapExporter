using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapExporter.Tabs
{
    internal sealed class OpUtil : OptionInterface
    {
        private OpUtil() { }
        public readonly static OpUtil Instance = new();

        public static Configurable<T> CosmeticBind<T>(T init) => new(Instance, null, init, null);
    }
}
