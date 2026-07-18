using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ArcGIS.Desktop.Framework.Contracts;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.DataManagement.MdbBatchToGdb
{
    internal partial class MdbBatchToGdbViewModel
    {

        private static void ExecuteOnUiThread(Action action)
        {
            if (action == null)
            {
                return;
            }

            PresentationServices.UiThread.InvokeOrRun(action);
        }
    }
}
