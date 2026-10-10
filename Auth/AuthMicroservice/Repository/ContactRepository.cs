using AuthMicroservice.Model;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AuthMicroservice.Repository
{
    public class ContactPageQuery
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
        // Matches name, email, phone, company or tags.
        public string? Search { get; set; }
        public string? Status { get; set; }
        public string? Tag { get; set; }
        // "newest" (default) | "oldest" | "name" | "lastInteraction" | "nextContact"
        public string? Sort { get; set; }
    }

    public interface IContactRepository : IGenericRepository<Contact>
    {
        Task<Contact> GetByEmailAndApplicationIdAsync(string email, Guid applicationId);
        Task<(List<Contact> Items, int Total)> GetPageAsync(Guid applicationId, ContactPageQuery query);
        Task<List<string>> GetDistinctStatusesAsync(Guid applicationId);
        Task<List<string>> GetTagsAsync(Guid applicationId);
    }

    public class ContactRepository : GenericRepository<Contact>, IContactRepository
    {
        public const int MaxPageSize = 200;

        public ContactRepository(UserDbContext context)
            : base(context)
        {
        }

        public async Task<Contact> GetByEmailAndApplicationIdAsync(string email, Guid applicationId)
        {
            return await _context.Set<Contact>().FirstOrDefaultAsync(c => c.Email == email && c.ApplicationId == applicationId);
        }

        // One page of contacts, filtered, sorted and paged in SQL so the browser never has to
        // download the whole contact table.
        public async Task<(List<Contact> Items, int Total)> GetPageAsync(Guid applicationId, ContactPageQuery query)
        {
            var contacts = _context.Set<Contact>().AsNoTracking().Where(c => c.ApplicationId == applicationId);

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var term = query.Search.Trim();
                contacts = contacts.Where(c =>
                    ((c.FirstName ?? "") + " " + (c.LastName ?? "")).Contains(term) ||
                    c.Email.Contains(term) ||
                    (c.Phone != null && c.Phone.Contains(term)) ||
                    (c.Company != null && c.Company.Contains(term)) ||
                    (c.Tags != null && c.Tags.Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(query.Status))
            {
                var status = query.Status.Trim();
                contacts = contacts.Where(c => c.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(query.Tag))
            {
                // Whole-tag match within the comma-separated list ("vip" must not match
                // "vip-old"); spaces are ignored since tags are stored as "a, b" or "a,b".
                var tagKey = "," + query.Tag.Replace(" ", "").Trim() + ",";
                contacts = contacts.Where(c => c.Tags != null && ("," + c.Tags.Replace(" ", "") + ",").Contains(tagKey));
            }

            var total = await contacts.CountAsync();

            contacts = (query.Sort ?? "").ToLowerInvariant() switch
            {
                "oldest" => contacts.OrderBy(c => c.CreatedDate).ThenBy(c => c.Id),
                "name" => contacts.OrderBy(c => c.FirstName).ThenBy(c => c.LastName).ThenBy(c => c.Id),
                "lastinteraction" => contacts.OrderByDescending(c => c.LastInteractionDate).ThenByDescending(c => c.CreatedDate).ThenBy(c => c.Id),
                // Overdue/soonest first; contacts with no date last.
                "nextcontact" => contacts.OrderBy(c => c.NextContactDate == null).ThenBy(c => c.NextContactDate).ThenBy(c => c.Id),
                _ => contacts.OrderByDescending(c => c.CreatedDate).ThenBy(c => c.Id),
            };

            var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);
            var page = Math.Max(1, query.Page);
            var items = await contacts.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            return (items, total);
        }

        public async Task<List<string>> GetDistinctStatusesAsync(Guid applicationId)
        {
            return await _context.Set<Contact>()
                .Where(c => c.ApplicationId == applicationId && c.Status != null && c.Status != "")
                .Select(c => c.Status!)
                .Distinct()
                .OrderBy(s => s)
                .ToListAsync();
        }

        // Only the Tags column is read; splitting the comma lists happens here.
        public async Task<List<string>> GetTagsAsync(Guid applicationId)
        {
            var tagLists = await _context.Set<Contact>()
                .Where(c => c.ApplicationId == applicationId && c.Tags != null && c.Tags != "")
                .Select(c => c.Tags!)
                .Distinct()
                .ToListAsync();

            return tagLists
                .SelectMany(t => t.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
