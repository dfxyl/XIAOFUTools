#define DEBUG
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Threading;
using ArcGIS.Desktop.Framework.Controls;
using ArcGIS.Desktop.Framework.Dialogs;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XIAOFUTools.Features.User.AIAssistant.Agent;
using XIAOFUTools.Features.User.AIAssistant.Application;
using XIAOFUTools.Features.User.AIAssistant.Database;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace XIAOFUTools.Features.User.AIAssistant
{
    public partial class AIAssistantDockPaneView
    {
    private sealed class ActiveRequestInfo
    {
        public string SessionId { get; }
        public CancellationTokenSource CancellationTokenSource { get; }

        public ActiveRequestInfo(string sessionId, CancellationTokenSource cancellationTokenSource)
        {
            SessionId = sessionId;
            CancellationTokenSource = cancellationTokenSource;
        }
    }

    private sealed class PendingToolApprovalInfo
    {
        public string SessionId { get; }
        public string StreamId { get; }
        public TaskCompletionSource<string> Completion { get; }

        public PendingToolApprovalInfo(string sessionId, string streamId, TaskCompletionSource<string> completion)
        {
            SessionId = sessionId;
            StreamId = streamId;
            Completion = completion;
        }
    }
    }
}
