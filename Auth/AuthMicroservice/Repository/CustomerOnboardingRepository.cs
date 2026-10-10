using AuthMicroservice.Model;

namespace AuthMicroservice.Repository
{
    public interface ICustomerOnboardingRepository : IGenericRepository<CustomerOnboarding>
    {
    }

    public class CustomerOnboardingRepository : GenericRepository<CustomerOnboarding>, ICustomerOnboardingRepository
    {
        public CustomerOnboardingRepository(UserDbContext context)
            : base(context)
        {
        }
    }
}
