using System;
using System.Windows.Input;

namespace XIAOFUTools.Shared.Mvvm
{
    public sealed class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;
        private readonly Action<object> _parameterizedExecute;
        private readonly Predicate<object> _parameterizedCanExecute;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public RelayCommand(Action<object> execute, Predicate<object> canExecute = null)
        {
            _parameterizedExecute = execute ?? throw new ArgumentNullException(nameof(execute));
            _parameterizedCanExecute = canExecute;
        }

        public bool CanExecute(object parameter) =>
            _parameterizedCanExecute?.Invoke(parameter) ?? _canExecute?.Invoke() ?? true;

        public void Execute(object parameter)
        {
            if (_parameterizedExecute != null)
            {
                _parameterizedExecute(parameter);
                return;
            }

            _execute();
        }

        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public void NotifyCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();

        public void RaiseCanExecuteChanged() => NotifyCanExecuteChanged();
    }

    public sealed class RelayCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
        private readonly Func<T, bool> _canExecute;

        public RelayCommand(Action<T> execute, Func<T, bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter) => _canExecute?.Invoke(ConvertParameter(parameter)) ?? true;

        public void Execute(object parameter) => _execute(ConvertParameter(parameter));

        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public void NotifyCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();

        public void RaiseCanExecuteChanged() => NotifyCanExecuteChanged();

        private static T ConvertParameter(object parameter)
        {
            if (parameter is T value)
            {
                return value;
            }

            return parameter == null ? default : (T)Convert.ChangeType(parameter, typeof(T));
        }
    }
}
