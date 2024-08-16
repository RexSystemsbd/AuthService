using Microsoft.EntityFrameworkCore;

namespace AuthMicroservice.Repository
{
    public interface IApplicationRepository : IGenericRepository<Application>
    {
        // Define any additional methods specific to the Application entity
    }

    public class ApplicationRepository : GenericRepository<Application>, IApplicationRepository
    {
        public ApplicationRepository(UserDbContext context) : base(context)
        { 
        }

        // Implement any additional methods specific to the Application entity
    }
}
