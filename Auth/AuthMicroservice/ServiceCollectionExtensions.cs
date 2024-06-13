using AuthMicroservice.Repository;
using AuthMicroservice.Service;

namespace AuthMicroservice
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            // Register your application services here
            // Register the generic repository and the specific repository
            services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IApplicationRepository, ApplicationRepository>();


            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IApplicationService, ApplicationService>();

            // Add other service registrations here

            return services;
        }
    }
}
