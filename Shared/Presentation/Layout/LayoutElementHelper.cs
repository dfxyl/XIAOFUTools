using ArcGIS.Core.CIM;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace XIAOFUTools.Shared.Presentation.ArcGisLayout
{
    /// <summary>
    /// 布局元素操作助手类
    /// </summary>
    internal static class LayoutElementHelper
    {

        /// <summary>
        /// 创建表格单元格（图形+文本）
        /// </summary>
        public static async Task<List<Element>> CreateTableCellAsync(Layout layout, string baseName,
            (double X, double Y) position, (double Width, double Height) size, string text, bool isHeader = false)
        {
            var elements = new List<Element>();

            try
            {
                await QueuedTask.Run(() =>
                {
                    // 以传入位置为单元格左上角，向下绘制高度
                    double xMin = position.X;
                    double xMax = position.X + size.Width;
                    double yMax = position.Y;
                    double yMin = position.Y - size.Height;

                    // 创建矩形边框（Envelope 以左下-右上）
                    var envelope = EnvelopeBuilderEx.CreateEnvelope(xMin, yMin, xMax, yMax);

                    var rectangle = ElementFactory.Instance.CreateGraphicElement(
                        layout, envelope, GetDefaultRectangleSymbol(), $"{baseName}_border");
                    if (rectangle != null) elements.Add(rectangle);

                    // 创建文本元素（当文本非空时） - 使用中心点定位
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        var centerPoint = MapPointBuilderEx.CreateMapPoint(
                            (xMin + xMax) / 2, (yMin + yMax) / 2);

                        // 文本高度按单元格高度比例缩放（标题 60%，正文 55%）
                        var cellHeightPt = MmToPoints(yMax - yMin);
                        var textSymbol = GetCellTextSymbolScaled(isHeader ? cellHeightPt * 0.6 : cellHeightPt * 0.55, isHeader);

                        var textElement = ElementFactory.Instance.CreateTextGraphicElement(
                            layout, TextType.PointText, centerPoint, textSymbol, 
                            text, $"{baseName}_text") as Element;
                        if (textElement != null) 
                        {
                            elements.Add(textElement);
                        }
                    }
                });

                return elements;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"创建表格单元格失败: {ex.Message}");
                return elements;
            }
        }

        /// <summary>
        /// 创建表格单元格（同步版本，用于QueuedTask内部）
        /// </summary>
        public static List<Element> CreateTableCellSync(Layout layout, string baseName,
            (double X, double Y) position, (double Width, double Height) size, string text, bool isHeader = false,
            CIMPolygonSymbol customRectSymbol = null, CIMTextSymbol customTextSymbol = null)
        {
            var elements = new List<Element>();

            try
            {
                // 以传入位置为单元格左上角，向下绘制高度
                double xMin = position.X;
                double xMax = position.X + size.Width;
                double yMax = position.Y;
                double yMin = position.Y - size.Height;

                // 创建矩形边框（Envelope 以左下-右上）
                var envelope = EnvelopeBuilderEx.CreateEnvelope(xMin, yMin, xMax, yMax);

                var rectSymbol = customRectSymbol ?? GetDefaultRectangleSymbol();
                var rectangle = ElementFactory.Instance.CreateGraphicElement(
                    layout, envelope, rectSymbol, $"{baseName}_border");
                if (rectangle != null) elements.Add(rectangle);

                // 创建文本元素（当文本非空时） - 使用中心点定位
                if (!string.IsNullOrWhiteSpace(text))
                {
                    var centerPoint = MapPointBuilderEx.CreateMapPoint(
                        (xMin + xMax) / 2, (yMin + yMax) / 2);

                    // 文本高度按单元格高度比例缩放（标题 60%，正文 55%）
                    var cellHeightPt = MmToPoints(yMax - yMin);
                    var textSymbol = customTextSymbol != null 
                        ? CloneTextSymbolWithSize(customTextSymbol, isHeader ? cellHeightPt * 0.6 : cellHeightPt * 0.55)
                        : GetCellTextSymbolScaled(isHeader ? cellHeightPt * 0.6 : cellHeightPt * 0.55, isHeader);

                    var textElement = ElementFactory.Instance.CreateTextGraphicElement(
                        layout, TextType.PointText, centerPoint, textSymbol, 
                        text, $"{baseName}_text") as Element;
                    if (textElement != null) 
                    {
                        elements.Add(textElement);
                    }
                }

                return elements;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"创建表格单元格失败: {ex.Message}");
                return elements;
            }
        }

        /// <summary>
        /// 创建矩形模板元素（同步，用于QueuedTask内部）。位置为左上角坐标，单位：毫米。
        /// </summary>
        public static Element CreateRectangleTemplateSync(Layout layout, string name,
            (double X, double Y) position, (double Width, double Height) size,
            CIMPolygonSymbol symbol = null)
        {
            // 以传入位置为左上角
            double xMin = position.X;
            double xMax = position.X + size.Width;
            double yMax = position.Y;
            double yMin = position.Y - size.Height;

            var envelope = EnvelopeBuilderEx.CreateEnvelope(xMin, yMin, xMax, yMax);
            var polygonSymbol = symbol ?? GetDefaultRectangleSymbol();
            var element = ElementFactory.Instance.CreateGraphicElement(layout, envelope, polygonSymbol, name);
            return element;
        }

        /// <summary>
        /// 创建文本模板元素（同步，用于QueuedTask内部）。位置为点文本中心坐标，单位：毫米。
        /// </summary>
        public static Element CreateTextTemplateSync(Layout layout, string name,
            (double X, double Y) position, string text,
            CIMTextSymbol symbol = null)
        {
            var center = MapPointBuilderEx.CreateMapPoint(position.X, position.Y);
            var textSymbol = symbol ?? GetCellTextSymbolScaled(9, false);
            var element = ElementFactory.Instance.CreateTextGraphicElement(
                layout, TextType.PointText, center, textSymbol, text, name) as Element;
            return element;
        }

        /// <summary>
        /// 获取默认矩形符号
        /// </summary>
        private static CIMPolygonSymbol GetDefaultRectangleSymbol()
        {
            return new CIMPolygonSymbol
            {
                SymbolLayers = new CIMSymbolLayer[]
                {
                    new CIMSolidStroke
                    {
                        Enable = true,
                        Color = new CIMRGBColor { R = 0, G = 0, B = 0, Alpha = 100 },
                        Width = 0.5
                    },
                    new CIMSolidFill
                    {
                        Enable = true,
                        Color = new CIMRGBColor { R = 255, G = 255, B = 255, Alpha = 100 }
                    }
                }
            };
        }

        /// <summary>
        /// 获取单元格文本符号
        /// </summary>
        private static CIMTextSymbol GetCellTextSymbol(bool isHeader = false)
        {
            // 保留兼容的默认文本尺寸（不推荐）
            return GetCellTextSymbolScaled(isHeader ? 9 : 8, isHeader);
        }

        /// <summary>
        /// 获取根据单元格高度缩放的文本符号（Height 单位：points）
        /// </summary>
        private static CIMTextSymbol GetCellTextSymbolScaled(double heightPoints, bool isHeader = false)
        {
            double clamped = Math.Max(6, Math.Min(48, heightPoints));
            return new CIMTextSymbol
            {
                FontFamilyName = "Arial",
                FontStyleName = isHeader ? "Bold" : "Regular",
                Height = clamped,
                Symbol = new CIMPolygonSymbol
                {
                    SymbolLayers = new CIMSymbolLayer[]
                    {
                        new CIMSolidFill
                        {
                            Enable = true,
                            Color = new CIMRGBColor { R = 0, G = 0, B = 0, Alpha = 100 }
                        }
                    }
                },
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        /// <summary>
        /// 获取默认文本符号
        /// </summary>
        private static CIMTextSymbol GetDefaultTextSymbol()
        {
            return GetCellTextSymbol(false);
        }

        /// <summary>
        /// 克隆文本符号并调整大小
        /// </summary>
        private static CIMTextSymbol CloneTextSymbolWithSize(CIMTextSymbol sourceSymbol, double heightPoints)
        {
            if (sourceSymbol == null) return null;
            
            // 克隆符号
            var clonedSymbol = sourceSymbol.Clone() as CIMTextSymbol;
            if (clonedSymbol != null)
            {
                // 调整大小
                double clamped = Math.Max(6, Math.Min(48, heightPoints));
                clonedSymbol.Height = clamped;
            }
            return clonedSymbol;
        }

        /// <summary>
        /// 毫米转点
        /// </summary>
        public static double MmToPoints(double mm)
        {
            return mm * 2.834645669; // 1mm = 2.834645669 points
        }

        /// <summary>
        /// 点转毫米
        /// </summary>
        public static double PointsToMm(double points)
        {
            return points / 2.834645669;
        }
    }
}
