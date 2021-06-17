// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.ProjectSystem
{

    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Diagnostics;
    using System.Globalization;
    using Microsoft.VisualStudio.Shell.Interop;
    using Microsoft.VisualStudio.Shell;
    using IServiceProvider = System.IServiceProvider;

    /// <summary>
    /// This object is in charge of reloading nodes that have file monikers that can be listened to changes
    /// </summary>
    internal class FileChangeManager : IVsFileChangeEvents
    {
        /// <summary>
        /// Defines a data structure that can link a item moniker to the item and its file change cookie.
        /// </summary>
        private struct ObservedItemInfo
        {
            /// <summary>
            /// Defines the id of the item that is to be reloaded.
            /// </summary>
            private uint itemID;

            /// <summary>
            /// Defines the file change cookie that is returned when listening on file changes on the nested project item.
            /// </summary>
            private uint fileChangeCookie;

            /// <summary>
            /// Defines the nested project item that is to be reloaded.
            /// </summary>
            public uint ItemID
            {
                get
                {
                    return this.itemID;
                }

                set
                {
                    this.itemID = value;
                }
            }

            /// <summary>
            /// Defines the file change cookie that is returned when listenning on file changes on the nested project item.
            /// </summary>
            public uint FileChangeCookie
            {
                get
                {
                    return this.fileChangeCookie;
                }

                set
                {
                    this.fileChangeCookie = value;
                }
            }
        }

        /// <summary>
        /// Event that is raised when one of the observed file names have changed on disk.
        /// </summary>
        internal event EventHandler<FileChangedOnDiskEventArgs> FileChangedOnDisk;

        /// <summary>
        /// Reference to the FileChange service.
        /// </summary>
        private IVsFileChangeEx fileChangeService;

        /// <summary>
        /// Maps between the observed item identified by its filename (in canonicalized form) and the cookie used for subscribing 
        /// to the events.
        /// </summary>
        private Dictionary<string, ObservedItemInfo> observedItems = new Dictionary<string, ObservedItemInfo>();

        private bool disposed;

        public FileChangeManager(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException("serviceProvider");
            }

            this.fileChangeService = (IVsFileChangeEx)serviceProvider.GetService(typeof(SVsFileChangeEx));

            if (this.fileChangeService == null)
            {
                // VS is in bad state, since the SVsFileChangeEx could not be proffered.
                throw new InvalidOperationException();
            }
        }

        public void Dispose()
        {
            // Don't dispose more than once
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;

            // Unsubscribe from the observed source files.
            foreach (ObservedItemInfo info in this.observedItems.Values)
            {
                this.fileChangeService.UnadviseFileChange(info.FileChangeCookie);
            }

            // Clean the observerItems list
            this.observedItems.Clear();
        }

        /// <summary>
        /// Called when one of the file have changed on disk.
        /// </summary>
        /// <param name="numberOfFilesChanged">Number of files changed.</param>
        /// <param name="filesChanged">Array of file names.</param>
        /// <param name="flags">Array of flags indicating the type of changes. See _VSFILECHANGEFLAGS.</param>
        /// <returns>If the method succeeds, it returns S_OK. If it fails, it returns an error code.</returns>
        int IVsFileChangeEvents.FilesChanged(uint numberOfFilesChanged, string[] filesChanged, uint[] flags)
        {
            if (this.FileChangedOnDisk != null)
            {
                for (int i = 0; i < numberOfFilesChanged; i++)
                {
                    string fullFileName = Utilities.CanonicalizeFileName(filesChanged[i]);
                    if (this.observedItems.ContainsKey(fullFileName))
                    {
                        ObservedItemInfo info = this.observedItems[fullFileName];
                        this.FileChangedOnDisk(this, new FileChangedOnDiskEventArgs(fullFileName, info.ItemID, (_VSFILECHANGEFLAGS)flags[i]));
                    }
                }
            }

            return VSConstants.S_OK;
        }

        /// <summary>
        /// Notifies clients of changes made to a directory. 
        /// </summary>
        /// <param name="directory">Name of the directory that had a change.</param>
        /// <returns>If the method succeeds, it returns S_OK. If it fails, it returns an error code. </returns>
        int IVsFileChangeEvents.DirectoryChanged(string directory)
        {
            return VSConstants.S_OK;
        }

        /// <summary>
        /// Observe when the given file is updated on disk. In this case we do not care about the item id that represents the file in the hierarchy.
        /// </summary>
        /// <param name="fileName">File to observe.</param>
        public void ObserveItem(string fileName)
        {
            this.ObserveItem(fileName, VSConstants.VSITEMID_NIL);
        }
        
        /// <summary>
        /// Observe when the given file is updated on disk.
        /// </summary>
        /// <param name="fileName">File to observe.</param>
        /// <param name="id">The item id of the item to observe.</param>
        public void ObserveItem(string fileName, uint id)
        {
            if (String.IsNullOrEmpty(fileName))
            {
                throw new ArgumentException(SR.GetString(SR.InvalidParameter, CultureInfo.CurrentUICulture), "fileName");
            }

            string fullFileName = Utilities.CanonicalizeFileName(fileName);
            if (!this.observedItems.ContainsKey(fullFileName))
            {
                // Observe changes to the file
                uint fileChangeCookie;
                ErrorHandler.ThrowOnFailure(this.fileChangeService.AdviseFileChange(fullFileName, (uint)(_VSFILECHANGEFLAGS.VSFILECHG_Time | _VSFILECHANGEFLAGS.VSFILECHG_Del), this, out fileChangeCookie));

                ObservedItemInfo itemInfo = new ObservedItemInfo();
                itemInfo.ItemID = id;
                itemInfo.FileChangeCookie = fileChangeCookie;

                // Remember that we're observing this file (used in FilesChanged event handler)
                this.observedItems.Add(fullFileName, itemInfo);
            }
        }

        /// <summary>
        /// Ignore item file changes for the specified item.
        /// </summary>
        /// <param name="fileName">File to ignore observing.</param>
        /// <param name="ignore">Flag indicating whether or not to ignore changes (1 to ignore, 0 to stop ignoring).</param>
        public void IgnoreItemChanges(string fileName, bool ignore)
        {
            if (String.IsNullOrEmpty(fileName))
            {
                throw new ArgumentException(SR.GetString(SR.InvalidParameter, CultureInfo.CurrentUICulture), "fileName");
            }

            string fullFileName = Utilities.CanonicalizeFileName(fileName);
            if (this.observedItems.ContainsKey(fullFileName))
            {
                // Call ignore file with the flags specified.
                ErrorHandler.ThrowOnFailure(this.fileChangeService.IgnoreFile(0, fileName, ignore ? 1 : 0));
            }
        }

        /// <summary>
        /// Stop observing when the file is updated on disk.
        /// </summary>
        /// <param name="fileName">File to stop observing.</param>
        public void StopObservingItem(string fileName)
        {
            if (String.IsNullOrEmpty(fileName))
            {
                throw new ArgumentException(SR.GetString(SR.InvalidParameter, CultureInfo.CurrentUICulture), "fileName");
            }

            string fullFileName = Utilities.CanonicalizeFileName(fileName);

            if (this.observedItems.ContainsKey(fullFileName))
            {
                // Get the cookie that was used for this.observedItems to this file.
                ObservedItemInfo itemInfo = this.observedItems[fullFileName];

                // Remove the file from our observed list. It's important that this is done before the call to 
                // UnadviseFileChange, because for some reason, the call to UnadviseFileChange can trigger a 
                // FilesChanged event, and we want to be able to filter that event away.
                this.observedItems.Remove(fullFileName);

                // Stop observing the file
                ErrorHandler.ThrowOnFailure(this.fileChangeService.UnadviseFileChange(itemInfo.FileChangeCookie));
            }
        }
    }
}
