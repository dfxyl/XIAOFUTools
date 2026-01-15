using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Core.CIM;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;
using System.Windows.Media;
using XIAOFUTools.Tools.Authorization;

namespace XIAOFUTools.Tools.PresetLayers
{
    /// <summary>
    /// 预设图层图库
    /// </summary>
    internal class PresetLayersGallery : Gallery
    {
        private bool _isInitialized = false;
        private readonly string _relativePath = @"Data\影像图层";

        public PresetLayersGallery()
        {
        }

        /// <summary>
        /// 初始化图库
        /// </summary>
        protected override void OnDropDownOpened()
        {
            if (!_isInitialized)
            {
                LoadLayerItems();
                _isInitialized = true;
            }
        }

        /// <summary>
        /// 加载图层项目
        /// </summary>
        private void LoadLayerItems()
        {
            try
            {
                // 清空现有项目
                Clear();

                // 获取程序集位置和相对路径
                string assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string installPath = Path.GetDirectoryName(assemblyLocation);
                string layersPath = Path.GetFullPath(Path.Combine(installPath, _relativePath));

                if (Directory.Exists(layersPath))
                {
                    var layerFiles = Directory.EnumerateFiles(layersPath, "*.*", SearchOption.TopDirectoryOnly)
                                              .Where(s => s.EndsWith(".lyr") || s.EndsWith(".lyrx"));

                    foreach (var layerFile in layerFiles)
                    {
                        string layerName = Path.GetFileNameWithoutExtension(layerFile);
                        // 创建每个层的图库项，不显示路径
                        Add(new GalleryItem(layerName, GetLayerIcon(layerFile), null)); // 将路径设置为 null
                    }
                }
                else
                {
                    MessageBox.Show($"指定的路径不存在：{layersPath}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载预设图层时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }



        /// <summary>
        /// 获取图层图标
        /// </summary>
        private ImageSource GetLayerIcon(string layerFile)
        {
            // 这里可以生成或返回层的默认图标
            // 目前返回空值，实际逻辑应替换为需要的图标
            return null; // 替换为实际图标生成逻辑
        }

        /// <summary>
        /// 处理图层项目点击事件
        /// </summary>
        protected override async void OnClick(GalleryItem item)
        {
            try
            {
                // 检查授权
                if (!AuthorizationChecker.CheckAuthorizationWithPrompt("添加预设图层工具"))
                {
                    return;
                }

                string selectedLayer = item.Text; // 获取没有扩展名的层名称
                string assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string installPath = Path.GetDirectoryName(assemblyLocation);
                string layersPath = Path.GetFullPath(Path.Combine(installPath, _relativePath));

                // 确定完整的层路径
                string layerPath = Directory.EnumerateFiles(layersPath, selectedLayer + ".*")
                                             .FirstOrDefault(); // 获取第一个匹配的层文件

                if (layerPath == null)
                {
                    MessageBox.Show("未找到图层文件。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                await QueuedTask.Run(() =>
                {
                    // 将层添加到地图
                    Layer newLayer = LayerFactory.Instance.CreateLayer(new Uri(layerPath), MapView.Active.Map);

                    var map = MapView.Active.Map;

                    // 获取所有图层
                    var allLayers = map.Layers.ToList();

                    // 将新添加的图层移动到底部
                    if (allLayers.Count > 1)
                    {
                        map.MoveLayer(newLayer, allLayers.Count - 1);
                    }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"添加图层时发生错误：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }




    }
}
