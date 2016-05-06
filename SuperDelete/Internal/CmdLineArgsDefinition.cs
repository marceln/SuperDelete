using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SuperDelete.Internal
{
    public static class CmdLineArgsDefinition
    {
        private static List<string> _silentArgsVariants;
        public static List<string> SilentArgsVariants => _silentArgsVariants ?? (_silentArgsVariants = new List<string> { "--silent", "-s" });
    }
}
