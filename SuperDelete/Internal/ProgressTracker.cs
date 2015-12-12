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
using System.Diagnostics;
using System.Threading;

namespace SuperDelete.Internal
{
    internal class ProgressTracker
    {
        #region Singleton

        private ProgressTracker()
        {
            _durationTracker = new Stopwatch();
            _durationTracker.Start();
        }

        private static ProgressTracker _instance;

        public static ProgressTracker Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new ProgressTracker();
                }

                return _instance;
            }
        }

        #endregion

        #region Private data

        private volatile int _numberOfDeletedFiles;
        private volatile int _numberOfDeletedFolders;
        private Stopwatch _durationTracker;

        #endregion

        #region API 
        public void LogEntry(string fileName, bool isFolder)
        {
            if (isFolder)
            {
                Interlocked.Increment(ref _numberOfDeletedFolders);
            }
            else
            {
                Interlocked.Increment(ref _numberOfDeletedFiles);
            }

            Console.Write($"\rDeleting {Utils.PathShortener(fileName)}\t\t\t\t");
        }

        public void Stop()
        {
            _durationTracker.Stop();
            var duration = TimeSpan.FromMilliseconds(_durationTracker.ElapsedMilliseconds);
            Console.Write($"\rDone. Deleted {_numberOfDeletedFiles} files and {_numberOfDeletedFolders} folders in {duration}.\t\t\t\t");
        }

        #endregion
    }
}
