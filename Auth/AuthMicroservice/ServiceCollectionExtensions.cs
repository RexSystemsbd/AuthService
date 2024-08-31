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

            // Add service registrations here

            services.AddScoped<ILoginService,LoginService>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IApplicationService, ApplicationService>();

          

            return services;
        }
    }
}
