// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

using System.Diagnostics;

namespace Microsoft.VisualStudio.FSharp.ProjectSystem
{
    internal class CCITracing
    {
        private CCITracing() { }

        [ConditionalAttribute("Enable_CCIDiagnostics")]
        static void InternalTraceCall(int levels)
        {
            System.Diagnostics.StackFrame stack;
            stack = new System.Diagnostics.StackFrame(levels);
            System.Reflection.MethodBase method = stack.GetMethod();
            if (method != null)
            {
                string name = method.Name + " \tin class " + method.DeclaringType.Name;
                System.Diagnostics.Trace.WriteLine("Call Trace: \t" + name);
            }
        }

        [ConditionalAttribute("CCI_TRACING")]
        static public void TraceCall()
        {
            // skip this one as well
            CCITracing.InternalTraceCall(2);
        }

        [ConditionalAttribute("CCI_TRACING")]
        static public void TraceCall(string strParameters)
        {
            CCITracing.InternalTraceCall(2);
            System.Diagnostics.Trace.WriteLine("\tParameters: \t" + strParameters);
        }

        [ConditionalAttribute("CCI_TRACING")]
        static public void Trace(System.Exception e)
        {
            CCITracing.InternalTraceCall(2);
            System.Diagnostics.Trace.WriteLine("ExceptionInfo: \t" + e.ToString());
        }

        [ConditionalAttribute("CCI_TRACING")]
        static public void Trace(string strOutput)
        {
            System.Diagnostics.Trace.WriteLine(strOutput);
        }

        [ConditionalAttribute("CCI_TRACING")]
        static public void TraceData(string strOutput)
        {
            System.Diagnostics.Trace.WriteLine("Data Trace: \t" + strOutput);
        }

        [ConditionalAttribute("Enable_CCIFileOutput")]
        [ConditionalAttribute("CCI_TRACING")]
        static public void AddTraceLog(string strFileName)
        {
            TextWriterTraceListener tw = new TextWriterTraceListener("c:\\mytrace.log");
            System.Diagnostics.Trace.Listeners.Add(tw);
        }
    }
}
