using AuthMicroservice.Model;
using AuthMicroservice.Repository;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AuthMicroservice.Service
{
    public interface ISmtpConfigService
    {
        Task<SmtpConfig> GetSmtpConfigAsync(string id);
        Task<IEnumerable<SmtpConfig>> GetSmtpConfigsAsync();
        Task<SmtpConfig> CreateSmtpConfigAsync(SmtpConfig smtpConfig);
        Task UpdateSmtpConfigAsync(string id, SmtpConfig smtpConfig);
        Task DeleteSmtpConfigAsync(string id);
        Task<SmtpConfig> GetSmtpConfigByApplicationIdAsync(Guid applicationId);
    }

    public class SmtpConfigService : ISmtpConfigService
    {
        private readonly ISmtpConfigRepository _smtpConfigRepository;

        public SmtpConfigService(ISmtpConfigRepository smtpConfigRepository)
        {
            _smtpConfigRepository = smtpConfigRepository;
        }

        public async Task<SmtpConfig> GetSmtpConfigAsync(string id)
        {
            return await _smtpConfigRepository.GetByIdAsync(id);
        }

        public async Task<IEnumerable<SmtpConfig>> GetSmtpConfigsAsync()
        {
            return await _smtpConfigRepository.GetAllAsync();
        }

        public async Task<SmtpConfig> CreateSmtpConfigAsync(SmtpConfig smtpConfig)
        {
            return await _smtpConfigRepository.AddAsync(smtpConfig);
        }

        public async Task UpdateSmtpConfigAsync(string id, SmtpConfig smtpConfig)
        {
            var existingSmtpConfig = await _smtpConfigRepository.GetByIdAsync(id);
            if (existingSmtpConfig == null)
            {
                return;
            }
            existingSmtpConfig.Host = smtpConfig.Host;
            existingSmtpConfig.Port = smtpConfig.Port;
            existingSmtpConfig.Username = smtpConfig.Username;
            existingSmtpConfig.Password = smtpConfig.Password;
            existingSmtpConfig.EnableSsl = smtpConfig.EnableSsl;
            existingSmtpConfig.FromAddress = smtpConfig.FromAddress;
            existingSmtpConfig.FromName = smtpConfig.FromName;
            existingSmtpConfig.ApplicationId = smtpConfig.ApplicationId;
            await _smtpConfigRepository.UpdateAsync(existingSmtpConfig);
        }

        public async Task DeleteSmtpConfigAsync(string id)
        {
            var smtpConfig = await _smtpConfigRepository.GetByIdAsync(id);
            if (smtpConfig != null)
            {
                await _smtpConfigRepository.DeleteAsync(smtpConfig);
            }
        }

        public async Task<SmtpConfig> GetSmtpConfigByApplicationIdAsync(Guid applicationId)
        {
            return await _smtpConfigRepository.GetByApplicationIdAsync(applicationId);
        }
    }
}
