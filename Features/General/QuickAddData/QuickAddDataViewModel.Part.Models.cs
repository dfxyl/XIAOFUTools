using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using Microsoft.Win32;

namespace XIAOFUTools.Features.General.QuickAddData
{
    internal sealed partial class QuickAddDataViewModel
    {

        private sealed class TreeViewStateSnapshot
        {
            public HashSet<string> ExpandedKeys { get; } = new HashSet<string>(StringComparer.Ordinal);

            public HashSet<string> SelectedKeys { get; } = new HashSet<string>(StringComparer.Ordinal);

            public string AnchorKey { get; set; }

            public string SelectedNodeKey { get; set; }
        }

    }
}
