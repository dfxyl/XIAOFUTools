using System;
using System.Windows;
using System.Windows.Media;
using ArcGIS.Desktop.Framework.Controls;
using XIAOFUTools.Features.User.AIAssistant.Database;
using XIAOFUTools.Features.User.AIAssistant.Services;

namespace XIAOFUTools.Features.User.AIAssistant
{
    /// <summary>
    /// 模型编辑对话框
    /// </summary>
    public partial class ModelEditDialog : ProWindow
    {
        /// <summary>
        /// 显示名称
        /// </summary>
        public string ModelName => txtName.Text.Trim();
        
        /// <summary>
        /// API端点
        /// </summary>
        public string ApiEndpoint => txtEndpoint.Text.Trim();
        
        /// <summary>
        /// 模型ID
        /// </summary>
        public string ModelId => txtModelId.Text.Trim();
        
        /// <summary>
        /// API密钥
        /// </summary>
        public string ApiKey
        {
            get
            {
                return txtApiKey.Text.Trim();
            }
        }
        
        /// <summary>
        /// 最大Tokens
        /// </summary>
        public int MaxTokens
        {
            get
            {
                if (int.TryParse(txtMaxTokens.Text.Trim(), out int val))
                    return val;
                return 4096;
            }
        }

        /// <summary>
        /// 上下文窗口长度
        /// </summary>
        public int ContextWindowTokens
        {
            get
            {
                if (int.TryParse(txtContextWindowTokens.Text.Trim(), out int val))
                    return val;
                return 0;
            }
        }
        
        /// <summary>
        /// Temperature
        /// </summary>
        public double Temperature
        {
            get
            {
                if (double.TryParse(txtTemperature.Text.Trim(), out double val))
                    return val;
                return 0.7;
            }
        }
        
        /// <summary>
        /// 是否支持视觉
        /// </summary>
        public bool SupportsVision => chkVision.IsChecked ?? false;
        
        /// <summary>
        /// 新建模式构造函数
        /// </summary>
        public ModelEditDialog()
        {
            InitializeComponent();
            txtTitle.Text = "添加模型";
        }
        
        /// <summary>
        /// 编辑模式构造函数
        /// </summary>
        public ModelEditDialog(ModelConfigViewModel model) : this()
        {
            txtTitle.Text = "编辑模型";
            
            txtName.Text = model.Name;
            txtEndpoint.Text = model.ApiEndpoint;
            txtModelId.Text = model.ModelName;
            txtApiKey.Text = model.ApiKey;
            
            txtMaxTokens.Text = model.MaxTokens.ToString();
            txtContextWindowTokens.Text = model.ContextWindowTokens > 0 ? model.ContextWindowTokens.ToString() : string.Empty;
            txtTemperature.Text = model.Temperature.ToString("F1");
            chkVision.IsChecked = model.SupportsVision;
        }
        
        /// <summary>
        /// 保存
        /// </summary>
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // 验证必填项
            if (string.IsNullOrWhiteSpace(ModelName))
            {
                MessageBox.Show("请输入显示名称", "验证失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtName.Focus();
                return;
            }
            
            if (string.IsNullOrWhiteSpace(ApiEndpoint))
            {
                MessageBox.Show("请输入API端点", "验证失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtEndpoint.Focus();
                return;
            }
            
            if (string.IsNullOrWhiteSpace(ModelId))
            {
                MessageBox.Show("请输入模型ID", "验证失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtModelId.Focus();
                return;
            }
            
            // API密钥可选（本地模型不需要）
            if (string.IsNullOrWhiteSpace(txtApiKey.Text))
            {
                txtApiKey.Text = "local-no-key";
            }
            
            DialogResult = true;
            Close();
        }
        
        /// <summary>
        /// 取消
        /// </summary>
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// 测试API连通性
        /// </summary>
        private async void BtnTestApi_Click(object sender, RoutedEventArgs e)
        {
            // 验证必填字段
            if (string.IsNullOrWhiteSpace(txtEndpoint.Text) || string.IsNullOrWhiteSpace(txtModelId.Text))
            {
                ShowTestResult("请先填写 API 端点和模型 ID", false);
                return;
            }

            btnTestApi.IsEnabled = false;
            btnTestApi.Content = "测试中...";
            ShowTestResult("正在连接...", null);

            try
            {
                var config = new AIServiceConfig
                {
                    ApiEndpoint = txtEndpoint.Text.Trim(),
                    ModelName = txtModelId.Text.Trim(),
                    ApiKey = ApiKey,
                    MaxTokens = 50,
                    ContextWindowTokens = 0,
                    Temperature = 0.1
                };

                var service = new OpenAICompatibleService(config);
                var isValid = await service.ValidateApiKeyAsync();
                service.Dispose();

                if (isValid)
                {
                    ShowTestResult("连接成功，API 可用", true);
                }
                else
                {
                    ShowTestResult("连接失败，请检查端点和密钥", false);
                }
            }
            catch (Exception ex)
            {
                var msg = ex.Message;
                if (msg.Length > 100) msg = msg.Substring(0, 100) + "...";
                ShowTestResult($"连接失败: {msg}", false);
            }
            finally
            {
                btnTestApi.IsEnabled = true;
                btnTestApi.Content = "测试连接";
            }
        }

        private void ShowTestResult(string message, bool? success)
        {
            txtTestResult.Text = message;
            txtTestResult.Visibility = Visibility.Visible;
            
            if (success == true)
                txtTestResult.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#16a34a"));
            else if (success == false)
                txtTestResult.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#dc2626"));
            else
                txtTestResult.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6b7280"));
        }
    }
}
