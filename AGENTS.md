# XIAOFUTools 开发规范

本文档为 AI 代理和开发者提供项目开发的完整指南，包含架构设计、编码规范和界面标准。

---

## 目录

- [项目概述](#项目概述)
- [技术栈](#技术栈)
- [项目架构](#项目架构)
- [开发规范](#开发规范)
- [界面规范](#界面规范)
- [新工具开发](#新工具开发)
- [常用模式](#常用模式)
- [菜单配置](#菜单配置)

---

## 项目概述

XIAOFUTools 是基于 ArcGIS Pro SDK 的 C# 扩展工具箱，采用 .NET 8.0 框架开发，提供 GIS 数据处理和分析功能。

### 核心信息
- **框架**: .NET 8.0 + WPF
- **架构**: MVVM
- **目标平台**: ArcGIS Pro 3.6+
- **当前版本**: 1.2.5

---

## 技术栈

| 技术 | 用途 |
|------|------|
| .NET 8.0 | 运行时框架 |
| ArcGIS Pro SDK | GIS 核心 API |
| WPF | UI 框架 |
| WebView2 | Web 界面支持 |
| DuckDB.NET | 高性能数据处理 |
| Newtonsoft.Json | JSON 处理 |
| ACadSharp/netDxf | CAD 文件处理 |

---

## 项目架构

### 目录结构

```
XIAOFUTools/
├── Common/                  # 公共组件
│   ├── CoordinateSystemSelector.xaml  # 坐标系选择器
│   ├── LayerUtils.cs        # 图层工具类
│   ├── OutputDatasetUtils.cs # 输出数据集工具
│   ├── PathDialogUtils.cs   # 路径对话框工具
│   ├── SelectionUtils.cs    # 选择工具类
│   └── VersionInfo.cs       # 版本信息
├── Data/                    # 数据文件
│   ├── Excel模板/           # Excel 模板
│   ├── 影像图层/            # 预设图层文件
│   └── 符号库/              # 符号库文件
├── Images/                  # 图标资源 (16px/32px)
├── Styles/                  # WPF 样式
│   └── ControlStyles.xaml   # 统一控件样式
├── Tools/                   # 工具模块
│   ├── Analysis/            # 分析工具
│   ├── Common/              # 通用工具
│   ├── Convert/             # 转换工具
│   ├── DataProcessing/      # 数据处理工具
│   ├── Edit/                # 编辑工具
│   └── User/                # 用户工具
├── Config.daml              # 插件配置
├── Module1.cs               # 主模块
└── XIAOFUTools.csproj       # 项目文件
```

### 工具模块结构

每个工具模块采用 MVVM 架构：

```
ToolName/
├── ToolNameButton.cs           # 按钮入口
├── ToolNameDockPane.cs         # 停靠窗格
├── ToolNameDockPaneView.xaml   # 视图界面
├── ToolNameDockPaneView.xaml.cs # 视图代码
└── ToolNameViewModel.cs        # 视图模型
```

### 组件职责

| 组件 | 职责 |
|------|------|
| Button | 工具入口，授权检查，打开窗格 |
| DockPane | 窗格容器，管理视图生命周期 |
| View | UI 界面定义，纯 XAML 声明式 |
| ViewModel | 业务逻辑，数据绑定，命令处理 |

---

## 开发规范

### 命名约定

```csharp
// 类名: PascalCase
public class BatchLayerClipViewModel { }

// 方法名: PascalCase
public async Task ProcessDataAsync() { }

// 变量名: camelCase
private string _selectedLayer;

// 常量: UPPER_CASE 或 PascalCase
public const string CurrentVersion = "1.2.2";

// DockPane ID: XIAOFUTools_{工具名}DockPane
private const string _dockPaneID = "XIAOFUTools_BatchLayerClipDockPane";
```

### 注释规范

```csharp
/// <summary>
/// 计算两点之间的距离
/// </summary>
/// <param name="point1">起始点</param>
/// <param name="point2">结束点</param>
/// <returns>距离值（米）</returns>
public double CalculateDistance(MapPoint point1, MapPoint point2)
{
    // 实现逻辑
}
```

### 异步操作

```csharp
// 所有 ArcGIS Pro API 调用必须在 QueuedTask 中执行
private async Task ProcessDataAsync()
{
    await QueuedTask.Run(() =>
    {
        var map = MapView.Active.Map;
        var layers = map.GetLayersAsFlattenedList();
        // 处理逻辑...
    });
}
```

### 错误处理

```csharp
private async Task SafeOperation()
{
    try
    {
        await QueuedTask.Run(() =>
        {
            // GIS 操作
        });
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"错误: {ex.Message}");
        MessageBox.Show($"操作失败: {ex.Message}", "错误", 
                       MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
```

### 资源管理

```csharp
// 使用 using 语句确保资源释放
using (var geodatabase = new Geodatabase(connectionPath))
{
    // 使用 geodatabase
} // 自动释放
```

---

## 界面规范

### 布局结构

```xml
<UserControl Loaded="UserControl_Loaded">
    <Grid Margin="12">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>  <!-- 参数区域 -->
            <RowDefinition Height="Auto"/>  <!-- 进度条 -->
            <RowDefinition Height="*"/>     <!-- 日志区域 -->
            <RowDefinition Height="Auto"/>  <!-- 按钮区域 -->
        </Grid.RowDefinitions>
    </Grid>
</UserControl>
```

### 样式应用

所有控件使用 `Styles/ControlStyles.xaml` 中定义的样式：

| 控件 | 样式名 |
|------|--------|
| 执行按钮 | ExecuteButtonStyle |
| 取消按钮 | CancelButtonStyle |
| 帮助按钮 | HelpButtonStyle |
| 文本框 | TextBoxStyle |
| 下拉列表 | ComboBoxStyle |
| 复选框 | CheckBoxStyle |
| 日志文本框 | LogTextBoxStyle |
| 进度条 | ProgressBarStyle |

### 按钮区域

```xml
<Border BorderBrush="{StaticResource DividerBrush}" 
        BorderThickness="0,1,0,0" 
        Margin="0,5,0,0" Padding="0,10,0,0">
    <Grid>
        <!-- 帮助按钮（左侧） -->
        <Button Content="?" Width="22" Height="22"
                Style="{StaticResource HelpButtonStyle}"
                Command="{Binding ShowHelpCommand}"
                HorizontalAlignment="Left"/>
        
        <!-- 功能按钮（右侧） -->
        <StackPanel Orientation="Horizontal" HorizontalAlignment="Right">
            <Button Content="停止" Width="80" 
                    Command="{Binding CancelCommand}" 
                    Style="{StaticResource CancelButtonStyle}"
                    Visibility="{Binding IsProcessing, Converter={StaticResource BooleanToVisibilityConverter}}"
                    Margin="0,0,10,0"/>
            <Button Content="运行" Width="80" 
                    Command="{Binding RunCommand}"
                    Style="{StaticResource ExecuteButtonStyle}"
                    IsEnabled="{Binding CanProcess}"/>
        </StackPanel>
    </Grid>
</Border>
```

### 日志区域

```xml
<!-- 进度条 -->
<ProgressBar Style="{StaticResource ProgressBarStyle}"
             Value="{Binding Progress}" Height="6" Margin="0,10,0,2"/>

<!-- 日志窗口 -->
<Border BorderBrush="#CDCDCD" BorderThickness="1" Margin="0,0,0,10">
    <TextBox Style="{StaticResource LogTextBoxStyle}"
             Text="{Binding LogContent, Mode=OneWay}"
             BorderThickness="0"/>
</Border>
```

### 布局规范

| 属性 | 值 |
|------|-----|
| 外边距 | 12px |
| 控件间距 | 10px |
| 标签与控件间距 | 10px |
| 窗口宽度 | 450px |
| 窗口高度（无日志） | 350px |
| 窗口高度（有日志） | 550px |
| 进度条高度 | 6px |

---

## 新工具开发

### 步骤 1: 创建文件结构

在 `Tools/{Category}/` 下创建工具文件夹：

```
Tools/DataProcessing/NewTool/
├── NewToolButton.cs
├── NewToolDockPane.cs
├── NewToolDockPaneView.xaml
├── NewToolDockPaneView.xaml.cs
└── NewToolViewModel.cs
```

### 步骤 2: 实现 Button

```csharp
using ArcGIS.Desktop.Framework.Contracts;
using XIAOFUTools.Tools.Authorization;

namespace XIAOFUTools.Tools.NewTool
{
    internal class NewToolButton : Button
    {
        protected override void OnClick()
        {
            try
            {
                if (!AuthorizationChecker.CheckAuthorizationWithPrompt("新工具"))
                    return;
                NewToolDockPane.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开工具时出错: {ex.Message}", "错误");
            }
        }
    }
}
```

### 步骤 3: 实现 DockPane

```csharp
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Tools.NewTool
{
    internal class NewToolDockPane : DockPane
    {
        private const string _dockPaneID = "XIAOFUTools_NewToolDockPane";

        protected NewToolDockPane() { }

        protected override Control OnCreateContent()
        {
            return new NewToolDockPaneView();
        }

        internal static void Show()
        {
            DockPane pane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);
            pane?.Activate();
        }
    }
}
```

### 步骤 4: 实现 ViewModel

```csharp
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace XIAOFUTools.Tools.NewTool
{
    public class NewToolViewModel : INotifyPropertyChanged
    {
        // 状态属性
        private bool _isProcessing;
        public bool IsProcessing
        {
            get => _isProcessing;
            set { _isProcessing = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanProcess)); }
        }
        public bool CanProcess => !IsProcessing;

        // 进度属性
        private int _progress;
        public int Progress
        {
            get => _progress;
            set { _progress = value; OnPropertyChanged(); }
        }

        // 日志属性
        private string _logContent = string.Empty;
        public string LogContent
        {
            get => _logContent;
            set { _logContent = value; OnPropertyChanged(); }
        }

        // 命令
        public ICommand RunCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ShowHelpCommand { get; }

        public NewToolViewModel()
        {
            RunCommand = new RelayCommand(async () => await RunAsync(), () => CanProcess);
            CancelCommand = new RelayCommand(() => CancelRequested = true);
            ShowHelpCommand = new RelayCommand(ShowHelp);
        }

        private async Task RunAsync()
        {
            IsProcessing = true;
            try
            {
                await QueuedTask.Run(() =>
                {
                    // 业务逻辑
                });
                LogInfo("处理完成");
            }
            catch (Exception ex)
            {
                LogError($"处理失败: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
            }
        }

        public void LogInfo(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            LogContent += $"[{timestamp}] {message}\n";
        }

        public void LogError(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            LogContent += $"[{timestamp}] 错误: {message}\n";
        }

        // INotifyPropertyChanged 实现
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
```

### 步骤 5: 注册到 Config.daml

```xml
<!-- 在 controls 节点添加按钮 -->
<button id="XIAOFUTools_NewToolButton" 
        caption="新工具" 
        className="XIAOFUTools.Tools.NewTool.NewToolButton" 
        loadOnClick="true" 
        smallImage="Images\NewTool_16.png" 
        largeImage="Images\NewTool_32.png">
  <tooltip heading="新工具">
    新工具的功能描述
    <disabledText>无权限使用新工具</disabledText>
  </tooltip>
</button>

<!-- 在 dockPanes 节点添加停靠窗格 -->
<dockPane id="XIAOFUTools_NewToolDockPane" 
          caption="新工具" 
          className="XIAOFUTools.Tools.NewTool.NewToolDockPane" 
          dock="group" 
          dockWith="esri_core_projectDockPane">
</dockPane>

<!-- 在相应的 buttonPalette 中添加按钮引用 -->
<button refID="XIAOFUTools_NewToolButton" />
```

---

## 常用模式

### 进度报告

```csharp
private async Task ProcessWithProgress(List<Layer> layers)
{
    var total = layers.Count;
    for (int i = 0; i < total; i++)
    {
        if (CancelRequested) break;
        
        Progress = (int)((double)(i + 1) / total * 100);
        StatusMessage = $"处理 {i + 1}/{total}";
        
        await ProcessSingleLayer(layers[i]);
    }
}
```

### 图层数据绑定

```csharp
public ObservableCollection<Layer> FeatureLayers { get; } = new();

private void LoadFeatureLayers()
{
    FeatureLayers.Clear();
    var map = MapView.Active?.Map;
    if (map == null) return;
    
    var layers = map.GetLayersAsFlattenedList()
                   .OfType<FeatureLayer>()
                   .Where(l => l.GetFeatureClass() != null);
    
    foreach (var layer in layers)
        FeatureLayers.Add(layer);
}
```

### 批量编辑操作

```csharp
var editOperation = new EditOperation();
editOperation.Name = "批量更新";

foreach (var feature in features)
{
    editOperation.Modify(feature, attributes);
}

await editOperation.ExecuteAsync();
```

---

## 菜单配置

### 常用菜单 ID

| 菜单类型 | 菜单 ID |
|---------|---------|
| 选择要素菜单 | `esri_mapping_selection2DContextMenu` |
| 地图框菜单 | `esri_mapping_mapContextMenu` |
| 地图视图菜单 | `esri_mapping_popupToolContextMenu` |
| 图层菜单 | `esri_mapping_layerContextMenu` |
| 未注册图层菜单 | `esri_mapping_unregisteredLayerContextMenu` |

### 添加菜单项

```xml
<updateMenu refID="esri_mapping_layerContextMenu">
  <insertButton refID="XIAOFUTools_NewToolButton" 
                insert="before" 
                placeWith="esri_editing_table_openTablePaneButton" 
                separator="true"/>
</updateMenu>
```

### 常用参考按钮 ID

| 按钮 | ID |
|------|-----|
| 复制 | `esri_core_editCopyButton` |
| 添加数据 | `esri_mapping_addDataButton` |
| 粘贴 | `esri_core_editPasteButton` |
| 打开属性表 | `esri_editing_table_openTablePaneButton` |

---

## 工具箱结构

```
XIAOFU工具箱 (选项卡)
├── 通用工具 (组)
│   ├── 通用 (按钮面板)
│   ├── 通用2 (按钮面板)
│   ├── 查看面积 (按钮)
│   ├── 添加预设图层 (图库)
│   └── 历史影像 (按钮)
├── 编辑/计算工具 (组)
│   ├── 编辑工具 (按钮面板)
│   ├── 界址 (按钮面板)
│   ├── 动态工具 (按钮面板)
│   └── 计算 (按钮面板)
├── 转换工具 (组)
│   ├── 文本 (按钮面板)
│   ├── 坐标 (按钮面板)
│   ├── 输出 (按钮面板)
│   ├── CAD相关 (按钮面板)
│   └── 文档相关 (按钮面板)
├── 数据分析/处理 (组)
│   ├── 图形检查 (按钮面板)
│   ├── 数据处理 (按钮面板)
│   ├── 图幅 (按钮面板)
│   └── 数据库 (按钮面板)
├── 用户 (组)
│   ├── AI助手 (按钮)
│   ├── 定制 (按钮面板)
│   └── 配置 (按钮面板)
└── 快速访问 (组)
```

---

## 开发检查清单

- [ ] 实现标准 MVVM 架构
- [ ] 使用统一样式和布局
- [ ] 添加错误处理
- [ ] 实现异步操作
- [ ] 支持取消功能
- [ ] 提供日志记录
- [ ] 添加帮助说明
- [ ] 注册到 Config.daml
- [ ] 添加工具图标 (16px/32px)
- [ ] 测试功能完整性
