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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SuperDelete.Internal
{
    internal static class Utils
    {
        /// <summary>
        /// See http://blog.codinghorror.com/shortening-long-file-paths/
        /// </summary>
        public static string PathShortener(string path)
        {
            if (path.Length > 64)
            {
                return $"{path.Substring(0, 20)}\\...\\{path.Substring(path.Length - 40, 40)}";
            }

            return path;
        }
    }
}
