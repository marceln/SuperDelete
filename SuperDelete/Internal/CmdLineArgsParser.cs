using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SuperDelete.Internal
{
    internal class CmdLineArgsParser
    {
        public static ParsedCmdLineArgs Parse(string[] args)
        {
            //Local list that we can work on
            var listArgs = args.ToList();
            var result = new ParsedCmdLineArgs();

            //Check if we have any silent args. If we do then remove them from the list and carry on
            var silentArgs = listArgs.Intersect(CmdLineArgsDefinition.SilentArgsVariants);
            result.SilentModeEnabled = silentArgs.Any();
            listArgs.RemoveAll(a => silentArgs.Contains(a));

            //We only support one extra arg for now so anything that's left over must be the folder path
            result.FileName = listArgs.FirstOrDefault();

            return result;
        }
    }
}
