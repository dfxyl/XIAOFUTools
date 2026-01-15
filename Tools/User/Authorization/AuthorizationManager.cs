using System;
using System.Management;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;
using System.Linq;

namespace XIAOFUTools.Tools.Authorization
{
    /// <summary>
    /// 授权管理器 - 负责机器码生成、授权码验证、授权状态管理
    /// </summary>
    public class AuthorizationManager
    {
        private const string REGISTRY_KEY = @"SOFTWARE\XIAOFUTools\Authorization";
        private const string AUTH_CODE_VALUE = "AuthCode";
        private const string EXPIRE_TIME_VALUE = "ExpireTime";
        private const string MACHINE_CODE_VALUE = "MachineCode";

        /// <summary>
        /// 获取机器码（基于多种硬件信息生成唯一标识，100个字符）
        /// </summary>
        /// <returns>机器码字符串</returns>
        public static string GetMachineCode()
        {
            try
            {
                StringBuilder machineInfo = new StringBuilder();

                // 获取CPU信息
                string cpuId = GetCpuId();
                if (!string.IsNullOrEmpty(cpuId))
                    machineInfo.Append($"CPU:{cpuId};");

                // 获取主板序列号
                string motherboardId = GetMotherboardId();
                if (!string.IsNullOrEmpty(motherboardId))
                    machineInfo.Append($"MB:{motherboardId};");

                // 获取硬盘序列号
                string diskId = GetDiskId();
                if (!string.IsNullOrEmpty(diskId))
                    machineInfo.Append($"DISK:{diskId};");

                // 获取MAC地址
                string macAddress = GetMacAddress();
                if (!string.IsNullOrEmpty(macAddress))
                    machineInfo.Append($"MAC:{macAddress};");

                // 获取BIOS序列号
                string biosId = GetBiosId();
                if (!string.IsNullOrEmpty(biosId))
                    machineInfo.Append($"BIOS:{biosId};");

                // 添加系统信息
                machineInfo.Append($"OS:{Environment.OSVersion};");
                machineInfo.Append($"USER:{Environment.UserName};");
                machineInfo.Append($"MACHINE:{Environment.MachineName};");

                // 生成复杂机器码（100个字符）
                return ConvertToMixedBase(machineInfo.ToString());
            }
            catch (Exception ex)
            {
                throw new Exception($"生成机器码时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 验证授权码
        /// </summary>
        /// <param name="authCode">授权码</param>
        /// <param name="machineCode">机器码</param>
        /// <returns>授权信息（是否有效、过期时间、授权类型）</returns>
        public static (bool IsValid, DateTime ExpireTime, string AuthType) ValidateAuthCode(string authCode, string machineCode)
        {
            try
            {
                if (string.IsNullOrEmpty(authCode))
                    return (false, DateTime.MinValue, "未知");

                // 首先尝试验证通用授权码
                var (isUniversalValid, universalExpireTime) = DecodeUniversalAuthCode(authCode);
                if (isUniversalValid)
                {
                    // 检查是否过期
                    if (DateTime.Now > universalExpireTime)
                        return (false, universalExpireTime, "通用版");

                    return (true, universalExpireTime, "通用版");
                }

                // 如果不是通用授权码，尝试验证个人授权码
                if (string.IsNullOrEmpty(machineCode))
                    return (false, DateTime.MinValue, "个人版");

                var (isPersonalValid, personalExpireTime) = DecodePersonalAuthCode(authCode, machineCode);

                if (!isPersonalValid)
                    return (false, DateTime.MinValue, "个人版");

                // 检查是否过期
                if (DateTime.Now > personalExpireTime)
                    return (false, personalExpireTime, "个人版");

                return (true, personalExpireTime, "个人版");
            }
            catch
            {
                return (false, DateTime.MinValue, "未知");
            }
        }

        /// <summary>
        /// 保存授权信息到注册表
        /// </summary>
        /// <param name="authCode">授权码</param>
        /// <param name="expireTime">过期时间</param>
        /// <param name="machineCode">机器码</param>
        /// <param name="authType">授权类型</param>
        public static void SaveAuthInfo(string authCode, DateTime expireTime, string machineCode, string authType)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(REGISTRY_KEY))
                {
                    if (key != null)
                    {
                        // 加密存储授权码
                        string encryptedAuthCode = EncryptString(authCode);
                        key.SetValue(AUTH_CODE_VALUE, encryptedAuthCode);
                        key.SetValue(EXPIRE_TIME_VALUE, expireTime.ToBinary());
                        key.SetValue(MACHINE_CODE_VALUE, machineCode);
                        key.SetValue("AuthType", authType); // 新增授权类型
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"保存授权信息时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 从注册表读取授权信息
        /// </summary>
        /// <returns>授权信息（授权码、过期时间、机器码、授权类型）</returns>
        public static (string AuthCode, DateTime ExpireTime, string MachineCode, string AuthType) LoadAuthInfo()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY))
                {
                    if (key != null)
                    {
                        string encryptedAuthCode = key.GetValue(AUTH_CODE_VALUE)?.ToString();
                        long expireTimeBinary = Convert.ToInt64(key.GetValue(EXPIRE_TIME_VALUE, 0L));
                        string machineCode = key.GetValue(MACHINE_CODE_VALUE)?.ToString();
                        string authType = key.GetValue("AuthType")?.ToString() ?? "个人版"; // 默认为个人版

                        if (!string.IsNullOrEmpty(encryptedAuthCode))
                        {
                            string authCode = DecryptString(encryptedAuthCode);
                            DateTime expireTime = DateTime.FromBinary(expireTimeBinary);
                            return (authCode, expireTime, machineCode, authType);
                        }
                    }
                }
            }
            catch
            {
                // 忽略读取错误，返回空值
            }

            return (string.Empty, DateTime.MinValue, string.Empty, "个人版");
        }

        /// <summary>
        /// 清除授权信息
        /// </summary>
        public static void ClearAuthInfo()
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(REGISTRY_KEY, false);
            }
            catch
            {
                // 忽略删除错误
            }
        }

        /// <summary>
        /// 获取当前授权状态
        /// </summary>
        /// <returns>授权状态信息</returns>
        public static AuthorizationStatus GetAuthorizationStatus()
        {
            try
            {
                string currentMachineCode = GetMachineCode();
                var (authCode, expireTime, savedMachineCode, authType) = LoadAuthInfo();

                if (string.IsNullOrEmpty(authCode))
                {
                    return new AuthorizationStatus
                    {
                        IsAuthorized = false,
                        Message = "未授权",
                        ExpireTime = DateTime.MinValue,
                        RemainingDays = 0,
                        MachineCode = currentMachineCode,
                        AuthType = "个人版"
                    };
                }

                // 验证授权码
                var (isValid, validExpireTime, validAuthType) = ValidateAuthCode(authCode, currentMachineCode);

                if (!isValid)
                {
                    // 如果是个人版且机器码不匹配
                    if (validAuthType == "个人版" && savedMachineCode != currentMachineCode)
                    {
                        return new AuthorizationStatus
                        {
                            IsAuthorized = false,
                            Message = "机器码不匹配，请重新授权",
                            ExpireTime = DateTime.MinValue,
                            RemainingDays = 0,
                            MachineCode = currentMachineCode,
                            AuthType = validAuthType
                        };
                    }

                    return new AuthorizationStatus
                    {
                        IsAuthorized = false,
                        Message = "授权码无效",
                        ExpireTime = DateTime.MinValue,
                        RemainingDays = 0,
                        MachineCode = currentMachineCode,
                        AuthType = validAuthType
                    };
                }

                if (DateTime.Now > validExpireTime)
                {
                    return new AuthorizationStatus
                    {
                        IsAuthorized = false,
                        Message = "授权已过期",
                        ExpireTime = validExpireTime,
                        RemainingDays = 0,
                        MachineCode = currentMachineCode,
                        AuthType = validAuthType
                    };
                }

                int remainingDays = (int)(validExpireTime - DateTime.Now).TotalDays;
                string typeDesc = validAuthType == "通用版" ? "（通用版）" : "（个人版）";
                return new AuthorizationStatus
                {
                    IsAuthorized = true,
                    Message = $"授权有效{typeDesc}，剩余 {remainingDays} 天",
                    ExpireTime = validExpireTime,
                    RemainingDays = remainingDays,
                    MachineCode = currentMachineCode,
                    AuthType = validAuthType
                };
            }
            catch (Exception ex)
            {
                return new AuthorizationStatus
                {
                    IsAuthorized = false,
                    Message = $"检查授权状态时出错: {ex.Message}",
                    ExpireTime = DateTime.MinValue,
                    RemainingDays = 0,
                    MachineCode = string.Empty,
                    AuthType = "个人版"
                };
            }
        }

        #region 私有方法

        /// <summary>
        /// 获取CPU ID
        /// </summary>
        private static string GetCpuId()
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        return obj["ProcessorId"]?.ToString() ?? string.Empty;
                    }
                }
            }
            catch { }
            return string.Empty;
        }

        /// <summary>
        /// 获取主板ID
        /// </summary>
        private static string GetMotherboardId()
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BaseBoard"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        return obj["SerialNumber"]?.ToString() ?? string.Empty;
                    }
                }
            }
            catch { }
            return string.Empty;
        }

        /// <summary>
        /// 获取硬盘ID
        /// </summary>
        private static string GetDiskId()
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_DiskDrive WHERE MediaType='Fixed hard disk media'"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        string serialNumber = obj["SerialNumber"]?.ToString();
                        if (!string.IsNullOrEmpty(serialNumber))
                            return serialNumber.Trim();
                    }
                }
            }
            catch { }
            return string.Empty;
        }

        /// <summary>
        /// 获取MAC地址
        /// </summary>
        private static string GetMacAddress()
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT MACAddress FROM Win32_NetworkAdapter WHERE MACAddress IS NOT NULL AND NetConnectionStatus=2"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        return obj["MACAddress"]?.ToString() ?? string.Empty;
                    }
                }
            }
            catch { }
            return string.Empty;
        }

        /// <summary>
        /// 获取BIOS ID
        /// </summary>
        private static string GetBiosId()
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BIOS"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        return obj["SerialNumber"]?.ToString() ?? string.Empty;
                    }
                }
            }
            catch { }
            return string.Empty;
        }

        /// <summary>
        /// 生成SHA256哈希
        /// </summary>
        private static string GenerateHash(string input)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
                return Convert.ToBase64String(hashBytes);
            }
        }

        /// <summary>
        /// 转换为复杂混合进制格式（100个字符）
        /// </summary>
        private static string ConvertToMixedBase(string input)
        {
            // 自定义字符集：数字+大小写字母+特殊字符
            const string charset = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz!@#$%^&*()_+-=[]{}|;:,.<>?";

            // 多重哈希处理
            string hash1 = GenerateHash(input);
            string hash2 = GenerateHash(hash1 + "XIAOFU_SALT_2024");
            string hash3 = GenerateHash(hash2 + input.Length.ToString());

            // 组合所有哈希
            string combined = hash1 + hash2 + hash3;

            // 转换为字节数组
            byte[] bytes = Encoding.UTF8.GetBytes(combined);

            // 使用自定义字符集进行多进制转换
            StringBuilder result = new StringBuilder();

            // 分组处理字节，每4个字节生成一组字符
            for (int i = 0; i < bytes.Length; i += 4)
            {
                uint value = 0;
                for (int j = 0; j < 4 && i + j < bytes.Length; j++)
                {
                    value = (value << 8) | bytes[i + j];
                }

                // 转换为自定义进制
                while (value > 0 && result.Length < 100)
                {
                    result.Append(charset[(int)(value % (uint)charset.Length)]);
                    value /= (uint)charset.Length;
                }
            }

            // 如果长度不足100,用固定哈希填充（确保稳定性）
            int fillRound = 0;
            while (result.Length < 100)
            {
                // 使用固定的填充逻辑，避免随机性
                string filler = GenerateHash(result.ToString() + fillRound.ToString() + "XIAOFU_FILL_STABLE");
                foreach (char c in filler)
                {
                    if (result.Length >= 100) break;
                    int index = Math.Abs(c.GetHashCode()) % charset.Length;
                    result.Append(charset[index]);
                }
                fillRound++;
            }

            // 确保正好100个字符
            return result.ToString().Substring(0, 100);
        }

        /// <summary>
        /// 解码个人版授权码
        /// </summary>
        private static (bool IsValid, DateTime ExpireTime) DecodePersonalAuthCode(string authCode, string machineCode)
        {
            try
            {
                if (string.IsNullOrEmpty(authCode) || authCode.Length < 50)
                    return (false, DateTime.MinValue);

                // 多层解码
                string decoded = DecryptComplexAuthCode(authCode);
                if (string.IsNullOrEmpty(decoded))
                    return (false, DateTime.MinValue);

                // 分离各部分（格式：machineHash|timestamp|checksum|signature）
                string[] parts = decoded.Split('|');
                if (parts.Length != 4)
                    return (false, DateTime.MinValue);

                string machineHash = parts[0];
                string timestampStr = parts[1];
                string checksum = parts[2];
                string signature = parts[3];

                // 验证机器码哈希（使用前32位）
                string machineHashFull = GenerateComplexHash(machineCode);
                string expectedMachineHash = machineHashFull.Length >= 32 ? machineHashFull.Substring(0, 32) : machineHashFull;
                if (machineHash != expectedMachineHash)
                    return (false, DateTime.MinValue);

                // 解析过期时间
                if (!long.TryParse(timestampStr, out long timestamp))
                    return (false, DateTime.MinValue);

                DateTime expireTime = DateTime.FromBinary(timestamp);

                // 验证校验码
                string checksumFull = GenerateComplexHash(machineHash + timestampStr + "XIAOFU_CHECK");
                string expectedChecksum = checksumFull.Length >= 16 ? checksumFull.Substring(0, 16) : checksumFull;
                if (checksum != expectedChecksum)
                    return (false, DateTime.MinValue);

                // 验证签名
                string signatureFull = GenerateComplexHash(machineHash + timestampStr + checksum + "XIAOFU_SIGN");
                string expectedSignature = signatureFull.Length >= 20 ? signatureFull.Substring(0, 20) : signatureFull;
                if (signature != expectedSignature)
                    return (false, DateTime.MinValue);

                return (true, expireTime);
            }
            catch
            {
                return (false, DateTime.MinValue);
            }
        }

        /// <summary>
        /// 解码通用版授权码
        /// </summary>
        private static (bool IsValid, DateTime ExpireTime) DecodeUniversalAuthCode(string authCode)
        {
            try
            {
                if (string.IsNullOrEmpty(authCode) || authCode.Length < 50)
                    return (false, DateTime.MinValue);

                // 通用授权码标识：以"UNIVERSAL_" 开头
                if (!authCode.StartsWith("UNIVERSAL_"))
                    return (false, DateTime.MinValue);

                // 移除前缀
                string codeWithoutPrefix = authCode.Substring(10);

                // 多层解码
                string decoded = DecryptComplexAuthCode(codeWithoutPrefix);
                if (string.IsNullOrEmpty(decoded))
                    return (false, DateTime.MinValue);

                // 分离各部分（格式：UNIVERSAL|timestamp|checksum|signature）
                string[] parts = decoded.Split('|');
                if (parts.Length != 4 || parts[0] != "UNIVERSAL")
                    return (false, DateTime.MinValue);

                string timestampStr = parts[1];
                string checksum = parts[2];
                string signature = parts[3];

                // 解析过期时间
                if (!long.TryParse(timestampStr, out long timestamp))
                    return (false, DateTime.MinValue);

                DateTime expireTime = DateTime.FromBinary(timestamp);

                // 验证校验码
                string checksumFull = GenerateComplexHash("UNIVERSAL" + timestampStr + "XIAOFU_UNIVERSAL_CHECK");
                string expectedChecksum = checksumFull.Length >= 16 ? checksumFull.Substring(0, 16) : checksumFull;
                if (checksum != expectedChecksum)
                    return (false, DateTime.MinValue);

                // 验证签名
                string signatureFull = GenerateComplexHash("UNIVERSAL" + timestampStr + checksum + "XIAOFU_UNIVERSAL_SIGN");
                string expectedSignature = signatureFull.Length >= 20 ? signatureFull.Substring(0, 20) : signatureFull;
                if (signature != expectedSignature)
                    return (false, DateTime.MinValue);

                return (true, expireTime);
            }
            catch
            {
                return (false, DateTime.MinValue);
            }
        }

        /// <summary>
        /// 生成复杂哈希
        /// </summary>
        private static string GenerateComplexHash(string input)
        {
            // 多重哈希
            string hash1 = GenerateHash(input);
            string hash2 = GenerateHash(hash1 + "COMPLEX_SALT_2024");
            string hash3 = GenerateHash(hash2 + input.Length.ToString());

            return hash1 + hash2 + hash3;
        }

        /// <summary>
        /// 解密复杂授权码
        /// </summary>
        private static string DecryptComplexAuthCode(string encryptedAuthCode)
        {
            try
            {
                // 多层Base64解码
                byte[] layer1 = Convert.FromBase64String(encryptedAuthCode);
                string layer1Str = Encoding.UTF8.GetString(layer1);

                // XOR解密
                byte[] key = Encoding.UTF8.GetBytes("XIAOFU_COMPLEX_KEY_2024_SECURE");
                byte[] encrypted = Convert.FromBase64String(layer1Str);

                for (int i = 0; i < encrypted.Length; i++)
                {
                    encrypted[i] ^= key[i % key.Length];
                }

                return Encoding.UTF8.GetString(encrypted);
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// 加密字符串
        /// </summary>
        private static string EncryptString(string plainText)
        {
            try
            {
                byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                // 简单的XOR加密（实际应用中应使用更强的加密算法）
                byte[] key = Encoding.UTF8.GetBytes("XIAOFUTools2024");

                for (int i = 0; i < plainBytes.Length; i++)
                {
                    plainBytes[i] ^= key[i % key.Length];
                }

                return Convert.ToBase64String(plainBytes);
            }
            catch
            {
                return plainText;
            }
        }

        /// <summary>
        /// 解密字符串
        /// </summary>
        private static string DecryptString(string encryptedText)
        {
            try
            {
                byte[] encryptedBytes = Convert.FromBase64String(encryptedText);
                byte[] key = Encoding.UTF8.GetBytes("XIAOFUTools2024");

                for (int i = 0; i < encryptedBytes.Length; i++)
                {
                    encryptedBytes[i] ^= key[i % key.Length];
                }

                return Encoding.UTF8.GetString(encryptedBytes);
            }
            catch
            {
                return encryptedText;
            }
        }

        #endregion
    }
}
