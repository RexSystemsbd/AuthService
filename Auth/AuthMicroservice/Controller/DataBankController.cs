using AuthMicroservice.Repository;
using AuthMicroservice.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AuthMicroservice.Controller
{
    // Bulk maintenance of the contact data bank, plus the cross-contact activity feed
    // (customer replies inbox, recent activity on the dashboard).
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class DataBankController : TenantControllerBase
    {
        private const int MaxBulkContacts = 5000;
        private const int MaxActivityLimit = 500;

        private readonly IContactRepository _contactRepository;
        private readonly IContactActivityRepository _contactActivityRepository;

        public DataBankController(IContactRepository contactRepository, IContactActivityRepository contactActivityRepository, IApplicationService applicationService)
            : base(applicationService)
        {
            _contactRepository = contactRepository;
            _contactActivityRepository = contactActivityRepository;
        }

        // Server-side paging for the contacts table: one page (default 50) plus the total
        // matching count, with search/status/tag filters and sorting applied in SQL.
        [HttpGet("contacts")]
        public async Task<IActionResult> GetContactsPage([FromQuery] ContactPageQuery query, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid) return InvalidAppKey();

            query ??= new ContactPageQuery();
            var (items, total) = await _contactRepository.GetPageAsync(app.Id, query);
            return Ok(new
            {
                items,
                total,
                page = Math.Max(1, query.Page),
                pageSize = Math.Clamp(query.PageSize, 1, ContactRepository.MaxPageSize),
            });
        }

        // Values in use for the contact filter dropdowns (contacts table, campaign audience).
        [HttpGet("contact-filters")]
        public async Task<IActionResult> GetContactFilters([FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid) return InvalidAppKey();

            return Ok(new
            {
                statuses = await _contactRepository.GetDistinctStatusesAsync(app.Id),
                tags = await _contactRepository.GetTagsAsync(app.Id),
                categories = await _contactRepository.GetDistinctValuesAsync(app.Id, c => c.Category),
                sources = await _contactRepository.GetDistinctValuesAsync(app.Id, c => c.Source),
                industries = await _contactRepository.GetDistinctValuesAsync(app.Id, c => c.Industry),
                countries = await _contactRepository.GetDistinctValuesAsync(app.Id, c => c.Country),
                cities = await _contactRepository.GetDistinctValuesAsync(app.Id, c => c.City),
            });
        }

        [HttpPost("bulk-update")]
        public async Task<IActionResult> BulkUpdate([FromBody] BulkUpdateContactsRequest request, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid) return InvalidAppKey();

            var ids = request?.ContactIds?.Distinct().ToHashSet() ?? new HashSet<Guid>();
            if (ids.Count == 0) return BadRequest(new { message = "Select at least one contact." });
            if (ids.Count > MaxBulkContacts) return BadRequest(new { message = $"Update at most {MaxBulkContacts} contacts at a time." });

            var addTags = SegmentService.SplitTags(string.Join(",", request.AddTags ?? new List<string>()));
            var removeTags = SegmentService.SplitTags(string.Join(",", request.RemoveTags ?? new List<string>()));
            var hasStatus = !string.IsNullOrWhiteSpace(request.Status);
            var hasCategory = !string.IsNullOrWhiteSpace(request.Category);
            if (addTags.Count == 0 && removeTags.Count == 0 && !hasStatus && !hasCategory && request.IsSubscribed == null)
                return BadRequest(new { message = "Nothing to change." });

            var contacts = (await _contactRepository.FindAsync(c => c.ApplicationId == app.Id))
                .Where(c => ids.Contains(c.Id))
                .ToList();

            foreach (var contact in contacts)
            {
                if (addTags.Count > 0 || removeTags.Count > 0)
                {
                    var tags = SegmentService.SplitTags(contact.Tags)
                        .Union(addTags, StringComparer.OrdinalIgnoreCase)
                        .Where(t => !removeTags.Contains(t, StringComparer.OrdinalIgnoreCase))
                        .ToList();
                    contact.Tags = tags.Count == 0 ? null : string.Join(", ", tags);
                }
                if (hasStatus) contact.Status = request.Status.Trim();
                if (hasCategory) contact.Category = request.Category.Trim();
                if (request.IsSubscribed.HasValue && contact.IsSubscribed != request.IsSubscribed.Value)
                {
                    contact.IsSubscribed = request.IsSubscribed.Value;
                    if (request.IsSubscribed.Value) contact.SubscribedDate = DateTime.UtcNow;
                    else contact.UnsubscribedDate = DateTime.UtcNow;
                }
            }

            if (contacts.Count > 0)
                await _contactRepository.UpdateRangeAsync(contacts);

            return Ok(new { updated = contacts.Count });
        }

        [HttpPost("bulk-delete")]
        public async Task<IActionResult> BulkDelete([FromBody] BulkContactIdsRequest request, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid) return InvalidAppKey();

            var ids = request?.ContactIds?.Distinct().ToHashSet() ?? new HashSet<Guid>();
            if (ids.Count == 0) return BadRequest(new { message = "Select at least one contact." });
            if (ids.Count > MaxBulkContacts) return BadRequest(new { message = $"Delete at most {MaxBulkContacts} contacts at a time." });

            var contacts = (await _contactRepository.FindAsync(c => c.ApplicationId == app.Id))
                .Where(c => ids.Contains(c.Id))
                .ToList();
            if (contacts.Count > 0)
                await _contactRepository.RemoveRangeAsync(contacts);

            return Ok(new { deleted = contacts.Count });
        }

        // Newest-first activity across all contacts. status=Received gives the inbox of
        // customer replies (IMAP-polled email replies, feedback answers).
        [HttpGet("activities")]
        public async Task<IActionResult> GetActivities([FromQuery] string? status, [FromQuery] string? type, [FromQuery] int? limit, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid) return InvalidAppKey();

            var take = Math.Clamp(limit ?? 100, 1, MaxActivityLimit);
            var activities = await _contactActivityRepository.GetRecentAsync(app.Id, status, type, take);
            var contacts = (await _contactRepository.FindAsync(c => c.ApplicationId == app.Id)).ToDictionary(c => c.Id);

            return Ok(activities.Select(a =>
            {
                contacts.TryGetValue(a.ContactId, out var contact);
                return new
                {
                    id = a.Id,
                    contactId = a.ContactId,
                    contactName = contact == null ? null : $"{contact.FirstName} {contact.LastName}".Trim(),
                    contactEmail = contact?.Email,
                    type = a.Type,
                    subject = a.Subject,
                    message = a.Message,
                    status = a.Status,
                    createdDate = a.CreatedDate,
                };
            }));
        }
    }

    public class BulkContactIdsRequest
    {
        public List<Guid>? ContactIds { get; set; }
    }

    public class BulkUpdateContactsRequest : BulkContactIdsRequest
    {
        public List<string>? AddTags { get; set; }
        public List<string>? RemoveTags { get; set; }
        public string? Status { get; set; }
        public string? Category { get; set; }
        public bool? IsSubscribed { get; set; }
    }
}
