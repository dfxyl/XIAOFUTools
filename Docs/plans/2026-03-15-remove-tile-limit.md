# Remove Tile Limit Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Remove the hard stop triggered by oversized tile download workloads while keeping all existing warning messages and fast-clip behavior.

**Architecture:** Keep the shared performance advisor as the single decision point for both historical imagery download and internet tile download. Change only the evaluation result so oversized workloads still emit warning text but no longer return a blocking flag.

**Tech Stack:** C#, .NET 8, xUnit

---

### Task 1: Adjust shared performance evaluation behavior

**Files:**
- Modify: `Tools/Common/HistoricalImageryDownload/Services/HistoricalDownloadPerformanceAdvisor.cs`
- Test: `XIAOFUTools.Tests/HistoricalImageryDownload/HistoricalDownloadPerformanceAdvisorTests.cs`

**Step 1: Write the failing test**

Add a test asserting huge workloads still warn and keep messages, but no longer set `ShouldBlock`.

**Step 2: Run test to verify it fails**

Run: `dotnet test XIAOFUTools.Tests/XIAOFUTools.Tests.csproj --filter HistoricalDownloadPerformanceAdvisorTests`

Expected: FAIL because the current implementation still marks huge workloads as blocked.

**Step 3: Write minimal implementation**

Update `HistoricalDownloadPerformanceAdvisor.Evaluate(...)` so blocking thresholds only influence warning/message severity and no longer produce a blocking result.

**Step 4: Run test to verify it passes**

Run: `dotnet test XIAOFUTools.Tests/XIAOFUTools.Tests.csproj --filter HistoricalDownloadPerformanceAdvisorTests`

Expected: PASS.

### Task 2: Verify shared consumers still behave correctly

**Files:**
- Review: `Tools/Common/HistoricalImageryDownload/HistoricalImageryDownloadViewModel.cs`
- Review: `Tools/Common/InternetTileDownload/InternetTileDownloadViewModel.cs`

**Step 1: Confirm no extra code changes are required**

Both consumers already distinguish warning from blocking via `inspection.ShouldWarn` and `inspection.ShouldBlock`.

**Step 2: Run focused verification**

Run: `dotnet test XIAOFUTools.Tests/XIAOFUTools.Tests.csproj --filter HistoricalDownloadPerformanceAdvisorTests`

Expected: PASS and shared behavior stays consistent for both tools.
