using System.Collections.Generic;

namespace XIAOFUTools.Features.User.AIAssistant.Database
{
    public partial class DatabaseManager
    {
        public AIServiceConfig GetDefaultService()
            => _serviceRepository.GetDefault();

        public List<AIServiceConfig> GetAllServices()
            => _serviceRepository.GetAll();

        public void InsertService(AIServiceConfig service)
            => _serviceRepository.Insert(service);

        public void UpdateService(AIServiceConfig service)
            => _serviceRepository.Update(service);

        public void DeleteService(int serviceId)
            => _serviceRepository.Delete(serviceId);

        public void ResetToDefaultServices()
            => _serviceRepository.ResetToDefaults();

        public void SetDefaultService(int serviceId)
            => _serviceRepository.SetDefault(serviceId);
    }
}
