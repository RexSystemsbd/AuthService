using AuthMicroservice.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;

namespace AuthMicroservice.Controller
{
    // Contact extraction for unstructured uploads. The browser turns PDF (via pdf.js), text
    // and vCard files, or pasted text, into plain text and posts it here. Like
    // crawl-and-extract this only returns candidates: the import wizard shows them for
    // review and mapping, and bulk-import does the actual create/update.
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ContactImportController : TenantControllerBase
    {
        private readonly IContactTextExtractionService _extractionService;

        public ContactImportController(IContactTextExtractionService extractionService, IApplicationService applicationService)
            : base(applicationService)
        {
            _extractionService = extractionService;
        }

        [HttpPost("extract-text")]
        [RequestSizeLimit(4 * 1024 * 1024)]
        public async Task<IActionResult> ExtractFromText([FromBody] ExtractTextRequest request, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, _) = await IsValidAppKey(appKey);
            if (!isValid) return InvalidAppKey();

            if (string.IsNullOrWhiteSpace(request?.Text))
                return BadRequest(new { message = "There's no text to read contacts from." });
            if (request.Text.Length > ContactTextExtractionService.MaxCharacters)
                return BadRequest(new { message = $"Send at most {ContactTextExtractionService.MaxCharacters:N0} characters at a time." });

            var result = _extractionService.Extract(request.Text);
            return Ok(new
            {
                characters = result.Characters,
                emailsFound = result.EmailsFound,
                phonesFound = result.PhonesFound,
                contacts = result.Contacts.Select(c => new
                {
                    email = c.Email,
                    phone = c.Phone,
                    firstName = c.FirstName,
                    lastName = c.LastName,
                    company = c.Company,
                    jobTitle = c.JobTitle,
                    notes = c.Notes,
                    snippet = c.RawSnippet,
                }),
            });
        }
    }

    public class ExtractTextRequest
    {
        public string? Text { get; set; }
    }
}
