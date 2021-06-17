// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

using System;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;

namespace Microsoft.VisualStudio.FSharp.ProjectSystem 
{
    internal sealed class VSProjectConstants 
    {
        public static readonly Guid guidCSharpBrowseLibrary = new Guid("58f1bad0-2288-45b9-ac3a-d56398f7781d"); // For IVsObjBrowser.NavigateTo to browse a referenced C# project
        public static readonly Guid guidVBBrowseLibrary = new Guid("414AC972-9829-4B6A-A8D7-A08152FEB8AA"); // For IVsObjBrowser.NavigateTo to browse a referenced VB project
        public static readonly Guid guidObjectBrowser = new Guid("{CFF630F8-2DB3-44BA-9FC9-6489665DE5B8}"); // For IVsUIShellOpenDocument3.GetProvisionalViewingStatusForEditor to check if object browser can preview

        /// CommandIDs matching the commands defined symbols in MenusAndCommands.vsct (stored with FSharp.ProjectSystem.FSharp)
        public static readonly Guid guidFSharpProjectCmdSet = new Guid("75AC5611-A912-4195-8A65-457AE17416FB");
        public static readonly CommandID MoveUpCmd = new CommandID(guidFSharpProjectCmdSet, 0x3002);
        public static readonly CommandID AddNewItemBelow = new CommandID(guidFSharpProjectCmdSet, 0x3003);
        public static readonly CommandID AddExistingItemBelow = new CommandID(guidFSharpProjectCmdSet, 0x3004);
        public static readonly CommandID AddNewItemAbove = new CommandID(guidFSharpProjectCmdSet, 0x3005);
        public static readonly CommandID AddExistingItemAbove = new CommandID(guidFSharpProjectCmdSet, 0x3006);
        public static readonly CommandID MoveDownCmd = new CommandID(guidFSharpProjectCmdSet, 0x3007);
        public static readonly CommandID NewFolderAbove = new CommandID(guidFSharpProjectCmdSet, 0x3008);
        public static readonly CommandID NewFolderBelow = new CommandID(guidFSharpProjectCmdSet, 0x3009);

        public static readonly CommandID FSharpSendThisReferenceToInteractiveCmd = new CommandID(guidFSharpProjectCmdSet, 0x5004);
        public static readonly CommandID FSharpSendReferencesToInteractiveCmd = new CommandID(guidFSharpProjectCmdSet, 0x5005);
        public static readonly CommandID FSharpSendProjectOutputToInteractiveCmd = new CommandID(guidFSharpProjectCmdSet, 0x5006);
    }
}
