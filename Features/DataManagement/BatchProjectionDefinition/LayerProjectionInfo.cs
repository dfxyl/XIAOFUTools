using ArcGIS.Desktop.Mapping;
using ArcGIS.Core.Geometry;
using System.ComponentModel;

namespace XIAOFUTools.Features.DataManagement.BatchProjectionDefinition
{
    /// <summary>
    /// 图层投影信息类
    /// </summary>
    public class LayerProjectionInfo : INotifyPropertyChanged
    {
        private bool _isSelected;
        private Layer _layer;
        private string _layerName;
        private string _layerType;
        private string _currentCoordinateSystem;

        /// <summary>
        /// 是否选中
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }

        /// <summary>
        /// 图层对象
        /// </summary>
        public Layer Layer
        {
            get => _layer;
            set
            {
                _layer = value;
                OnPropertyChanged(nameof(Layer));
            }
        }

        /// <summary>
        /// 图层名称
        /// </summary>
        public string LayerName
        {
            get => _layerName;
            set
            {
                _layerName = value;
                OnPropertyChanged(nameof(LayerName));
            }
        }

        /// <summary>
        /// 图层类型
        /// </summary>
        public string LayerType
        {
            get => _layerType;
            set
            {
                _layerType = value;
                OnPropertyChanged(nameof(LayerType));
            }
        }

        /// <summary>
        /// 当前坐标系
        /// </summary>
        public string CurrentCoordinateSystem
        {
            get => _currentCoordinateSystem;
            set
            {
                _currentCoordinateSystem = value;
                OnPropertyChanged(nameof(CurrentCoordinateSystem));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
