using AuthMicroservice.Model;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace AuthMicroservice.Repository
{
    public interface IContactRepository : IGenericRepository<Contact>
    {
        Task<Contact> GetByEmailAndApplicationIdAsync(string email, Guid applicationId);
    }

    public class ContactRepository : GenericRepository<Contact>, IContactRepository
    {
        public ContactRepository(UserDbContext context)
            : base(context)
        {
        }

        public async Task<Contact> GetByEmailAndApplicationIdAsync(string email, Guid applicationId)
        {
            return await _context.Set<Contact>().FirstOrDefaultAsync(c => c.Email == email && c.ApplicationId == applicationId);
        }
    }
}
