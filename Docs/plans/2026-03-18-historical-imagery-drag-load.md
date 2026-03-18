# Historical Imagery Drag Load Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add drag-to-map loading for historical imagery versions while keeping the existing context-menu load behavior and sharing one load path.

**Architecture:** Extract a small request-building layer from the current ViewModel so validation, layer naming, and URL normalization become testable. Then route both the existing add-to-map command and the new ArcGIS drag source through shared request logic and a dedicated map load service.

**Tech Stack:** C#, .NET 8, WPF, ArcGIS Pro SDK, xUnit

---

### Task 1: Add request-building tests

**Files:**
- Create: `XIAOFUTools.Tests/HistoricalImagery/HistoricalImageryLayerRequestFactoryTests.cs`
- Modify: `XIAOFUTools.Tests/XIAOFUTools.Tests.csproj`
- Create: `Tools/Common/HistoricalImagery/HistoricalImageryLayerRequest.cs`

**Step 1: Write the failing test**

```csharp
[Fact]
public void TryCreate_ReturnsLayerNameAndUri_WhenVersionIsValid()
{
    var version = new HistoricalImageryLayerRequestFactory.WaybackVersionLike(
        "2024-01-01",
        "https://example.com/tile/{z}/{y}/{x}",
        "2024-01-01",
        123);

    var ok = HistoricalImageryLayerRequestFactory.TryCreate(version, out var request, out var error);

    Assert.True(ok);
    Assert.Equal("Wayback 2024-01-01", request!.LayerName);
    Assert.Equal("https://example.com/tile/{z}/{y}/{x}", request.LayerUri.AbsoluteUri);
    Assert.Null(error);
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test "XIAOFUTools.Tests/XIAOFUTools.Tests.csproj" --filter HistoricalImageryLayerRequestFactoryTests`
Expected: FAIL because the request factory does not exist yet.

**Step 3: Write minimal implementation**

```csharp
public sealed record HistoricalImageryLayerRequest(string LayerName, Uri LayerUri);
```

Add `HistoricalImageryLayerRequestFactory.TryCreate(...)` with URL validation and naming fallback.

**Step 4: Run test to verify it passes**

Run: `dotnet test "XIAOFUTools.Tests/XIAOFUTools.Tests.csproj" --filter HistoricalImageryLayerRequestFactoryTests`
Expected: PASS

### Task 2: Extract map loading service

**Files:**
- Create: `Tools/Common/HistoricalImagery/HistoricalImageryMapLoadService.cs`
- Modify: `Tools/Common/HistoricalImagery/HistoricalImageryDockPaneViewModel.cs`

**Step 1: Write the failing test**

Use Task 1 tests to add a case proving invalid URL input is rejected before any map load is attempted.

**Step 2: Run test to verify it fails**

Run: `dotnet test "XIAOFUTools.Tests/XIAOFUTools.Tests.csproj" --filter HistoricalImageryLayerRequestFactoryTests`
Expected: FAIL on invalid-input coverage.

**Step 3: Write minimal implementation**

Implement a service that:
- accepts `HistoricalImageryLayerRequest`
- creates the layer in `QueuedTask.Run`
- moves the new layer near the bottom
- returns a small result object instead of directly showing UI

**Step 4: Run test to verify it passes**

Run: `dotnet test "XIAOFUTools.Tests/XIAOFUTools.Tests.csproj" --filter HistoricalImageryLayerRequestFactoryTests`
Expected: PASS

### Task 3: Add drag source support

**Files:**
- Create: `Tools/Common/HistoricalImagery/HistoricalImageryTreeDragDropHandler.cs`
- Modify: `Tools/Common/HistoricalImagery/HistoricalImageryDockPaneView.xaml`
- Modify: `Tools/Common/HistoricalImagery/HistoricalImageryDockPaneViewModel.cs`

**Step 1: Write the failing test**

Add a request-factory test that verifies fallback naming when `ReleaseDate` is missing, because drag and right-click will both rely on that shared naming behavior.

**Step 2: Run test to verify it fails**

Run: `dotnet test "XIAOFUTools.Tests/XIAOFUTools.Tests.csproj" --filter HistoricalImageryLayerRequestFactoryTests`
Expected: FAIL until fallback naming is implemented.

**Step 3: Write minimal implementation**

Implement `IDragSource` to:
- allow only leaf nodes with valid requests
- construct ArcGIS drag payload data from the shared request
- expose the handler from the ViewModel and bind it in XAML

**Step 4: Run test to verify it passes**

Run: `dotnet test "XIAOFUTools.Tests/XIAOFUTools.Tests.csproj" --filter HistoricalImageryLayerRequestFactoryTests`
Expected: PASS

### Task 4: Verify full integration

**Files:**
- Modify: `Tools/Common/HistoricalImagery/HistoricalImageryDockPaneView.xaml.cs`
- Modify: `Tools/Common/HistoricalImagery/HistoricalImageryDockPaneViewModel.cs`

**Step 1: Run targeted tests**

Run: `dotnet test "XIAOFUTools.Tests/XIAOFUTools.Tests.csproj" --filter HistoricalImageryLayerRequestFactoryTests`
Expected: PASS

**Step 2: Run project build**

Run: `dotnet build "XIAOFUTools.csproj"`
Expected: BUILD SUCCEEDED

**Step 3: Manual verification checklist**

- Right-click a version and choose `添加到地图`.
- Drag a version leaf node onto an ArcGIS map view.
- Confirm the map loads a `Wayback <date>` layer.
- Confirm invalid nodes do not start drag.
