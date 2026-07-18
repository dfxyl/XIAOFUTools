using ArcGIS.Core.CIM;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Mapping.Events;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;
using XIAOFUTools.Features.General.HistoricalImageryDownload;
using XIAOFUTools.Features.General.HistoricalImageryDownload.Services;

namespace XIAOFUTools.Features.General.HistoricalImagery
{
    internal partial class HistoricalImageryDockPaneViewModel
    {

        public ObservableCollection<TreeNode> TreeNodes
        {
            get => _treeNodes;
            set => SetProperty(ref _treeNodes, value);
        }


        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }


        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }


        public TreeNode SelectedNode
        {
            get => _selectedNode;
            set => SetProperty(ref _selectedNode, value);
        }


        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    FilterVersions();
                }
            }
        }


        public ImageryMetadata CurrentMetadata
        {
            get => _currentMetadata;
            set => SetProperty(ref _currentMetadata, value);
        }


        public string MetadataDisplay
        {
            get => _metadataDisplay;
            set => SetProperty(ref _metadataDisplay, value);
        }


        public bool ShowChangedOnly
        {
            get => _showChangedOnly;
            set
            {
                if (SetProperty(ref _showChangedOnly, value))
                {
                    if (value)
                    {
                        _ = DetectChangedVersionsAsync();
                    }
                    else
                    {
                        _allVersions = _catalogVersions.Select(CloneVersion).ToList();
                        FilterVersions();
                    }
                }
            }
        }


        public ICommand RefreshCommand { get; }

        public ICommand AddLayerCommand { get; }

        public ICommand ClearSearchCommand { get; }

        public ICommand QueryMetadataCommand { get; }

        public ICommand ShowHelpCommand { get; }

        public HistoricalImageryTreeDragDropHandler TreeDragDropHandler { get; }

    }
}
