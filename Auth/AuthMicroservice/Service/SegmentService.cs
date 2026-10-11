using AuthMicroservice.Model;
using AuthMicroservice.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace AuthMicroservice.Service
{
    // Criteria are AND-ed together; within a list (e.g. Statuses) any value matches.
    // Empty/null criteria are ignored, so an empty filter matches every contact.
    public class SegmentFilter
    {
        public List<string>? Statuses { get; set; }
        public List<string>? Categories { get; set; }
        public List<string>? Sources { get; set; }
        public List<string>? Industries { get; set; }
        public List<string>? Countries { get; set; }
        public List<string>? Cities { get; set; }
        public List<string>? Tags { get; set; }
        public string? Search { get; set; }
        public bool? HasEmail { get; set; }
        public bool? HasPhone { get; set; }
        public bool? VerifiedOnly { get; set; }
        public bool? SubscribedOnly { get; set; }
        public int? CreatedWithinDays { get; set; }
        // No recorded interaction in this many days (or ever).
        public int? InactiveForDays { get; set; }

        public bool Matches(Contact contact)
        {
            if (!MatchesAny(Statuses, contact.Status)) return false;
            if (!MatchesAny(Categories, contact.Category)) return false;
            if (!MatchesAny(Sources, contact.Source)) return false;
            if (!MatchesAny(Industries, contact.Industry)) return false;
            if (!MatchesAny(Countries, contact.Country)) return false;
            if (!MatchesAny(Cities, contact.City)) return false;

            if (Tags != null && Tags.Count > 0)
            {
                var contactTags = SegmentService.SplitTags(contact.Tags);
                if (!Tags.Any(t => contactTags.Contains(t.Trim(), StringComparer.OrdinalIgnoreCase)))
                    return false;
            }

            if (HasEmail.HasValue && HasEmail.Value == string.IsNullOrWhiteSpace(contact.Email)) return false;
            if (HasPhone.HasValue && HasPhone.Value == string.IsNullOrWhiteSpace(contact.Phone)) return false;
            if (VerifiedOnly == true && !contact.IsVerified) return false;
            if (SubscribedOnly == true && !contact.IsSubscribed) return false;

            if (CreatedWithinDays.HasValue && contact.CreatedDate < DateTime.Now.AddDays(-CreatedWithinDays.Value))
                return false;

            if (InactiveForDays.HasValue && contact.LastInteractionDate.HasValue &&
                contact.LastInteractionDate.Value > DateTime.UtcNow.AddDays(-InactiveForDays.Value))
                return false;

            if (!string.IsNullOrWhiteSpace(Search))
            {
                var term = Search.Trim();
                var haystack = new[]
                {
                    contact.FirstName, contact.LastName, contact.Email, contact.WorkEmail, contact.PersonalEmail,
                    contact.Phone, contact.AlternatePhone, contact.WorkPhone, contact.WhatsappNumber, contact.Company, contact.Notes,
                };
                if (!haystack.Any(v => v != null && v.Contains(term, StringComparison.OrdinalIgnoreCase)))
                    return false;
            }

            return true;
        }

        private static bool MatchesAny(List<string>? allowed, string? value) =>
            allowed == null || allowed.Count == 0 ||
            (!string.IsNullOrWhiteSpace(value) && allowed.Any(a => string.Equals(a?.Trim(), value.Trim(), StringComparison.OrdinalIgnoreCase)));
    }

    public interface ISegmentService
    {
        Task<IEnumerable<Segment>> GetSegmentsAsync(Guid applicationId);
        Task<Segment> GetSegmentByIdAsync(Guid id, Guid applicationId);
        Task<Segment> CreateSegmentAsync(string name, string description, SegmentFilter filter, Guid applicationId);
        Task<Segment> UpdateSegmentAsync(Guid id, string name, string description, SegmentFilter filter, Guid applicationId);
        Task DeleteSegmentAsync(Guid id, Guid applicationId);
        Task<List<Contact>> GetMatchingContactsAsync(SegmentFilter filter, Guid applicationId);
        // Contacts a campaign/survey should reach: the segment's members when targetGroup is
        // "segment", otherwise every contact. Returns null when the segment no longer exists.
        Task<List<Contact>> ResolveAudienceAsync(string targetGroup, Guid? segmentId, Guid applicationId);
        SegmentFilter ParseFilter(string filterJson);
    }

    public class SegmentService : ISegmentService
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
        };

        private readonly ISegmentRepository _segmentRepository;
        private readonly IContactRepository _contactRepository;

        public SegmentService(ISegmentRepository segmentRepository, IContactRepository contactRepository)
        {
            _segmentRepository = segmentRepository;
            _contactRepository = contactRepository;
        }

        public static List<string> SplitTags(string? tags) =>
            string.IsNullOrWhiteSpace(tags)
                ? new List<string>()
                : tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

        public SegmentFilter ParseFilter(string filterJson)
        {
            if (string.IsNullOrWhiteSpace(filterJson)) return new SegmentFilter();
            try
            {
                return JsonSerializer.Deserialize<SegmentFilter>(filterJson, JsonOptions) ?? new SegmentFilter();
            }
            catch (JsonException)
            {
                return new SegmentFilter();
            }
        }

        public async Task<IEnumerable<Segment>> GetSegmentsAsync(Guid applicationId)
        {
            var segments = await _segmentRepository.FindAsync(s => s.ApplicationId == applicationId);
            return segments.OrderBy(s => s.Name);
        }

        public async Task<Segment> GetSegmentByIdAsync(Guid id, Guid applicationId)
        {
            var segment = await _segmentRepository.GetByIdAsync(id);
            return segment == null || segment.ApplicationId != applicationId ? null : segment;
        }

        public async Task<Segment> CreateSegmentAsync(string name, string description, SegmentFilter filter, Guid applicationId)
        {
            var segment = new Segment
            {
                Name = name.Trim(),
                Description = description,
                FilterJson = JsonSerializer.Serialize(filter ?? new SegmentFilter(), JsonOptions),
                ApplicationId = applicationId,
            };
            await _segmentRepository.AddAsync(segment);
            return segment;
        }

        public async Task<Segment> UpdateSegmentAsync(Guid id, string name, string description, SegmentFilter filter, Guid applicationId)
        {
            var segment = await GetSegmentByIdAsync(id, applicationId);
            if (segment == null) return null;

            segment.Name = name.Trim();
            segment.Description = description;
            segment.FilterJson = JsonSerializer.Serialize(filter ?? new SegmentFilter(), JsonOptions);
            await _segmentRepository.UpdateAsync(segment);
            return segment;
        }

        public async Task DeleteSegmentAsync(Guid id, Guid applicationId)
        {
            var segment = await GetSegmentByIdAsync(id, applicationId);
            if (segment != null)
                await _segmentRepository.RemoveAsync(segment);
        }

        public async Task<List<Contact>> GetMatchingContactsAsync(SegmentFilter filter, Guid applicationId)
        {
            var contacts = await _contactRepository.FindAsync(c => c.ApplicationId == applicationId);
            filter ??= new SegmentFilter();
            return contacts.Where(filter.Matches).ToList();
        }

        public async Task<List<Contact>> ResolveAudienceAsync(string targetGroup, Guid? segmentId, Guid applicationId)
        {
            if (string.Equals(targetGroup, "segment", StringComparison.OrdinalIgnoreCase))
            {
                if (!segmentId.HasValue) return null;
                var segment = await GetSegmentByIdAsync(segmentId.Value, applicationId);
                if (segment == null) return null;
                return await GetMatchingContactsAsync(ParseFilter(segment.FilterJson), applicationId);
            }

            return (await _contactRepository.FindAsync(c => c.ApplicationId == applicationId)).ToList();
        }
    }
}
