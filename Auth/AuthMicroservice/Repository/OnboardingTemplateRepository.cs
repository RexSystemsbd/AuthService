using AuthMicroservice.Model;

namespace AuthMicroservice.Repository
{
    public interface IOnboardingTemplateRepository : IGenericRepository<OnboardingTemplate>
    {
    }

    public class OnboardingTemplateRepository : GenericRepository<OnboardingTemplate>, IOnboardingTemplateRepository
    {
        public OnboardingTemplateRepository(UserDbContext context)
            : base(context)
        {
        }
    }
}
