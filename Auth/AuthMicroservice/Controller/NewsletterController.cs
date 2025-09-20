using AuthMicroservice.Model;
using AuthMicroservice.Service;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AuthMicroservice.Controller
{
    [Route("api/[controller]")]
    [ApiController]
    public class NewsletterController : ControllerBase
    {
        private readonly IApplicationService _applicationService;
        private readonly IUserService _userService;
        private readonly ISmtpConfigService _smtpConfigService;
        private readonly IEmailService _emailService;

        public NewsletterController(
            IApplicationService applicationService,
            IUserService userService,
            ISmtpConfigService smtpConfigService,
            IEmailService emailService)
        {
            _applicationService = applicationService;
            _userService = userService;
            _smtpConfigService = smtpConfigService;
            _emailService = emailService;
        }

        // SMTP Configuration Management

        [HttpGet("smtp-configs")]
        public async Task<IActionResult> GetSmtpConfigs([FromHeader(Name = "AppKey")] string appKey)
        {
            if (!await IsValidAppKey(appKey, out var app))
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            var configs = await _smtpConfigService.GetSmtpConfigsAsync();
            return Ok(configs.Where(c => c.ApplicationId == app.Id));
        }

        [HttpGet("smtp-configs/{id}")]
        public async Task<IActionResult> GetSmtpConfig(string id, [FromHeader(Name = "AppKey")] string appKey)
        {
            if (!await IsValidAppKey(appKey, out var app))
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            var config = await _smtpConfigService.GetSmtpConfigAsync(id);
            if (config == null || config.ApplicationId != app.Id)
                return NotFound();

            return Ok(config);
        }

        [HttpPost("smtp-configs")]
        public async Task<IActionResult> CreateSmtpConfig([FromBody] SmtpConfig smtpConfig, [FromHeader(Name = "AppKey")] string appKey)
        {
            if (!await IsValidAppKey(appKey, out var app))
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            smtpConfig.ApplicationId = app.Id;
            var createdConfig = await _smtpConfigService.CreateSmtpConfigAsync(smtpConfig);
            return CreatedAtAction(nameof(GetSmtpConfig), new { id = createdConfig.Id }, createdConfig);
        }

        [HttpPut("smtp-configs/{id}")]
        public async Task<IActionResult> UpdateSmtpConfig(string id, [FromBody] SmtpConfig smtpConfig, [FromHeader(Name = "AppKey")] string appKey)
        {
            if (!await IsValidAppKey(appKey, out var app))
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            if (id != smtpConfig.Id || smtpConfig.ApplicationId != app.Id)
                return BadRequest();

            await _smtpConfigService.UpdateSmtpConfigAsync(id, smtpConfig);
            return NoContent();
        }

        [HttpDelete("smtp-configs/{id}")]
        public async Task<IActionResult> DeleteSmtpConfig(string id, [FromHeader(Name = "AppKey")] string appKey)
        {
            if (!await IsValidAppKey(appKey, out var app))
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            var config = await _smtpConfigService.GetSmtpConfigAsync(id);
            if (config == null || config.ApplicationId != app.Id)
                return NotFound();

            await _smtpConfigService.DeleteSmtpConfigAsync(id);
            return NoContent();
        }

        // Email Sending

        [HttpPost("send-to-list")]
        public async Task<IActionResult> SendEmailToList([FromBody] SendEmailRequest request, [FromHeader(Name = "AppKey")] string appKey)
        {
            if (!await IsValidAppKey(appKey, out var app))
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            await _emailService.SendEmailAsync(app.Id, request.Subject, request.Body, request.To);

            return Ok(new { message = "Email sent successfully to the list." });
        }

        [HttpPost("send-by-category/{category}")]
        public async Task<IActionResult> SendEmailByCategory(string category, [FromBody] SendEmailByCategoryRequest request, [FromHeader(Name = "AppKey")] string appKey)
        {
            if (!await IsValidAppKey(appKey, out var app))
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            var users = await _userService.GetUsersByCategoryAsync(category);
            var recipientList = users.Select(u => u.Email).ToList();

            if (recipientList.Count == 0)
                return Ok(new { message = "No users found in the specified category." });

            await _emailService.SendEmailAsync(app.Id, request.Subject, request.Body, recipientList);

            return Ok(new { message = $"Email sent successfully to users in the '{category}' category." });
        }

        private async Task<bool> IsValidAppKey(string appKey, out Application app)
        {
            var applications = await _applicationService.GetApplicationsAsync(appKey);
            app = applications.FirstOrDefault(a => a.AppKey == appKey);
            return app != null && await _applicationService.ValidateAppKeyAndSecretAsync(appKey, app.AppSecret);
        }
    }

    public class SendEmailRequest
    {
        public string Subject { get; set; }
        public string Body { get; set; }
        public List<string> To { get; set; }
    }

    public class SendEmailByCategoryRequest
    {
        public string Subject { get; set; }
        public string Body { get; set; }
    }
}
