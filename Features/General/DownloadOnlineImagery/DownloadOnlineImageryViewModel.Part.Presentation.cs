using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks; 
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.DDL;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Core.Geoprocessing;
using System.Text;
using System.Globalization;

namespace XIAOFUTools.Features.General.DownloadOnlineImagery
{
    internal partial class DownloadOnlineImageryViewModel
    {

        /// <summary>
        /// 刷新图层列表
        /// </summary>
        public void RefreshLayers()
        {
            AddLogMessage("正在刷新图层列表...");
            LoadFeatureLayers();
        }

        /// <summary>
        /// 浏览输出文件夹
        /// </summary>
        private void BrowseOutputFolder()
        {
            try
            {
                AddLogMessage("正在打开文件夹选择对话框...");

                var selectedFolder = PresentationServices.Files.SelectFolder(
                    "选择影像输出文件夹",
                    OutputFolder);
                if (!string.IsNullOrWhiteSpace(selectedFolder))
                {
                    OutputFolder = selectedFolder;
                    AddLogMessage($"已选择输出文件夹: {OutputFolder}");
                }
                else
                {
                    AddLogMessage("用户取消了文件夹选择。");
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"浏览文件夹时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 显示帮助
        /// </summary>
        private void ShowHelp()
        {
            try
            {
                AddLogMessage("正在显示帮助信息...");

                string helpText = @"下载在线影像工具使用说明：

1. 要素图层：选择用于确定下载范围的要素图层
2. 输出文件夹：选择影像保存的文件夹
3. 下载级别：选择影像的缩放级别（9-21级）
   - 级别越高，影像越清晰，文件越大
   - 建议根据实际需要选择合适的级别
4. 合并影像：是否将下载的多个影像合并为一个文件

注意事项：
- 下载时间取决于范围大小和网络速度
- 请确保有足够的磁盘空间
- 下载过程中请勿关闭程序";

                PresentationServices.Dialogs.Show(helpText, "帮助信息");
                AddLogMessage("帮助信息已显示。");
            }
            catch (Exception ex)
            {
                AddLogMessage($"显示帮助时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 添加日志消息
        /// </summary>
        private void AddLogMessage(string message)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("HH:mm:ss");
                string logEntry = $"[{timestamp}] {message}\r\n";

                PresentationServices.UiThread.InvokeOrRun(() =>
                {
                    LogMessages += logEntry;
                });
            }
            catch (Exception ex)
            {
                // 如果日志记录失败，至少不要让程序崩溃
                System.Diagnostics.Debug.WriteLine($"AddLogMessage failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 通知命令状态可能已改变
        /// </summary>
        private void NotifyCanExecuteChanged()
        {
            ((RelayCommand)StartDownloadCommand)?.RaiseCanExecuteChanged();
            ((RelayCommand)StopDownloadCommand)?.RaiseCanExecuteChanged();
        }

        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected void NotifyPropertyChanged(System.Linq.Expressions.Expression<Func<object>> propertyExpression)
        {
            var memberExpression = propertyExpression.Body as System.Linq.Expressions.MemberExpression;
            if (memberExpression == null)
            {
                var unaryExpression = propertyExpression.Body as System.Linq.Expressions.UnaryExpression;
                if (unaryExpression != null)
                {
                    memberExpression = unaryExpression.Operand as System.Linq.Expressions.MemberExpression;
                }
            }

            if (memberExpression != null)
            {
                OnPropertyChanged(memberExpression.Member.Name);
            }
        }
    }
}
