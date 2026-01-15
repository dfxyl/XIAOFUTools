using System;
using System.Collections.Generic;

namespace XIAOFUTools.Tools.SpecialCoordinateTransform
{
    /// <summary>
    /// 坐标转换核心算法类
    /// 支持WGS84、GCJ02、BD09坐标系之间的相互转换
    /// </summary>
    public static class CoordinateTransformCore
    {
        private const double PI = 3.1415926535897932384626;
        private const double A = 6378245.0; // 长半轴
        private const double EE = 0.00669342162296594323; // 偏心率平方
        private const double X_PI = 3.14159265358979324 * 3000.0 / 180.0;

        /// <summary>
        /// 判断是否在中国境内
        /// </summary>
        /// <param name="lng">经度</param>
        /// <param name="lat">纬度</param>
        /// <returns>是否在中国境内</returns>
        public static bool IsInChina(double lng, double lat)
        {
            if (lng < 72.004 || lng > 137.8347)
                return false;
            if (lat < 0.8293 || lat > 55.8271)
                return false;
            return true;
        }

        /// <summary>
        /// WGS84转GCJ02
        /// </summary>
        /// <param name="lng">WGS84经度</param>
        /// <param name="lat">WGS84纬度</param>
        /// <returns>GCJ02坐标</returns>
        public static (double lng, double lat) Wgs84ToGcj02(double lng, double lat)
        {
            if (!IsInChina(lng, lat))
                return (lng, lat);

            double dlat = TransformLat(lng - 105.0, lat - 35.0);
            double dlng = TransformLng(lng - 105.0, lat - 35.0);
            double radlat = lat / 180.0 * PI;
            double magic = Math.Sin(radlat);
            magic = 1 - EE * magic * magic;
            double sqrtmagic = Math.Sqrt(magic);
            dlat = (dlat * 180.0) / ((A * (1 - EE)) / (magic * sqrtmagic) * PI);
            dlng = (dlng * 180.0) / (A / sqrtmagic * Math.Cos(radlat) * PI);
            double mglat = lat + dlat;
            double mglng = lng + dlng;
            return (mglng, mglat);
        }

        /// <summary>
        /// GCJ02转WGS84
        /// </summary>
        /// <param name="lng">GCJ02经度</param>
        /// <param name="lat">GCJ02纬度</param>
        /// <returns>WGS84坐标</returns>
        public static (double lng, double lat) Gcj02ToWgs84(double lng, double lat)
        {
            if (!IsInChina(lng, lat))
                return (lng, lat);

            double dlat = TransformLat(lng - 105.0, lat - 35.0);
            double dlng = TransformLng(lng - 105.0, lat - 35.0);

            double radlat = lat / 180.0 * PI;
            double magic = Math.Sin(radlat);
            magic = 1 - EE * magic * magic;
            double sqrtmagic = Math.Sqrt(magic);
            dlat = (dlat * 180.0) / ((A * (1 - EE)) / (magic * sqrtmagic) * PI);
            dlng = (dlng * 180.0) / (A / sqrtmagic * Math.Cos(radlat) * PI);
            double mglng = lng - dlng;
            double mglat = lat - dlat;
            return (mglng, mglat);
        }

        /// <summary>
        /// GCJ02转BD09
        /// </summary>
        /// <param name="lng">GCJ02经度</param>
        /// <param name="lat">GCJ02纬度</param>
        /// <returns>BD09坐标</returns>
        public static (double lng, double lat) Gcj02ToBd09(double lng, double lat)
        {
            double z = Math.Sqrt(lng * lng + lat * lat) + 0.00002 * Math.Sin(lat * X_PI);
            double theta = Math.Atan2(lat, lng) + 0.000003 * Math.Cos(lng * X_PI);
            double bd_lng = z * Math.Cos(theta) + 0.0065;
            double bd_lat = z * Math.Sin(theta) + 0.006;
            return (bd_lng, bd_lat);
        }

        /// <summary>
        /// BD09转GCJ02
        /// </summary>
        /// <param name="lng">BD09经度</param>
        /// <param name="lat">BD09纬度</param>
        /// <returns>GCJ02坐标</returns>
        public static (double lng, double lat) Bd09ToGcj02(double lng, double lat)
        {
            double x = lng - 0.0065;
            double y = lat - 0.006;
            double z = Math.Sqrt(x * x + y * y) - 0.00002 * Math.Sin(y * X_PI);
            double theta = Math.Atan2(y, x) - 0.000003 * Math.Cos(x * X_PI);
            double gcj_lng = z * Math.Cos(theta);
            double gcj_lat = z * Math.Sin(theta);
            return (gcj_lng, gcj_lat);
        }

        /// <summary>
        /// WGS84转BD09
        /// </summary>
        /// <param name="lng">WGS84经度</param>
        /// <param name="lat">WGS84纬度</param>
        /// <returns>BD09坐标</returns>
        public static (double lng, double lat) Wgs84ToBd09(double lng, double lat)
        {
            var gcj = Wgs84ToGcj02(lng, lat);
            return Gcj02ToBd09(gcj.lng, gcj.lat);
        }

        /// <summary>
        /// BD09转WGS84
        /// </summary>
        /// <param name="lng">BD09经度</param>
        /// <param name="lat">BD09纬度</param>
        /// <returns>WGS84坐标</returns>
        public static (double lng, double lat) Bd09ToWgs84(double lng, double lat)
        {
            var gcj = Bd09ToGcj02(lng, lat);
            return Gcj02ToWgs84(gcj.lng, gcj.lat);
        }

        /// <summary>
        /// 根据转换类型执行坐标转换
        /// </summary>
        /// <param name="lng">经度</param>
        /// <param name="lat">纬度</param>
        /// <param name="conversionType">转换类型</param>
        /// <returns>转换后的坐标</returns>
        public static (double lng, double lat) TransformCoordinate(double lng, double lat, string conversionType)
        {
            return conversionType switch
            {
                "wgs84_to_gcj02" => Wgs84ToGcj02(lng, lat),
                "gcj02_to_wgs84" => Gcj02ToWgs84(lng, lat),
                "gcj02_to_bd09" => Gcj02ToBd09(lng, lat),
                "bd09_to_gcj02" => Bd09ToGcj02(lng, lat),
                "wgs84_to_bd09" => Wgs84ToBd09(lng, lat),
                "bd09_to_wgs84" => Bd09ToWgs84(lng, lat),
                _ => (lng, lat)
            };
        }

        #region 私有辅助方法

        private static double TransformLat(double lng, double lat)
        {
            double ret = -100.0 + 2.0 * lng + 3.0 * lat + 0.2 * lat * lat + 0.1 * lng * lat + 0.2 * Math.Sqrt(Math.Abs(lng));
            ret += (20.0 * Math.Sin(6.0 * lng * PI) + 20.0 * Math.Sin(2.0 * lng * PI)) * 2.0 / 3.0;
            ret += (20.0 * Math.Sin(lat * PI) + 40.0 * Math.Sin(lat / 3.0 * PI)) * 2.0 / 3.0;
            ret += (160.0 * Math.Sin(lat / 12.0 * PI) + 320 * Math.Sin(lat * PI / 30.0)) * 2.0 / 3.0;
            return ret;
        }

        private static double TransformLng(double lng, double lat)
        {
            double ret = 300.0 + lng + 2.0 * lat + 0.1 * lng * lng + 0.1 * lng * lat + 0.1 * Math.Sqrt(Math.Abs(lng));
            ret += (20.0 * Math.Sin(6.0 * lng * PI) + 20.0 * Math.Sin(2.0 * lng * PI)) * 2.0 / 3.0;
            ret += (20.0 * Math.Sin(lng * PI) + 40.0 * Math.Sin(lng / 3.0 * PI)) * 2.0 / 3.0;
            ret += (150.0 * Math.Sin(lng / 12.0 * PI) + 300.0 * Math.Sin(lng / 30.0 * PI)) * 2.0 / 3.0;
            return ret;
        }

        #endregion
    }

    /// <summary>
    /// 坐标转换类型枚举
    /// </summary>
    public static class CoordinateConversionTypes
    {
        public static readonly Dictionary<string, string> Types = new Dictionary<string, string>
        {
            { "wgs84_to_gcj02", "WGS84 → GCJ02（火星坐标）" },
            { "gcj02_to_wgs84", "GCJ02（火星坐标）→ WGS84" },
            { "gcj02_to_bd09", "GCJ02（火星坐标）→ BD09（百度坐标）" },
            { "bd09_to_gcj02", "BD09（百度坐标）→ GCJ02（火星坐标）" },
            { "wgs84_to_bd09", "WGS84 → BD09（百度坐标）" },
            { "bd09_to_wgs84", "BD09（百度坐标）→ WGS84" }
        };
    }
}
