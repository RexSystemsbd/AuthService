using AuthMicroservice.Repository;

namespace AuthMicroservice.Service
{
    public interface IApplicationService
    {
        Task<IEnumerable<Application>> GetApplication(string appKey);
        Task<Application> RegisterApplication(string name);
        bool ValidateAppKeyAndSecret(string appKey, string appSecret);
        //Application GetApplication(string appKey);
        //bool ValidateAppKeyAndSecret(string appKey, string appSecret);
    }

    public class ApplicationService : IApplicationService
    {
        private readonly IApplicationRepository _applicationRepository;

        public ApplicationService(IApplicationRepository applicationRepository)
        {
            _applicationRepository = applicationRepository;
        }

        public async Task<IEnumerable<Application>> GetApplication(string appKey)
        {
            return await _applicationRepository.FindAsync(a=>a.AppKey == appKey);
        }

        public Task<Application> RegisterApplication(string name)
        {
            var application = new Application
            {
                Id = Guid.NewGuid(),
                Name = name,
                AppKey = Guid.NewGuid().ToString(),
                AppSecret = Guid.NewGuid().ToString()
            };

            _applicationRepository.AddAsync(application);
            return Task.FromResult(application);
        }

        public bool ValidateAppKeyAndSecret(string appKey, string appSecret)
        {
           return _applicationRepository.FindAsync(a=>a.AppKey==appKey && a.AppSecret==appSecret) != null;
        }
    }


}
