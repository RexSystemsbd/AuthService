using AuthMicroservice.Model;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AuthMicroservice.Repository
{
    public interface IContactActivityRepository : IGenericRepository<ContactActivity>
    {
        Task<List<ContactActivity>> GetRecentAsync(Guid applicationId, string status, string type, int limit);
    }

    public class ContactActivityRepository : GenericRepository<ContactActivity>, IContactActivityRepository
    {
        public ContactActivityRepository(UserDbContext context)
            : base(context)
        {
        }

        // Newest-first slice of the tenant's activity log, filtered and limited in SQL
        // (the table grows with every message sent, so FindAsync's load-everything
        // approach isn't appropriate here).
        public async Task<List<ContactActivity>> GetRecentAsync(Guid applicationId, string status, string type, int limit)
        {
            var query = _context.Set<ContactActivity>().Where(a => a.ApplicationId == applicationId);
            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(a => a.Status == status);
            if (!string.IsNullOrWhiteSpace(type))
                query = query.Where(a => a.Type == type);

            return await query.OrderByDescending(a => a.CreatedDate).Take(limit).ToListAsync();
        }
    }
}
