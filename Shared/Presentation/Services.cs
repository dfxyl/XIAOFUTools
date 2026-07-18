using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Threading.Tasks;

namespace XIAOFUTools.Shared.Presentation
{
    internal interface IUserDialogService
    {
        MessageBoxResult Show(
            string message,
            string title = null,
            MessageBoxButton buttons = MessageBoxButton.OK,
            MessageBoxImage image = MessageBoxImage.None);
    }

    internal interface IFileDialogService
    {
        string OpenFile(string filter, string initialDirectory = null, string title = null);
        IReadOnlyList<string> OpenFiles(string filter, string initialDirectory = null, string title = null);
        string SaveFile(
            string filter,
            string defaultFileName = null,
            string initialDirectory = null,
            string title = null,
            string defaultExtension = null);
        string SelectFolder(string description, string initialDirectory = null);
        string SelectGeodatabase(string title, string initialLocation = null);
    }

    internal interface IClipboardService
    {
        Task<bool> TrySetTextAsync(string text, int maxRetries = 3, int retryDelayMilliseconds = 100);
    }

    internal interface IExternalProcessService
    {
        void Open(string target);
    }

    internal interface IUiThreadService
    {
        void Invoke(Action action);
        void InvokeOrRun(Action action);
        Task InvokeAsync(Action action);
        Task<T> InvokeAsync<T>(Func<T> action);
        void Post(Action action);
        void PostBackground(Action action);
        void PostLoaded(Action action);
    }

    internal interface IProgressDialogSession : IDisposable
    {
    }

    internal interface ICancelableProgressDialogSession : IProgressDialogSession
    {
        CancelableProgressorSource ProgressorSource { get; }
    }

    internal interface IProgressDialogService
    {
        IProgressDialogSession Show(string title);
        ICancelableProgressDialogSession CreateCancelable(string title, string cancelButtonText, int max, bool canCancel);
    }

    internal interface IWindowLifecycleService
    {
        void CloseProWindow(string title);
        void CloseWindow(string title);
    }

    internal sealed class WpfUserDialogService : IUserDialogService
    {
        public MessageBoxResult Show(string message, string title, MessageBoxButton buttons, MessageBoxImage image) =>
            MessageBox.Show(message, title, buttons, image);
    }

    internal sealed class WindowsFileDialogService : IFileDialogService
    {
        public string OpenFile(string filter, string initialDirectory = null, string title = null)
        {
            var dialog = new OpenFileDialog
            {
                Filter = filter ?? "所有文件 (*.*)|*.*",
                InitialDirectory = initialDirectory ?? string.Empty,
                Title = title ?? string.Empty
            };
            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        public IReadOnlyList<string> OpenFiles(string filter, string initialDirectory = null, string title = null)
        {
            var dialog = new OpenFileDialog
            {
                Filter = filter ?? "所有文件 (*.*)|*.*",
                InitialDirectory = initialDirectory ?? string.Empty,
                Title = title ?? string.Empty,
                Multiselect = true,
                CheckFileExists = true
            };
            return dialog.ShowDialog() == true ? dialog.FileNames : Array.Empty<string>();
        }

        public string SaveFile(
            string filter,
            string defaultFileName = null,
            string initialDirectory = null,
            string title = null,
            string defaultExtension = null)
        {
            var dialog = new SaveFileDialog
            {
                Filter = filter ?? "所有文件 (*.*)|*.*",
                FileName = defaultFileName ?? string.Empty,
                InitialDirectory = initialDirectory ?? string.Empty,
                Title = title ?? string.Empty,
                DefaultExt = defaultExtension ?? string.Empty
            };
            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        public string SelectFolder(string description, string initialDirectory = null)
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = description ?? "选择文件夹",
                SelectedPath = initialDirectory ?? string.Empty,
                ShowNewFolderButton = true
            };
            return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK ? dialog.SelectedPath : null;
        }

        public string SelectGeodatabase(string title, string initialLocation = null)
        {
            var dialog = new OpenItemDialog
            {
                Title = title ?? "选择文件地理数据库",
                InitialLocation = initialLocation,
                MultiSelect = false,
                Filter = ItemFilters.Geodatabases
            };
            return dialog.ShowDialog() == true ? dialog.Items.FirstOrDefault()?.Path : null;
        }
    }

    internal sealed class ClipboardService : IClipboardService
    {
        public async Task<bool> TrySetTextAsync(
            string text,
            int maxRetries = 3,
            int retryDelayMilliseconds = 100)
        {
            if (maxRetries <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxRetries));
            }

            if (retryDelayMilliseconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(retryDelayMilliseconds));
            }

            for (var attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    await InvokeOnUiThreadAsync(() =>
                    {
                        Clipboard.Clear();
                        Clipboard.SetText(text ?? string.Empty);
                    });
                    return true;
                }
                catch (COMException exception) when (
                    exception.HResult == unchecked((int)0x800401D0) &&
                    attempt < maxRetries - 1)
                {
                    await Task.Delay(retryDelayMilliseconds * (attempt + 1));
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        private static Task InvokeOnUiThreadAsync(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is null || dispatcher.CheckAccess())
            {
                action();
                return Task.CompletedTask;
            }

            return dispatcher.InvokeAsync(action).Task;
        }
    }

    internal sealed class ExternalProcessService : IExternalProcessService
    {
        public void Open(string target)
        {
            if (string.IsNullOrWhiteSpace(target))
            {
                throw new ArgumentException("目标不能为空。", nameof(target));
            }

            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
    }

    internal sealed class WpfUiThreadService : IUiThreadService
    {
        public void Invoke(Action action)
        {
            ArgumentNullException.ThrowIfNull(action);
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is null)
            {
                return;
            }

            if (dispatcher.CheckAccess())
            {
                action();
                return;
            }

            dispatcher.Invoke(action);
        }

        public void InvokeOrRun(Action action)
        {
            ArgumentNullException.ThrowIfNull(action);
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is null || dispatcher.CheckAccess())
            {
                action();
                return;
            }

            dispatcher.Invoke(action);
        }

        public Task InvokeAsync(Action action)
        {
            ArgumentNullException.ThrowIfNull(action);
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is null)
            {
                return Task.CompletedTask;
            }

            if (dispatcher.CheckAccess())
            {
                action();
                return Task.CompletedTask;
            }

            return dispatcher.InvokeAsync(action).Task;
        }

        public Task<T> InvokeAsync<T>(Func<T> action)
        {
            ArgumentNullException.ThrowIfNull(action);
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is null || dispatcher.CheckAccess())
            {
                return Task.FromResult(action());
            }

            return dispatcher.InvokeAsync(action).Task;
        }

        public void Post(Action action)
        {
            Post(action, DispatcherPriority.Normal);
        }

        public void PostBackground(Action action)
        {
            Post(action, DispatcherPriority.Background);
        }

        public void PostLoaded(Action action)
        {
            Post(action, DispatcherPriority.Loaded);
        }

        private static void Post(Action action, DispatcherPriority priority)
        {
            ArgumentNullException.ThrowIfNull(action);
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is null)
            {
                return;
            }

            if (dispatcher.CheckAccess())
            {
                action();
                return;
            }

            _ = dispatcher.BeginInvoke(action, priority);
        }
    }

    internal sealed class ArcGisProgressDialogService : IProgressDialogService
    {
        public IProgressDialogSession Show(string title)
        {
            var dialog = new ProgressDialog(title);
            dialog.Show();
            return new ProgressDialogSession(dialog);
        }

        public ICancelableProgressDialogSession CreateCancelable(string title, string cancelButtonText, int max, bool canCancel)
        {
            var dialog = new ProgressDialog(title, cancelButtonText, (uint)max, canCancel);
            return new CancelableProgressDialogSession(dialog, new CancelableProgressorSource(dialog));
        }

        private class ProgressDialogSession : IProgressDialogSession
        {
            private readonly ProgressDialog _dialog;
            private bool _disposed;

            internal ProgressDialogSession(ProgressDialog dialog)
            {
                _dialog = dialog;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _dialog.Dispose();
            }
        }

        private sealed class CancelableProgressDialogSession : ProgressDialogSession, ICancelableProgressDialogSession
        {
            internal CancelableProgressDialogSession(ProgressDialog dialog, CancelableProgressorSource progressorSource)
                : base(dialog)
            {
                ProgressorSource = progressorSource;
            }

            public CancelableProgressorSource ProgressorSource { get; }
        }
    }

    internal sealed class WpfWindowLifecycleService : IWindowLifecycleService
    {
        public void CloseProWindow(string title)
        {
            CloseWindowCore(title, requireProWindow: true);
        }

        public void CloseWindow(string title)
        {
            CloseWindowCore(title, requireProWindow: false);
        }

        private static void CloseWindowCore(string title, bool requireProWindow)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return;
            }

            void CloseMatchingWindow()
            {
                var application = Application.Current;
                if (application is null)
                {
                    return;
                }

                foreach (Window window in application.Windows)
                {
                    if ((!requireProWindow || window is ArcGIS.Desktop.Framework.Controls.ProWindow) &&
                        string.Equals(window.Title, title, StringComparison.Ordinal))
                    {
                        window.Close();
                        break;
                    }
                }
            }

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is null || dispatcher.CheckAccess())
            {
                CloseMatchingWindow();
                return;
            }

            dispatcher.Invoke(CloseMatchingWindow);
        }
    }

    /// <summary>
    /// UI 边界的应用级组合入口。生产环境使用 WPF/Windows 实现，测试可替换为无界面实现。
    /// </summary>
    internal static class PresentationServices
    {
        private static IUserDialogService _dialogs = new WpfUserDialogService();
        private static IFileDialogService _files = new WindowsFileDialogService();
        private static IClipboardService _clipboard = new ClipboardService();
        private static IExternalProcessService _externalProcesses = new ExternalProcessService();
        private static IUiThreadService _uiThread = new WpfUiThreadService();
        private static IProgressDialogService _progressDialogs = new ArcGisProgressDialogService();
        private static IWindowLifecycleService _windows = new WpfWindowLifecycleService();

        internal static IUserDialogService Dialogs => _dialogs;
        internal static IFileDialogService Files => _files;
        internal static IClipboardService Clipboard => _clipboard;
        internal static IExternalProcessService ExternalProcesses => _externalProcesses;
        internal static IUiThreadService UiThread => _uiThread;
        internal static IProgressDialogService ProgressDialogs => _progressDialogs;
        internal static IWindowLifecycleService Windows => _windows;

        internal static void Configure(
            IUserDialogService dialogs = null,
            IFileDialogService files = null,
            IClipboardService clipboard = null,
            IExternalProcessService externalProcesses = null,
            IUiThreadService uiThread = null,
            IProgressDialogService progressDialogs = null,
            IWindowLifecycleService windows = null)
        {
            _dialogs = dialogs ?? _dialogs;
            _files = files ?? _files;
            _clipboard = clipboard ?? _clipboard;
            _externalProcesses = externalProcesses ?? _externalProcesses;
            _uiThread = uiThread ?? _uiThread;
            _progressDialogs = progressDialogs ?? _progressDialogs;
            _windows = windows ?? _windows;
        }
    }
}
