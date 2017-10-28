//Copyright 2016 Marcel Nita (marcel.nita@gmail.com)
//
//Licensed under the Apache License, Version 2.0 (the "License");
//you may not use this file except in compliance with the License.
//You may obtain a copy of the License at
//
//    http://www.apache.org/licenses/LICENSE-2.0
//
//Unless required by applicable law or agreed to in writing, software
//distributed under the License is distributed on an "AS IS" BASIS,
//WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//See the License for the specific language governing permissions and
//limitations under the License.


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

            //Check if we have any args, if so, process and move on
            var parsedArgs = listArgs.Intersect(ParsedCmdLineArgs.Args.Keys);
            foreach(var arg in parsedArgs)
            {
                ParsedCmdLineArgs.Args[arg](result, arg);
            }

            listArgs.RemoveAll(a => parsedArgs.Contains(a));

            //We only support one extra arg for now so anything that's left over must be the folder path
            result.FileName = listArgs.FirstOrDefault();

            return result;
        }
    }
}
