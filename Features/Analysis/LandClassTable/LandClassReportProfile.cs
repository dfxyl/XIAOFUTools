using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;

namespace XIAOFUTools.Features.Analysis.LandClassTable
{
    internal sealed class LandClassReportProfile : INotifyPropertyChanged
    {
        private string _name = "默认";
        private string _reportYear = DateTime.Now.Year.ToString();
        private string _locationName = string.Empty;
        private string _reportCompany = string.Empty;
        private string _preparedBy = string.Empty;
        private string _reviewedBy = string.Empty;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string ReportYear
        {
            get => _reportYear;
            set => SetProperty(ref _reportYear, value);
        }

        public string LocationName
        {
            get => _locationName;
            set => SetProperty(ref _locationName, value);
        }

        public string ReportCompany
        {
            get => _reportCompany;
            set => SetProperty(ref _reportCompany, value);
        }

        public string PreparedBy
        {
            get => _preparedBy;
            set => SetProperty(ref _preparedBy, value);
        }

        public string ReviewedBy
        {
            get => _reviewedBy;
            set => SetProperty(ref _reviewedBy, value);
        }

        public string DisplayName => Name;

        public LandClassReportProfile Clone()
        {
            return new LandClassReportProfile
            {
                Name = Name,
                ReportYear = ReportYear,
                LocationName = LocationName,
                ReportCompany = ReportCompany,
                PreparedBy = PreparedBy,
                ReviewedBy = ReviewedBy
            };
        }

        public override string ToString() => Name;

        public event PropertyChangedEventHandler PropertyChanged;

        private void SetProperty(ref string field, string value, [CallerMemberName] string propertyName = null)
        {
            if (field == value)
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            if (propertyName == nameof(Name))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ToString)));
            }
        }
    }

    internal static class LandClassReportProfileStore
    {
        private static readonly string StorePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "XIAOFUTools",
            "LandClassTable",
            "report_profiles.json");

        public static ObservableCollection<LandClassReportProfile> Load()
        {
            try
            {
                if (!File.Exists(StorePath))
                {
                    return CreateDefaultProfiles();
                }

                var profiles = JsonConvert.DeserializeObject<List<LandClassReportProfile>>(File.ReadAllText(StorePath))
                    ?? new List<LandClassReportProfile>();
                if (profiles.Count == 0)
                {
                    return CreateDefaultProfiles();
                }

                return new ObservableCollection<LandClassReportProfile>(profiles.Where(x => !string.IsNullOrWhiteSpace(x.Name)));
            }
            catch
            {
                return CreateDefaultProfiles();
            }
        }

        public static void Save(IEnumerable<LandClassReportProfile> profiles)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StorePath) ?? Environment.CurrentDirectory);
            string json = JsonConvert.SerializeObject(profiles.ToList(), Formatting.Indented);
            File.WriteAllText(StorePath, json);
        }

        private static ObservableCollection<LandClassReportProfile> CreateDefaultProfiles()
        {
            return new ObservableCollection<LandClassReportProfile>
            {
                new LandClassReportProfile()
            };
        }
    }
}
