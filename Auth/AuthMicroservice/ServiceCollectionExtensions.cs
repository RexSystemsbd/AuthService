using System;
using System.Net.Http;
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
            services.AddScoped<IFacebookConfigRepository, FacebookConfigRepository>();
            services.AddScoped<IWhatsappConfigRepository, WhatsappConfigRepository>();
            services.AddScoped<IEmailHistoryRepository, EmailHistoryRepository>();
            services.AddScoped<ISubscriberRepository, SubscriberRepository>();
            services.AddScoped<IContactRepository, ContactRepository>();
            services.AddScoped<ICampaignRepository, CampaignRepository>();
            services.AddScoped<IContactActivityRepository, ContactActivityRepository>();
            services.AddScoped<ISegmentRepository, SegmentRepository>();
            services.AddScoped<IFeedbackSurveyRepository, FeedbackSurveyRepository>();
            services.AddScoped<IFeedbackResponseRepository, FeedbackResponseRepository>();
            services.AddScoped<IOnboardingTemplateRepository, OnboardingTemplateRepository>();
            services.AddScoped<ICustomerOnboardingRepository, CustomerOnboardingRepository>();

            // Add service registrations here
            //services.AddScoped<HttpClient, HttpClient>();
            services.AddScoped<ILoginService,LoginService>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IApplicationService, ApplicationService>();
            services.AddScoped<ISmtpConfigService, SmtpConfigService>();
            services.AddScoped<IFacebookConfigService, FacebookConfigService>();
            services.AddScoped<IWhatsappConfigService, WhatsappConfigService>();
            services.AddScoped<IFacebookService, FacebookService>();
            services.AddScoped<IWhatsappService, WhatsappService>();
            services.AddScoped<IEmailService, EmailService>();
            services.AddScoped<ISubscriberService, SubscriberService>();
            services.AddScoped<IContactService, ContactService>();
            services.AddScoped<ICampaignService, CampaignService>();
            services.AddScoped<IVerificationService, VerificationService>();
            services.AddScoped<IContactActivityService, ContactActivityService>();
            services.AddScoped<ISmsService, SmsService>();
            services.AddScoped<IWebCrawlerService, WebCrawlerService>();
            services.AddScoped<ISegmentService, SegmentService>();
            services.AddScoped<IFeedbackService, FeedbackService>();
            services.AddScoped<IOnboardingService, OnboardingService>();
            services.AddScoped<IPasswordResetService, PasswordResetService>();
            services.AddHttpClient();
            // Auto-redirect disabled so WebCrawlerService can re-validate each redirect hop's
            // resolved address before following it (see WebCrawlerService.FetchSafeAsync).
            services.AddHttpClient(WebCrawlerService.HttpClientName, client =>
                {
                    client.Timeout = TimeSpan.FromSeconds(15);
                })
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                {
                    AllowAutoRedirect = false,
                });

            services.AddMemoryCache();
            services.AddHttpContextAccessor();
            services.AddSingleton<ITenantKeyProvider, TenantKeyProvider>();
            services.AddSingleton<IContactExtractionService, ContactExtractionService>();
            services.AddSingleton<IContactTextExtractionService, ContactTextExtractionService>();

            return services;
        }
    }
}
