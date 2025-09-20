using AuthMicroservice.Model;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace AuthMicroservice.Repository
{
    public interface ISubscriberRepository : IGenericRepository<Subscriber>
    {
        Task<Subscriber> GetByEmailAndApplicationIdAsync(string email, string applicationId);
    }

    public class SubscriberRepository : GenericRepository<Subscriber>, ISubscriberRepository
    {
        public SubscriberRepository(UserDbContext context)
            : base(context)
        {
        }

        public async Task<Subscriber> GetByEmailAndApplicationIdAsync(string email, string applicationId)
        {
            return await _context.Set<Subscriber>().FirstOrDefaultAsync(s => s.Email == email && s.ApplicationId == applicationId);
        }
    }
}
