using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.Conversion.PdfToImages
{
    internal partial class PdfToImagesDockPaneViewModel
    {
        /// <summary>
        /// 输入文件夹
        /// </summary>
        public string InputFolder
        {
            get => _inputFolder;
            set
            {
                if (_inputFolder != value)
                {
                    _inputFolder = value;
                    OnPropertyChanged(nameof(InputFolder));
                }
            }
        }
        /// <summary>
        /// 输出文件夹
        /// </summary>
        public string OutputFolder
        {
            get => _outputFolder;
            set
            {
                if (_outputFolder != value)
                {
                    _outputFolder = value;
                    OnPropertyChanged(nameof(OutputFolder));
                    OnPropertyChanged(nameof(ShowKeepOriginalStructure));
                    OnPropertyChanged(nameof(ShowOutputFolder));
                }
            }
        }
        /// <summary>
        /// 是否保存在源路径
        /// </summary>
        public bool SaveToSourcePath
        {
            get => _saveToSourcePath;
            set
            {
                if (_saveToSourcePath != value)
                {
                    _saveToSourcePath = value;
                    OnPropertyChanged(nameof(SaveToSourcePath));
                    OnPropertyChanged(nameof(ShowKeepOriginalStructure));
                    OnPropertyChanged(nameof(ShowOutputFolder));
                }
            }
        }
        /// <summary>
        /// 是否按原结构输出
        /// </summary>
        public bool KeepOriginalStructure
        {
            get => _keepOriginalStructure;
            set
            {
                if (_keepOriginalStructure != value)
                {
                    _keepOriginalStructure = value;
                    OnPropertyChanged(nameof(KeepOriginalStructure));
                }
            }
        }
        /// <summary>
        /// 是否单独创建文件夹（每一个PDF一个文件夹）
        /// </summary>
        public bool CreateSeparateFolder
        {
            get => _createSeparateFolder;
            set
            {
                if (_createSeparateFolder != value)
                {
                    _createSeparateFolder = value;
                    OnPropertyChanged(nameof(CreateSeparateFolder));
                }
            }
        }
        /// <summary>
        /// 是否遍历文件夹
        /// </summary>
        public bool TraverseSubfolders
        {
            get => _traverseSubfolders;
            set
            {
                if (_traverseSubfolders != value)
                {
                    _traverseSubfolders = value;
                    OnPropertyChanged(nameof(TraverseSubfolders));
                }
            }
        }
        /// <summary>
        /// 分辨率
        /// </summary>
        public int Resolution
        {
            get => _resolution;
            set
            {
                if (_resolution != value)
                {
                    _resolution = value;
                    OnPropertyChanged(nameof(Resolution));
                }
            }
        }
        /// <summary>
        /// 输出格式
        /// </summary>
        public string OutputFormat
        {
            get => _outputFormat;
            set
            {
                if (_outputFormat != value)
                {
                    _outputFormat = value;
                    OnPropertyChanged(nameof(OutputFormat));
                }
            }
        }

        /// <summary>
        /// 输出格式选项
        /// </summary>
        public List<string> OutputFormats { get; } = new List<string> { "JPG", "PNG", "WEBP", "JPEG" };

        /// <summary>
        /// 是否显示按原结构输出选项（有输出文件夹且未勾选保存在源路径时显示）
        /// </summary>
        public bool ShowKeepOriginalStructure => !SaveToSourcePath && !string.IsNullOrWhiteSpace(OutputFolder);

        /// <summary>
        /// 是否显示输出文件夹选择（未勾选保存在源路径时显示）
        /// </summary>
        public bool ShowOutputFolder => !SaveToSourcePath;
        /// <summary>
        /// 进度
        /// </summary>
        public double Progress
        {
            get => _progress;
            set
            {
                if (Math.Abs(_progress - value) > 0.01)
                {
                    _progress = value;
                    OnPropertyChanged(nameof(Progress));
                }
            }
        }
        /// <summary>
        /// 是否不确定进度
        /// </summary>
        public bool IsProgressIndeterminate
        {
            get => _isProgressIndeterminate;
            set
            {
                if (_isProgressIndeterminate != value)
                {
                    _isProgressIndeterminate = value;
                    OnPropertyChanged(nameof(IsProgressIndeterminate));
                }
            }
        }
        /// <summary>
        /// 日志文本
        /// </summary>
        public string LogText
        {
            get => _logText;
            set
            {
                if (_logText != value)
                {
                    _logText = value;
                    OnPropertyChanged(nameof(LogText));
                }
            }
        }
        /// <summary>
        /// 状态文本
        /// </summary>
        public string StatusText
        {
            get => _statusText;
            set
            {
                if (_statusText != value)
                {
                    _statusText = value;
                    OnPropertyChanged(nameof(StatusText));
                }
            }
        }
        /// <summary>
        /// 是否正在处理
        /// </summary>
        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                if (_isProcessing != value)
                {
                    _isProcessing = value;
                    OnPropertyChanged(nameof(IsProcessing));
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        /// <summary>
        /// 选择输入文件夹命令
        /// </summary>
        public ICommand SelectInputFolderCommand { get; }

        /// <summary>
        /// 选择输出文件夹命令
        /// </summary>
        public ICommand SelectOutputFolderCommand { get; }

        /// <summary>
        /// 开始转换命令
        /// </summary>
        public ICommand StartConversionCommand { get; }

        /// <summary>
        /// 停止转换命令
        /// </summary>
        public ICommand StopConversionCommand { get; }

        /// <summary>
        /// 帮助命令
        /// </summary>
        public ICommand HelpCommand { get; }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
