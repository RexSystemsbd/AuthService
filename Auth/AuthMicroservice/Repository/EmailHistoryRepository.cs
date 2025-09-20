using AuthMicroservice.Model;

namespace AuthMicroservice.Repository
{
    public interface IEmailHistoryRepository : IGenericRepository<EmailHistory>
    {
    }

    public class EmailHistoryRepository : GenericRepository<EmailHistory>, IEmailHistoryRepository
    {
        public EmailHistoryRepository(UserDbContext context)
            : base(context)
        {
        }
    }
}
