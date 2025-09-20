using AuthMicroservice.Model;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace AuthMicroservice.Repository
{
    public interface ISmtpConfigRepository : IGenericRepository<SmtpConfig>
    {
        Task<SmtpConfig> GetByApplicationIdAsync(Guid applicationId);
    }

    public class SmtpConfigRepository : GenericRepository<SmtpConfig>, ISmtpConfigRepository
    {
        public SmtpConfigRepository(UserDbContext context)
            : base(context)
        {
        }

        public async Task<SmtpConfig> GetByApplicationIdAsync(Guid applicationId)
        {
            return await _context.Set<SmtpConfig>().FirstOrDefaultAsync(s => s.ApplicationId == applicationId);
        }
    }
}
