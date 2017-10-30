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
using System.Runtime.InteropServices;
using System.Text;

using Microsoft.Win32.SafeHandles;

namespace SuperDelete.Internal
{
    internal class FileDeleter
    {
        private const string FileNamePrefix = "\\\\?\\";

        private class DirectoryWithAttributes
        {
            public string Directory;
            public NativeMethods.FileAttributes Attributes;
        }

        public static string GetFullPath(string path)
        {
            // check to see if this is an absolute path, if so, we don't need to do anything
            // starts with \\blahblah or is in the form x:\blahblah
            if (path.StartsWith(@"\\") || (path.Length >= 3 && path.Substring(1,2) == @":\"))
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

        /// <summary>
        /// Deletes the specified directory or filename
        /// </summary>
        /// <param name="path">Path to delete</param>
        /// <param name="bypassAcl">If we should bypass ACLs</param>
        public static void Delete(string path, bool bypassAcl)
        {
            // bypassing ACLs requires first to enable these privileges. If you are not an admin, you'll get an error here
            if (bypassAcl)
            {
                EnablePrivilege("SeBackupPrivilege");
                EnablePrivilege("SeRestorePrivilege");
                EnablePrivilege("SeTakeOwnershipPrivilege");
                EnablePrivilege("SeSecurityPrivilege");
            }

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

        /// <summary>
        /// Enables a specified privilege. Required to perform certain administrative actions in Windows.
        /// </summary>
        /// <param name="priv">Name of the privilege</param>
        private static void EnablePrivilege(string priv)
        {
            NativeMethods.LUID privLuid;
            if (!NativeMethods.LookupPrivilegeValue(null, priv, out privLuid))
            {
                ThrowLastErrorException("Could not look up privilege {0}", priv);
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

        private static void DeleteSingleFile(string filePath, bool bypassAcl)
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
                    ThrowLastErrorException("Failed to delete file {0}", filePath);
                }
            }
        }

        /// <summary>
        /// Deletes a file using backup semantics. This bypasses ACLs if the user
        /// has administrative rights
        /// </summary>
        /// <param name="lpFileName"></param>
        /// <returns></returns>
        private unsafe static void DeleteFileBackupSemantics(string lpFileName)
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
                    ThrowLastErrorException("Failed attempting open file {0} with backup semantics", lpFileName);
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

        private static void DeleteFolder(string folderPath, bool bypassAclCheck, NativeMethods.FileAttributes parentAttributes)
        {
            var baseFolderPath = EnsureFileName(folderPath);
            var searchTerm = Path.Combine(baseFolderPath, "*");

            var directories = new List<DirectoryWithAttributes>();

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
                            ThrowLastErrorException("Failed to remove reparse point {0}", fullFilePath);
                        }
                    }
                    else if (isDirectory)
                    {
                        if (string.Compare(findInfo.cFileName, ".", StringComparison.InvariantCultureIgnoreCase) == 0 ||
                            string.Compare(findInfo.cFileName, "..", StringComparison.InvariantCultureIgnoreCase) == 0)
                        {
                            continue;
                        }

                        directories.Add(new DirectoryWithAttributes { Directory = fullFilePath, Attributes = findInfo.dwFileAttributes });
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
                RemoveReadonlyAttribute(directory.Directory, directory.Attributes);

                DeleteFolder(directory.Directory, bypassAclCheck, directory.Attributes);
            }

            ProgressTracker.Instance.LogEntry(baseFolderPath, true);
            if (!NativeMethods.RemoveDirectoryW(baseFolderPath))
            {
                ThrowLastErrorException("Failed to remove directory {0}", baseFolderPath);
            }
        }

        /// <summary>
        /// Removes read only attribute from file or directory if it has one
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="currentAttributes"></param>
        private static unsafe void RemoveReadonlyAttribute(string filename, NativeMethods.FileAttributes currentAttributes)
        {
            NativeMethods.FileAttributes attributesToRemove = NativeMethods.FileAttributes.Readonly;

            if (((uint)currentAttributes & (uint)attributesToRemove) != 0)
            {
                var newAttributes = (uint)(currentAttributes & (~attributesToRemove));

                if (!NativeMethods.SetFileAttributesW(EnsureFileName(filename), newAttributes))
                {
                    ThrowLastErrorException("Failed to remove {0} attribute on {1}", (currentAttributes & attributesToRemove), filename);
                }
            }
        }

        private static void ThrowLastErrorException(string message, params object[] args)
        {
            ThrowLastErrorException(Marshal.GetLastWin32Error(), message, args);
        }

        private static void ThrowLastErrorException(int error, string message, params object[] args)
        {
            string errorMessage = new Win32Exception(error).Message;

            throw new Win32Exception(error, errorMessage + ". " + string.Format(message, args));
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
