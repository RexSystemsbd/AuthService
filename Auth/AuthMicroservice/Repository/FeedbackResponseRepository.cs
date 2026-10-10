using AuthMicroservice.Model;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace AuthMicroservice.Repository
{
    public interface IFeedbackResponseRepository : IGenericRepository<FeedbackResponse>
    {
        Task<FeedbackResponse> GetByTokenAsync(string token);
    }

    public class FeedbackResponseRepository : GenericRepository<FeedbackResponse>, IFeedbackResponseRepository
    {
        public FeedbackResponseRepository(UserDbContext context)
            : base(context)
        {
        }

        public async Task<FeedbackResponse> GetByTokenAsync(string token)
        {
            return await _context.Set<FeedbackResponse>().FirstOrDefaultAsync(r => r.Token == token);
        }
    }
}
