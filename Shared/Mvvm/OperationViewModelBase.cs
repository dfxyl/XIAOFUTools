using System;
using System.Text;
using System.Threading;
using XIAOFUTools.Shared.Diagnostics;

namespace XIAOFUTools.Shared.Mvvm
{
    internal abstract class OperationViewModelBase : ObservableObject, IOperationProgress, IDisposable
    {
        private readonly StringBuilder _log = new StringBuilder();
        private CancellationTokenSource _cancellation;
        private bool _isBusy;
        private int _progress;
        private string _statusMessage = string.Empty;
        private string _logContent = string.Empty;

        public bool IsBusy
        {
            get => _isBusy;
            private set => SetProperty(ref _isBusy, value);
        }

        public int Progress
        {
            get => _progress;
            private set => SetProperty(ref _progress, Math.Clamp(value, 0, 100));
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value ?? string.Empty);
        }

        public string LogContent
        {
            get => _logContent;
            private set => SetProperty(ref _logContent, value ?? string.Empty);
        }

        protected CancellationToken BeginOperation(string initialStatus = null)
        {
            _cancellation?.Dispose();
            _cancellation = new CancellationTokenSource();
            IsBusy = true;
            Progress = 0;
            StatusMessage = initialStatus ?? string.Empty;
            return _cancellation.Token;
        }

        protected void EndOperation(string finalStatus = null)
        {
            IsBusy = false;
            if (finalStatus != null)
            {
                StatusMessage = finalStatus;
            }
        }

        public void CancelOperation() => _cancellation?.Cancel();

        public void Report(int percentage, string status = null)
        {
            Progress = percentage;
            if (status != null)
            {
                StatusMessage = status;
            }
        }

        protected void AppendLog(AppLogLevel level, string message)
        {
            _log.Append('[').Append(DateTime.Now.ToString("HH:mm:ss")).Append("] ")
                .Append(level).Append(": ").AppendLine(message ?? string.Empty);
            LogContent = _log.ToString();
        }

        public virtual void Dispose()
        {
            _cancellation?.Cancel();
            _cancellation?.Dispose();
        }
    }
}
