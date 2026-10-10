using AuthMicroservice.Model;
using AuthMicroservice.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AuthMicroservice.Controller
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class SegmentController : TenantControllerBase
    {
        private const int PreviewSampleSize = 10;

        private readonly ISegmentService _segmentService;
        private readonly IContactService _contactService;

        public SegmentController(ISegmentService segmentService, IContactService contactService, IApplicationService applicationService)
            : base(applicationService)
        {
            _segmentService = segmentService;
            _contactService = contactService;
        }

        [HttpGet]
        public async Task<IActionResult> GetSegments([FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid) return InvalidAppKey();

            // Counts are evaluated against one load of the contacts, not one per segment.
            var contacts = (await _contactService.GetContactsAsync(app.Id)).ToList();
            var segments = await _segmentService.GetSegmentsAsync(app.Id);
            return Ok(segments.Select(s => ToResponse(s, contacts)));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetSegment(Guid id, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid) return InvalidAppKey();

            var segment = await _segmentService.GetSegmentByIdAsync(id, app.Id);
            if (segment == null) return NotFound();

            var contacts = (await _contactService.GetContactsAsync(app.Id)).ToList();
            return Ok(ToResponse(segment, contacts));
        }

        // Full member records, for exporting a segment from the data bank.
        [HttpGet("{id}/contacts")]
        public async Task<IActionResult> GetSegmentContacts(Guid id, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid) return InvalidAppKey();

            var segment = await _segmentService.GetSegmentByIdAsync(id, app.Id);
            if (segment == null) return NotFound();

            var contacts = await _segmentService.GetMatchingContactsAsync(_segmentService.ParseFilter(segment.FilterJson), app.Id);
            return Ok(contacts);
        }

        // Live count + sample for the segment builder, before anything is saved.
        [HttpPost("preview")]
        public async Task<IActionResult> Preview([FromBody] SegmentFilter filter, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid) return InvalidAppKey();

            var contacts = await _segmentService.GetMatchingContactsAsync(filter ?? new SegmentFilter(), app.Id);
            return Ok(new
            {
                count = contacts.Count,
                withEmail = contacts.Count(c => !string.IsNullOrWhiteSpace(c.Email)),
                withPhone = contacts.Count(c => !string.IsNullOrWhiteSpace(c.Phone)),
                sample = contacts.Take(PreviewSampleSize).Select(c => new
                {
                    id = c.Id,
                    name = $"{c.FirstName} {c.LastName}".Trim(),
                    email = c.Email,
                    phone = c.Phone,
                    company = c.Company,
                    status = c.Status,
                }),
            });
        }

        [HttpPost]
        public async Task<IActionResult> CreateSegment([FromBody] SegmentRequest request, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid) return InvalidAppKey();

            if (string.IsNullOrWhiteSpace(request?.Name))
                return BadRequest(new { message = "Segment name is required." });

            var segment = await _segmentService.CreateSegmentAsync(request.Name, request.Description, request.Filter, app.Id);
            var contacts = (await _contactService.GetContactsAsync(app.Id)).ToList();
            return CreatedAtAction(nameof(GetSegment), new { id = segment.Id }, ToResponse(segment, contacts));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateSegment(Guid id, [FromBody] SegmentRequest request, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid) return InvalidAppKey();

            if (string.IsNullOrWhiteSpace(request?.Name))
                return BadRequest(new { message = "Segment name is required." });

            var segment = await _segmentService.UpdateSegmentAsync(id, request.Name, request.Description, request.Filter, app.Id);
            if (segment == null) return NotFound();

            var contacts = (await _contactService.GetContactsAsync(app.Id)).ToList();
            return Ok(ToResponse(segment, contacts));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSegment(Guid id, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid) return InvalidAppKey();

            await _segmentService.DeleteSegmentAsync(id, app.Id);
            return NoContent();
        }

        private object ToResponse(Segment segment, List<Contact> contacts)
        {
            var filter = _segmentService.ParseFilter(segment.FilterJson);
            return new
            {
                id = segment.Id,
                name = segment.Name,
                description = segment.Description,
                filter,
                contactCount = contacts.Count(filter.Matches),
                createdDate = segment.CreatedDate,
            };
        }
    }

    public class SegmentRequest
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public SegmentFilter? Filter { get; set; }
    }
}
