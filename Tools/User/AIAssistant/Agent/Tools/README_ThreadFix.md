# ProjectContextTool 线程问题修复说明

## 问题描述

在使用AI助手查询"当前地图有什么"时,出现以下错误:

```
引发的异常:"ArcGIS.Core.CalledOnWrongThreadException"(位于 ArcGIS.Desktop.Mapping.dll 中)
获取工程上下文失败: This method or property must be called on the thread this object was created on.
```

## 问题原因

`ProjectContextTool.GetSimplifiedProjectContext()` 方法在 `GISAgentCore.BuildMessageHistory()` 中被调用,而该方法可能在后台线程执行。

ArcGIS Pro SDK 的许多API(如 `MapView.Active`, `Map.GetLayersAsFlattenedList()`, `FeatureLayer.GetFeatureClass()` 等)必须在**主线程(UI线程)**上调用,否则会抛出 `CalledOnWrongThreadException` 异常。

## 解决方案

使用 `QueuedTask.Run()` 包装所有访问ArcGIS API的代码,确保在ArcGIS的主线程上执行。

### 修改内容

#### 1. 添加必要的命名空间

```csharp
using System.Threading.Tasks;
using ArcGIS.Desktop.Framework.Threading.Tasks;
```

#### 2. 修改 `GetProjectContext()` 方法

**修改前:**
```csharp
public static string GetProjectContext()
{
    try
    {
        var project = Project.Current;
        // ... 直接访问ArcGIS API
    }
    catch (Exception ex)
    {
        // ...
    }
}
```

**修改后:**
```csharp
public static string GetProjectContext()
{
    try
    {
        // 使用QueuedTask.Run确保在ArcGIS主线程上执行
        return QueuedTask.Run(() =>
        {
            var project = Project.Current;
            // ... 访问ArcGIS API
            return context.ToString();
        }).Result;
    }
    catch (Exception ex)
    {
        // ...
    }
}
```

#### 3. 修改 `GetSimplifiedProjectContext()` 方法

采用相同的方式,使用 `QueuedTask.Run()` 包装所有ArcGIS API调用。

## QueuedTask.Run 说明

### 什么是 QueuedTask?

`QueuedTask` 是ArcGIS Pro SDK提供的线程管理机制,用于确保代码在正确的线程上执行。

### 使用场景

- 访问 `MapView.Active`
- 访问 `Map` 对象及其属性
- 访问图层(`Layer`)及其属性
- 访问要素类(`FeatureClass`)
- 访问几何对象(`Geometry`)
- 执行地理处理操作

### 基本用法

```csharp
// 同步方式
var result = QueuedTask.Run(() =>
{
    // 访问ArcGIS API的代码
    return someValue;
}).Result;

// 异步方式
var result = await QueuedTask.Run(() =>
{
    // 访问ArcGIS API的代码
    return someValue;
});
```

### 注意事项

1. **避免死锁**: 在UI线程上使用 `.Result` 可能导致死锁,建议使用 `await`
2. **性能考虑**: `QueuedTask.Run()` 会将任务排队到主线程,可能有轻微延迟
3. **异常处理**: 确保在 `QueuedTask.Run()` 外部捕获异常

## 测试验证

修复后,AI助手应该能够正常响应以下查询:

- "当前地图有什么"
- "显示当前工程信息"
- "列出所有图层"
- "当前选中了什么要素"

## 相关文档

- [ArcGIS Pro SDK - QueuedTask](https://pro.arcgis.com/en/pro-app/latest/sdk/api-reference/topic1.html)
- [ArcGIS Pro SDK - Threading](https://github.com/Esri/arcgis-pro-sdk/wiki/ProConcepts-Advanced-Topics#threading)
