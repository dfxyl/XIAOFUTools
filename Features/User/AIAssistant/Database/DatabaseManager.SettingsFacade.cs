namespace XIAOFUTools.Features.User.AIAssistant.Database
{
    public partial class DatabaseManager
    {
        public SearchSettings GetSearchSettings()
            => _settingsRepository.GetSearchSettings();

        public ToolExecutionSettings GetToolExecutionSettings()
            => _settingsRepository.GetToolExecutionSettings();

        public void SaveSearchSettings(SearchSettings settings)
            => _settingsRepository.SaveSearchSettings(settings);

        public void SaveToolExecutionSettings(ToolExecutionSettings settings)
            => _settingsRepository.SaveToolExecutionSettings(settings);
    }
}
