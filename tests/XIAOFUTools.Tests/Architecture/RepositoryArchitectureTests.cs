using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.IO.Compression;

namespace XIAOFUTools.Tests.Architecture;

public sealed class RepositoryArchitectureTests
{
    private static readonly string RepositoryRoot = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".."));

    [Fact]
    public void ConfigDaml_InternalReferencesAndResourcesAreValid()
    {
        var document = XDocument.Load(Path.Combine(RepositoryRoot, "Config.daml"));
        var elements = document.Descendants().ToArray();
        var ids = elements
            .Select(element => (string?)element.Attribute("id"))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);

        var missingReferences = elements
            .Select(element => (string?)element.Attribute("refID"))
            .Where(reference => reference?.StartsWith("XIAOFUTools_", StringComparison.Ordinal) == true)
            .Where(reference => !ids.Contains(reference!))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(missingReferences);

        var localResourceAttributes = elements
            .SelectMany(element => new[]
            {
                (string?)element.Attribute("smallImage"),
                (string?)element.Attribute("largeImage"),
                (string?)element.Attribute("dataTemplateFile")
            })
            .Where(path => !string.IsNullOrWhiteSpace(path) && !path.StartsWith("pack://", StringComparison.OrdinalIgnoreCase));

        foreach (var relativePath in localResourceAttributes)
        {
            Assert.True(
                File.Exists(Path.Combine(RepositoryRoot, relativePath!.Replace('\\', Path.DirectorySeparatorChar))),
                $"Config.daml 引用了不存在的资源：{relativePath}");
        }
    }

    [Fact]
    public void ConfigDaml_ClassNamesFollowCurrentFeatureNamespaces()
    {
        var document = XDocument.Load(Path.Combine(RepositoryRoot, "Config.daml"));
        var classNames = document.Descendants()
            .Select(element => (string?)element.Attribute("className"))
            .Where(name => name?.StartsWith("XIAOFUTools.", StringComparison.Ordinal) == true)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.DoesNotContain(classNames, name => name!.Contains(".Tools.", StringComparison.Ordinal));
        Assert.DoesNotContain(classNames, name => name!.Contains(".Common.", StringComparison.Ordinal));

        var productionSources = Directory.EnumerateFiles(RepositoryRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsGeneratedOrTestPath(path))
            .Select(File.ReadAllText)
            .ToArray();

        foreach (var className in classNames)
        {
            var separator = className!.LastIndexOf('.');
            var namespaceName = className[..separator];
            var typeName = className[(separator + 1)..];
            Assert.Contains(productionSources, source => source.Contains($"namespace {namespaceName}", StringComparison.Ordinal));
            Assert.Contains(productionSources, source => Regex.IsMatch(source, $@"\b(class|record)\s+{Regex.Escape(typeName)}\b"));
        }
    }

    [Fact]
    public void ConfigDaml_ClassNamesResolveFromProductionAssembly()
    {
        var assembly = typeof(XIAOFUTools.Shared.VersionInfo).Assembly;
        using var stream = File.OpenRead(assembly.Location);
        using var peReader = new PEReader(stream);
        var metadata = peReader.GetMetadataReader();
        var productionTypes = metadata.TypeDefinitions
            .Select(handle => metadata.GetTypeDefinition(handle))
            .Select(definition =>
            {
                var namespaceName = metadata.GetString(definition.Namespace);
                var typeName = metadata.GetString(definition.Name);
                return string.IsNullOrWhiteSpace(namespaceName) ? typeName : $"{namespaceName}.{typeName}";
            })
            .ToHashSet(StringComparer.Ordinal);
        var classNames = XDocument.Load(Path.Combine(RepositoryRoot, "Config.daml"))
            .Descendants()
            .Select(element => (string?)element.Attribute("className"))
            .Where(name => name?.StartsWith("XIAOFUTools.", StringComparison.Ordinal) == true)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var unresolved = classNames
            .Where(className => !productionTypes.Contains(className!))
            .ToArray();

        Assert.Empty(unresolved);
    }

    [Fact]
    public void SharedLayer_DoesNotReferenceFeatures()
    {
        var violations = Directory.EnumerateFiles(Path.Combine(RepositoryRoot, "Shared"), "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains("XIAOFUTools.Features", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(RepositoryRoot, path))
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void RelayCommands_AreDefinedOnlyInSharedMvvm()
    {
        var violations = Directory.EnumerateFiles(Path.Combine(RepositoryRoot, "Features"), "*.cs", SearchOption.AllDirectories)
            .Where(path => Regex.IsMatch(File.ReadAllText(path), @"\bclass\s+(RelayCommand|SimpleRelayCommand|SettingsRelayCommand)\b"))
            .Select(path => Path.GetRelativePath(RepositoryRoot, path))
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void ProductionSources_RespectFileSizeBoundaries()
    {
        var oversizedSources = Directory.EnumerateFiles(RepositoryRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsGeneratedOrTestPath(path))
            .Where(path => File.ReadLines(path).Count() > 800)
            .Select(path => Path.GetRelativePath(RepositoryRoot, path))
            .ToArray();
        var oversizedCodeBehind = Directory.EnumerateFiles(Path.Combine(RepositoryRoot, "Features"), "*.xaml.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadLines(path).Count() > 300)
            .Select(path => Path.GetRelativePath(RepositoryRoot, path))
            .ToArray();

        Assert.Empty(oversizedSources);
        Assert.Empty(oversizedCodeBehind);
    }

    [Fact]
    public void ViewModelSources_DoNotExceedFiveHundredLines()
    {
        var oversizedViewModels = Directory
            .EnumerateFiles(Path.Combine(RepositoryRoot, "Features"), "*ViewModel*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadLines(path).Count() > 500)
            .Select(path => Path.GetRelativePath(RepositoryRoot, path))
            .ToArray();

        Assert.Empty(oversizedViewModels);
    }

    [Fact]
    public void Features_DoNotReferenceOtherFeaturePresentationOrInfrastructureTypes()
    {
        var featureRoot = Path.Combine(RepositoryRoot, "Features");
        var featureReferencePattern = new Regex(
            @"XIAOFUTools\.Features\.(?<domain>[A-Za-z0-9_]+)(?<remainder>[A-Za-z0-9_\.]*)",
            RegexOptions.CultureInvariant);
        var violations = new List<string>();

        foreach (var path in Directory.EnumerateFiles(featureRoot, "*.cs", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(featureRoot, path);
            var sourceDomain = relativePath.Split(Path.DirectorySeparatorChar)[0];
            foreach (Match match in featureReferencePattern.Matches(File.ReadAllText(path)))
            {
                var targetDomain = match.Groups["domain"].Value;
                var remainder = match.Groups["remainder"].Value;
                if (string.Equals(sourceDomain, targetDomain, StringComparison.Ordinal) ||
                    !IsForbiddenCrossFeatureReference(remainder))
                {
                    continue;
                }

                violations.Add($"{relativePath} -> {match.Value}");
            }
        }

        Assert.Empty(violations.Distinct(StringComparer.Ordinal));
    }

    [Fact]
    public void DockPaneIdsUsedByProductionCode_AreRegisteredInConfigDaml()
    {
        var registeredIds = XDocument.Load(Path.Combine(RepositoryRoot, "Config.daml"))
            .Descendants()
            .Select(element => (string?)element.Attribute("id"))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);
        var dockPaneIdPattern = new Regex(
            "\\\"(?<id>XIAOFUTools_[A-Za-z0-9_]+DockPane)\\\"",
            RegexOptions.CultureInvariant);
        var referencedIds = Directory.EnumerateFiles(RepositoryRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsGeneratedOrTestPath(path))
            .SelectMany(path => dockPaneIdPattern.Matches(File.ReadAllText(path)).Select(match => match.Groups["id"].Value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.All(referencedIds, id => Assert.Contains(id, registeredIds));
    }

    [Fact]
    public void AddInPackage_ContainsRuntimeAssemblyAndAllPublishedAssets()
    {
        var configuration = AppContext.BaseDirectory.Contains(
            $"{Path.DirectorySeparatorChar}Release{Path.DirectorySeparatorChar}",
            StringComparison.OrdinalIgnoreCase)
            ? "Release"
            : "Debug";
        var packagePath = Path.Combine(
            RepositoryRoot,
            "bin",
            "x64",
            configuration,
            "net10.0-windows",
            "XIAOFUTools.esriAddinX");
        Assert.True(File.Exists(packagePath), $"未找到 Add-in 包：{packagePath}");

        using var archive = ZipFile.OpenRead(packagePath);
        var entries = archive.Entries
            .Select(entry => entry.FullName.Replace('\\', '/'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("Config.daml", entries);
        Assert.Contains("Install/XIAOFUTools.dll", entries);

        var embeddedWpfResources = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Toolbox.png", "反馈表二维码.png", "新增工具收集清单二维码.png"
        };
        var imageFiles = Directory.EnumerateFiles(Path.Combine(RepositoryRoot, "Assets", "Images"), "*", SearchOption.AllDirectories)
            .Where(path => !embeddedWpfResources.Contains(Path.GetFileName(path)))
            .Select(path => "Assets/Images/" + Path.GetRelativePath(Path.Combine(RepositoryRoot, "Assets", "Images"), path).Replace('\\', '/'));
        var dataFiles = Directory.EnumerateFiles(Path.Combine(RepositoryRoot, "Assets", "Data"), "*", SearchOption.AllDirectories)
            .Select(path => "Install/Assets/Data/" + Path.GetRelativePath(Path.Combine(RepositoryRoot, "Assets", "Data"), path).Replace('\\', '/'));
        var aiWebFiles = new[]
        {
            "bridge.js", "chat.html", "interactions.js", "render.js", "script.js", "state.js", "style.css"
        }.Select(file => $"Install/Features/User/AIAssistant/UI/{file}");

        Assert.All(imageFiles.Concat(dataFiles).Concat(aiWebFiles), entry => Assert.Contains(entry, entries));
    }

    [Fact]
    public void ViewModels_DoNotCallDesktopUiApisDirectly()
    {
        var forbiddenApiPattern = new Regex(
            @"(?<!PresentationServices\.Dialogs\.)\bMessageBox\.Show\(|new\s+(OpenItemDialog|OpenFileDialog|SaveFileDialog)|FolderBrowserDialog|\bProcess\.Start\(|System\.Windows\.Clipboard",
            RegexOptions.CultureInvariant);
        var violations = Directory.EnumerateFiles(Path.Combine(RepositoryRoot, "Features"), "*ViewModel*.cs", SearchOption.AllDirectories)
            .Where(path => forbiddenApiPattern.IsMatch(File.ReadAllText(path)))
            .Select(path => Path.GetRelativePath(RepositoryRoot, path))
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void ViewModels_DelegateUiThreadDispatchingToPresentationServices()
    {
        var directDispatcherPattern = new Regex(
            @"(?:System\.Windows\.)?Application\.Current\??\.Dispatcher|(?:System\.Windows\.Threading\.)?Dispatcher\.CurrentDispatcher",
            RegexOptions.CultureInvariant);
        var violations = Directory.EnumerateFiles(Path.Combine(RepositoryRoot, "Features"), "*ViewModel*.cs", SearchOption.AllDirectories)
            .Where(path => directDispatcherPattern.IsMatch(File.ReadAllText(path)))
            .Select(path => Path.GetRelativePath(RepositoryRoot, path))
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void ViewModels_DelegateCustomWindowConstructionToPresentationServices()
    {
        var directWindowConstructionPattern = new Regex(
            @"new\s+[A-Za-z0-9_]*(?:Dialog|Window)\s*\(",
            RegexOptions.CultureInvariant);
        var violations = Directory.EnumerateFiles(Path.Combine(RepositoryRoot, "Features"), "*ViewModel*.cs", SearchOption.AllDirectories)
            .Where(path => directWindowConstructionPattern.IsMatch(File.ReadAllText(path)))
            .Select(path => Path.GetRelativePath(RepositoryRoot, path))
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void ViewModels_DelegateFileSystemAccessToFeatureServices()
    {
        var directFileSystemPattern = new Regex(
            @"(?:System\.IO\.)?(?:File|Directory)\.",
            RegexOptions.CultureInvariant);
        var violations = Directory.EnumerateFiles(Path.Combine(RepositoryRoot, "Features"), "*ViewModel*.cs", SearchOption.AllDirectories)
            .Where(path => directFileSystemPattern.IsMatch(File.ReadAllText(path)))
            .Select(path => Path.GetRelativePath(RepositoryRoot, path))
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void FieldSelectionDialog_IsSharedAndPreservesSelectionActions()
    {
        var featureDialogFiles = Directory.EnumerateFiles(
                Path.Combine(RepositoryRoot, "Features"),
                "FieldSelectionDialog.*",
                SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(RepositoryRoot, path))
            .ToArray();
        var sharedSource = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "Shared",
            "Presentation",
            "Dialogs",
            "FieldSelectionDialog.cs"));
        var migratedViewModels = new[]
        {
            Path.Combine(RepositoryRoot, "Features", "DataManagement", "BoundaryPointGenerator", "BoundaryPointGeneratorDockPaneViewModel.Part.Presentation.cs"),
            Path.Combine(RepositoryRoot, "Features", "Editing", "NodeDistanceCheck", "NodeDistanceCheckDockPaneViewModel.Part.Presentation.cs"),
            Path.Combine(RepositoryRoot, "Features", "Editing", "OverlapCheck", "OverlapCheckDockPaneViewModel.Part.Presentation.cs")
        };

        Assert.Empty(featureDialogFiles);
        Assert.All(migratedViewModels, path => Assert.Contains(
            "SharedFieldSelectionDialog.Select",
            File.ReadAllText(path),
            StringComparison.Ordinal));
        Assert.Contains("已选择 {selectedCount} 个字段，共 {checkBoxes.Count} 个字段", sharedSource, StringComparison.Ordinal);
        Assert.Contains("\"全选\"", sharedSource, StringComparison.Ordinal);
        Assert.Contains("\"全不选\"", sharedSource, StringComparison.Ordinal);
        Assert.Contains("\"反选\"", sharedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void QuickAddData_UsesSharedTextInputDialog()
    {
        var featureDirectory = Path.Combine(RepositoryRoot, "Features", "General", "QuickAddData");
        var viewModelSource = File.ReadAllText(Path.Combine(
            featureDirectory,
            "QuickAddDataViewModel.Part.Helpers.cs"));
        var sharedDialogSource = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "Shared",
            "Presentation",
            "Dialogs",
            "TextInputDialog.cs"));

        Assert.False(File.Exists(Path.Combine(featureDirectory, "QuickDataTextInputDialog.xaml")));
        Assert.False(File.Exists(Path.Combine(featureDirectory, "QuickDataTextInputDialog.xaml.cs")));
        Assert.Contains("XIAOFUTools.Shared.Presentation.Dialogs.TextInputDialog.Prompt", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("请输入名称。", sharedDialogSource, StringComparison.Ordinal);
        Assert.Contains("ControlStyles.xaml", sharedDialogSource, StringComparison.Ordinal);
    }

    [Fact]
    public void BrowseFeatures_ViewModelDelegatesSettingsWindowPresentation()
    {
        var featureDirectory = Path.Combine(RepositoryRoot, "Features", "Analysis", "BrowseFeatures");
        var viewModelSources = Directory.EnumerateFiles(featureDirectory, "*ViewModel*.cs", SearchOption.TopDirectoryOnly)
            .Select(File.ReadAllText)
            .ToArray();
        var serviceSource = File.ReadAllText(Path.Combine(featureDirectory, "BrowseFeaturesSettingsWindowService.cs"));

        Assert.DoesNotContain(viewModelSources, source => Regex.IsMatch(
            source,
            @"new\s+BrowseFeaturesSettingsWindow\s*\(",
            RegexOptions.CultureInvariant));
        Assert.Contains(viewModelSources, source => source.Contains(
            "_settingsWindowService.Show",
            StringComparison.Ordinal));
        Assert.Contains("new BrowseFeaturesSettingsWindow", serviceSource, StringComparison.Ordinal);
    }

    [Fact]
    public void HistoricalImageryDownload_ViewModelDelegatesVersionSelectionPresentation()
    {
        var featureDirectory = Path.Combine(RepositoryRoot, "Features", "General", "HistoricalImageryDownload");
        var viewModelSources = Directory.EnumerateFiles(featureDirectory, "*ViewModel*.cs", SearchOption.TopDirectoryOnly)
            .Select(File.ReadAllText)
            .ToArray();
        var serviceSource = File.ReadAllText(Path.Combine(featureDirectory, "HistoricalVersionSelectionDialogService.cs"));

        Assert.DoesNotContain(viewModelSources, source => Regex.IsMatch(
            source,
            @"new\s+HistoricalVersionSelectionDialog\s*\(",
            RegexOptions.CultureInvariant));
        Assert.Contains(viewModelSources, source => source.Contains(
            "_versionSelectionDialogService.Show",
            StringComparison.Ordinal));
        Assert.Contains("new HistoricalVersionSelectionDialog", serviceSource, StringComparison.Ordinal);
    }

    [Fact]
    public void HistoricalImagery_ViewModelDelegatesMetadataSourcePresentation()
    {
        var featureDirectory = Path.Combine(RepositoryRoot, "Features", "General", "HistoricalImagery");
        var viewModelSources = Directory.EnumerateFiles(featureDirectory, "*ViewModel*.cs", SearchOption.TopDirectoryOnly)
            .Select(File.ReadAllText)
            .ToArray();
        var serviceSource = File.ReadAllText(Path.Combine(featureDirectory, "HistoricalImageryMetadataSourceDialogService.cs"));

        Assert.DoesNotContain(viewModelSources, source => Regex.IsMatch(
            source,
            @"(?:Application\.Current|new\s+HistoricalImageryMetadataSourceDialog\s*\()",
            RegexOptions.CultureInvariant));
        Assert.Contains(viewModelSources, source => source.Contains(
            "_metadataSourceDialogService.SelectAsync",
            StringComparison.Ordinal));
        Assert.Contains("PresentationServices.UiThread.InvokeAsync", serviceSource, StringComparison.Ordinal);
    }

    [Fact]
    public void LandClassTable_ViewModelDelegatesReportProfilePresentation()
    {
        var featureDirectory = Path.Combine(RepositoryRoot, "Features", "Analysis", "LandClassTable");
        var viewModelSources = Directory.EnumerateFiles(featureDirectory, "*ViewModel*.cs", SearchOption.TopDirectoryOnly)
            .Select(File.ReadAllText)
            .ToArray();
        var serviceSource = File.ReadAllText(Path.Combine(featureDirectory, "LandClassReportProfileDialogService.cs"));

        Assert.DoesNotContain(viewModelSources, source => Regex.IsMatch(
            source,
            @"(?:Application\.Current|new\s+LandClassReportProfileWindow\s*\()",
            RegexOptions.CultureInvariant));
        Assert.Contains(viewModelSources, source => source.Contains(
            "_reportProfileDialogService.Show",
            StringComparison.Ordinal));
        Assert.Contains("new LandClassReportProfileWindow", serviceSource, StringComparison.Ordinal);
    }

    [Fact]
    public void FeatureToTxt_ViewModelDelegatesConfigurationPresentation()
    {
        var featureDirectory = Path.Combine(RepositoryRoot, "Features", "Conversion", "FeatureToTxt");
        var viewModelSources = Directory.EnumerateFiles(featureDirectory, "*ViewModel*.cs", SearchOption.TopDirectoryOnly)
            .Select(File.ReadAllText)
            .ToArray();
        var serviceSource = File.ReadAllText(Path.Combine(featureDirectory, "FeatureToTxtDialogService.cs"));

        Assert.DoesNotContain(viewModelSources, source => Regex.IsMatch(
            source,
            @"(?:Application\.Current|new\s+(?:HeaderConfigDialog|FieldConfigDialog)\s*\()",
            RegexOptions.CultureInvariant));
        Assert.Contains(viewModelSources, source => source.Contains(
            "_dialogService.ShowHeaderConfiguration",
            StringComparison.Ordinal));
        Assert.Contains(viewModelSources, source => source.Contains(
            "_dialogService.ShowFieldConfiguration",
            StringComparison.Ordinal));
        Assert.Contains("new HeaderConfigDialog", serviceSource, StringComparison.Ordinal);
        Assert.Contains("new FieldConfigDialog", serviceSource, StringComparison.Ordinal);
    }

    [Fact]
    public void AnalysisResult_ViewModelsDelegateWindowPresentation()
    {
        var intersectDirectory = Path.Combine(RepositoryRoot, "Features", "Analysis", "IntersectSummary");
        var overlayDirectory = Path.Combine(RepositoryRoot, "Features", "Analysis", "MultiOverlaySummary");
        var intersectViewModels = Directory.EnumerateFiles(intersectDirectory, "*ViewModel*.cs", SearchOption.TopDirectoryOnly)
            .Select(File.ReadAllText)
            .ToArray();
        var overlayViewModels = Directory.EnumerateFiles(overlayDirectory, "*ViewModel*.cs", SearchOption.TopDirectoryOnly)
            .Select(File.ReadAllText)
            .ToArray();

        Assert.DoesNotContain(intersectViewModels, source => Regex.IsMatch(source, @"new\s+IntersectSummaryResultWindow\s*\(", RegexOptions.CultureInvariant));
        Assert.DoesNotContain(overlayViewModels, source => Regex.IsMatch(source, @"new\s+(?:MultiOverlaySummaryResultWindow|ExportOptionsDialog)\s*\(", RegexOptions.CultureInvariant));
        Assert.Contains(intersectViewModels, source => source.Contains("_resultWindowService.Show", StringComparison.Ordinal));
        Assert.Contains(overlayViewModels, source => source.Contains("_dialogService.ShowResult", StringComparison.Ordinal));
        Assert.Contains(overlayViewModels, source => source.Contains("_dialogService.SelectExportOptions", StringComparison.Ordinal));
    }

    [Fact]
    public void ViewModels_DelegateArcGisProgressDialogsToPresentationServices()
    {
        var directProgressDialogPattern = new Regex(
            @"new\s+(?:ProgressDialog|CancelableProgressorSource)\s*\(",
            RegexOptions.CultureInvariant);
        var violations = Directory.EnumerateFiles(Path.Combine(RepositoryRoot, "Features"), "*ViewModel*.cs", SearchOption.AllDirectories)
            .Where(path => directProgressDialogPattern.IsMatch(File.ReadAllText(path)))
            .Select(path => Path.GetRelativePath(RepositoryRoot, path))
            .ToArray();
        var serviceSource = File.ReadAllText(Path.Combine(RepositoryRoot, "Shared", "Presentation", "Services.cs"));

        Assert.Empty(violations);
        Assert.Contains("internal interface IProgressDialogService", serviceSource, StringComparison.Ordinal);
        Assert.Contains("class ArcGisProgressDialogService", serviceSource, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionSources_DoNotContainUnimplementedPlaceholders()
    {
        var placeholderPattern = new Regex(
            @"\bTODO\b|throw\s+new\s+NotImplementedException\s*\(",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        var violations = Directory.EnumerateFiles(RepositoryRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsGeneratedOrTestPath(path))
            .Where(path => placeholderPattern.IsMatch(File.ReadAllText(path)))
            .Select(path => Path.GetRelativePath(RepositoryRoot, path))
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void AiAssistant_WebScriptsAreSplitAndRegistered()
    {
        var uiDirectory = Path.Combine(RepositoryRoot, "Features", "User", "AIAssistant", "UI");
        var expectedScripts = new[] { "state.js", "render.js", "bridge.js", "interactions.js", "script.js" };
        var html = File.ReadAllText(Path.Combine(uiDirectory, "chat.html"));

        foreach (var script in expectedScripts)
        {
            Assert.True(File.Exists(Path.Combine(uiDirectory, script)), $"缺少 AI 前端脚本：{script}");
            Assert.Contains($"src=\"{script}", html, StringComparison.Ordinal);
        }

        Assert.True(File.ReadLines(Path.Combine(uiDirectory, "script.js")).Count() <= 300);
    }

    [Fact]
    public void AiAssistantDatabaseManager_IsOnlyARepositoryFacade()
    {
        var databaseDirectory = Path.Combine(
            RepositoryRoot,
            "Features",
            "User",
            "AIAssistant",
            "Database");
        var violations = Directory
            .EnumerateFiles(databaseDirectory, "DatabaseManager*.cs", SearchOption.TopDirectoryOnly)
            .Where(path => File.ReadAllText(path).Contains("CommandText", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(RepositoryRoot, path))
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void FeatureToTxt_CoreAndViewModel_DoNotRetainArcGisRowObjects()
    {
        var featureDirectory = Path.Combine(
            RepositoryRoot,
            "Features",
            "Conversion",
            "FeatureToTxt");
        var coreViolations = Directory
            .EnumerateFiles(Path.Combine(featureDirectory, "Core"), "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains("ArcGIS.", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(RepositoryRoot, path));
        var rowObjectPattern = new Regex(
            @"\b(?:List|IReadOnlyList|IEnumerable)<Feature>|\bFeature\s+[A-Za-z_]",
            RegexOptions.CultureInvariant);
        var viewModelViolations = Directory
            .EnumerateFiles(featureDirectory, "*ViewModel*.cs", SearchOption.TopDirectoryOnly)
            .Where(path => rowObjectPattern.IsMatch(File.ReadAllText(path)))
            .Select(path => Path.GetRelativePath(RepositoryRoot, path));

        Assert.Empty(coreViolations.Concat(viewModelViolations));
    }

    [Fact]
    public void OvertureLoader_CoreAndApplication_KeepInfrastructureDependenciesOut()
    {
        var featureDirectory = Path.Combine(
            RepositoryRoot,
            "Features",
            "DataManagement",
            "OvertureLoader");
        var forbiddenDependencyPattern = new Regex(
            @"ArcGIS\.|System\.Windows|PresentationServices|\b(?:File|Directory)\.(?:Exists|Delete|EnumerateFiles)|OvertureLoader\.(?:Views|Infrastructure|Services)",
            RegexOptions.CultureInvariant);
        var violations = new[] { "Core", "Application" }
            .SelectMany(layer => Directory.EnumerateFiles(
                Path.Combine(featureDirectory, layer),
                "*.cs",
                SearchOption.AllDirectories))
            .Where(path => forbiddenDependencyPattern.IsMatch(File.ReadAllText(path)))
            .Select(path => Path.GetRelativePath(RepositoryRoot, path))
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void OvertureLoader_MapCleanup_IsImplementedOnlyByInfrastructure()
    {
        var featureDirectory = Path.Combine(
            RepositoryRoot,
            "Features",
            "DataManagement",
            "OvertureLoader");
        var forbiddenImplementationPattern = new Regex(
            @"RemoveLayersUsing(?:File|Folder)Async|GetActualFilePath|normalizedFolderPath",
            RegexOptions.CultureInvariant);
        var violations = Directory
            .EnumerateFiles(featureDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains(
                $"{Path.DirectorySeparatorChar}Infrastructure{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase))
            .Where(path => forbiddenImplementationPattern.IsMatch(File.ReadAllText(path)))
            .Select(path => Path.GetRelativePath(RepositoryRoot, path))
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void OvertureDuckDbInitialization_IsOwnedByInfrastructure()
    {
        var featureDirectory = Path.Combine(
            RepositoryRoot,
            "Features",
            "DataManagement",
            "OvertureLoader");
        var dataProcessorSources = Directory
            .EnumerateFiles(
                Path.Combine(featureDirectory, "Services"),
                "DataProcessor*.cs",
                SearchOption.TopDirectoryOnly)
            .Select(path => new
            {
                Path = path,
                Source = File.ReadAllText(path)
            })
            .ToArray();
        var sqlViolations = dataProcessorSources
            .Where(item =>
                item.Source.Contains("INSTALL spatial", StringComparison.Ordinal) ||
                item.Source.Contains("SET extension_directory", StringComparison.Ordinal) ||
                item.Source.Contains("DuckDB版本(1.2.0)", StringComparison.Ordinal))
            .Select(item => Path.GetRelativePath(RepositoryRoot, item.Path))
            .ToArray();
        var initializationSource = dataProcessorSources
            .Single(item => item.Source.Contains(
                "InitializeDuckDBAsync",
                StringComparison.Ordinal))
            .Source;
        var initializationStart = initializationSource.IndexOf(
            "InitializeDuckDBAsync",
            StringComparison.Ordinal);
        var initializationEnd = initializationSource.IndexOf(
            "IngestFileAsync",
            initializationStart,
            StringComparison.Ordinal);
        var initializationMethod = initializationSource[
            initializationStart..initializationEnd];

        Assert.Empty(sqlViolations);
        Assert.DoesNotContain("QueuedTask.Run", initializationMethod, StringComparison.Ordinal);
        Assert.Contains("_duckDbInitializer.InitializeAsync", initializationMethod, StringComparison.Ordinal);
    }

    [Fact]
    public void OvertureDataProcessor_DatabaseOperationsAreOwnedByInfrastructure()
    {
        var servicesDirectory = Path.Combine(
            RepositoryRoot,
            "Features",
            "DataManagement",
            "OvertureLoader",
            "Services");
        var dataProcessorSources = Directory
            .EnumerateFiles(servicesDirectory, "DataProcessor*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => new
            {
                Path = path,
                Source = File.ReadAllText(path)
            })
            .ToArray();
        var databaseOperationPattern = new Regex(
            @"\.CreateCommand\(|Execute(?:Reader|Scalar|NonQuery)Async\(|read_parquet\(|\bCOPY\s*\(",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        var databaseViolations = dataProcessorSources
            .Where(item => databaseOperationPattern.IsMatch(item.Source))
            .Select(item => Path.GetRelativePath(RepositoryRoot, item.Path))
            .ToArray();
        var queuedTaskViolations = dataProcessorSources
            .Where(item =>
                item.Path.EndsWith("Part.Data.cs", StringComparison.OrdinalIgnoreCase) ||
                item.Path.EndsWith("Part.Export.cs", StringComparison.OrdinalIgnoreCase) ||
                item.Path.EndsWith("Part.Helpers.cs", StringComparison.OrdinalIgnoreCase))
            .Where(item => item.Source.Contains("QueuedTask.Run", StringComparison.Ordinal))
            .Select(item => Path.GetRelativePath(RepositoryRoot, item.Path))
            .ToArray();

        Assert.Empty(databaseViolations);
        Assert.Empty(queuedTaskViolations);
    }

    [Fact]
    public void OvertureMfcUtility_IsOnlyACompatibilityFacade()
    {
        var servicesDirectory = Path.Combine(
            RepositoryRoot,
            "Features",
            "DataManagement",
            "OvertureLoader",
            "Services");
        var mfcUtilityFiles = Directory
            .EnumerateFiles(servicesDirectory, "MfcUtility*.cs", SearchOption.TopDirectoryOnly)
            .ToArray();
        var forbiddenImplementationPattern = new Regex(
            @"DuckDBConnection|\.CreateCommand\(|Execute(?:Reader|NonQuery)Async\(|read_parquet\(|QueuedTask\.Run|JsonSerializer\.Serialize",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        var violations = mfcUtilityFiles
            .Where(path => forbiddenImplementationPattern.IsMatch(File.ReadAllText(path)))
            .Select(path => Path.GetRelativePath(RepositoryRoot, path))
            .ToArray();

        Assert.Single(mfcUtilityFiles);
        Assert.Empty(violations);
    }

    [Fact]
    public void OvertureMfcCreation_ViewModelDelegatesFolderInspectionToInfrastructure()
    {
        var directory = Path.Combine(
            RepositoryRoot,
            "Features",
            "DataManagement",
            "OvertureLoader",
            "Views");
        var source = File.ReadAllText(Path.Combine(directory, "WizardDockpaneViewModel.Part.Export.cs"));

        Assert.DoesNotMatch(
            new Regex(@"\b(?:File|Directory)\.(?:Exists|CreateDirectory|GetFiles|GetDirectories|EnumerateFiles|EnumerateDirectories)", RegexOptions.CultureInvariant),
            source);
        Assert.Contains("_mfcDataFolderInspector", source, StringComparison.Ordinal);
        Assert.Contains("EnsureOutputFolderAsync", source, StringComparison.Ordinal);
        Assert.Contains("InspectAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DocumentBatchReplace_ViewModelDoesNotOwnOfficeOrFileSystemOperations()
    {
        var featureDirectory = Path.Combine(
            RepositoryRoot,
            "Features",
            "Conversion",
            "DocumentBatchReplace");
        var viewModelSources = Directory
            .EnumerateFiles(
                featureDirectory,
                "*ViewModel*.cs",
                SearchOption.TopDirectoryOnly)
            .Select(path => new
            {
                Path = path,
                Source = File.ReadAllText(path)
            })
            .ToArray();
        var forbiddenPattern = new Regex(
            @"Microsoft\.Office|\bWord\.|Runtime\.InteropServices|\b(?:File|Directory)\.|new\s+Thread\b|SetApartmentState",
            RegexOptions.CultureInvariant);
        var violations = viewModelSources
            .Where(item => forbiddenPattern.IsMatch(item.Source))
            .Select(item => Path.GetRelativePath(RepositoryRoot, item.Path))
            .ToArray();

        Assert.Empty(violations);
        Assert.Contains(
            viewModelSources,
            item => item.Source.Contains(
                "_replacementService.ReplaceAsync",
                StringComparison.Ordinal));
    }

    [Fact]
    public void PdfConversionViewModels_DoNotOwnOfficeOrFileSystemOperations()
    {
        var directories = new[]
        {
            Path.Combine(RepositoryRoot, "Features", "Conversion", "WordToPdf"),
            Path.Combine(RepositoryRoot, "Features", "Conversion", "ExcelToPdf")
        };
        var sources = directories
            .SelectMany(directory => Directory.EnumerateFiles(
                directory,
                "*ViewModel*.cs",
                SearchOption.TopDirectoryOnly))
            .Select(path => new { Path = path, Source = File.ReadAllText(path) })
            .ToArray();
        var forbiddenPattern = new Regex(
            @"Microsoft\.Office|\b(?:Word|Excel)\.|Runtime\.InteropServices|\b(?:File|Directory)\.|new\s+Thread\b|SetApartmentState",
            RegexOptions.CultureInvariant);
        var violations = sources
            .Where(source => forbiddenPattern.IsMatch(source.Source))
            .Select(source => Path.GetRelativePath(RepositoryRoot, source.Path))
            .ToArray();

        Assert.Empty(violations);
        Assert.Contains(sources, source => source.Source.Contains(
            "_conversionService.ConvertAsync",
            StringComparison.Ordinal));
    }

    [Fact]
    public void DevZoneCheck_ViewModelDoesNotOwnExcelReportInfrastructure()
    {
        var featureDirectory = Path.Combine(
            RepositoryRoot,
            "Features",
            "Custom",
            "DevZoneCheck");
        var sources = Directory.EnumerateFiles(
                featureDirectory,
                "*ViewModel*.cs",
                SearchOption.TopDirectoryOnly)
            .Select(path => new { Path = path, Source = File.ReadAllText(path) })
            .ToArray();
        var forbiddenPattern = new Regex(
            @"Microsoft\.Office|\bExcel\.|Runtime\.InteropServices|\b(?:File|Directory)\.|(?<!Queued)Task\.Run|new\s+Thread\b|SetApartmentState",
            RegexOptions.CultureInvariant);
        var violations = sources
            .Where(source => forbiddenPattern.IsMatch(source.Source))
            .Select(source => Path.GetRelativePath(RepositoryRoot, source.Path))
            .ToArray();

        Assert.Empty(violations);
        Assert.Contains(sources, source => source.Source.Contains(
            "DevZoneExcelReportExporter.ExportAsync",
            StringComparison.Ordinal));
    }

    [Fact]
    public void ExportShpFieldTable_CoreSchemaModels_AreIndependentOfHostApis()
    {
        var coreDirectory = Path.Combine(
            RepositoryRoot,
            "Features",
            "DataManagement",
            "ExportShpFieldTable",
            "Core");
        var violations = Directory.EnumerateFiles(coreDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path => Regex.IsMatch(
                File.ReadAllText(path),
                @"ArcGIS\.|Microsoft\.Office|System\.Windows|PresentationServices",
                RegexOptions.CultureInvariant))
            .Select(path => Path.GetRelativePath(RepositoryRoot, path))
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void ExportShpFieldTable_ViewModelDoesNotOwnExcelInfrastructure()
    {
        var directory = Path.Combine(RepositoryRoot, "Features", "DataManagement", "ExportShpFieldTable");
        var sources = Directory.EnumerateFiles(directory, "*ViewModel*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => new { Path = path, Source = File.ReadAllText(path) })
            .ToArray();
        var forbidden = new Regex(
            @"Type\.GetTypeFromProgID|Marshal\.|new\s+Thread\b|SetApartmentState|\.Workbooks|\.Worksheets|Excel\.",
            RegexOptions.CultureInvariant);
        Assert.Empty(sources.Where(source => forbidden.IsMatch(source.Source))
            .Select(source => Path.GetRelativePath(RepositoryRoot, source.Path)));
        Assert.Contains(sources, source => source.Source.Contains("_excelExporter.ExportAsync", StringComparison.Ordinal));
    }

    [Fact]
    public void ExportDatabaseSchema_CoreModels_AreIndependentOfHostApis()
    {
        var coreDirectory = Path.Combine(
            RepositoryRoot,
            "Features",
            "DataManagement",
            "ExportDatabaseSchema",
            "Core");
        var violations = Directory.EnumerateFiles(coreDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path => Regex.IsMatch(
                File.ReadAllText(path),
                @"ArcGIS\.|Microsoft\.Office|System\.Windows|PresentationServices",
                RegexOptions.CultureInvariant))
            .Select(path => Path.GetRelativePath(RepositoryRoot, path))
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void ExportDatabaseSchema_ViewModelDoesNotOwnExcelInfrastructure()
    {
        var directory = Path.Combine(RepositoryRoot, "Features", "DataManagement", "ExportDatabaseSchema");
        var sources = Directory.EnumerateFiles(directory, "*ViewModel*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => new { Path = path, Source = File.ReadAllText(path) })
            .ToArray();
        var forbidden = new Regex(
            @"Microsoft\.Office|\bExcel\.(?:Application|Workbook|Worksheet|Sheets|Workbooks|Range|Xl)|Marshal\.|(?<!Queued)Task\.Run|new\s+Thread\b|SetApartmentState|\.Workbooks|\.Worksheets",
            RegexOptions.CultureInvariant);
        Assert.Empty(sources.Where(source => forbidden.IsMatch(source.Source))
            .Select(source => Path.GetRelativePath(RepositoryRoot, source.Path)));
        Assert.Contains(sources, source => source.Source.Contains("_excelExporter.ExportAsync", StringComparison.Ordinal));
    }

    [Fact]
    public void MultiOverlaySummary_ViewModelDoesNotOwnExcelInfrastructure()
    {
        var directory = Path.Combine(RepositoryRoot, "Features", "Analysis", "MultiOverlaySummary");
        var sources = Directory.EnumerateFiles(directory, "*ViewModel*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => new { Path = path, Source = File.ReadAllText(path) })
            .ToArray();
        var forbidden = new Regex(
            @"Microsoft\.Office|\bExcel\.(?:Application|Workbook|Worksheet|Workbooks|Sheets|Range|Xl)|Marshal\.|new\s+Thread\b|SetApartmentState|\.Workbooks|\.Worksheets",
            RegexOptions.CultureInvariant);
        Assert.Empty(sources.Where(source => forbidden.IsMatch(source.Source))
            .Select(source => Path.GetRelativePath(RepositoryRoot, source.Path)));
        Assert.Contains(sources, source => source.Source.Contains("_excelExporter.ExportAsync", StringComparison.Ordinal));
    }

    [Fact]
    public void MultiOverlaySummary_ViewModelDelegatesUiThreadDispatchingToPresentationService()
    {
        var directory = Path.Combine(RepositoryRoot, "Features", "Analysis", "MultiOverlaySummary");
        var sources = Directory.EnumerateFiles(directory, "*ViewModel*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => new { Path = path, Source = File.ReadAllText(path) })
            .ToArray();

        Assert.Empty(sources.Where(source => source.Source.Contains(
                "System.Windows.Application.Current",
                StringComparison.Ordinal))
            .Select(source => Path.GetRelativePath(RepositoryRoot, source.Path)));
        Assert.Contains(sources, source => source.Source.Contains(
            "PresentationServices.UiThread.Invoke",
            StringComparison.Ordinal));
    }

    [Fact]
    public void AreaCalculator_ViewModelDelegatesUiThreadDispatchingToPresentationService()
    {
        var directory = Path.Combine(RepositoryRoot, "Features", "Analysis", "AreaCalculator");
        var sources = Directory.EnumerateFiles(directory, "*ViewModel*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => new { Path = path, Source = File.ReadAllText(path) })
            .ToArray();

        Assert.Empty(sources.Where(source => source.Source.Contains(
                "System.Windows.Application.Current",
                StringComparison.Ordinal))
            .Select(source => Path.GetRelativePath(RepositoryRoot, source.Path)));
        Assert.Contains(sources, source => source.Source.Contains(
            "PresentationServices.UiThread.Invoke",
            StringComparison.Ordinal));
    }

    [Fact]
    public void FeatureToTxt_ViewModelDelegatesUiThreadDispatchingToPresentationService()
    {
        var directory = Path.Combine(RepositoryRoot, "Features", "Conversion", "FeatureToTxt");
        var sources = Directory.EnumerateFiles(directory, "*ViewModel*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => new { Path = path, Source = File.ReadAllText(path) })
            .ToArray();

        Assert.Empty(sources.Where(source => source.Source.Contains(
                "Application.Current.Dispatcher",
                StringComparison.Ordinal) ||
                source.Source.Contains(
                    "Application.Current?.Dispatcher",
                    StringComparison.Ordinal))
            .Select(source => Path.GetRelativePath(RepositoryRoot, source.Path)));
        Assert.Contains(sources, source => source.Source.Contains(
            "PresentationServices.UiThread.",
            StringComparison.Ordinal));
    }

    [Fact]
    public void ExportToKml_ViewModelDelegatesUiThreadDispatchingToPresentationService()
    {
        var directory = Path.Combine(RepositoryRoot, "Features", "Conversion", "ExportToKml");
        var sources = Directory.EnumerateFiles(directory, "*ViewModel*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => new { Path = path, Source = File.ReadAllText(path) })
            .ToArray();

        Assert.Empty(sources.Where(source => source.Source.Contains(
                "Application.Current.Dispatcher",
                StringComparison.Ordinal) ||
                source.Source.Contains(
                    "Application.Current?.Dispatcher",
                    StringComparison.Ordinal))
            .Select(source => Path.GetRelativePath(RepositoryRoot, source.Path)));
        Assert.Contains(sources, source => source.Source.Contains(
            "PresentationServices.UiThread.",
            StringComparison.Ordinal));
    }

    [Fact]
    public void TxtToFeature_ViewModelDelegatesTextFileReadingToInfrastructure()
    {
        var directory = Path.Combine(RepositoryRoot, "Features", "Conversion", "TxtToFeature");
        var sources = Directory.EnumerateFiles(directory, "*ViewModel*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => new { Path = path, Source = File.ReadAllText(path) })
            .ToArray();
        var forbidden = new Regex(
            @"\b(?:File|Directory)\.(?:Exists|GetFiles|EnumerateFiles|GetDirectories|EnumerateDirectories|CreateDirectory|Delete|ReadAllBytes|ReadAllLines|SetAttributes)\b|new\s+(?:FileInfo|DirectoryInfo)\s*\(|Thread\.Sleep",
            RegexOptions.CultureInvariant);

        Assert.Empty(sources.Where(source => forbidden.IsMatch(source.Source))
            .Select(source => Path.GetRelativePath(RepositoryRoot, source.Path)));
        Assert.Contains(sources, source => source.Source.Contains(
            "_plotFileReader.ReadAsync",
            StringComparison.Ordinal));
    }

    [Fact]
    public void TxtToFeature_WorkflowDelegatesFileDiscoveryAndDirectoryCreationToInfrastructure()
    {
        var directory = Path.Combine(RepositoryRoot, "Features", "Conversion", "TxtToFeature");
        var workflow = File.ReadAllText(Path.Combine(directory, "TxtToFeatureDockPaneViewModel.Part.Workflow.cs"));
        var forbidden = new Regex(
            @"\bDirectory\.(?:Exists|GetFiles|EnumerateFiles|CreateDirectory)\b|\bFile\.Exists\b|new\s+FileInfo\s*\(",
            RegexOptions.CultureInvariant);

        Assert.DoesNotMatch(forbidden, workflow);
        Assert.Contains("_fileStore.FindTextFilesAsync", workflow, StringComparison.Ordinal);
        Assert.Contains("_fileStore.EnsureDirectoryAsync", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void OnlineImagery_PreparationAndCleanupDelegateFileLifecycleToInfrastructure()
    {
        var directory = Path.Combine(RepositoryRoot, "Features", "General", "DownloadOnlineImagery");
        var sources = Directory.EnumerateFiles(directory, "*ViewModel*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => Path.GetFileName(path)!)
            .Select(name => new { Name = name, Source = File.ReadAllText(Path.Combine(directory, name)) })
            .ToArray();
        var forbidden = new Regex(
            @"\b(?:File|Directory)\.(?:Exists|GetFiles|EnumerateFiles|CreateDirectory|Delete)\b",
            RegexOptions.CultureInvariant);

        Assert.Empty(sources.Where(source => forbidden.IsMatch(source.Source)).Select(source => source.Name));
        Assert.Contains(sources, source => source.Source.Contains(
            "_fileLifecycle",
            StringComparison.Ordinal));
    }

    [Fact]
    public void OnlineImagery_ViewModelDelegatesUiThreadDispatchingToPresentationService()
    {
        var directory = Path.Combine(RepositoryRoot, "Features", "General", "DownloadOnlineImagery");
        var sources = Directory.EnumerateFiles(directory, "*ViewModel*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => new { Path = path, Source = File.ReadAllText(path) })
            .ToArray();

        Assert.Empty(sources.Where(source => source.Source.Contains(
                "Application.Current.Dispatcher",
                StringComparison.Ordinal) ||
                source.Source.Contains(
                    "Application.Current?.Dispatcher",
                    StringComparison.Ordinal) ||
                source.Source.Contains(
                    "Dispatcher.CurrentDispatcher",
                    StringComparison.Ordinal))
            .Select(source => Path.GetRelativePath(RepositoryRoot, source.Path)));
        Assert.Contains(sources, source => source.Source.Contains(
            "PresentationServices.UiThread.",
            StringComparison.Ordinal));
    }

    [Fact]
    public void ImagesToPdf_ViewModelDelegatesFileScanningAndOutputPlanningToInfrastructure()
    {
        var directory = Path.Combine(RepositoryRoot, "Features", "Conversion", "ImagesToPdf");
        var sources = Directory.EnumerateFiles(directory, "*ViewModel*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => new { Path = path, Source = File.ReadAllText(path) })
            .ToArray();
        var forbidden = new Regex(
            @"\b(?:File|Directory)\.(?:Exists|GetFiles|EnumerateFiles|GetDirectories|EnumerateDirectories|CreateDirectory|Delete)\b",
            RegexOptions.CultureInvariant);

        Assert.Empty(sources.Where(source => forbidden.IsMatch(source.Source))
            .Select(source => Path.GetRelativePath(RepositoryRoot, source.Path)));
        Assert.Contains(sources, source => source.Source.Contains(
            "_fileStore.",
            StringComparison.Ordinal));
        Assert.Empty(sources.Where(source => Regex.IsMatch(
                source.Source,
                @"PdfSharp\.|System\.Drawing\.Image\.FromFile",
                RegexOptions.CultureInvariant))
            .Select(source => Path.GetRelativePath(RepositoryRoot, source.Path)));
        Assert.Contains(sources, source => source.Source.Contains(
            "_documentWriter.Write",
            StringComparison.Ordinal));
    }

    [Fact]
    public void PdfToImages_ViewModelDelegatesPdfRenderingAndFileOperationsToInfrastructure()
    {
        var directory = Path.Combine(RepositoryRoot, "Features", "Conversion", "PdfToImages");
        var sources = Directory.EnumerateFiles(directory, "*ViewModel*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => new { Path = path, Source = File.ReadAllText(path) })
            .ToArray();
        var forbidden = new Regex(
            @"\b(?:File|Directory)\.(?:Exists|GetFiles|EnumerateFiles|GetDirectories|EnumerateDirectories|CreateDirectory|Delete|OpenRead|Create)\b|PDFtoImage\.|SkiaSharp\.",
            RegexOptions.CultureInvariant);

        Assert.Empty(sources.Where(source => forbidden.IsMatch(source.Source))
            .Select(source => Path.GetRelativePath(RepositoryRoot, source.Path)));
        Assert.Contains(sources, source => source.Source.Contains(
            "_conversionService.Convert",
            StringComparison.Ordinal));
    }

    [Fact]
    public void DataPivot_ViewModelDelegatesWorkspaceChecksAndUiThreadDispatching()
    {
        var directory = Path.Combine(RepositoryRoot, "Features", "Analysis", "DataPivot");
        var sources = Directory.EnumerateFiles(directory, "*ViewModel*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => new { Path = path, Source = File.ReadAllText(path) })
            .ToArray();
        var directFileSystem = new Regex(
            @"\bDirectory\.Exists\b",
            RegexOptions.CultureInvariant);

        Assert.Empty(sources.Where(source => directFileSystem.IsMatch(source.Source))
            .Select(source => Path.GetRelativePath(RepositoryRoot, source.Path)));
        Assert.Empty(sources.Where(source => source.Source.Contains(
                "Application.Current?.Dispatcher",
                StringComparison.Ordinal) ||
                source.Source.Contains(
                    "Application.Current.Dispatcher",
                    StringComparison.Ordinal))
            .Select(source => Path.GetRelativePath(RepositoryRoot, source.Path)));
        Assert.Contains(sources, source => source.Source.Contains(
            "_workspaceResolver.",
            StringComparison.Ordinal));
        Assert.Contains(sources, source => source.Source.Contains(
            "PresentationServices.UiThread.",
            StringComparison.Ordinal));
    }

    [Fact]
    public void ExportToKml_ViewModelDelegatesArchiveAndFileLifecycleToInfrastructure()
    {
        var directory = Path.Combine(RepositoryRoot, "Features", "Conversion", "ExportToKml");
        var sources = Directory.EnumerateFiles(directory, "*ViewModel*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => new { Path = path, Source = File.ReadAllText(path) })
            .ToArray();
        var forbidden = new Regex(
            @"\b(?:File|Directory)\.(?:Exists|CreateDirectory|Delete)\b|\b(?:ZipFile|FileStream|StreamWriter)\b",
            RegexOptions.CultureInvariant);

        Assert.Empty(sources.Where(source => forbidden.IsMatch(source.Source))
            .Select(source => Path.GetRelativePath(RepositoryRoot, source.Path)));
        Assert.Contains(sources, source => source.Source.Contains(
            "_fileStore.",
            StringComparison.Ordinal));
    }

    [Fact]
    public void FeatureToTxt_ViewModelDelegatesOutputFolderValidationToInfrastructure()
    {
        var directory = Path.Combine(RepositoryRoot, "Features", "Conversion", "FeatureToTxt");
        var sources = Directory.EnumerateFiles(directory, "*ViewModel*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => new { Path = path, Source = File.ReadAllText(path) })
            .ToArray();
        var directDirectoryExists = new Regex(
            @"\bDirectory\.Exists\b",
            RegexOptions.CultureInvariant);

        Assert.Empty(sources.Where(source => directDirectoryExists.IsMatch(source.Source))
            .Select(source => Path.GetRelativePath(RepositoryRoot, source.Path)));
        Assert.Contains(sources, source => source.Source.Contains(
            "_outputFolderResolver.",
            StringComparison.Ordinal));
    }

    [Fact]
    public void SpecialCoordinateTransform_ViewModelDelegatesFileSystemAndUiThreadBoundaries()
    {
        var directory = Path.Combine(RepositoryRoot, "Features", "Conversion", "SpecialCoordinateTransform");
        var sources = Directory.EnumerateFiles(directory, "*ViewModel*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => new { Path = path, Source = File.ReadAllText(path) })
            .ToArray();
        var directFileSystem = new Regex(
            @"\b(?:File|Directory)\.(?:Exists|GetFiles|EnumerateFiles|GetDirectories|EnumerateDirectories|CreateDirectory|Delete)\b",
            RegexOptions.CultureInvariant);

        Assert.Empty(sources.Where(source => directFileSystem.IsMatch(source.Source))
            .Select(source => Path.GetRelativePath(RepositoryRoot, source.Path)));
        Assert.Empty(sources.Where(source => source.Source.Contains(
                "Application.Current?.Dispatcher",
                StringComparison.Ordinal) ||
                source.Source.Contains(
                    "Application.Current.Dispatcher",
                    StringComparison.Ordinal))
            .Select(source => Path.GetRelativePath(RepositoryRoot, source.Path)));
        Assert.Contains(sources, source => source.Source.Contains(
            "_fileStore.",
            StringComparison.Ordinal));
        Assert.Contains(sources, source => source.Source.Contains(
            "PresentationServices.UiThread.",
            StringComparison.Ordinal));
    }

    [Fact]
    public void ExportLayout_ViewModelDelegatesFileDialogsFileSystemAndUiThreadBoundaries()
    {
        var directory = Path.Combine(RepositoryRoot, "Features", "Cartography", "ExportLayout");
        var sources = Directory.EnumerateFiles(directory, "*ViewModel*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => new { Path = path, Source = File.ReadAllText(path) })
            .ToArray();
        var directFileSystem = new Regex(
            @"\bDirectory\.(?:Exists|CreateDirectory)\b|\bOpenFolderDialog\b",
            RegexOptions.CultureInvariant);

        Assert.Empty(sources.Where(source => directFileSystem.IsMatch(source.Source))
            .Select(source => Path.GetRelativePath(RepositoryRoot, source.Path)));
        Assert.Empty(sources.Where(source => source.Source.Contains(
                "Application.Current.Dispatcher",
                StringComparison.Ordinal) ||
                source.Source.Contains(
                    "Application.Current?.Dispatcher",
                    StringComparison.Ordinal))
            .Select(source => Path.GetRelativePath(RepositoryRoot, source.Path)));
        Assert.Contains(sources, source => source.Source.Contains(
            "_fileStore.",
            StringComparison.Ordinal));
        Assert.Contains(sources, source => source.Source.Contains(
            "PresentationServices.Files.SelectFolder",
            StringComparison.Ordinal));
        Assert.Contains(sources, source => source.Source.Contains(
            "PresentationServices.UiThread.",
            StringComparison.Ordinal));
    }

    [Fact]
    public void MapSeriesExport_ViewModelDelegatesFileDialogsFileSystemAndUiWindows()
    {
        var directory = Path.Combine(RepositoryRoot, "Features", "Cartography", "MapSeriesExport");
        var sources = Directory.EnumerateFiles(directory, "*ViewModel*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => new { Path = path, Source = File.ReadAllText(path) })
            .ToArray();
        var forbidden = new Regex(
            @"\bDirectory\.(?:Exists|CreateDirectory)\b|\bOpenFolderDialog\b|\bnew\s+MapSeriesSettingsWindow\b",
            RegexOptions.CultureInvariant);

        Assert.Empty(sources.Where(source => forbidden.IsMatch(source.Source))
            .Select(source => Path.GetRelativePath(RepositoryRoot, source.Path)));
        Assert.Empty(sources.Where(source => source.Source.Contains(
                "Application.Current.Dispatcher",
                StringComparison.Ordinal) ||
                source.Source.Contains(
                    "Application.Current?.Dispatcher",
                    StringComparison.Ordinal) ||
                source.Source.Contains(
                    "Application.Current.MainWindow",
                    StringComparison.Ordinal))
            .Select(source => Path.GetRelativePath(RepositoryRoot, source.Path)));
        Assert.Contains(sources, source => source.Source.Contains(
            "_outputFolderStore.",
            StringComparison.Ordinal));
        Assert.Contains(sources, source => source.Source.Contains(
            "PresentationServices.Files.SelectFolder",
            StringComparison.Ordinal));
        Assert.Contains(sources, source => source.Source.Contains(
            "PresentationServices.UiThread.",
            StringComparison.Ordinal));
        Assert.Contains(sources, source => source.Source.Contains(
            "_settingsWindowService.Show",
            StringComparison.Ordinal));
    }

    [Fact]
    public void PolygonToDxfWithFill_ViewModelDelegatesDxfWritingFileSystemAndUiThreadBoundaries()
    {
        var directory = Path.Combine(RepositoryRoot, "Features", "Conversion", "PolygonToDxfWithFill");
        var sources = Directory.EnumerateFiles(directory, "*ViewModel*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => new { Path = path, Source = File.ReadAllText(path) })
            .ToArray();
        var forbidden = new Regex(
            @"\b(?:File|Directory)\.(?:Exists|CreateDirectory|Delete)\b|\b(?:DxfDocument|HatchBoundaryPath|Polyline2D|AciColor)\b|netDxf\.|Application\.Current(?:\?\.Dispatcher|\.Dispatcher)",
            RegexOptions.CultureInvariant);

        Assert.Empty(sources.Where(source => forbidden.IsMatch(source.Source))
            .Select(source => Path.GetRelativePath(RepositoryRoot, source.Path)));
        Assert.Contains(sources, source => source.Source.Contains(
            "_documentWriter.WriteAsync",
            StringComparison.Ordinal));
        Assert.Contains(sources, source => source.Source.Contains(
            "_polygonReader.ReadAsync",
            StringComparison.Ordinal));
        Assert.Contains(sources, source => source.Source.Contains(
            "PresentationServices.UiThread.",
            StringComparison.Ordinal));
    }

    [Fact]
    public void PolygonToDwgWithFill_ViewModelDelegatesDwgWritingFileSystemAndUiThreadBoundaries()
    {
        var directory = Path.Combine(RepositoryRoot, "Features", "Conversion", "PolygonToDwgWithFill");
        var sources = Directory.EnumerateFiles(directory, "*ViewModel*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => new { Path = path, Source = File.ReadAllText(path) })
            .ToArray();
        var forbidden = new Regex(
            @"\b(?:File|Directory)\.(?:Exists|CreateDirectory|Delete)\b|\b(?:CadDocument|DwgWriter)\b|ACadSharp\.|using\s+ACadSharp|Application\.Current(?:\?\.Dispatcher|\.Dispatcher)",
            RegexOptions.CultureInvariant);

        Assert.Empty(sources.Where(source => forbidden.IsMatch(source.Source))
            .Select(source => Path.GetRelativePath(RepositoryRoot, source.Path)));
        Assert.Contains(sources, source => source.Source.Contains(
            "_documentWriter.WriteAsync",
            StringComparison.Ordinal));
        Assert.Contains(sources, source => source.Source.Contains(
            "_polygonReader.ReadAsync",
            StringComparison.Ordinal));
        Assert.Contains(sources, source => source.Source.Contains(
            "PresentationServices.UiThread.",
            StringComparison.Ordinal));
    }

    [Fact]
    public void BatchMergeShp_ViewModelDelegatesFileSystemAndUiThreadBoundaries()
    {
        var directory = Path.Combine(RepositoryRoot, "Features", "DataManagement", "BatchMergeShp");
        var sources = Directory.EnumerateFiles(directory, "*ViewModel*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => new { Path = path, Source = File.ReadAllText(path) })
            .ToArray();
        var forbidden = new Regex(
            @"\bDirectory\.(?:Exists|CreateDirectory|EnumerateFiles|EnumerateDirectories)\b|Application\.Current(?:\?\.Dispatcher|\.Dispatcher)",
            RegexOptions.CultureInvariant);

        Assert.Empty(sources.Where(source => forbidden.IsMatch(source.Source))
            .Select(source => Path.GetRelativePath(RepositoryRoot, source.Path)));
        Assert.Contains(sources, source => source.Source.Contains(
            "_fileStore.",
            StringComparison.Ordinal));
        Assert.Contains(sources, source => source.Source.Contains(
            "PresentationServices.UiThread.",
            StringComparison.Ordinal));
    }

    [Fact]
    public void ExportShpFieldTable_ViewModelDelegatesFileSystemAndUiThreadBoundaries()
    {
        var directory = Path.Combine(RepositoryRoot, "Features", "DataManagement", "ExportShpFieldTable");
        var sources = Directory.EnumerateFiles(directory, "*ViewModel*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => new { Path = path, Source = File.ReadAllText(path) })
            .ToArray();
        var forbidden = new Regex(
            @"\bDirectory\.(?:Exists|CreateDirectory)\b|\bDirectory\.GetFiles\b|Application\.Current(?:\?\.Dispatcher|\.Dispatcher)",
            RegexOptions.CultureInvariant);

        Assert.Empty(sources.Where(source => forbidden.IsMatch(source.Source))
            .Select(source => Path.GetRelativePath(RepositoryRoot, source.Path)));
        Assert.Contains(sources, source => source.Source.Contains(
            "_fileStore.",
            StringComparison.Ordinal));
        Assert.Contains(sources, source => source.Source.Contains(
            "PresentationServices.UiThread.",
            StringComparison.Ordinal));
    }

    [Fact]
    public void BatchLayerClip_ViewModelDelegatesFileSystemAndUiThreadBoundaries()
    {
        var directory = Path.Combine(RepositoryRoot, "Features", "DataManagement", "BatchLayerClip");
        var sources = Directory.EnumerateFiles(directory, "*ViewModel*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => new { Path = path, Source = File.ReadAllText(path) })
            .ToArray();
        var forbidden = new Regex(
            @"\bDirectory\.(?:Exists|CreateDirectory)\b|Application\.Current(?:\?\.Dispatcher|\.Dispatcher)|System\.Windows\.Application",
            RegexOptions.CultureInvariant);

        Assert.Empty(sources.Where(source => forbidden.IsMatch(source.Source))
            .Select(source => Path.GetRelativePath(RepositoryRoot, source.Path)));
        Assert.Contains(sources, source => source.Source.Contains(
            "_fileStore.",
            StringComparison.Ordinal));
        Assert.Contains(sources, source => source.Source.Contains(
            "PresentationServices.UiThread.",
            StringComparison.Ordinal));
    }

    [Fact]
    public void MdbBatchToGdb_ViewModelDelegatesFileSystemAndUiThreadBoundaries()
    {
        var directory = Path.Combine(RepositoryRoot, "Features", "DataManagement", "MdbBatchToGdb");
        var sources = Directory.EnumerateFiles(directory, "*ViewModel*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => new { Path = path, Source = File.ReadAllText(path) })
            .ToArray();
        var forbidden = new Regex(
            @"\b(?:File|Directory)\.(?:Exists|CreateDirectory|Delete|EnumerateFiles|EnumerateDirectories)\b|Application\.Current(?:\?\.Dispatcher|\.Dispatcher)|System\.Windows\.Application",
            RegexOptions.CultureInvariant);

        Assert.Empty(sources.Where(source => forbidden.IsMatch(source.Source))
            .Select(source => Path.GetRelativePath(RepositoryRoot, source.Path)));
        Assert.Contains(sources, source => source.Source.Contains(
            "_fileStore.",
            StringComparison.Ordinal));
        Assert.Contains(sources, source => source.Source.Contains(
            "PresentationServices.UiThread.",
            StringComparison.Ordinal));
    }

    [Fact]
    public void RangeClipTool_ViewModelDelegatesFileSystemAndPresentationBoundaries()
    {
        var directory = Path.Combine(RepositoryRoot, "Features", "DataManagement", "RangeClipTool");
        var sources = Directory.EnumerateFiles(directory, "*ViewModel*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => new { Path = path, Source = File.ReadAllText(path) })
            .ToArray();
        var forbidden = new Regex(
            @"\b(?:File|Directory)\.(?:Exists|CreateDirectory|Delete|EnumerateFiles|EnumerateDirectories)\b|Application\.Current(?:\?\.Dispatcher|\.Dispatcher)|System\.Windows\.Application",
            RegexOptions.CultureInvariant);

        Assert.Empty(sources.Where(source => forbidden.IsMatch(source.Source))
            .Select(source => Path.GetRelativePath(RepositoryRoot, source.Path)));
        Assert.Contains(sources, source => source.Source.Contains(
            "_outputFolderStore.",
            StringComparison.Ordinal));
        Assert.Contains(sources, source => source.Source.Contains(
            "PresentationServices.UiThread.",
            StringComparison.Ordinal));
        Assert.Contains(sources, source => source.Source.Contains(
            "PresentationServices.Windows.",
            StringComparison.Ordinal));
    }

    [Fact]
    public void DatabaseBuilder_ViewModelDelegatesFileSystemAndUiThreadBoundaries()
    {
        var directory = Path.Combine(RepositoryRoot, "Features", "DataManagement", "DatabaseBuilder");
        var sources = Directory.EnumerateFiles(directory, "*ViewModel*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => new { Path = path, Source = File.ReadAllText(path) })
            .ToArray();
        var forbidden = new Regex(
            @"\b(?:File|Directory)\.(?:Exists|Copy|CreateDirectory|Delete|EnumerateFiles|EnumerateDirectories)\b|Application\.Current(?:\?\.Dispatcher|\.Dispatcher)|System\.Windows\.Application",
            RegexOptions.CultureInvariant);

        Assert.Empty(sources.Where(source => forbidden.IsMatch(source.Source))
            .Select(source => Path.GetRelativePath(RepositoryRoot, source.Path)));
        Assert.Contains(sources, source => source.Source.Contains(
            "_fileStore.",
            StringComparison.Ordinal));
        Assert.Contains(sources, source => source.Source.Contains(
            "PresentationServices.UiThread.",
            StringComparison.Ordinal));
    }

    [Fact]
    public void ExportDatabaseSchema_ViewModelDelegatesFileSystemAndUiThreadBoundaries()
    {
        var directory = Path.Combine(RepositoryRoot, "Features", "DataManagement", "ExportDatabaseSchema");
        var sources = Directory.EnumerateFiles(directory, "*ViewModel*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => new { Path = path, Source = File.ReadAllText(path) })
            .ToArray();
        var forbidden = new Regex(
            @"\b(?:File|Directory)\.(?:Exists|Copy|CreateDirectory|Delete|EnumerateFiles|EnumerateDirectories)\b|Application\.Current(?:\?\.Dispatcher|\.Dispatcher)|System\.Windows\.Application",
            RegexOptions.CultureInvariant);

        Assert.Empty(sources.Where(source => forbidden.IsMatch(source.Source))
            .Select(source => Path.GetRelativePath(RepositoryRoot, source.Path)));
        Assert.Contains(sources, source => source.Source.Contains(
            "_pathStore.",
            StringComparison.Ordinal));
        Assert.Contains(sources, source => source.Source.Contains(
            "PresentationServices.UiThread.",
            StringComparison.Ordinal));
    }

    [Fact]
    public void MirrorDatabase_ViewModelDelegatesFileSystemAndUiThreadBoundaries()
    {
        var directory = Path.Combine(RepositoryRoot, "Features", "DataManagement", "MirrorDatabase");
        var sources = Directory.EnumerateFiles(directory, "*ViewModel*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => new { Path = path, Source = File.ReadAllText(path) })
            .ToArray();
        var forbidden = new Regex(
            @"\b(?:File|Directory)\.(?:Exists|Copy|CreateDirectory|Delete|EnumerateFiles|EnumerateDirectories)\b|Application\.Current(?:\?\.Dispatcher|\.Dispatcher)|System\.Windows\.Application",
            RegexOptions.CultureInvariant);

        Assert.Empty(sources.Where(source => forbidden.IsMatch(source.Source))
            .Select(source => Path.GetRelativePath(RepositoryRoot, source.Path)));
        Assert.Contains(sources, source => source.Source.Contains(
            "_pathStore.",
            StringComparison.Ordinal));
        Assert.Contains(sources, source => source.Source.Contains(
            "PresentationServices.UiThread.",
            StringComparison.Ordinal));
    }

    [Fact]
    public void GapCheck_ViewModelDelegatesTemporaryFileSystemAndUiThreadBoundaries()
    {
        var directory = Path.Combine(RepositoryRoot, "Features", "Editing", "GapCheck");
        var sources = Directory.EnumerateFiles(directory, "*ViewModel*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => new { Path = path, Source = File.ReadAllText(path) })
            .ToArray();
        var forbidden = new Regex(
            @"\b(?:File|Directory)\.(?:Exists|Copy|CreateDirectory|Delete|GetFiles|EnumerateFiles|EnumerateDirectories|SetAttributes)\b|Application\.Current(?:\?\.Dispatcher|\.Dispatcher)|System\.Windows\.Application",
            RegexOptions.CultureInvariant);

        Assert.Empty(sources.Where(source => forbidden.IsMatch(source.Source))
            .Select(source => Path.GetRelativePath(RepositoryRoot, source.Path)));
        Assert.Contains(sources, source => source.Source.Contains(
            "_temporaryWorkspaceStore.",
            StringComparison.Ordinal));
        Assert.Contains(sources, source => source.Source.Contains(
            "PresentationServices.UiThread.",
            StringComparison.Ordinal));
    }

    [Fact]
    public void ProtocolLineExtract_ViewModelDoesNotReferenceOtherFeaturePresentation()
    {
        var directory = Path.Combine(RepositoryRoot, "Features", "DataManagement", "ProtocolLineExtract");
        var sources = Directory.EnumerateFiles(directory, "*ViewModel*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => new { Path = path, Source = File.ReadAllText(path) })
            .ToArray();
        var forbidden = new Regex(
            @"BoundaryPointGenerator|System\.Windows\.Application|new\s+SaveItemDialog|new\s+FieldSelectionDialog|\bDirectory\.(?:Exists|CreateDirectory|Delete)\b",
            RegexOptions.CultureInvariant);

        Assert.Empty(sources.Where(source => forbidden.IsMatch(source.Source))
            .Select(source => Path.GetRelativePath(RepositoryRoot, source.Path)));
        Assert.Contains(sources, source => source.Source.Contains(
            "Shared.Presentation.Dialogs.FieldSelectionDialog.Select",
            StringComparison.Ordinal));
        Assert.Contains(sources, source => source.Source.Contains(
            "PathDialogUtils.PickSaveFeatureClassPath",
            StringComparison.Ordinal));
        Assert.Contains(sources, source => source.Source.Contains(
            "_temporaryWorkspaceStore.",
            StringComparison.Ordinal));
    }

    [Fact]
    public void Settings_ViewModelDelegatesPresetLayerFileSystemOperations()
    {
        var directory = Path.Combine(RepositoryRoot, "Features", "User", "Settings");
        var sources = Directory.EnumerateFiles(directory, "*ViewModel*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => new { Path = path, Source = File.ReadAllText(path) })
            .ToArray();
        var forbidden = new Regex(
            @"\b(?:File|Directory)\.(?:Exists|Copy|CreateDirectory|Delete|GetFiles|EnumerateFiles|EnumerateDirectories|SetAttributes)\b",
            RegexOptions.CultureInvariant);

        Assert.Empty(sources.Where(source => forbidden.IsMatch(source.Source))
            .Select(source => Path.GetRelativePath(RepositoryRoot, source.Path)));
        Assert.Contains(sources, source => source.Source.Contains(
            "_presetLayerFileStore.",
            StringComparison.Ordinal));
    }

    [Fact]
    public void ShapefileBuilder_ViewModelDelegatesFileSystemAndUiThreadBoundaries()
    {
        var directory = Path.Combine(RepositoryRoot, "Features", "DataManagement", "ShapefileBuilder");
        var sources = Directory.EnumerateFiles(directory, "*ViewModel*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => new { Path = path, Source = File.ReadAllText(path) })
            .ToArray();
        var forbidden = new Regex(
            @"\b(?:File|Directory)\.(?:Exists|Copy|CreateDirectory|Delete|GetFiles|EnumerateFiles|EnumerateDirectories|SetAttributes)\b|Application\.Current(?:\?\.Dispatcher|\.Dispatcher)|System\.Windows\.Application",
            RegexOptions.CultureInvariant);

        Assert.Empty(sources.Where(source => forbidden.IsMatch(source.Source))
            .Select(source => Path.GetRelativePath(RepositoryRoot, source.Path)));
        Assert.Contains(sources, source => source.Source.Contains(
            "_fileStore.",
            StringComparison.Ordinal));
        Assert.Contains(sources, source => source.Source.Contains(
            "PresentationServices.UiThread.",
            StringComparison.Ordinal));
    }

    [Fact]
    public void BatchAddData_ViewModelDelegatesSearchPathAndUiThreadBoundaries()
    {
        var directory = Path.Combine(RepositoryRoot, "Features", "DataManagement", "BatchAddData");
        var sources = Directory.EnumerateFiles(directory, "*ViewModel*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => new { Path = path, Source = File.ReadAllText(path) })
            .ToArray();
        var forbidden = new Regex(
            @"\b(?:File|Directory)\.(?:Exists|Copy|CreateDirectory|Delete|GetFiles|EnumerateFiles|EnumerateDirectories|SetAttributes)\b|Application\.Current(?:\?\.Dispatcher|\.Dispatcher)|System\.Windows\.Application",
            RegexOptions.CultureInvariant);

        Assert.Empty(sources.Where(source => forbidden.IsMatch(source.Source))
            .Select(source => Path.GetRelativePath(RepositoryRoot, source.Path)));
        Assert.Contains(sources, source => source.Source.Contains(
            "_searchPathValidator.",
            StringComparison.Ordinal));
        Assert.Contains(sources, source => source.Source.Contains(
            "PresentationServices.UiThread.",
            StringComparison.Ordinal));
    }

    [Fact]
    public void AiAssistant_ViewModelDelegatesUiResourceFileSystemAccess()
    {
        var directory = Path.Combine(RepositoryRoot, "Features", "User", "AIAssistant");
        var sources = Directory.EnumerateFiles(directory, "*ViewModel*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => new { Path = path, Source = File.ReadAllText(path) })
            .ToArray();
        var forbidden = new Regex(
            @"\b(?:File|Directory)\.(?:Exists|Copy|CreateDirectory|Delete|GetFiles|EnumerateFiles|EnumerateDirectories|SetAttributes)\b",
            RegexOptions.CultureInvariant);

        Assert.Empty(sources.Where(source => forbidden.IsMatch(source.Source))
            .Select(source => Path.GetRelativePath(RepositoryRoot, source.Path)));
        Assert.Contains(sources, source => source.Source.Contains(
            "_uiPathResolver.ResolveChatHtmlPath",
            StringComparison.Ordinal));
    }

    [Fact]
    public void LandClassTable_ViewModelDoesNotOwnExcelInfrastructure()
    {
        var directory = Path.Combine(RepositoryRoot, "Features", "Analysis", "LandClassTable");
        var sources = Directory.EnumerateFiles(directory, "*ViewModel*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => new { Path = path, Source = File.ReadAllText(path) })
            .ToArray();
        var forbidden = new Regex(
            @"Microsoft\.Office|\bExcel\.(?:Application|Workbook|Worksheet|Workbooks|Sheets|Range|Xl)|Marshal\.|new\s+Thread\b|SetApartmentState|\.Workbooks|\.Worksheets|Runtime\.InteropServices",
            RegexOptions.CultureInvariant);
        Assert.Empty(sources.Where(source => forbidden.IsMatch(source.Source))
            .Select(source => Path.GetRelativePath(RepositoryRoot, source.Path)));
        Assert.Contains(sources, source => source.Source.Contains(
            "LandClassExcelExporter.ExportResultsAsync",
            StringComparison.Ordinal));
    }

    [Fact]
    public void LandClassTable_ViewModelDelegatesUiThreadDispatchingToPresentationService()
    {
        var directory = Path.Combine(RepositoryRoot, "Features", "Analysis", "LandClassTable");
        var sources = Directory.EnumerateFiles(directory, "*ViewModel*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => new { Path = path, Source = File.ReadAllText(path) })
            .ToArray();

        Assert.Empty(sources.Where(source => source.Source.Contains(
                "Application.Current?.Dispatcher",
                StringComparison.Ordinal))
            .Select(source => Path.GetRelativePath(RepositoryRoot, source.Path)));
        Assert.Contains(sources, source => source.Source.Contains(
            "PresentationServices.UiThread.",
            StringComparison.Ordinal));
    }

    [Fact]
    public void LandClassTable_ViewModelDoesNotOwnOutputFileSystemOperations()
    {
        var directory = Path.Combine(RepositoryRoot, "Features", "Analysis", "LandClassTable");
        var sources = Directory.EnumerateFiles(directory, "*ViewModel*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => new { Path = path, Source = File.ReadAllText(path) })
            .ToArray();
        var forbidden = new Regex(
            @"\b(?:File|Directory)\.(?:Exists|Copy|CreateDirectory|Delete|GetFiles|EnumerateFiles|EnumerateDirectories|SetAttributes)\b",
            RegexOptions.CultureInvariant);

        Assert.Empty(sources.Where(source => forbidden.IsMatch(source.Source))
            .Select(source => Path.GetRelativePath(RepositoryRoot, source.Path)));
    }

    [Fact]
    public void IntersectSummary_ViewModelDoesNotOwnExcelInfrastructure()
    {
        var directory = Path.Combine(RepositoryRoot, "Features", "Analysis", "IntersectSummary");
        var sources = Directory.EnumerateFiles(directory, "*ViewModel*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => new { Path = path, Source = File.ReadAllText(path) })
            .ToArray();
        var forbidden = new Regex(
            @"Microsoft\.Office|\bExcel\.(?:Application|Workbook|Worksheet|Workbooks|Sheets|Range|Xl)|Marshal\.|new\s+Thread\b|SetApartmentState|\.Workbooks|\.Worksheets|Runtime\.InteropServices",
            RegexOptions.CultureInvariant);
        Assert.Empty(sources.Where(source => forbidden.IsMatch(source.Source))
            .Select(source => Path.GetRelativePath(RepositoryRoot, source.Path)));
        Assert.Contains(sources, source => source.Source.Contains(
            "IntersectSummaryExcelExporter.Export",
            StringComparison.Ordinal));
    }

    [Fact]
    public void IntersectSummary_ViewModelDelegatesUiThreadDispatchingToPresentationService()
    {
        var directory = Path.Combine(RepositoryRoot, "Features", "Analysis", "IntersectSummary");
        var sources = Directory.EnumerateFiles(directory, "*ViewModel*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => new { Path = path, Source = File.ReadAllText(path) })
            .ToArray();

        Assert.Empty(sources.Where(source => source.Source.Contains(
                "System.Windows.Application.Current",
                StringComparison.Ordinal))
            .Select(source => Path.GetRelativePath(RepositoryRoot, source.Path)));
        Assert.Contains(sources, source => source.Source.Contains(
            "PresentationServices.UiThread.Invoke",
            StringComparison.Ordinal));
    }

    [Fact]
    public void ViewArea_ViewModelDelegatesClipboardRetryToPresentationService()
    {
        var directory = Path.Combine(RepositoryRoot, "Features", "Analysis", "ViewArea");
        var sources = Directory.EnumerateFiles(directory, "*ViewModel*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => new { Path = path, Source = File.ReadAllText(path) })
            .ToArray();
        var forbidden = new Regex(
            @"System\.Windows\.Clipboard|PresentationServices\.Clipboard\.(?:Clear|SetText)|new\s+Thread\b|SetApartmentState|Thread\.Sleep",
            RegexOptions.CultureInvariant);
        Assert.Empty(sources.Where(source => forbidden.IsMatch(source.Source))
            .Select(source => Path.GetRelativePath(RepositoryRoot, source.Path)));
        Assert.Contains(sources, source => source.Source.Contains(
            "PresentationServices.Clipboard.TrySetTextAsync",
            StringComparison.Ordinal));
    }

    [Fact]
    public void ViewArea_ViewModelDelegatesUiThreadDispatchingToPresentationService()
    {
        var directory = Path.Combine(RepositoryRoot, "Features", "Analysis", "ViewArea");
        var sources = Directory.EnumerateFiles(directory, "*ViewModel*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => new { Path = path, Source = File.ReadAllText(path) })
            .ToArray();

        Assert.Empty(sources.Where(source => source.Source.Contains(
                "System.Windows.Application.Current",
                StringComparison.Ordinal))
            .Select(source => Path.GetRelativePath(RepositoryRoot, source.Path)));
        Assert.Contains(sources, source => source.Source.Contains(
            "PresentationServices.UiThread.InvokeOrRun",
            StringComparison.Ordinal));
    }

    [Fact]
    public void ChineseNumbering_ViewModelDelegatesUiThreadDispatchingToPresentationService()
    {
        var directory = Path.Combine(RepositoryRoot, "Features", "Editing", "ChineseNumbering");
        var sources = Directory.EnumerateFiles(directory, "*ViewModel*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => new { Path = path, Source = File.ReadAllText(path) })
            .ToArray();

        Assert.Empty(sources.Where(source => source.Source.Contains(
                "System.Windows.Application.Current",
                StringComparison.Ordinal))
            .Select(source => Path.GetRelativePath(RepositoryRoot, source.Path)));
        Assert.Contains(sources, source => source.Source.Contains(
            "PresentationServices.UiThread.InvokeOrRun",
            StringComparison.Ordinal));
    }

    [Fact]
    public void GroupNumbering_ViewModelDelegatesUiThreadDispatchingToPresentationService()
    {
        var directory = Path.Combine(RepositoryRoot, "Features", "Editing", "GroupNumbering");
        var sources = Directory.EnumerateFiles(directory, "*ViewModel*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => new { Path = path, Source = File.ReadAllText(path) })
            .ToArray();

        Assert.Empty(sources.Where(source => source.Source.Contains(
                "System.Windows.Application.Current",
                StringComparison.Ordinal))
            .Select(source => Path.GetRelativePath(RepositoryRoot, source.Path)));
        Assert.Contains(sources, source => source.Source.Contains(
            "PresentationServices.UiThread.InvokeOrRun",
            StringComparison.Ordinal));
    }

    [Fact]
    public void AttributeTransferFields_ViewModelDelegatesUiThreadDispatchingToPresentationService()
    {
        var directory = Path.Combine(RepositoryRoot, "Features", "DataManagement", "AttributeTransferFields");
        var sources = Directory.EnumerateFiles(directory, "*ViewModel*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => new { Path = path, Source = File.ReadAllText(path) })
            .ToArray();

        Assert.Empty(sources.Where(source => source.Source.Contains(
                "System.Windows.Application.Current",
                StringComparison.Ordinal))
            .Select(source => Path.GetRelativePath(RepositoryRoot, source.Path)));
        Assert.Contains(sources, source => source.Source.Contains(
            "PresentationServices.UiThread.InvokeOrRun",
            StringComparison.Ordinal));
    }

    [Fact]
    public void DevZoneCheck_ViewModelDelegatesUiThreadDispatchingToPresentationService()
    {
        var directory = Path.Combine(RepositoryRoot, "Features", "Custom", "DevZoneCheck");
        var sources = Directory.EnumerateFiles(directory, "*ViewModel*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => new { Path = path, Source = File.ReadAllText(path) })
            .ToArray();

        Assert.Empty(sources.Where(source => source.Source.Contains(
                "System.Windows.Application.Current",
                StringComparison.Ordinal))
            .Select(source => Path.GetRelativePath(RepositoryRoot, source.Path)));
        Assert.Contains(sources, source => source.Source.Contains(
            "PresentationServices.UiThread.InvokeOrRun",
            StringComparison.Ordinal));
    }

    [Fact]
    public void BatchGeometryRepair_ViewModelDelegatesUiThreadDispatchingToPresentationService()
    {
        var directory = Path.Combine(RepositoryRoot, "Features", "DataManagement", "BatchGeometryRepair");
        var sources = Directory.EnumerateFiles(directory, "*ViewModel*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => new { Path = path, Source = File.ReadAllText(path) })
            .ToArray();

        Assert.Empty(sources.Where(source => source.Source.Contains(
                "System.Windows.Application.Current",
                StringComparison.Ordinal))
            .Select(source => Path.GetRelativePath(RepositoryRoot, source.Path)));
        Assert.Contains(sources, source => source.Source.Contains(
            "PresentationServices.UiThread.InvokeOrRun",
            StringComparison.Ordinal));
    }

    [Fact]
    public void NodeDistanceCheck_ViewModelDelegatesUiThreadDispatchingToPresentationService()
    {
        var directory = Path.Combine(RepositoryRoot, "Features", "Editing", "NodeDistanceCheck");
        var sources = Directory.EnumerateFiles(directory, "*ViewModel*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => new { Path = path, Source = File.ReadAllText(path) })
            .ToArray();

        Assert.Empty(sources.Where(source => source.Source.Contains(
                "System.Windows.Application.Current",
                StringComparison.Ordinal))
            .Select(source => Path.GetRelativePath(RepositoryRoot, source.Path)));
        Assert.Contains(sources, source => source.Source.Contains(
            "PresentationServices.UiThread.InvokeOrRun",
            StringComparison.Ordinal));
    }

    [Fact]
    public void BoundaryPointGenerator_ViewModelDelegatesUiThreadDispatchingToPresentationService()
    {
        var directory = Path.Combine(RepositoryRoot, "Features", "DataManagement", "BoundaryPointGenerator");
        var sources = Directory.EnumerateFiles(directory, "*ViewModel*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => new { Path = path, Source = File.ReadAllText(path) })
            .ToArray();

        Assert.Empty(sources.Where(source => source.Source.Contains(
                "System.Windows.Application.Current",
                StringComparison.Ordinal))
            .Select(source => Path.GetRelativePath(RepositoryRoot, source.Path)));
        Assert.Contains(sources, source => source.Source.Contains(
            "PresentationServices.UiThread.InvokeOrRun",
            StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("MapSheetsLarge")]
    [InlineData("MapSheetsLargeAssign")]
    [InlineData("MapSheetsSmall")]
    [InlineData("MapSheetsSmallAssign")]
    public void MapSheets_ViewModelsDelegateUiThreadDispatchingToPresentationService(string feature)
    {
        var directory = Path.Combine(RepositoryRoot, "Features", "DataManagement", feature);
        var sources = Directory.EnumerateFiles(directory, "*ViewModel*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => new { Path = path, Source = File.ReadAllText(path) })
            .ToArray();

        Assert.Empty(sources.Where(source => source.Source.Contains(
                "System.Windows.Application.Current",
                StringComparison.Ordinal))
            .Select(source => Path.GetRelativePath(RepositoryRoot, source.Path)));
        Assert.Contains(sources, source => source.Source.Contains(
            "PresentationServices.UiThread.InvokeOrRun",
            StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("DataManagement", "BatchProjectionDefinition")]
    [InlineData("DataManagement", "FieldCopyTool")]
    [InlineData("Editing", "OverlapCheck")]
    public void SmallFeature_ViewModelsDelegateUiThreadDispatchingToPresentationService(string domain, string feature)
    {
        var directory = Path.Combine(RepositoryRoot, "Features", domain, feature);
        var sources = Directory.EnumerateFiles(directory, "*ViewModel*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => new { Path = path, Source = File.ReadAllText(path) })
            .ToArray();

        Assert.Empty(sources.Where(source => source.Source.Contains(
                "System.Windows.Application.Current",
                StringComparison.Ordinal))
            .Select(source => Path.GetRelativePath(RepositoryRoot, source.Path)));
        Assert.Contains(sources, source => source.Source.Contains(
            "PresentationServices.UiThread.InvokeOrRun",
            StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("HistoricalImageryDownload")]
    [InlineData("InternetTileDownload")]
    public void NetworkDownload_ViewModelsDelegateUiThreadDispatchingToPresentationService(string feature)
    {
        var directory = Path.Combine(RepositoryRoot, "Features", "General", feature);
        var sources = Directory.EnumerateFiles(directory, "*ViewModel*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => new { Path = path, Source = File.ReadAllText(path) })
            .ToArray();

        Assert.Empty(sources.Where(source => source.Source.Contains(
                "System.Windows.Application.Current",
                StringComparison.Ordinal))
            .Select(source => Path.GetRelativePath(RepositoryRoot, source.Path)));
        Assert.Contains(sources, source => source.Source.Contains(
            "PresentationServices.UiThread.InvokeOrRun",
            StringComparison.Ordinal));
    }

    [Fact]
    public void RotateGeometry_ViewModelDelegatesUiThreadDispatchingToPresentationService()
    {
        var directory = Path.Combine(RepositoryRoot, "Features", "DataManagement", "RotateGeometry");
        var sources = Directory.EnumerateFiles(directory, "*ViewModel*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => new { Path = path, Source = File.ReadAllText(path) })
            .ToArray();

        Assert.Empty(sources.Where(source => source.Source.Contains(
                "System.Windows.Application.Current",
                StringComparison.Ordinal))
            .Select(source => Path.GetRelativePath(RepositoryRoot, source.Path)));
        Assert.Contains(sources, source => source.Source.Contains(
            "PresentationServices.UiThread.Post",
            StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("BoundaryPointLineGenerator")]
    [InlineData("MapBoundaryPointLineGenerator")]
    public void BoundaryPointLine_ViewModelsDelegateUiThreadDispatchingToPresentationService(string feature)
    {
        var directory = Path.Combine(RepositoryRoot, "Features", "Editing", "Boundary", feature);
        var sources = Directory.EnumerateFiles(directory, "*ViewModel*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => new { Path = path, Source = File.ReadAllText(path) })
            .ToArray();

        Assert.Empty(sources.Where(source => source.Source.Contains(
                "System.Windows.Application.Current",
                StringComparison.Ordinal))
            .Select(source => Path.GetRelativePath(RepositoryRoot, source.Path)));
        Assert.Contains(sources, source => source.Source.Contains(
            "PresentationServices.UiThread.InvokeOrRun",
            StringComparison.Ordinal));
    }

    private static bool IsGeneratedOrTestPath(string path)
    {
        var relative = Path.GetRelativePath(RepositoryRoot, path);
        return relative.StartsWith("bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
               || relative.StartsWith("obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
               || relative.StartsWith("tests" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
               || relative.StartsWith("tmp" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsForbiddenCrossFeatureReference(string namespaceRemainder)
    {
        return namespaceRemainder.Contains(".Infrastructure", StringComparison.Ordinal)
               || Regex.IsMatch(
                   namespaceRemainder,
                   @"(?:ViewModel|Window|DockPaneView)(?:\.|$)",
                   RegexOptions.CultureInvariant);
    }
}
