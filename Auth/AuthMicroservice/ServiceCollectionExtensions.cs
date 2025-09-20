using AuthMicroservice.Repository;
using AuthMicroservice.Service;

namespace AuthMicroservice
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {

            // Register the generic repository and the specific repository
            services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
            services.AddScoped<IApplicationRepository, ApplicationRepository>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IUserRoleRepository, UserRoleRepository>();
            services.AddScoped<ISmtpConfigRepository, SmtpConfigRepository>();
            services.AddScoped<IEmailHistoryRepository, EmailHistoryRepository>();
            services.AddScoped<ISubscriberRepository, SubscriberRepository>();

            // Add service registrations here
            //services.AddScoped<HttpClient, HttpClient>();
            services.AddScoped<ILoginService,LoginService>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IApplicationService, ApplicationService>();
            services.AddScoped<ISmtpConfigService, SmtpConfigService>();
            services.AddScoped<IEmailService, EmailService>();
            services.AddScoped<ISubscriberService, SubscriberService>();
            services.AddHttpClient();

            services.AddMemoryCache();

            return services;
        }
    }
}
