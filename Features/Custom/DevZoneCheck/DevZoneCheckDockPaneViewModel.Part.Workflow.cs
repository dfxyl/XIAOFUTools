using System;
using System.Threading.Tasks;
using System.Windows;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using XIAOFUTools.Features.Custom.DevZoneCheck.Infrastructure;

namespace XIAOFUTools.Features.Custom.DevZoneCheck
{
    internal partial class DevZoneCheckDockPaneViewModel
    {
        private readonly ArcGisDevZoneCheckGeometryService _geometryService = new();
        /// <summary>
        /// 执行核查
        /// </summary>
        private async Task RunCheckAsync()
        {
            if (!ValidateCheckInput())
            {
                return;
            }

            try
            {
                IsProcessing = true;
                CancelRequested = false;
                Progress = 0;
                ClearLog();
                _checkResults.Clear();
                HasResult = false;

                LogInfo("开始开发区整合优化核查...");
                LogInfo($"园区图层: {SelectedParkLayer.Name}");
                LogInfo($"面积单位: {SelectedAreaUnit}，小数位数: {DecimalPlaces}");
                LogInfo($"面积计算方式: {SelectedAreaCalculationMethod}");

                var parkFeatures = await _geometryService.LoadParkFeaturesAsync(
                    SelectedParkLayer,
                    SelectedParkField,
                    LogInfo);

                LogInfo($"共 {parkFeatures.Count} 个分组");

                int totalSteps = parkFeatures.Count;
                int currentStep = 0;

                foreach (var (parkName, parkGeom) in parkFeatures)
                {
                    if (CancelRequested)
                    {
                        LogInfo("用户取消操作");
                        break;
                    }

                    currentStep++;
                    Progress = (int)((double)currentStep / totalSteps * 100);
                    LogInfo($"正在核查: {parkName} ({currentStep}/{totalSteps})");

                    var result = new CheckResultData
                    {
                        ParkName = parkName,
                        ParkArea = ConvertArea(CalculateArea(parkGeom))
                    };

                    // 底线类核查
                    if (EnableUrbanBoundary && UrbanBoundaryLayer != null)
                    {
                        result.AreaInUrbanBoundary = ConvertArea(
                            await CalculateIntersectAreaAsync(UrbanBoundaryLayer, parkGeom));
                        result.AreaOutUrbanBoundary = result.ParkArea - result.AreaInUrbanBoundary;
                        if (result.AreaOutUrbanBoundary < 0) result.AreaOutUrbanBoundary = 0;
                    }

                    if (EnablePermanentFarmland && PermanentFarmlandLayer != null)
                    {
                        result.AreaOnPermanentFarmland = ConvertArea(
                            await CalculateIntersectAreaAsync(PermanentFarmlandLayer, parkGeom));
                    }

                    if (EnableEcoRedline && EcoRedlineLayer != null)
                    {
                        result.AreaOnEcoRedline = ConvertArea(
                            await CalculateIntersectAreaAsync(EcoRedlineLayer, parkGeom));
                    }

                    // 节约集约类核查
                    if (EnableLandSurvey && LandSurveyLayer != null && !string.IsNullOrEmpty(SelectedLandSurveyField))
                    {
                        result.CurrentConstructionLandArea = ConvertArea(
                            await CalculateIntersectAreaAsync(LandSurveyLayer, parkGeom, 
                                SelectedLandSurveyField, ConstructionLandCodes));

                        result.CurrentIndustrialLandArea = ConvertArea(
                            await CalculateIntersectAreaAsync(LandSurveyLayer, parkGeom, 
                                SelectedLandSurveyField, IndustrialLandCodes));

                        result.CurrentMiningLandArea = ConvertArea(
                            await CalculateIntersectAreaAsync(LandSurveyLayer, parkGeom, 
                                SelectedLandSurveyField, MiningLandCodes));

                        result.CurrentSaltFieldArea = ConvertArea(
                            await CalculateIntersectAreaAsync(LandSurveyLayer, parkGeom, 
                                SelectedLandSurveyField, SaltFieldCodes));

                        result.CurrentWarehouseLandArea = ConvertArea(
                            await CalculateIntersectAreaAsync(LandSurveyLayer, parkGeom, 
                                SelectedLandSurveyField, WarehouseLandCodes));

                        // 现状工业用地率 = 现状工矿仓储用地面积/现状建设用地面积*100%
                        double industrialWarehouseArea = result.CurrentIndustrialLandArea + 
                            result.CurrentMiningLandArea + result.CurrentSaltFieldArea + 
                            result.CurrentWarehouseLandArea;
                        if (result.CurrentConstructionLandArea > 0)
                        {
                            result.CurrentIndustrialRate = industrialWarehouseArea / 
                                result.CurrentConstructionLandArea * 100;
                        }
                    }

                    if (EnableApprovedLand && ApprovedLandLayer != null)
                    {
                        LogInfo("  计算已批建设用地...");
                        result.ApprovedLandArea = ConvertArea(
                            await CalculateIntersectAreaAsync(ApprovedLandLayer, parkGeom));
                    }

                    if (EnableApprovedNotSupplied && ApprovedNotSuppliedLayer != null && ApprovedLandLayer != null)
                    {
                        LogInfo("  计算批而未供面积...");
                        
                        result.ApprovedNotSuppliedArea = await _geometryService
                            .CalculateApprovedNotSuppliedAreaAsync(
                                ApprovedLandLayer,
                                ApprovedNotSuppliedLayer,
                                parkGeom,
                                () => CancelRequested,
                                CalculateArea);
                        
                        result.ApprovedNotSuppliedArea = ConvertArea(result.ApprovedNotSuppliedArea);
                        
                        // 批而未供率 = 批而未供面积/已批准建设用地面积*100%
                        if (result.ApprovedLandArea > 0)
                        {
                            result.ApprovedNotSuppliedRate = result.ApprovedNotSuppliedArea / 
                                result.ApprovedLandArea * 100;
                        }
                    }

                    if (EnableSuppliedLand && SuppliedLandLayer != null)
                    {
                        LogInfo("  计算已供应建设用地...");
                        result.SuppliedLandArea = ConvertArea(
                            await CalculateIntersectAreaAsync(SuppliedLandLayer, parkGeom));
                    }

                    if (EnableIdleLand && IdleLandLayer != null)
                    {
                        LogInfo("  计算闲置土地面积...");
                        result.IdleLandArea = ConvertArea(
                            await CalculateIntersectAreaAsync(IdleLandLayer, parkGeom));
                        
                        // 闲置土地率 = 闲置土地面积/已供应建设用地面积*100%
                        if (result.SuppliedLandArea > 0)
                        {
                            result.IdleLandRate = result.IdleLandArea / result.SuppliedLandArea * 100;
                        }
                    }

                    // 发展空间类核查
                    if (EnableSpatialPlanning && SpatialPlanningLayer != null && 
                        !string.IsNullOrEmpty(SelectedSpatialPlanningField))
                    {
                        LogInfo("  计算规划建设用地...");
                        result.PlannedConstructionLandArea = ConvertArea(
                            await CalculateIntersectAreaAsync(SpatialPlanningLayer, parkGeom, 
                                SelectedSpatialPlanningField, PlannedConstructionCodes));

                        LogInfo("  计算规划工矿仓储用地...");
                        result.PlannedIndustrialLandArea = ConvertArea(
                            await CalculateIntersectAreaAsync(SpatialPlanningLayer, parkGeom, 
                                SelectedSpatialPlanningField, PlannedIndustrialCodes));

                        // 规划工业用地率 = 规划工矿仓储用地面积/规划建设用地面积*100%
                        if (result.PlannedConstructionLandArea > 0)
                        {
                            result.PlannedIndustrialRate = result.PlannedIndustrialLandArea / 
                                result.PlannedConstructionLandArea * 100;
                        }
                    }

                    // 举证面积计算
                    if (EnableEvidenceData && EvidenceDataLayer != null)
                    {
                        result.EvidenceArea = ConvertArea(
                            await CalculateIntersectAreaAsync(EvidenceDataLayer, parkGeom));
                    }

                    // 尚可供应年限计算（使用空间擦除）
                    // 公式: 尚可供应年限 = (规划建设用地 - 已供应) ÷ (园区内19-23年供地面积 ÷ 5)
                    // 举证数据为可选，不强制要求
                    if (EnableSpatialPlanning && EnableSuppliedLand && EnableSupplyYearData &&
                        SpatialPlanningLayer != null && SuppliedLandLayer != null &&
                        SupplyYearDataLayer != null)
                    {
                        LogInfo("  计算尚可供应年限...");
                        // 使用空间擦除计算尚可供应面积
                        double availableArea = await QueuedTask.Run(async () =>
                        {
                            var planningFC = SpatialPlanningLayer.GetFeatureClass();
                            var suppliedFC = SuppliedLandLayer.GetFeatureClass();
                            // 举证数据可选
                            var evidenceFC = (EnableEvidenceData && EvidenceDataLayer != null) 
                                ? EvidenceDataLayer.GetFeatureClass() : null;
                            
                            if (planningFC == null || suppliedFC == null)
                                return 0.0;

                            // 使用园区几何的空间参考作为统一目标坐标系
                            var targetSpatialRef = parkGeom.SpatialReference;
                            if (targetSpatialRef == null)
                            {
                                targetSpatialRef = planningFC.GetDefinition().GetSpatialReference();
                            }
                            
                            Geometry remainingGeom = null;

                            // 1. 获取规划建设用地与园区的交集
                            var planningSpatialRef = planningFC.GetDefinition().GetSpatialReference();
                            var parkGeomForPlanning = parkGeom;
                            if (planningSpatialRef != null && targetSpatialRef != null &&
                                !SpatialReference.AreEqual(parkGeom.SpatialReference, planningSpatialRef, false))
                            {
                                try
                                {
                                    parkGeomForPlanning = GeometryEngine.Instance.Project(parkGeom, planningSpatialRef);
                                }
                                catch
                                {
                                    return 0.0;
                                }
                            }

                            var planningFilter = new SpatialQueryFilter
                            {
                                FilterGeometry = parkGeomForPlanning,
                                SpatialRelationship = SpatialRelationship.Intersects
                            };

                            using (var cursor = planningFC.Search(planningFilter))
                            {
                                while (cursor.MoveNext())
                                {
                                    if (CancelRequested) break;

                                    using (var feature = cursor.Current as Feature)
                                    {
                                        var geom = feature.GetShape();
                                        if (geom == null || geom.IsEmpty) continue;

                                        // 投影到目标坐标系
                                        var geomSpatialRef = geom.SpatialReference ?? planningSpatialRef;
                                        if (geomSpatialRef != null && targetSpatialRef != null &&
                                            !SpatialReference.AreEqual(geomSpatialRef, targetSpatialRef, false))
                                        {
                                            try
                                            {
                                                geom = GeometryEngine.Instance.Project(geom, targetSpatialRef);
                                            }
                                            catch { continue; }
                                        }

                                        try
                                        {
                                            var intersect = GeometryEngine.Instance.Intersection(geom, parkGeom);
                                            if (intersect != null && !intersect.IsEmpty)
                                            {
                                                if (remainingGeom == null)
                                                {
                                                    remainingGeom = intersect;
                                                }
                                                else
                                                {
                                                    var unionResult = GeometryEngine.Instance.Union(remainingGeom, intersect);
                                                    if (unionResult != null && !unionResult.IsEmpty)
                                                    {
                                                        remainingGeom = unionResult;
                                                    }
                                                }
                                            }
                                        }
                                        catch { continue; }
                                    }
                                }
                            }

                            if (remainingGeom == null || remainingGeom.IsEmpty)
                                return 0.0;

                            // 2. 擦除已供应数据
                            var suppliedSpatialRef = suppliedFC.GetDefinition().GetSpatialReference();
                            var remainingGeomForSupplied = remainingGeom;
                            if (suppliedSpatialRef != null && targetSpatialRef != null &&
                                !SpatialReference.AreEqual(remainingGeom.SpatialReference, suppliedSpatialRef, false))
                            {
                                try
                                {
                                    remainingGeomForSupplied = GeometryEngine.Instance.Project(remainingGeom, suppliedSpatialRef);
                                }
                                catch
                                {
                                    remainingGeomForSupplied = remainingGeom;
                                }
                            }

                            var suppliedFilter = new SpatialQueryFilter
                            {
                                FilterGeometry = remainingGeomForSupplied,
                                SpatialRelationship = SpatialRelationship.Intersects
                            };

                            using (var cursor = suppliedFC.Search(suppliedFilter))
                            {
                                while (cursor.MoveNext())
                                {
                                    if (CancelRequested) break;

                                    using (var feature = cursor.Current as Feature)
                                    {
                                        var geom = feature.GetShape();
                                        if (geom == null || geom.IsEmpty) continue;

                                        // 投影到目标坐标系
                                        var geomSpatialRef = geom.SpatialReference ?? suppliedSpatialRef;
                                        if (geomSpatialRef != null && targetSpatialRef != null &&
                                            !SpatialReference.AreEqual(geomSpatialRef, targetSpatialRef, false))
                                        {
                                            try
                                            {
                                                geom = GeometryEngine.Instance.Project(geom, targetSpatialRef);
                                            }
                                            catch { continue; }
                                        }

                                        try
                                        {
                                            var diffResult = GeometryEngine.Instance.Difference(remainingGeom, geom);
                                            if (diffResult == null || diffResult.IsEmpty)
                                            {
                                                remainingGeom = null;
                                                break;
                                            }
                                            remainingGeom = diffResult;
                                        }
                                        catch { continue; }
                                    }
                                }
                            }

                            // 如果已供应擦除后为空，直接返回0
                            if (remainingGeom == null || remainingGeom.IsEmpty)
                                return 0.0;

                            // 3. 擦除举证数据（可选，只有启用举证数据时才执行）
                            if (evidenceFC != null)
                            {
                                var evidenceSpatialRef = evidenceFC.GetDefinition().GetSpatialReference();
                                var remainingGeomForEvidence = remainingGeom;
                                if (evidenceSpatialRef != null && targetSpatialRef != null &&
                                    !SpatialReference.AreEqual(remainingGeom.SpatialReference, evidenceSpatialRef, false))
                                {
                                    try
                                    {
                                        remainingGeomForEvidence = GeometryEngine.Instance.Project(remainingGeom, evidenceSpatialRef);
                                    }
                                    catch
                                    {
                                        remainingGeomForEvidence = remainingGeom;
                                    }
                                }

                                var evidenceFilter = new SpatialQueryFilter
                                {
                                    FilterGeometry = remainingGeomForEvidence,
                                    SpatialRelationship = SpatialRelationship.Intersects
                                };

                                using (var cursor = evidenceFC.Search(evidenceFilter))
                                {
                                    while (cursor.MoveNext())
                                    {
                                        if (CancelRequested) break;

                                        using (var feature = cursor.Current as Feature)
                                        {
                                            var geom = feature.GetShape();
                                            if (geom == null || geom.IsEmpty) continue;

                                            // 投影到目标坐标系
                                            var geomSpatialRef = geom.SpatialReference ?? evidenceSpatialRef;
                                            if (geomSpatialRef != null && targetSpatialRef != null &&
                                                !SpatialReference.AreEqual(geomSpatialRef, targetSpatialRef, false))
                                            {
                                                try
                                                {
                                                    geom = GeometryEngine.Instance.Project(geom, targetSpatialRef);
                                                }
                                                catch { continue; }
                                            }

                                            try
                                            {
                                                var diffResult = GeometryEngine.Instance.Difference(remainingGeom, geom);
                                                if (diffResult == null || diffResult.IsEmpty)
                                                {
                                                    remainingGeom = null;
                                                    break;
                                                }
                                                remainingGeom = diffResult;
                                            }
                                            catch { continue; }
                                        }
                                    }
                                }
                            }

                            // 4. 计算剩余面积（即使举证为空，remainingGeom仍然有效）
                            if (remainingGeom == null || remainingGeom.IsEmpty)
                                return 0.0;
                                
                            return CalculateArea(remainingGeom);
                        });
                        
                        // 转换面积单位
                        availableArea = ConvertArea(availableArea);
                        
                        // 年限数据（19-23年，5年）与园区交集面积
                        double yearDataArea = ConvertArea(
                            await CalculateIntersectAreaAsync(SupplyYearDataLayer, parkGeom));
                        
                        // 年均供应量 = 年限数据面积 / 5
                        double avgAnnualSupply = yearDataArea / 5.0;
                        if (avgAnnualSupply > 0 && availableArea > 0)
                        {
                            result.AvailableSupplyYears = availableArea / avgAnnualSupply;
                        }
                    }

                    _checkResults.Add(result);

                    // 输出简要结果
                    LogInfo($"  园区面积: {FormatArea(result.ParkArea)} {SelectedAreaUnit}");
                    if (EnableUrbanBoundary)
                    {
                        LogInfo($"  开发边界内: {FormatArea(result.AreaInUrbanBoundary)}, 边界外: {FormatArea(result.AreaOutUrbanBoundary)}");
                    }
                    if (EnablePermanentFarmland)
                    {
                        LogInfo($"  压占永久基本农田: {FormatArea(result.AreaOnPermanentFarmland)}");
                    }
                    if (EnableEcoRedline)
                    {
                        LogInfo($"  压占生态保护红线: {FormatArea(result.AreaOnEcoRedline)}");
                    }
                    if (EnableLandSurvey)
                    {
                        LogInfo($"  现状工业用地率: {result.CurrentIndustrialRate:F2}%");
                    }
                    if (EnableApprovedNotSupplied)
                    {
                        LogInfo($"  批而未供率: {result.ApprovedNotSuppliedRate:F2}%");
                    }
                    if (EnableIdleLand)
                    {
                        LogInfo($"  闲置土地率: {result.IdleLandRate:F2}%");
                    }
                    if (EnableSpatialPlanning)
                    {
                        LogInfo($"  规划工业用地率: {result.PlannedIndustrialRate:F2}%");
                    }
                }

                if (!CancelRequested)
                {
                    HasResult = _checkResults.Count > 0;
                    Progress = 100;
                    LogInfo($"核查完成！共处理 {_checkResults.Count} 个园区");
                }
            }
            catch (Exception ex)
            {
                LogError($"核查过程中发生错误: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
            }
        }
    }
}
