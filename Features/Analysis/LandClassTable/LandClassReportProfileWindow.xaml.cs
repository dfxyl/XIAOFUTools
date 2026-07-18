using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Features.Analysis.LandClassTable
{
    public partial class LandClassReportProfileWindow : Window
    {
        internal LandClassReportProfileWindow(ObservableCollection<LandClassReportProfile> profiles, LandClassReportProfile selectedProfile)
        {
            InitializeComponent();
            DataContext = new LandClassReportProfileWindowViewModel(profiles, selectedProfile, this);
        }

        internal LandClassReportProfile SelectedProfile =>
            ((LandClassReportProfileWindowViewModel)DataContext).SelectedProfile;
    }

    internal sealed class LandClassReportProfileWindowViewModel : INotifyPropertyChanged
    {
        private readonly Window _window;
        private LandClassReportProfile _selectedProfile;

        internal LandClassReportProfileWindowViewModel(
            ObservableCollection<LandClassReportProfile> profiles,
            LandClassReportProfile selectedProfile,
            Window window)
        {
            Profiles = profiles;
            _selectedProfile = selectedProfile ?? profiles.FirstOrDefault() ?? new LandClassReportProfile();
            _window = window;
            AddCommand = new RelayCommand(AddProfile);
            DeleteCommand = new RelayCommand(DeleteProfile, () => Profiles.Count > 1);
            SaveCommand = new RelayCommand(SaveProfiles);
        }

        public ObservableCollection<LandClassReportProfile> Profiles { get; }

        public LandClassReportProfile SelectedProfile
        {
            get => _selectedProfile;
            set
            {
                _selectedProfile = value;
                OnPropertyChanged();
            }
        }

        public ICommand AddCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand SaveCommand { get; }

        public event PropertyChangedEventHandler PropertyChanged;

        private void AddProfile()
        {
            string name = CreateUniqueName("新配置");
            var profile = (SelectedProfile ?? new LandClassReportProfile()).Clone();
            profile.Name = name;
            Profiles.Add(profile);
            SelectedProfile = profile;
        }

        private void DeleteProfile()
        {
            if (Profiles.Count <= 1 || SelectedProfile == null)
            {
                return;
            }

            int index = Profiles.IndexOf(SelectedProfile);
            Profiles.Remove(SelectedProfile);
            SelectedProfile = Profiles[Math.Max(0, Math.Min(index, Profiles.Count - 1))];
        }

        private void SaveProfiles()
        {
            foreach (var profile in Profiles)
            {
                if (string.IsNullOrWhiteSpace(profile.Name))
                {
                    profile.Name = CreateUniqueName("未命名");
                }
            }

            LandClassReportProfileStore.Save(Profiles);
            _window.DialogResult = true;
            _window.Close();
        }

        private string CreateUniqueName(string prefix)
        {
            int index = 1;
            string name = prefix;
            while (Profiles.Any(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                index++;
                name = $"{prefix}{index}";
            }

            return name;
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    
}
