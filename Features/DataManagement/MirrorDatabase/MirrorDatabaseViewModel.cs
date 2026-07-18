using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Data;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.DDL;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;

namespace XIAOFUTools.Features.DataManagement.MirrorDatabase
{
    /// <summary>
    /// 布尔值反转转换器
    /// </summary>
    public class BooleanInverseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return !boolValue;
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return !boolValue;
            return value;
        }
    }

    /// <summary>
    /// 镜像数据库视图模型
    /// </summary>
    public partial class MirrorDatabaseViewModel : PropertyChangedBase
    {
        private readonly Infrastructure.MirrorDatabasePathStore _pathStore = new Infrastructure.MirrorDatabasePathStore();

        private string _sourceDatabasePath;
        private string _outputFolderPath;
        private string _databaseName;
        private bool _isProcessing;
        private string _logText;
        private CancellationTokenSource _cancellationTokenSource;

        public MirrorDatabaseViewModel()
        {
            Initialize();
            InitializeCommands();
        }
    }
}
