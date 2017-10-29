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
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;

namespace SuperDelete.Internal
{
    internal class FileDeleter
    {
        private const string FileNamePrefix = "\\\\?\\";

        public static void EnablePrivilege(string priv)
        {
            NativeMethods.LUID privLuid;
            if (!NativeMethods.LookupPrivilegeValue(null, priv, out privLuid))
            {
                ThrowLastErrorException("Could not look up restore privilege {0}", priv);
            }

            NativeMethods.SafeAccessTokenHandle token;
            if (!NativeMethods.OpenProcessToken(NativeMethods.GetCurrentProcess(), System.Security.Principal.TokenAccessLevels.AdjustPrivileges, out token))
            {
                ThrowLastErrorException("Could not open process token");
            }

            using (token)
            {
                NativeMethods.TOKEN_PRIVILEGE tokenpriv = new NativeMethods.TOKEN_PRIVILEGE();
                tokenpriv.PrivilegeCount = 1;
                tokenpriv.Privilege.Luid = privLuid;
                tokenpriv.Privilege.Attributes = NativeMethods.SE_PRIVILEGE_ENABLED;
                if (!NativeMethods.AdjustTokenPrivileges(token, false, ref tokenpriv, 0, IntPtr.Zero, IntPtr.Zero))
                {
                    ThrowLastErrorException("Could not not adjust token for privilege {0}", priv);
                }

                int lastError = Marshal.GetLastWin32Error();
                if (lastError != 0)
                {
                    ThrowLastErrorException("Could not not enable token for privilege {0}. Are you running as Administrator?", priv);
                }
            }
        }

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
                ThrowLastErrorException("Could not convert relative to absolute path. Try specifying absolute path. {0} ", path);
            }

            return fullName.ToString();
        }


        public static void Delete(string path, bool bypassAcl)
        {
            uint fileAttrs = NativeMethods.GetFileAttributesW(EnsureFileName(path));
            if ((fileAttrs & (uint)FileAttributes.Directory) == (uint)FileAttributes.Directory)
            {
                DeleteFolder(path, bypassAcl, 0);
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
                    ThrowLastErrorException("Attempting delete file {0} ", filePath);
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
                    ThrowLastErrorException("Attempting open file {0} with backup semantics", lpFileName);
                }

                var dispositionInfo = new NativeMethods.FILE_DISPOSITION_INFORMATION();
                dispositionInfo.DeleteFile = true;

                var ioStatusBlock = new NativeMethods.IO_STATUS_BLOCK();
                int retVal = NativeMethods.NtSetInformationFile(fileHandle, ref ioStatusBlock, new IntPtr(&dispositionInfo), Marshal.SizeOf(dispositionInfo), NativeMethods.FILE_INFORMATION_CLASS.FileDispositionInformation);
                if (retVal != 0)
                {
                    ThrowLastErrorException(NativeMethods.RtlNtStatusToDosError(retVal), "Couldn't set delete disposition on {0}", lpFileName);
                }
            }
        }

        private static unsafe void RemoveReadonlyAttribute(string filename, NativeMethods.FileAttributes currentAttributes)
        {
            NativeMethods.FileAttributes attributesToRemove = NativeMethods.FileAttributes.Readonly;

            if (((uint)currentAttributes & (uint)attributesToRemove) != 0)
            {
                var newAttributes = (uint)(currentAttributes & (~attributesToRemove));

                if (!NativeMethods.SetFileAttributesW(EnsureFileName(filename), newAttributes))
                {
                    ThrowLastErrorException("Attempting to remove {0} attribute on {1}",  (currentAttributes & attributesToRemove), filename);
                }
            }
        }

        public static void DeleteFolder(string folderPath, bool bypassAclCheck, NativeMethods.FileAttributes parentAttributes)
        {
            var baseFolderPath = EnsureFileName(folderPath);
            var searchTerm = Path.Combine(baseFolderPath, "*");

            var directories = new List<KeyValuePair<string, NativeMethods.FileAttributes>>();

            NativeMethods.WIN32_FIND_DATAW findInfo;
            NativeMethods.FindFileSafeHandle searchHandle = NativeMethods.FindFirstFileW(searchTerm, out findInfo);
            if (searchHandle.IsInvalid)
            {
                ThrowLastErrorException("Error locating files in {0}", searchTerm);
            }

            using (searchHandle)
            { 
                do
                {
                    var isDirectory = ((uint)findInfo.dwFileAttributes & (uint)NativeMethods.FileAttributes.Directory) == (uint)NativeMethods.FileAttributes.Directory;
                    var fullFilePath = Path.Combine(folderPath, findInfo.cFileName);

                    if ((findInfo.dwFileAttributes & NativeMethods.FileAttributes.ReparsePoint) != 0)
                    {
                        // reparse points can be removed directly. If we attempt to follow down into the reprase
                        // point, then we start getting weird error messages when unexpected files get deleted
                        // or permissions cannot be obtained.

                        if (!NativeMethods.RemoveDirectoryW(fullFilePath))
                        {
                            ThrowLastErrorException("Attempting to remove reparse point {0}", fullFilePath);
                        }
                    }
                    else if (isDirectory)
                    {
                        if (string.Compare(findInfo.cFileName, ".", StringComparison.InvariantCultureIgnoreCase) == 0 ||
                            string.Compare(findInfo.cFileName, "..", StringComparison.InvariantCultureIgnoreCase) == 0)
                        {
                            continue;
                        }

                        directories.Add(new KeyValuePair<string, NativeMethods.FileAttributes>(fullFilePath, findInfo.dwFileAttributes));
                    }
                    else
                    {
                        RemoveReadonlyAttribute(fullFilePath, findInfo.dwFileAttributes);

                        DeleteSingleFile(fullFilePath, bypassAclCheck);
                    }

                } while (NativeMethods.FindNextFileW(searchHandle, out findInfo));
            }

            foreach (var directory in directories)
            {
                RemoveReadonlyAttribute(directory.Key, directory.Value);
                DeleteFolder(directory.Key, bypassAclCheck, directory.Value);
            }

            ProgressTracker.Instance.LogEntry(baseFolderPath, true);
            if (!NativeMethods.RemoveDirectoryW(baseFolderPath))
            {
                ThrowLastErrorException("Attempting to remove directory {0}", baseFolderPath);
            }
        }

        private static void ThrowLastErrorException(string message, params object[] args)
        {
            ThrowLastErrorException(Marshal.GetLastWin32Error(), message, args);
        }

        private static void ThrowLastErrorException(int error, string message, params object[] args)
        {
            string errorMessage = new Win32Exception(error).Message;

            throw new Win32Exception(error, errorMessage + " " + string.Format(message, args));
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
