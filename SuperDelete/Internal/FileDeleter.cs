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
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace SuperDelete.Internal
{
    internal class FileDeleter
    {
        private const string FileNamePrefix = "\\\\?\\";

        public static bool Delete(string path)
        {
            uint fileAttrs = NativeMethods.GetFileAttributesW(EnsureFileName(path));
            if ((fileAttrs & (uint)FileAttributes.Directory) == (uint)FileAttributes.Directory)
            {
                return DeleteFolder(path);
            }
            else
            {
                return DeleteSingleFile(path);
            }
        }

        public static bool DeleteSingleFile(string filePath)
        {
            ProgressTracker.Instance.LogEntry(filePath, false);
            filePath = EnsureFileName(filePath);
            return NativeMethods.DeleteFileW(filePath);
        }

        public static bool DeleteFolder(string folderPath)
        {
            var baseFolderPath = EnsureFileName(folderPath);
            var searchTerm = Path.Combine(baseFolderPath, "*");
            NativeMethods.WIN32_FIND_DATAW findInfo;
            IntPtr searchHandle = NativeMethods.FindFirstFileW(searchTerm, out findInfo);
            if (searchHandle == NativeMethods.INVALID_HANDLE_VALUE)
            {
                return false;
            }

            var directories = new List<string>();
            do
            {
                var isDirectory = ((uint)findInfo.dwFileAttributes & (uint)NativeMethods.FileAttributes.Directory) == (uint)NativeMethods.FileAttributes.Directory;
                if (isDirectory)
                {
                    if (string.Compare(findInfo.cFileName, ".", StringComparison.InvariantCultureIgnoreCase) == 0 ||
                        string.Compare(findInfo.cFileName, "..", StringComparison.InvariantCultureIgnoreCase) == 0)
                    {
                        continue;
                    }

                    var subFolderName = Path.Combine(folderPath, findInfo.cFileName);
                    directories.Add(subFolderName);
                }
                else
                {
                    var fileName = Path.Combine(folderPath, findInfo.cFileName);

                    var isReadonly = ((uint)findInfo.dwFileAttributes & (uint)NativeMethods.FileAttributes.Readonly) == (uint)NativeMethods.FileAttributes.Readonly;
                    if (isReadonly)
                    {
                        var newAttributes = (uint)(findInfo.dwFileAttributes & (~NativeMethods.FileAttributes.Readonly));
                        if (!NativeMethods.SetFileAttributesW(EnsureFileName(fileName), newAttributes))
                        {
                            return false;
                        }
                    }

                    if (!DeleteSingleFile(fileName))
                    {
                        return false;
                    }
                }

            } while (NativeMethods.FindNextFileW(searchHandle, out findInfo));

            if (!NativeMethods.FindClose(searchHandle))
            {
                return false;
            }

            foreach (var directory in directories)
            {
                if (!DeleteFolder(directory))
                {
                    return false;
                }
            }

            ProgressTracker.Instance.LogEntry(baseFolderPath, true);
            return NativeMethods.RemoveDirectoryW(baseFolderPath);
        }

        private static string EnsureFileName(string fileName)
        {
            if (fileName.StartsWith(FileNamePrefix))
            {
                return fileName;
            }

            return $"{FileNamePrefix}{fileName}";
        }
    }
}
