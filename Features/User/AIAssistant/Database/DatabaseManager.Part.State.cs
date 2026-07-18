using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Data.Sqlite;

namespace XIAOFUTools.Features.User.AIAssistant.Database
{
    public partial class DatabaseManager
    {

        public static DatabaseManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            try
                            {
                                _instance = new DatabaseManager();
                                _lastInitError = null;
                            }
                            catch (Exception ex)
                            {
                                _lastInitError = ex.Message;
                                System.Diagnostics.Debug.WriteLine($"DatabaseManager初始化失败: {ex.Message}");
                                throw;
                            }
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// 获取上次初始化错误信息（用于诊断）
        /// </summary>
        public static string LastInitError => _lastInitError;

        /// <summary>
        /// 检查数据库是否可用
        /// </summary>
        public static bool IsAvailable
        {
            get
            {
                try
                {
                    return _instance != null && _instance._isInitialized;
                }
                catch
                {
                    return false;
                }
            }
        }
    }
}
