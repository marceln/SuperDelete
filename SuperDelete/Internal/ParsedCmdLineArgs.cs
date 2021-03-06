﻿//Copyright 2016 Marcel Nita (marcel.nita@gmail.com)
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
    internal class ParsedCmdLineArgs
    {
        public bool SilentModeEnabled { get; set; }
        public string FileName { get; set; }

        public bool BypassAcl { get; set;  }

        public bool PrintStackTrace { get; set; }

        /// <summary>
        /// Defines all arguments and contains the logic to set the correct member with the value given
        /// </summary>
        public static readonly Dictionary<string, Action<ParsedCmdLineArgs>> Args = new Dictionary<string, Action<ParsedCmdLineArgs>>(StringComparer.InvariantCultureIgnoreCase)
        {
            {  "-s", (a) => a.SilentModeEnabled = true },
            {  "--silentMode", (a) => a.SilentModeEnabled = true },
            {  "--bypassAcl", (a) => a.BypassAcl = true },
            {  "--printStackTrace", (a) => a.PrintStackTrace = true }
        };
    }
}
