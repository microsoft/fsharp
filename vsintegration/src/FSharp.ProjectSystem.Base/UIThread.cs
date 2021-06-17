// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

using System;
using System.Runtime.InteropServices;
using System.Xml;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.IO;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using System.Diagnostics;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Shell;
using IOleServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;
using IServiceProvider = System.IServiceProvider;
using VsShell = Microsoft.VisualStudio.Shell.VsShellUtilities;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.VisualStudio.FSharp.ProjectSystem
{
    internal static class UIThread
    {
        static SynchronizationContext ctxt;
        static bool isUnitTestingMode = false;
#if DEBUG
        static StackTrace captureStackTrace; // stack trace when ctxt was captured
        static Thread uithread;
#endif
        public static SynchronizationContext TheSynchronizationContext
        {
            get
            {
                Debug.Assert(ctxt != null, "Tried to get TheSynchronizationContext before it was captured");
                return ctxt;
            }
        }

        public static void InitUnitTestingMode()
        {
            Debug.Assert(ctxt == null, "Context has already been captured; too late to InitUnitTestingMode");
            isUnitTestingMode = true;
        }

        [Conditional("DEBUG")]
        public static void MustBeCalledFromUIThread()
        {
#if DEBUG
            Debug.Assert(uithread == System.Threading.Thread.CurrentThread || isUnitTestingMode, "This must be called from the GUI thread");
#endif
        }
        public static void CaptureSynchronizationContext()
        {
            if (isUnitTestingMode) return;
#if DEBUG
            uithread = System.Threading.Thread.CurrentThread;
#endif

            if (ctxt == null)
            {
#if DEBUG
                // This is a handy place to do this, since the product and all interesting unit tests
                // must go through this code path.
                AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(delegate (object sender, UnhandledExceptionEventArgs args)
                {
                    if (args.IsTerminating)
                    {
                        string s = String.Format("An unhandled exception is about to terminate the process.  Exception info:\n{0}", args.ExceptionObject.ToString());
                        Debug.Assert(false, s);
                    }
                });
                captureStackTrace = new StackTrace(true);
#endif
                ctxt = new WindowsFormsSynchronizationContext();
            }
            else
            {
#if DEBUG
                // Make sure we are always capturing the same thread.
                Debug.Assert(uithread == Thread.CurrentThread);
#endif
            }
        }
        private static readonly Queue<Action> ourUIQueue = new Queue<Action>();
        private static bool ourIsReentrancy;

        // Runs the action on UI thread. Prevents from reentracy.
        public static void Run(Action action)
        {
            if (isUnitTestingMode)
            {
                action();
                return;
            }
            Debug.Assert(ctxt != null, "The SynchronizationContext must be captured before calling this method");
#if DEBUG
            StackTrace stackTrace = new StackTrace(true);
#endif
#pragma warning disable VSTHRD001 // Avoid legacy thread switching APIs
            ctxt.Post(delegate (object ignore)
#pragma warning restore VSTHRD001 // Avoid legacy thread switching APIs
            {
                UIThread.MustBeCalledFromUIThread();
                ourUIQueue.Enqueue(action);
                if (ourIsReentrancy) return;
                ourIsReentrancy = true;
                try
                {
                    while (ourUIQueue.Count > 0)
                    {
                        try
                        {
                            var a = ourUIQueue.Dequeue();
                            a();
                        }
#if DEBUG
                        catch (Exception e)
                        {
                            // swallow, random exceptions should not kill process
                            Debug.Assert(false, string.Format("UIThread.Run caught and swallowed exception: {0}\n\noriginally invoked from stack:\n{1}", e.ToString(), stackTrace.ToString()));
                        }
#else
                        catch (Exception) {
                            // swallow, random exceptions should not kill process
                        }
#endif

                    }
                }
                finally
                {
                    ourIsReentrancy = false;
                }
            }, null);

        }

        /// <summary>
        /// RunSync puts orignal exception stacktrace to Exception.Data by this key if action throws on UI thread
        /// </summary>
        /// WrappedStacktraceKey is a string to keep exception serializable.
        public static readonly string WrappedStacktraceKey = "$$Microsoft.VisualStudio.Package.UIThread.WrappedStacktraceKey$$";

        public static void RunSync(Action a)
        {
            if (isUnitTestingMode)
            {
                a();
                return;
            }
            Exception exn = null;
            Debug.Assert(ctxt != null, "The SynchronizationContext must be captured before calling this method");
            // Send on UI thread will execute immediately.
#pragma warning disable VSTHRD001 // Avoid legacy thread switching APIs
            ctxt.Send(ignore =>
#pragma warning restore VSTHRD001 // Avoid legacy thread switching APIs
            {
                try
                {
                    UIThread.MustBeCalledFromUIThread();
                    a();
                }
                catch (Exception e)
                {
                    exn = e;
                }
            }, null
            );
            if (exn != null)
            {
                // throw exception on calling thread, preserve stacktrace
                if (!exn.Data.Contains(WrappedStacktraceKey)) exn.Data[WrappedStacktraceKey] = exn.StackTrace;
                throw exn;
            }
        }

        /// <summary>
        /// Local JoinableTaskContext
        /// ensuring non-reentrancy.
        /// </summary>
        private static JoinableTaskFactory JTF => ThreadHelper.JoinableTaskContext.Factory;

        /// <summary>
        /// Performs a callback on the UI thread and blocks until it is done, using the VS mechanism for
        /// ensuring non-reentrancy.
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        internal static T DoOnUIThread<T>(Func<T> callback)
        {
            return JTF.Run<T>(async delegate
            {
                await JTF.SwitchToMainThreadAsync();
                return callback();
            });
        }

        /// <summary>
        /// Performs a callback on the UI thread and blocks until it is done, using the VS mechanism for
        /// ensuring non-reentrancy.
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        internal static void DoOnUIThread(Action callback)
        {
            JTF.Run(async delegate
            {
                await JTF.SwitchToMainThreadAsync();
                callback();
            });
        }
    }
}