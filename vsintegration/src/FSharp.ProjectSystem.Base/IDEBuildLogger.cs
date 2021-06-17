// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.
using System;
using System.Threading;
using System.Diagnostics;
using System.Globalization;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.Win32;
using IOleServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;

namespace Microsoft.VisualStudio.FSharp.ProjectSystem
{
    public static class LoggingConstants
    {
        public const string DefaultVSRegistryRoot = @"Software\Microsoft\VisualStudio\15.0";
        public const string BuildVerbosityRegistrySubKey = @"General";
        public const string BuildVerbosityRegistryValue = "MSBuildLoggerVerbosity";
        public const string UpToDateVerbosityRegistryValue = "U2DCheckVerbosity";
    }
    /// <summary>
    /// This class implements an MSBuild logger that output events to VS outputwindow and tasklist.
    /// </summary>
    [ComVisible(true)]
    public sealed class IDEBuildLogger : Logger
    {
        // TODO: Remove these constants when we have a version that supports getting the verbosity using automation.
        private string buildVerbosityRegistryRoot = LoggingConstants.DefaultVSRegistryRoot;
        // TODO: Re-enable this constants when we have a version that suppoerts getting the verbosity using automation.

		private int currentIndent;
		private IVsOutputWindowPane outputWindowPane;
		private string errorString = SR.GetString(SR.Error, CultureInfo.CurrentUICulture);
		private string warningString = SR.GetString(SR.Warning, CultureInfo.CurrentUICulture);
		private bool isLogTaskDone;
		private IVsHierarchy hierarchy;
		private IServiceProvider serviceProvider;
        private IVsLanguageServiceBuildErrorReporter2 errorReporter;
        private bool haveCachedRegistry = false;

		public string WarningString
		{
			get { return this.warningString; }
			set { this.warningString = value; }
		}
		public string ErrorString
		{
			get { return this.errorString; }
			set { this.errorString = value; }
		}
		public bool IsLogTaskDone
		{
			get { return this.isLogTaskDone; }
			set { this.isLogTaskDone = value; }
		}
		/// <summary>
		/// When building from within VS, setting this will
		/// enable the logger to retrive the verbosity from
		/// the correct registry hive.
		/// </summary>
		public string BuildVerbosityRegistryRoot
		{
			get { return buildVerbosityRegistryRoot; }
			set { buildVerbosityRegistryRoot = value; }
		}
		/// <summary>
		/// Set to null to avoid writing to the output window
		/// </summary>
		public IVsOutputWindowPane OutputWindowPane
		{
			get { return outputWindowPane; }
			set { outputWindowPane = value; }
		}

        public IVsLanguageServiceBuildErrorReporter2 ErrorReporter
        {
            get { return errorReporter; }
            set { errorReporter = value; }
        }

        internal IDEBuildLogger(IVsOutputWindowPane output, IVsHierarchy hierarchy, IVsLanguageServiceBuildErrorReporter2 errorReporter)
		{
			if (hierarchy == null)
				throw new ArgumentNullException("hierarchy");

			this.errorReporter = errorReporter;
			this.outputWindowPane = output;
			this.hierarchy = hierarchy;
			IOleServiceProvider site;
			Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(hierarchy.GetSite(out site));
			this.serviceProvider = new Shell.ServiceProvider (site);
		}

		public override void Initialize(IEventSource eventSource)
		{
			if (null == eventSource)
			{
				throw new ArgumentNullException("eventSource");
			}
            // Note that the MSBuild logger thread does not have an exception handler, so
            // we swallow all exceptions (lest some random bad thing bring down the process).
            eventSource.BuildStarted += new BuildStartedEventHandler(BuildStartedHandler);
            eventSource.BuildFinished += new BuildFinishedEventHandler(BuildFinishedHandler);
            eventSource.ProjectStarted += new ProjectStartedEventHandler(ProjectStartedHandler);
            eventSource.ProjectFinished += new ProjectFinishedEventHandler(ProjectFinishedHandler);
            eventSource.TargetStarted += new TargetStartedEventHandler(TargetStartedHandler);
            eventSource.TargetFinished += new TargetFinishedEventHandler(TargetFinishedHandler);
            eventSource.TaskStarted += new TaskStartedEventHandler(TaskStartedHandler);
            eventSource.TaskFinished += new TaskFinishedEventHandler(TaskFinishedHandler);
            eventSource.CustomEventRaised += new CustomBuildEventHandler(CustomHandler);
            eventSource.ErrorRaised += new BuildErrorEventHandler(ErrorHandler);
            eventSource.WarningRaised += new BuildWarningEventHandler(WarningHandler);
            eventSource.MessageRaised += new BuildMessageEventHandler(MessageHandler);
        }

		/// <summary>
		/// This is the delegate for error events.
		/// </summary>
		private void ErrorHandler(object sender, BuildErrorEventArgs errorEvent)
		{
            try
            {
                AddToErrorList(
                    errorEvent,
                    errorEvent.Subcategory,
                    errorEvent.Code,
                    errorEvent.File,
                    errorEvent.LineNumber,
                    errorEvent.ColumnNumber,
                    errorEvent.EndLineNumber,
                    errorEvent.EndColumnNumber);

            }
            catch (Exception e)
            {
                Debug.Assert(false, "Problem adding error to error list: " + e.Message + " at " + e.TargetSite);
            }
		}

		/// <summary>
		/// This is the delegate for warning events.
		/// </summary>
		private void WarningHandler(object sender, BuildWarningEventArgs errorEvent)
		{
            try
            {
                AddToErrorList(
                    errorEvent,
                    errorEvent.Subcategory, 
                    errorEvent.Code,
                    errorEvent.File,
                    errorEvent.LineNumber,
                    errorEvent.ColumnNumber,
                    errorEvent.EndLineNumber,
                    errorEvent.EndColumnNumber);
            }
            catch (Exception e)
            {
                Debug.Assert(false, "Problem adding warning to warning list: " + e.Message + " at " + e.TargetSite);
            }
		}
        /// <summary>
        /// Private internal class for capturing full compiler error line/column span information
        /// </summary>
        private class DefaultCompilerError : CompilerError
        {
            private int endLine;
            private int endColumn;

            public int EndLine
            {
                get { return endLine; }
                set { endLine = value; }
            }

            public int EndColumn
            {
                get { return endColumn; }
                set { endColumn = value; }
            }

            public DefaultCompilerError(string fileName,
                int startLine,
                int startColumn,
                int endLine,
                int endColumn,
                string errorNumber,
                string errorText)
                : base(fileName, startLine, startColumn, errorNumber, errorText)
            {
                EndLine = endLine;
                EndColumn = endColumn;
            }
        }

        private void Output(string s)
        {
            // Various events can call SetOutputLogger(null), which will null out "this.OutputWindowPane".  
            // So we capture a reference to it.  At some point in the future, after this build finishes, 
            // the pane reference we have will no longer accept input from us.
            // But here there is no race, because
            //  - we only log user-invoked builds to the Output window
            //  - user-invoked buils always run MSBuild ASYNC
            //  - in an ASYNC build, the BuildCoda uses UIThread.Run() to schedule itself to be run on the UI thread
            //  - UIThread.Run() protects against re-entrancy and thus always preserves the queuing order of its actions
            //  - the pane is good until at least the point when BuildCoda runs and we declare to MSBuild that we are finished with this build
            var pane = this.OutputWindowPane;  // copy to capture in delegate
            UIThread.Run(delegate()
            {
                try
                {
                    pane.OutputStringThreadSafe(s);
                }
                catch (Exception e)
                {
                    Debug.Assert(false, "We would like to know if this happens; exception in IDEBuildLogger.Output(): " + e.ToString());
                    // Don't crash process due to random exception, just swallow it
                }
            });
        }
 
		/// <summary>
		/// Add the error/warning to the error list and potentially to the output window.
		/// </summary>
		private void AddToErrorList(
			BuildEventArgs errorEvent,
            string subcategory,
			string errorCode,
			string file,
			int startLine,
			int startColumn,
            int endLine,
            int endColumn)
		{
            if (file == null)
                file = String.Empty;
            
            bool isWarning = errorEvent is BuildWarningEventArgs;
            Shell.TaskPriority priority = isWarning ? Shell.TaskPriority.Normal : Shell.TaskPriority.High;
            
            TextSpan span;
            span.iStartLine = startLine;
            span.iStartIndex = startColumn;
            span.iEndLine = endLine < startLine ? span.iStartLine : endLine;
            span.iEndIndex = (endColumn < startColumn) && (span.iStartLine == span.iEndLine) ? span.iStartIndex : endColumn;

            if (OutputWindowPane != null
                && (this.Verbosity != LoggerVerbosity.Quiet || errorEvent is BuildErrorEventArgs))
            {
                // Format error and output it to the output window
                string message = this.FormatMessage(errorEvent.Message);
                DefaultCompilerError e = new DefaultCompilerError(file,
                                                span.iStartLine,
                                                span.iStartIndex,
                                                span.iEndLine,
                                                span.iEndIndex,
                                                errorCode,
                                                message);
                e.IsWarning = isWarning;

                Output(GetFormattedErrorMessage(e));
            }

            UIThread.Run(delegate()
            {
                IVsUIShellOpenDocument openDoc = serviceProvider.GetService(typeof(IVsUIShellOpenDocument)) as IVsUIShellOpenDocument;
                if (openDoc == null)
                    return;

                IVsWindowFrame frame;
                IOleServiceProvider sp;
                IVsUIHierarchy hier;
                uint itemid;
                Guid logicalView = VSConstants.LOGVIEWID_Code;

                IVsTextLines buffer = null;
                // JAF 
                // Notes about acquiring the buffer:
                // If the file physically exists then this will open the document in the current project. It doesn't matter if the file is a member of the project.
                // Also, it doesn't matter if this is an F# file. For example, an error in Microsoft.Common.targets will cause a file to be opened here.
                // However, opening the document does not mean it will be shown in VS. 
                if (!Microsoft.VisualStudio.ErrorHandler.Failed(openDoc.OpenDocumentViaProject(file, ref logicalView, out sp, out hier, out itemid, out frame)) && frame != null) {
                    object docData;
                    frame.GetProperty((int)__VSFPROPID.VSFPROPID_DocData, out docData);

                    // Get the text lines
                    buffer = docData as IVsTextLines;

                    if (buffer == null) {
                        IVsTextBufferProvider bufferProvider = docData as IVsTextBufferProvider;
                        if (bufferProvider != null) {
                            bufferProvider.GetTextBuffer(out buffer);
                        }
                    }
                }

                // Need to adjust line and column indexing for the task window, which assumes zero-based values
                if (span.iStartLine > 0 && span.iStartIndex > 0)
                {
                    span.iStartLine -= 1;
                    span.iEndLine -= 1;
                    span.iStartIndex -= 1;
                    span.iEndIndex -= 1;
                }

                // Add error to task list
                // Code below is for --flaterrors flag that is only used by the IDE

                var stringThatIsAProxyForANewlineInFlatErrors = new string(new[] { (char)29 });
                var taskText = errorEvent.Message.Replace(stringThatIsAProxyForANewlineInFlatErrors, Environment.NewLine);

                if (errorReporter != null)
                {
                    errorReporter.ReportError2(taskText, errorCode, (VSTASKPRIORITY) priority, span.iStartLine, span.iStartIndex, span.iEndLine, span.iEndIndex, file);
                }

            });
		}


		/// <summary>
		/// This is the delegate for Message event types
		/// </summary>		
		private void MessageHandler(object sender, BuildMessageEventArgs messageEvent)
		{
            try
            {
                if (LogAtImportance(messageEvent.Importance))
                {
                    LogEvent(sender, messageEvent);
                }
            }
            catch (Exception e)
            {
                Debug.Assert(false, "Problem logging message event: " + e.Message + " at " + e.TargetSite);
                // swallow the exception
            }
		}


		/// <summary>
		/// This is the delegate for BuildStartedHandler events.
		/// </summary>
		private void BuildStartedHandler(object sender, BuildStartedEventArgs buildEvent)
		{
            try
            {
                this.haveCachedRegistry = false;
                if (LogAtImportance(MessageImportance.Normal))
                {
                    LogEvent(sender, buildEvent);
                }
            }
            catch (Exception e)
            {
                Debug.Assert(false, "Problem logging buildstarted event: " + e.Message + " at " + e.TargetSite);
                // swallow the exception
            }
            finally
            {
                if (errorReporter != null) { 
                    errorReporter.ClearErrors();
                }
            }
		}

		/// <summary>
		/// This is the delegate for BuildFinishedHandler events.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="buildEvent"></param>
		private void BuildFinishedHandler(object sender, BuildFinishedEventArgs buildEvent)
		{
            try
            {
                if (LogAtImportance(buildEvent.Succeeded ? MessageImportance.Normal :
                                                           MessageImportance.High))
                {
                    if (this.outputWindowPane != null)
                        Output(Environment.NewLine);
                    LogEvent(sender, buildEvent);
                }
            }
            catch (Exception e)
            {
                Debug.Assert(false, "Problem logging buildfinished event: " + e.Message + " at " + e.TargetSite);
                // swallow the exception
            }
		}


		/// <summary>
		/// This is the delegate for ProjectStartedHandler events.
		/// </summary>
		private void ProjectStartedHandler(object sender, ProjectStartedEventArgs buildEvent)
		{
            try
            {
                if (LogAtImportance(MessageImportance.Normal))
                {
                    LogEvent(sender, buildEvent);
                }
            }
            catch (Exception e)
            {
                Debug.Assert(false, "Problem logging projectstarted event: " + e.Message + " at " + e.TargetSite);
                // swallow the exception
            }
		}

		/// <summary>
		/// This is the delegate for ProjectFinishedHandler events.
		/// </summary>
		private void ProjectFinishedHandler(object sender, ProjectFinishedEventArgs buildEvent)
		{
            try
            {
                if (LogAtImportance(buildEvent.Succeeded ? MessageImportance.Normal
                                                         : MessageImportance.High))
                {
                    LogEvent(sender, buildEvent);
                }
            }
            catch (Exception e)
            {
                Debug.Assert(false, "Problem logging projectfinished event: " + e.Message + " at " + e.TargetSite);
                // swallow the exception
            }
		}

		/// <summary>
		/// This is the delegate for TargetStartedHandler events.
		/// </summary>
		private void TargetStartedHandler(object sender, TargetStartedEventArgs buildEvent)
		{
            try
            {
                if (LogAtImportance(MessageImportance.Normal))
                {
                    LogEvent(sender, buildEvent);
                }
            }
            catch (Exception e)
            {
                Debug.Assert(false, "Problem logging targetstarted event: " + e.Message + " at " + e.TargetSite);
                // swallow the exception
            }
            finally
            {
                ++this.currentIndent;
            }
		}


		/// <summary>
		/// This is the delegate for TargetFinishedHandler events.
		/// </summary>
		private void TargetFinishedHandler(object sender, TargetFinishedEventArgs buildEvent)
		{
            try
            {
                --this.currentIndent;
                if ((isLogTaskDone) &&
                    LogAtImportance(buildEvent.Succeeded ? MessageImportance.Normal
                                                         : MessageImportance.High))
                {
                    LogEvent(sender, buildEvent);
                }
            }
            catch (Exception e)
            {
                Debug.Assert(false, "Problem logging targetfinished event: " + e.Message + " at " + e.TargetSite);
                // swallow the exception
            }
		}


		/// <summary>
		/// This is the delegate for TaskStartedHandler events.
		/// </summary>
		private void TaskStartedHandler(object sender, TaskStartedEventArgs buildEvent)
		{
            try
            {
                if (LogAtImportance(MessageImportance.Normal))
                {
                    LogEvent(sender, buildEvent);
                }
            }
            catch (Exception e)
            {
                Debug.Assert(false, "Problem logging taskstarted event: " + e.Message + " at " + e.TargetSite);
                // swallow the exception
            }
            finally
            {
                ++this.currentIndent;
            }
		}


		/// <summary>
		/// This is the delegate for TaskFinishedHandler events.
		/// </summary>
		private void TaskFinishedHandler(object sender, TaskFinishedEventArgs buildEvent)
		{
            try
            {
                --this.currentIndent;
                if ((isLogTaskDone) &&
                    LogAtImportance(buildEvent.Succeeded ? MessageImportance.Normal
                                                         : MessageImportance.High))
                {
                    LogEvent(sender, buildEvent);
                }
            }
            catch (Exception e)
            {
                Debug.Assert(false, "Problem logging taskfinished event: " + e.Message + " at " + e.TargetSite);
                // swallow the exception
            }
		}


		/// <summary>
		/// This is the delegate for CustomHandler events.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="buildEvent"></param>
		private void CustomHandler(object sender, CustomBuildEventArgs buildEvent)
		{
            try
            {
                LogEvent(sender, buildEvent);
            }
            catch (Exception e)
            {
                Debug.Assert(false, "Problem logging custom event: " + e.Message + " at " + e.TargetSite);
                // swallow the exception
            }
		}

		/// <summary>
		/// This method takes a MessageImportance and returns true if messages
		/// at importance i should be loggeed.  Otherwise return false.
		/// </summary>
        private bool LogAtImportance(MessageImportance importance)
        {
            // If importance is too low for current settings, ignore the event
            bool logIt = false;

            this.SetVerbosity();

            switch (this.Verbosity)
            {
                case LoggerVerbosity.Quiet:
                    logIt = false;
                    break;
                case LoggerVerbosity.Minimal:
                    logIt = (importance == MessageImportance.High);
                    break;
                case LoggerVerbosity.Normal:
                    logIt = (importance == MessageImportance.Normal) || (importance == MessageImportance.High);
                    break;
                case LoggerVerbosity.Detailed:
                    logIt = (importance == MessageImportance.Low) || (importance == MessageImportance.Normal) || (importance == MessageImportance.High);
                    break;
                case LoggerVerbosity.Diagnostic:
                    logIt = true;
                    break;
                default:
                    Debug.Fail("Unknown Verbosity level. Ignoring will cause everything to be logged");
                    break;
            }

            return logIt;
        }

		/// <summary>
		/// This is the method that does the main work of logging an event
		/// when one is sent to this logger.
		/// </summary>
		private void LogEvent(object sender, BuildEventArgs buildEvent)
		{
            try
            {
                // Fill in the Message text
                if (OutputWindowPane != null && !String.IsNullOrEmpty(buildEvent.Message))
                {
                    int startingSize = this.currentIndent + buildEvent.Message.Length + 1;
                    StringBuilder msg = new StringBuilder(startingSize);
                    if (this.currentIndent > 0)
                    {
                        msg.Append('\t', this.currentIndent);
                    }
                    msg.AppendLine(buildEvent.Message);
                    Output(msg.ToString());
                }
            }
            catch (Exception e)
            {
                try
                {
                    System.Diagnostics.Debug.Assert(false, "Error thrown from IDEBuildLogger::LogEvent");
                    System.Diagnostics.Debug.Assert(false, e.ToString());
                    // For retail, also try to show in the output window.
                    Output(e.ToString());
                }
                catch 
                { 
                    // We're going to throw the original exception anyway
                }
                throw;
            }
		}

		/// <summary>
		/// This is called when the build complete.
		/// </summary>
		private void ShutdownLogger()
		{
		}


		/// <summary>
		/// Format error messages for the task list
		/// </summary>
		/// <param name="e"></param>
		/// <returns></returns>
        private string GetFormattedErrorMessage(DefaultCompilerError e)
		{
			if (e == null) return String.Empty;

			string errCode = (e.IsWarning) ? this.warningString : this.errorString;
			StringBuilder fileRef = new StringBuilder();

            // JAF:
            // Even if fsc.exe returns a canonical message with no file at all, MSBuild will set the file to the name 
            // of the task (FSC). In principle, e.FileName will not be null or empty but handle this case anyway. 
            bool thereIsAFile = !string.IsNullOrEmpty(e.FileName);
            bool thereIsASpan = e.Line!=0 || e.Column!=0 || e.EndLine!=0 || e.EndColumn!=0;
            if (thereIsAFile) {
                fileRef.AppendFormat(CultureInfo.CurrentUICulture, "{0}", e.FileName);
                if (thereIsASpan) {
                    fileRef.AppendFormat(CultureInfo.CurrentUICulture, "({0},{1})", e.Line, e.Column);
                }
                fileRef.AppendFormat(CultureInfo.CurrentUICulture, ":");
            }

            fileRef.AppendFormat(CultureInfo.CurrentUICulture, " {0} {1}: {2}", errCode, e.ErrorNumber, e.ErrorText);
            return fileRef.ToString();
		}

		/// <summary>
		/// Formats the message that is to be output.
		/// </summary>
		/// <param name="message">The message string.</param>
		/// <returns>The new message</returns>
		private string FormatMessage(string message)
		{
			if (string.IsNullOrEmpty(message))
			{
				return Environment.NewLine;
			}

			StringBuilder sb = new StringBuilder(message.Length + Environment.NewLine.Length);

			sb.AppendLine(message);
			return sb.ToString();
		}

		/// <summary>
		/// Sets the verbosity level.
		/// </summary>
        private void SetVerbosity()
        {
            // TODO: This should be replaced when we have a version that supports automation.
            if (!this.haveCachedRegistry)
            {
                string verbosityKey = String.Format(CultureInfo.InvariantCulture, @"{0}\{1}", BuildVerbosityRegistryRoot, LoggingConstants.BuildVerbosityRegistrySubKey);
                using (RegistryKey subKey = Registry.CurrentUser.OpenSubKey(verbosityKey))
                {
                    if (subKey != null)
                    {
                        object valueAsObject = subKey.GetValue(LoggingConstants.BuildVerbosityRegistryValue);
                        if (valueAsObject != null)
                        {
                            this.Verbosity = (LoggerVerbosity)((int)valueAsObject);
                        }
                    }
                }
                this.haveCachedRegistry = true;
            }

            // TODO: Continue this code to get the Verbosity when we have a version that supports automation to get the Verbosity.
            //EnvDTE.DTE dte = this.serviceProvider.GetService(typeof(EnvDTE.DTE)) as EnvDTE.DTE;
            //EnvDTE.Properties properties = dte.get_Properties(EnvironmentCategory, ProjectsAndSolutionSubCategory);
        }
    }

    /// <summary>
    /// Helper for logging to the output window
    /// </summary>
    public sealed class OutputWindowLogger
    {
        private readonly Func<bool> predicate;
        private readonly Action<string> print;

        /// <summary>
        /// Helper to create output window logger for project up-to-date check
        /// </summary>
        /// <param name="pane">Output window pane to use for logging</param>
        /// <returns>Logger</returns>
        public static OutputWindowLogger CreateUpToDateCheckLogger(IVsOutputWindowPane pane)
        {
            string upToDateVerbosityKey =
                String.Format(CultureInfo.InvariantCulture, @"{0}\{1}", LoggingConstants.DefaultVSRegistryRoot, LoggingConstants.BuildVerbosityRegistrySubKey);

            var shouldLog = false;
            using (RegistryKey subKey = Registry.CurrentUser.OpenSubKey(upToDateVerbosityKey))
            {
                if (subKey != null)
                {
                    object valueAsObject = subKey.GetValue(LoggingConstants.UpToDateVerbosityRegistryValue);
                    if (valueAsObject != null && valueAsObject is int)
                    {
                        shouldLog = ((int)valueAsObject) == 1;
                    }
                }
            }

            return new OutputWindowLogger(() => shouldLog, pane);
        }

        /// <summary>
        /// Creates a logger instance
        /// </summary>
        /// <param name="shouldLog">Predicate that will be called when logging. Should return true if logging is to be performed, false otherwise.</param>
        /// <param name="pane">The output pane where logging should be targeted</param>
        public OutputWindowLogger(Func<bool> shouldLog, IVsOutputWindowPane pane)
        {
            this.predicate = shouldLog;

            if (pane is IVsOutputWindowPaneNoPump)
            {
                var asNoPump = pane as IVsOutputWindowPaneNoPump;
                this.print = (s) => asNoPump.OutputStringNoPump(s);
            }
            else
            {
                this.print = (s) => pane.OutputStringThreadSafe(s);
            }
        }

        /// <summary>
        /// Logs a message to the output window, if the original predicate returns true
        /// </summary>
        /// <param name="message">Log message, can be a String.Format-style format string</param>
        /// <param name="args">Optional aruments for format string</param>
        public void WriteLine(string message, params object[] args)
        {
            if (this.predicate())
            {
                var s = String.Format(message, args);
                s = String.Format("{0}{1}", s, Environment.NewLine);
                this.print(s);
            }
        }
    }
}
