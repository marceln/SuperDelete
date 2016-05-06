//Copyright 2015 Marcel Nita (marcel.nita@gmail.com)
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

using SuperDelete.Internal;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace SuperDelete
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            var parsedArgs = CmdLineArgsParser.Parse(args);
            if (!args.Any() || string.IsNullOrEmpty(parsedArgs.FileName))
            {
                UsageInformation.Print();
                return;
            }

            //If silent mode is not specified
            if (!parsedArgs.SilentModeEnabled)
            {
                Console.WriteLine(Resources.ConfirmationLine, parsedArgs.FileName);
                var keyInfo = Console.ReadKey();
                if (keyInfo.Key != ConsoleKey.Y && keyInfo.Key != ConsoleKey.Enter)
                {
                    return;
                }
            }

            if (!FileDeleter.Delete(parsedArgs.FileName))
            {
                var lastError = Marshal.GetLastWin32Error();
                Console.WriteLine();
                Console.WriteLine($"Error: {new Win32Exception(lastError).Message}");
            }
            else
            {
                ProgressTracker.Instance.Stop();
            }
        }
    }
}
