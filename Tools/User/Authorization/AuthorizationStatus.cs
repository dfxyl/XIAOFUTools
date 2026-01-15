using System;

namespace XIAOFUTools.Tools.Authorization
{
    /// <summary>
    /// 授权状态信息
    /// </summary>
    public class AuthorizationStatus
    {
        /// <summary>
        /// 是否已授权
        /// </summary>
        public bool IsAuthorized { get; set; }

        /// <summary>
        /// 状态消息
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// 过期时间
        /// </summary>
        public DateTime ExpireTime { get; set; }

        /// <summary>
        /// 剩余天数
        /// </summary>
        public int RemainingDays { get; set; }

        /// <summary>
        /// 机器码
        /// </summary>
        public string MachineCode { get; set; } = string.Empty;

        /// <summary>
        /// 授权类型（个人版/通用版）
        /// </summary>
        public string AuthType { get; set; } = "个人版";

        /// <summary>
        /// 剩余时间描述
        /// </summary>
        public string RemainingTimeDescription
        {
            get
            {
                if (!IsAuthorized)
                    return "未授权";

                if (RemainingDays <= 0)
                    return "已过期";

                if (RemainingDays == 1)
                    return "剩余1天";

                return $"剩余{RemainingDays}天";
            }
        }

        /// <summary>
        /// 授权状态颜色（用于UI显示）
        /// </summary>
        public string StatusColor
        {
            get
            {
                if (!IsAuthorized)
                    return "#FF6B6B"; // 红色

                if (RemainingDays <= 7)
                    return "#FFB347"; // 橙色

                return "#4ECDC4"; // 绿色
            }
        }

        /// <summary>
        /// 格式化的过期时间
        /// </summary>
        public string FormattedExpireTime
        {
            get
            {
                if (ExpireTime == DateTime.MinValue)
                    return "无";

                return ExpireTime.ToString("yyyy-MM-dd HH:mm:ss");
            }
        }
    }
}
