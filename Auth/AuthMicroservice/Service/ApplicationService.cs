using AuthMicroservice.Repository;

namespace AuthMicroservice.Service
{
    public interface IApplicationService
    {
        //Task<IEnumerable<Application>> GetApplication(string appKey);
        //Task<Application> RegisterApplication(string name);
        //bool ValidateAppKeyAndSecret(string appKey, string appSecret);
        Task<IEnumerable<Application>> GetApplicationsAsync(string appKey);
        Task<Application> RegisterApplicationAsync(string name);
        Task<bool> ValidateAppKeyAndSecretAsync(string appKey, string appSecret);
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

        public async Task<IEnumerable<Application>> GetApplicationsAsync(string appKey)
        {
            // Assuming FindAsync returns IEnumerable<Application>
            return await _applicationRepository.FindAsync(a => a.AppKey == appKey);
        }

        public async Task<Application> RegisterApplicationAsync(string name)
        {
            var application = new Application
            {
                Id = Guid.NewGuid(),
                Name = name,
                AppKey = Guid.NewGuid().ToString(),
                AppSecret = Guid.NewGuid().ToString(),
                Description = "Default description" // Provide a value for the Description column
            };

            try
            {
                await _applicationRepository.AddAsync(application);
                return application;
            }
            catch (Exception ex)
            {
                // Log the exception for debugging purposes
                // Use your preferred logging framework here
                // Example: _logger.LogError(ex, "An error occurred while registering the application");
                throw new InvalidOperationException("An error occurred while registering the application.", ex);
            }
        }

        public async Task<bool> ValidateAppKeyAndSecretAsync(string appKey, string appSecret)
        {
            // Assuming FindAsync returns IEnumerable<Application>
            var app = (await _applicationRepository.FindAsync(a => a.AppKey == appKey && a.AppSecret == appSecret)).FirstOrDefault();
            return app != null;
        }
    }
}
