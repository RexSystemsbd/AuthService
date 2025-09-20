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
        private readonly ISubscriberService _subscriberService;

        public NewsletterController(
            IApplicationService applicationService,
            IUserService userService,
            ISmtpConfigService smtpConfigService,
            IEmailService emailService,
            ISubscriberService subscriberService)
        {
            _applicationService = applicationService;
            _userService = userService;
            _smtpConfigService = smtpConfigService;
            _emailService = emailService;
            _subscriberService = subscriberService;
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

        // Subscriber Management

        [HttpPost("subscribe")]
        public async Task<IActionResult> Subscribe([FromBody] SubscriberRequest request, [FromHeader(Name = "AppKey")] string appKey)
        {
            if (!await IsValidAppKey(appKey, out var app))
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            var subscriber = await _subscriberService.SubscribeAsync(request.Email, app.Id);
            return Ok(subscriber);
        }

        [HttpPost("unsubscribe")]
        public async Task<IActionResult> Unsubscribe([FromBody] SubscriberRequest request, [FromHeader(Name = "AppKey")] string appKey)
        {
            if (!await IsValidAppKey(appKey, out var app))
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            await _subscriberService.UnsubscribeAsync(request.Email, app.Id);
            return Ok(new { message = "Unsubscribed successfully." });
        }

        [HttpGet("subscribers")]
        public async Task<IActionResult> GetSubscribers([FromHeader(Name = "AppKey")] string appKey)
        {
            if (!await IsValidAppKey(appKey, out var app))
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            var subscribers = await _subscriberService.GetSubscribersAsync(app.Id);
            return Ok(subscribers);
        }

        // Email Sending

        [HttpPost("send-to-subscribers")]
        public async Task<IActionResult> SendToSubscribers([FromBody] EmailContentRequest request, [FromHeader(Name = "AppKey")] string appKey)
        {
            if (!await IsValidAppKey(appKey, out var app))
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            var subscribers = await _subscriberService.GetSubscribersAsync(app.Id);
            var recipientList = subscribers.Select(s => s.Email).ToList();

            if (recipientList.Count == 0)
                return Ok(new { message = "No subscribers to send to." });

            await _emailService.SendEmailAsync(app.Id, request.Subject, request.Body, recipientList);

            return Ok(new { message = "Email sent successfully to subscribers." });
        }

        [HttpPost("send-by-group/{groupName}")]
        public async Task<IActionResult> SendByGroup(string groupName, [FromBody] EmailContentRequest request, [FromHeader(Name = "AppKey")] string appKey)
        {
            if (!await IsValidAppKey(appKey, out var app))
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            // This is a placeholder for getting users by group. You would need to implement this functionality.
            var users = await _userService.GetUsersByGroupAsync(groupName, app.Id);
            var recipientList = users.Select(u => u.Email).ToList();

            if (recipientList.Count == 0)
                return Ok(new { message = $"No users found in group '{groupName}'." });

            await _emailService.SendEmailAsync(app.Id, request.Subject, request.Body, recipientList);

            return Ok(new { message = $"Email sent successfully to the '{groupName}' group." });
        }
        
        // Email History

        [HttpGet("history")]
        public async Task<IActionResult> GetEmailHistory([FromHeader(Name = "AppKey")] string appKey)
        {
            if (!await IsValidAppKey(appKey, out var app))
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            var history = await _emailService.GetEmailHistoryAsync(app.Id);
            return Ok(history);
        }

        private async Task<bool> IsValidAppKey(string appKey, out Application app)
        {
            var applications = await _applicationService.GetApplicationsAsync(appKey);
            app = applications.FirstOrDefault(a => a.AppKey == appKey);
            return app != null && await _applicationService.ValidateAppKeyAndSecretAsync(appKey, app.AppSecret);
        }
    }

    public class SubscriberRequest
    {
        public string Email { get; set; }
    }

    public class EmailContentRequest
    {
        public string Subject { get; set; }
        public string Body { get; set; }
    }
}
