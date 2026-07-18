using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace XIAOFUTools.Shared.Mvvm
{
    internal sealed class AsyncRelayCommand : ICommand, IDisposable
    {
        private readonly Func<CancellationToken, Task> _execute;
        private readonly Func<bool> _canExecute;
        private readonly Action<Exception> _onError;
        private CancellationTokenSource _cancellation;
        private bool _isRunning;

        public AsyncRelayCommand(
            Func<CancellationToken, Task> execute,
            Func<bool> canExecute = null,
            Action<Exception> onError = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
            _onError = onError;
        }

        public AsyncRelayCommand(Func<Task> execute, Func<bool> canExecute = null, Action<Exception> onError = null)
            : this(_ => execute(), canExecute, onError)
        {
        }

        public bool IsRunning => _isRunning;

        public bool CanExecute(object parameter) => !_isRunning && (_canExecute?.Invoke() ?? true);

        public async void Execute(object parameter)
        {
            if (!CanExecute(parameter))
            {
                return;
            }

            _isRunning = true;
            _cancellation = new CancellationTokenSource();
            NotifyCanExecuteChanged();

            try
            {
                await _execute(_cancellation.Token);
            }
            catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                if (_onError == null)
                {
                    throw;
                }

                _onError(ex);
            }
            finally
            {
                _cancellation.Dispose();
                _cancellation = null;
                _isRunning = false;
                NotifyCanExecuteChanged();
            }
        }

        public void Cancel() => _cancellation?.Cancel();

        public event EventHandler CanExecuteChanged;

        public void NotifyCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);

        public void Dispose()
        {
            _cancellation?.Cancel();
            _cancellation?.Dispose();
        }
    }
}
