using AuthMicroservice.Model;
using AuthMicroservice.Service;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AuthMicroservice.Controller
{
    [Route("api/[controller]")]
    [ApiController]
    public class ContactController : ControllerBase
    {
        private readonly IContactService _contactService;
        private readonly IApplicationService _applicationService;

        public ContactController(IContactService contactService, IApplicationService applicationService)
        {
            _contactService = contactService;
            _applicationService = applicationService;
        }

        [HttpPost]
        public async Task<IActionResult> CreateContact([FromBody] Contact contact, [FromHeader(Name = "AppKey")] string appKey)
        {
            if (!await IsValidAppKey(appKey, out var app))
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var createdContact = await _contactService.CreateContactAsync(contact, new Guid(app.Id));
            return CreatedAtAction(nameof(GetContact), new { id = createdContact.Id }, createdContact);
        }

        [HttpGet]
        public async Task<IActionResult> GetContacts([FromHeader(Name = "AppKey")] string appKey)
        {
            if (!await IsValidAppKey(appKey, out var app))
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            var contacts = await _contactService.GetContactsAsync(new Guid(app.Id));
            return Ok(contacts);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetContact(string id, [FromHeader(Name = "AppKey")] string appKey)
        {
            if (!await IsValidAppKey(appKey, out var app))
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            var contact = await _contactService.GetContactByIdAsync(id, new Guid(app.Id));
            if (contact == null)
                return NotFound();

            return Ok(contact);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateContact(string id, [FromBody] Contact contact, [FromHeader(Name = "AppKey")] string appKey)
        {
            if (!await IsValidAppKey(appKey, out var app))
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            if (id != contact.Id || contact.ApplicationId != new Guid(app.Id))
                return BadRequest();

            await _contactService.UpdateContactAsync(id, contact, new Guid(app.Id));
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteContact(string id, [FromHeader(Name = "AppKey")] string appKey)
        {
            if (!await IsValidAppKey(appKey, out var app))
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            await _contactService.DeleteContactAsync(id, new Guid(app.Id));
            return NoContent();
        }

        private async Task<bool> IsValidAppKey(string appKey, out Application app)
        {
            var applications = await _applicationService.GetApplicationsAsync(appKey);
            app = applications.FirstOrDefault(a => a.AppKey == appKey);
            return app != null && await _applicationService.ValidateAppKeyAndSecretAsync(appKey, app.AppSecret);
        }
    }
}
