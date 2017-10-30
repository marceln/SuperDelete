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
using System.Reflection;
using System.Text;

namespace SuperDelete.Internal
{
    internal class CmdLineArgsParser
    {
        public class InvalidCmdLineException : Exception
        {
            public InvalidCmdLineException(string message) : base(message)
            {
            }
        }

        public static void PrintUsage(CmdLineArgsParser.InvalidCmdLineException e)
        {
            var appVersion = Assembly.
                GetExecutingAssembly().
                GetName().
                Version.
                ToString();

            var versionLine = String.Format(Resources.VersionLine, appVersion);
            Console.WriteLine(versionLine);

            StringBuilder args = new StringBuilder();
            foreach (var arg in ParsedCmdLineArgs.Args.Keys)
            {
                args.AppendFormat("[{0}]", arg);
            }

            Console.WriteLine(Resources.UsageLine, e.Message, args.ToString());
        }

        public static ParsedCmdLineArgs Parse(string[] args)
        {
            var result = new ParsedCmdLineArgs();

            //Check if we have any args
            foreach(string arg in args)
            {
                if (arg.StartsWith("-"))
                {
                    // this is a switch
                    Action<ParsedCmdLineArgs> a;

                    if (ParsedCmdLineArgs.Args.TryGetValue(arg, out a))
                    {
                        a(result);
                    }
                    else
                    {
                        throw new InvalidCmdLineException(string.Format(Resources.InvalidSwitchError, arg));
                    }
                }
                else
                {
                    if (result.FileName == null)
                    {
                        result.FileName = arg;
                    }
                    else
                    {
                        throw new InvalidCmdLineException(Resources.TooManyFilenamesError);
                    }
                }
            }

            if(result.FileName == null)
            {
                throw new InvalidCmdLineException(Resources.NoFilenamesSpecified);
            }

            return result;
        }
    }
}
