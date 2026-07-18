using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using Microsoft.Win32;

namespace XIAOFUTools.Features.Custom.DevZoneCheck
{
    internal partial class DevZoneCheckDockPaneViewModel
    {

        public void AutoMatchLayers()
        {
            if (PolygonLayers == null || PolygonLayers.Count == 0)
            {
                RefreshLayers();
            }

            var keywords = new Dictionary<string, Action<FeatureLayer>>
            {
                { "园区", layer => SelectedParkLayer = layer },
                { "城镇开发边界", layer => { EnableUrbanBoundary = true; UrbanBoundaryLayer = layer; } },
                { "永久基本农田", layer => { EnablePermanentFarmland = true; PermanentFarmlandLayer = layer; } },
                { "生态保护红线", layer => { EnableEcoRedline = true; EcoRedlineLayer = layer; } },
                { "三调", layer => { EnableLandSurvey = true; LandSurveyLayer = layer; } },
                { "现状", layer => { EnableLandSurvey = true; LandSurveyLayer = layer; } },
                { "批而未供", layer => { EnableApprovedNotSupplied = true; ApprovedNotSuppliedLayer = layer; } },
                { "已批", layer => { EnableApprovedLand = true; ApprovedLandLayer = layer; } },
                { "已供", layer => { EnableSuppliedLand = true; SuppliedLandLayer = layer; } },
                { "供地", layer => { EnableSuppliedLand = true; SuppliedLandLayer = layer; } },
                { "闲置", layer => { EnableIdleLand = true; IdleLandLayer = layer; } },
                { "规划", layer => { EnableSpatialPlanning = true; SpatialPlanningLayer = layer; } },
                { "国土空间", layer => { EnableSpatialPlanning = true; SpatialPlanningLayer = layer; } },
                { "举证", layer => { EnableEvidenceData = true; EvidenceDataLayer = layer; } },
                { "年限", layer => { EnableSupplyYearData = true; SupplyYearDataLayer = layer; } },
                { "19-23", layer => { EnableSupplyYearData = true; SupplyYearDataLayer = layer; } },
                { "2019", layer => { EnableSupplyYearData = true; SupplyYearDataLayer = layer; } }
            };

            int matchCount = 0;
            foreach (var layer in PolygonLayers)
            {
                var layerName = layer.Name;
                foreach (var keyword in keywords)
                {
                    if (layerName.Contains(keyword.Key))
                    {
                        keyword.Value(layer);
                        matchCount++;
                        break;
                    }
                }
            }

            LogInfo($"自动匹配完成，共匹配 {matchCount} 个图层");
        }
    }
}
