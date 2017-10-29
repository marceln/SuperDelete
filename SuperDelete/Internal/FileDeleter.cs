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

using Microsoft.Win32.SafeHandles;
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

        public static string GetFullPath(string path)
        {
            // method does not call win32, just checks to see if the prefix is a \ or drive letter
            if (Path.IsPathRooted(path))
            {
                return path;
            }

            // resolve to absolute path to avoid confusion since long filename API won't accept relative paths
            StringBuilder fullName = new StringBuilder(32768);

            if (NativeMethods.GetFullPathNameW(path, fullName.MaxCapacity, fullName, IntPtr.Zero) == 0)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not convert relative to absolute path. Try specifying absolute path.");
            }

            return fullName.ToString();
        }


        public static void Delete(string path, bool bypassAcl)
        {
            uint fileAttrs = NativeMethods.GetFileAttributesW(EnsureFileName(path));
            if ((fileAttrs & (uint)FileAttributes.Directory) == (uint)FileAttributes.Directory)
            {
                DeleteFolder(path, bypassAcl);
            }
            else
            {
                DeleteSingleFile(path, bypassAcl);
            }
        }

        public static void DeleteSingleFile(string filePath, bool bypassAcl)
        {
            ProgressTracker.Instance.LogEntry(filePath, false);
            filePath = EnsureFileName(filePath);

            if (bypassAcl)
            {
                DeleteFileBackupSemantics(filePath);
            }
            else
            {
                if (!NativeMethods.DeleteFileW(filePath))
                {
                    int lastError = Marshal.GetLastWin32Error();
                    throw new Win32Exception(lastError);
                }
            }
        }

        /// <summary>
        /// Deletes a file using backup semantics. This bypasses ACLs if the user
        /// has administrative rights
        /// </summary>
        /// <param name="lpFileName"></param>
        /// <returns></returns>
        public unsafe static void DeleteFileBackupSemantics(string lpFileName)
        {
            var dispositionInfo = new NativeMethods.FILE_DISPOSITION_INFORMATION();
            dispositionInfo.DeleteFile = true;

            var ioStatusBlock = new NativeMethods.IO_STATUS_BLOCK();

            using (SafeFileHandle fileHandle = NativeMethods.CreateFile(lpFileName,
                NativeMethods.EFileAccess.DELETE,
                FileShare.None,
                IntPtr.Zero,
                FileMode.Open,
                (int)(NativeMethods.FileAttributes.DeleteOnClose | NativeMethods.FileAttributes.BackupSemantics),
                IntPtr.Zero))
            {
                if (fileHandle.IsInvalid)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                int retVal = NativeMethods.NtSetInformationFile(fileHandle, ref ioStatusBlock, new IntPtr(&dispositionInfo), Marshal.SizeOf(dispositionInfo), NativeMethods.FILE_INFORMATION_CLASS.FileDispositionInformation);
                if (retVal != 0)
                {
                    throw new Win32Exception(NativeMethods.RtlNtStatusToDosError(retVal));
                }
            }
        }

        public static void DeleteFolder(string folderPath, bool bypassAclCheck)
        {
            var baseFolderPath = EnsureFileName(folderPath);
            var searchTerm = Path.Combine(baseFolderPath, "*");

            var directories = new List<string>();

            NativeMethods.WIN32_FIND_DATAW findInfo;
            using (NativeMethods.FindFileSafeHandle searchHandle = NativeMethods.FindFirstFileW(searchTerm, out findInfo))
            {
                if (searchHandle.IsInvalid)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

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
                                throw new Win32Exception(Marshal.GetLastWin32Error());
                            }
                        }

                        DeleteSingleFile(fileName, bypassAclCheck);
                    }

                } while (NativeMethods.FindNextFileW(searchHandle, out findInfo));
            }

            foreach (var directory in directories)
            {
                DeleteFolder(directory, bypassAclCheck);
            }

            ProgressTracker.Instance.LogEntry(baseFolderPath, true);
            if (!NativeMethods.RemoveDirectoryW(baseFolderPath))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
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
