using AuthMicroservice.Model;
using AuthMicroservice.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AuthMicroservice.Controller
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ContactController : ControllerBase
    {
        private readonly IContactService _contactService;
        private readonly IApplicationService _applicationService;
        private readonly IContactActivityService _contactActivityService;
        private readonly IEmailService _emailService;
        private readonly ISmsService _smsService;
        private readonly IFacebookService _facebookService;
        private readonly IWhatsappService _whatsappService;
        private readonly IFacebookConfigService _facebookConfigService;
        private readonly IWhatsappConfigService _whatsappConfigService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IContactExtractionService _contactExtractionService;
        private readonly IWebCrawlerService _webCrawlerService;

        public ContactController(
            IContactService contactService,
            IApplicationService applicationService,
            IContactActivityService contactActivityService,
            IEmailService emailService,
            ISmsService smsService,
            IFacebookService facebookService,
            IWhatsappService whatsappService,
            IFacebookConfigService facebookConfigService,
            IWhatsappConfigService whatsappConfigService,
            IHttpClientFactory httpClientFactory,
            IContactExtractionService contactExtractionService,
            IWebCrawlerService webCrawlerService)
        {
            _contactService = contactService;
            _applicationService = applicationService;
            _contactActivityService = contactActivityService;
            _emailService = emailService;
            _smsService = smsService;
            _facebookService = facebookService;
            _whatsappService = whatsappService;
            _facebookConfigService = facebookConfigService;
            _whatsappConfigService = whatsappConfigService;
            _httpClientFactory = httpClientFactory;
            _contactExtractionService = contactExtractionService;
            _webCrawlerService = webCrawlerService;
        }

        [HttpPost]
        public async Task<IActionResult> CreateContact([FromBody] Contact contact, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid)
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var createdContact = await _contactService.CreateContactAsync(contact, app.Id);
            return CreatedAtAction(nameof(GetContact), new { id = createdContact.Id }, createdContact);
        }

        [HttpGet]
        public async Task<IActionResult> GetContacts([FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid)
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            var contacts = await _contactService.GetContactsAsync(app.Id);
            return Ok(contacts);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetContact(string id, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid)
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            var contact = await _contactService.GetContactByIdAsync(id, app.Id);
            if (contact == null)
                return NotFound();

            return Ok(contact);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateContact(string id, [FromBody] Contact contact, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid)
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            if (id != contact.Id.ToString())
                return BadRequest();

            contact.ApplicationId = app.Id;
            await _contactService.UpdateContactAsync(id, contact, app.Id);
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteContact(string id, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid)
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            await _contactService.DeleteContactAsync(id, app.Id);
            return NoContent();
        }

        [HttpGet("{id}/activities")]
        public async Task<IActionResult> GetContactActivities(string id, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid)
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            if (!Guid.TryParse(id, out var contactId))
                return BadRequest(new { message = "Invalid contact id." });

            var activities = await _contactActivityService.GetActivitiesForContactAsync(contactId, app.Id);
            return Ok(activities);
        }

        [HttpPost("{id}/send-email")]
        public async Task<IActionResult> SendContactEmail(string id, [FromBody] ContactEmailRequest request, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid)
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            var contact = await _contactService.GetContactByIdAsync(id, app.Id);
            if (contact == null)
                return NotFound();

            if (string.IsNullOrWhiteSpace(request?.Subject) || string.IsNullOrWhiteSpace(request?.Body))
                return BadRequest(new { message = "Subject and body are required." });

            var status = await _emailService.SendEmailAsync(app.Id, request.Subject, request.Body, new List<string> { contact.Email });
            return Ok(new { status });
        }

        [HttpPost("{id}/send-sms")]
        public async Task<IActionResult> SendContactSms(string id, [FromBody] ContactSmsRequest request, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid)
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            if (!Guid.TryParse(id, out var contactId))
                return BadRequest(new { message = "Invalid contact id." });

            var contact = await _contactService.GetContactByIdAsync(id, app.Id);
            if (contact == null)
                return NotFound();

            if (string.IsNullOrWhiteSpace(contact.Phone))
                return BadRequest(new { message = "Contact has no phone number on file." });

            if (string.IsNullOrWhiteSpace(request?.Message))
                return BadRequest(new { message = "Message is required." });

            var sent = await _smsService.SendSmsAsync(contact.Phone, request.Message);
            var status = sent ? "Sent" : "Failed";
            await _contactActivityService.LogActivityForContactAsync(app.Id, contactId, "Sms", "SMS", request.Message, status);
            return Ok(new { status });
        }

        [HttpPost("{id}/send-facebook-message")]
        public async Task<IActionResult> SendContactFacebookMessage(string id, [FromBody] ContactFacebookMessageRequest request, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid)
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            if (!Guid.TryParse(id, out var contactId))
                return BadRequest(new { message = "Invalid contact id." });

            var contact = await _contactService.GetContactByIdAsync(id, app.Id);
            if (contact == null)
                return NotFound();

            if (string.IsNullOrWhiteSpace(contact.FacebookPsid))
                return BadRequest(new { message = "This contact has no linked Facebook Messenger conversation on file." });

            if (string.IsNullOrWhiteSpace(request?.Message))
                return BadRequest(new { message = "Message is required." });

            var config = await _facebookConfigService.GetFacebookConfigByApplicationIdAsync(app.Id);
            if (config == null || string.IsNullOrWhiteSpace(config.PageAccessToken))
                return BadRequest(new { message = "No Facebook Page is connected for this application." });

            var sent = await _facebookService.SendMessageAsync(config.PageAccessToken, contact.FacebookPsid, request.Message);
            var status = sent ? "Sent" : "Failed";
            await _contactActivityService.LogActivityForContactAsync(app.Id, contactId, "facebook-message", "Facebook Message", request.Message, status);
            return Ok(new { status });
        }

        [HttpPost("{id}/send-whatsapp")]
        public async Task<IActionResult> SendContactWhatsapp(string id, [FromBody] ContactWhatsappRequest request, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid)
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            if (!Guid.TryParse(id, out var contactId))
                return BadRequest(new { message = "Invalid contact id." });

            var contact = await _contactService.GetContactByIdAsync(id, app.Id);
            if (contact == null)
                return NotFound();

            if (string.IsNullOrWhiteSpace(contact.Phone))
                return BadRequest(new { message = "Contact has no phone number on file." });

            if (string.IsNullOrWhiteSpace(request?.Message))
                return BadRequest(new { message = "Message is required." });

            var config = await _whatsappConfigService.GetWhatsappConfigByApplicationIdAsync(app.Id);
            if (config == null || string.IsNullOrWhiteSpace(config.AccessToken))
                return BadRequest(new { message = "No WhatsApp Business number is connected for this application." });

            var sent = await _whatsappService.SendMessageAsync(config.AccessToken, config.PhoneNumberId, contact.Phone, request.Message, request.TemplateName, request.TemplateParams);
            var status = sent ? "Sent" : "Failed";
            await _contactActivityService.LogActivityForContactAsync(app.Id, contactId, "whatsapp-message", "WhatsApp Message", request.Message, status);
            return Ok(new { status });
        }

        [HttpPost("{id}/log-activity")]
        public async Task<IActionResult> LogContactActivity(string id, [FromBody] ContactLogActivityRequest request, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid)
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            if (!Guid.TryParse(id, out var contactId))
                return BadRequest(new { message = "Invalid contact id." });

            if (string.IsNullOrWhiteSpace(request?.Message))
                return BadRequest(new { message = "Message is required." });

            var type = string.IsNullOrWhiteSpace(request.Type) ? "Note" : request.Type;
            await _contactActivityService.LogActivityForContactAsync(app.Id, contactId, type, request.Subject ?? type, request.Message, "Logged");
            return Ok();
        }

        // Resolves a Google Sheets or Google Drive file share link to its raw file bytes,
        // so the browser never needs to fetch docs.google.com/drive.google.com directly
        // (blocked by CORS for cross-origin script fetches anyway). Only these two host+
        // path shapes are accepted — this is a URL-fetching proxy, so anything broader
        // would be an SSRF hole. The frontend sniffs the returned bytes itself (JSON vs.
        // CSV/XLSX) since Drive's direct-download endpoint doesn't reliably report a
        // useful Content-Type.
        [HttpPost("import-from-url")]
        public async Task<IActionResult> ImportFromUrl([FromBody] ImportFromUrlRequest request, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid)
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            if (string.IsNullOrWhiteSpace(request?.Url) || !Uri.TryCreate(request.Url, UriKind.Absolute, out var uri))
                return BadRequest(new { message = "A valid URL is required." });

            string fetchUrl;
            if (string.Equals(uri.Host, "docs.google.com", StringComparison.OrdinalIgnoreCase) &&
                uri.AbsolutePath.StartsWith("/spreadsheets/d/", StringComparison.OrdinalIgnoreCase))
            {
                var sheetMatch = Regex.Match(uri.AbsolutePath, @"/spreadsheets/d/([a-zA-Z0-9_-]+)");
                if (!sheetMatch.Success)
                    return BadRequest(new { message = "Could not find a sheet ID in that URL." });

                var gidMatch = Regex.Match(request.Url, @"[?&#]gid=(\d+)");
                var gid = gidMatch.Success ? gidMatch.Groups[1].Value : "0";
                fetchUrl = $"https://docs.google.com/spreadsheets/d/{sheetMatch.Groups[1].Value}/export?format=csv&gid={gid}";
            }
            else if (string.Equals(uri.Host, "drive.google.com", StringComparison.OrdinalIgnoreCase) &&
                     uri.AbsolutePath.StartsWith("/file/d/", StringComparison.OrdinalIgnoreCase))
            {
                var fileMatch = Regex.Match(uri.AbsolutePath, @"/file/d/([a-zA-Z0-9_-]+)");
                if (!fileMatch.Success)
                    return BadRequest(new { message = "Could not find a file ID in that URL." });

                fetchUrl = $"https://drive.google.com/uc?export=download&id={fileMatch.Groups[1].Value}";
            }
            else
            {
                return BadRequest(new { message = "Only Google Sheets or Google Drive file links are supported right now." });
            }

            var client = _httpClientFactory.CreateClient();
            HttpResponseMessage response;
            try
            {
                response = await client.GetAsync(fetchUrl);
            }
            catch
            {
                return BadRequest(new { message = "Could not reach that Google URL." });
            }

            if (!response.IsSuccessStatusCode)
                return BadRequest(new { message = "Could not fetch that file. Make sure it's shared as \"Anyone with the link can view\"." });

            var bytes = await response.Content.ReadAsByteArrayAsync();
            // Drive files over ~25MB return an HTML virus-scan warning page instead of the
            // file itself; not handled here (would need scraping a confirm token from
            // that page) — surface it as a clear error rather than silently mis-importing it.
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
            if (contentType.Contains("html", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { message = "That link returned a web page instead of a file — check sharing permissions, or the file may be too large for a direct link." });

            return File(bytes, "application/octet-stream");
        }

        // Crawls a tenant-supplied URL (plus same-domain "contact us"/"about"/"team" pages
        // it links to, up to MaxDepth hops) and combines two extraction paths: the ML.NET
        // classifier in ContactExtractionService running over each page's visible text,
        // and WebCrawlerService's structured harvest (mailto:/tel: links, schema.org
        // Person/Organization JSON-LD). Returns structured records rather than raw bytes
        // — unlike import-from-url — since the frontend can feed them straight into
        // ImportContactsModal's existing mapping step. This does not create/update any
        // contacts itself; bulk-import still does that after the user reviews the mapping.
        [HttpPost("crawl-and-extract")]
        public async Task<IActionResult> CrawlAndExtractContacts([FromBody] CrawlAndExtractRequest request, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid)
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            var url = request?.Url?.Trim();
            if (string.IsNullOrWhiteSpace(url))
                return BadRequest(new { message = "A URL is required." });

            var (success, error, crawlResult, merged) = await CrawlAndMergeAsync(url, request.MaxPages, request.MaxDepth);
            if (!success)
                return BadRequest(new { message = error ?? "Could not crawl that URL." });

            var contacts = merged
                .Take(2000)
                .Select(c => new
                {
                    email = c.Email,
                    firstName = c.FirstName,
                    lastName = c.LastName,
                    phone = c.Phone,
                    company = c.Company,
                    jobTitle = c.JobTitle,
                    notes = c.Notes,
                    source = $"Web crawl: {url}",
                })
                .ToList();

            return Ok(new
            {
                pagesCrawled = crawlResult.Pages.Select(p => p.Url).ToList(),
                contacts,
            });
        }

        // Same crawl + extraction pipeline as crawl-and-extract, but skips the
        // review/mapping step and bulk-inserts the results straight into this tenant's
        // contacts via ContactService.BulkImportContactsAsync — which dedupes against
        // existing contacts (and within this batch) by email OR phone. For collecting a
        // full CRM list from a site in one shot rather than reviewing each field mapping.
        [HttpPost("crawl-and-import")]
        public async Task<IActionResult> CrawlAndImportContacts([FromBody] CrawlAndExtractRequest request, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid)
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            var url = request?.Url?.Trim();
            if (string.IsNullOrWhiteSpace(url))
                return BadRequest(new { message = "A URL is required." });

            var (success, error, crawlResult, merged) = await CrawlAndMergeAsync(url, request.MaxPages, request.MaxDepth);
            if (!success)
                return BadRequest(new { message = error ?? "Could not crawl that URL." });

            var pagesCrawled = crawlResult.Pages.Select(p => p.Url).ToList();

            if (merged.Count == 0)
                return Ok(new { pagesCrawled, created = 0, updated = 0, skipped = 0 });

            if (merged.Count > 2000)
                return BadRequest(new { message = $"Found {merged.Count} contacts, which is over the 2000 per-run import limit — crawl a narrower section of the site (lower maxPages/maxDepth)." });

            var contactsToImport = merged.Select(c => new Contact
            {
                Email = c.Email,
                FirstName = c.FirstName,
                LastName = c.LastName,
                Phone = c.Phone,
                Company = c.Company,
                JobTitle = c.JobTitle,
                Notes = c.Notes,
                Source = $"Web crawl: {url}",
            }).ToList();

            var result = await _contactService.BulkImportContactsAsync(contactsToImport, app.Id);
            return Ok(new { pagesCrawled, result.Created, result.Updated, result.Skipped });
        }

        private async Task<(bool success, string error, CrawlResult crawlResult, List<ExtractedContact> merged)> CrawlAndMergeAsync(string url, int? maxPages, int? maxDepth)
        {
            var (success, error, crawlResult) = await _webCrawlerService.CrawlAsync(url, maxPages ?? 10, maxDepth ?? 2);
            if (!success)
                return (false, error, null, null);

            var combinedText = string.Join("\n\n", crawlResult.Pages.Select(p => $"[{p.Url}]\n{p.Text}"));
            var mlContacts = _contactExtractionService.ExtractContacts(combinedText);
            var merged = MergeExtractedContacts(mlContacts.Concat(crawlResult.StructuredContacts));
            return (true, null, crawlResult, merged);
        }

        // Combines ML-classified candidates (from page text) with structured candidates
        // (mailto:/tel: links, JSON-LD) into one deduped list, keyed by email OR phone —
        // the same person frequently shows up with only one of the two on any given page.
        private static List<ExtractedContact> MergeExtractedContacts(IEnumerable<ExtractedContact> candidates)
        {
            var byEmail = new Dictionary<string, ExtractedContact>();
            var byPhone = new Dictionary<string, ExtractedContact>();
            var merged = new List<ExtractedContact>();

            foreach (var candidate in candidates)
            {
                var emailKey = ContactMatching.NormalizeEmail(candidate.Email);
                var phoneKey = ContactMatching.NormalizePhone(candidate.Phone);
                if (emailKey == null && phoneKey == null)
                    continue;

                ExtractedContact target = null;
                if (emailKey != null) byEmail.TryGetValue(emailKey, out target);
                if (target == null && phoneKey != null) byPhone.TryGetValue(phoneKey, out target);

                if (target == null)
                {
                    target = new ExtractedContact();
                    merged.Add(target);
                }

                target.Email ??= candidate.Email;
                target.Phone ??= candidate.Phone;
                target.FirstName ??= candidate.FirstName;
                target.LastName ??= candidate.LastName;
                target.Company ??= candidate.Company;
                target.JobTitle ??= candidate.JobTitle;
                target.Notes ??= candidate.Notes;
                target.RawSnippet ??= candidate.RawSnippet;

                var newEmailKey = ContactMatching.NormalizeEmail(target.Email);
                var newPhoneKey = ContactMatching.NormalizePhone(target.Phone);
                if (newEmailKey != null) byEmail[newEmailKey] = target;
                if (newPhoneKey != null) byPhone[newPhoneKey] = target;
            }

            return merged;
        }

        [HttpPost("bulk-import")]
        public async Task<IActionResult> BulkImportContacts([FromBody] List<Contact> contacts, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid)
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            if (contacts == null || contacts.Count == 0)
                return BadRequest(new { message = "No contacts provided." });

            if (contacts.Count > 2000)
                return BadRequest(new { message = "Batch too large; import in chunks of 2000 or fewer." });

            var result = await _contactService.BulkImportContactsAsync(contacts, app.Id);
            return Ok(result);
        }

        private async Task<(bool, Application)> IsValidAppKey(string appKey)
        {
            var applications = await _applicationService.GetApplicationsAsync(appKey);
            var app = applications.FirstOrDefault(a => a.AppKey == appKey);
            if (app == null) return (false, null);

            // [Authorize] has already validated the JWT itself; this confirms the token's
            // own tenant (ApplicationId claim) matches the AppKey-resolved tenant for this
            // request, so a valid token for tenant A can't be replayed against tenant B's
            // AppKey.
            var applicationIdClaim = User.FindFirst("ApplicationId")?.Value;
            var isValid = !string.IsNullOrEmpty(applicationIdClaim) && applicationIdClaim == app.Id.ToString();
            return (isValid, app);
        }
    }

    public class ImportFromUrlRequest
    {
        public string Url { get; set; }
    }

    public class CrawlAndExtractRequest
    {
        public string Url { get; set; }
        public int? MaxPages { get; set; }
        public int? MaxDepth { get; set; }
    }

    public class ContactEmailRequest
    {
        public string Subject { get; set; }
        public string Body { get; set; }
    }

    public class ContactSmsRequest
    {
        public string Message { get; set; }
    }

    public class ContactFacebookMessageRequest
    {
        public string Message { get; set; }
    }

    public class ContactWhatsappRequest
    {
        public string Message { get; set; }
        public string TemplateName { get; set; }
        public string[] TemplateParams { get; set; }
    }

    public class ContactLogActivityRequest
    {
        public string Type { get; set; }
        public string Subject { get; set; }
        public string Message { get; set; }
    }
}
