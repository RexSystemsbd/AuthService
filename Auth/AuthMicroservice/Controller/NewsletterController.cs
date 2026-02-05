using AuthMicroservice.Model;
using AuthMicroservice.Service;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mail;
using Microsoft.AspNetCore.Http;

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
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid)
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            var configs = await _smtpConfigService.GetSmtpConfigsAsync();
            return Ok(configs.Where(c => c.ApplicationId == app.Id));
        }

        [HttpGet("smtp-configs/{id}")]
        public async Task<IActionResult> GetSmtpConfig(string id, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid)
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            var config = await _smtpConfigService.GetSmtpConfigAsync(new Guid(id));
            if (config == null || config.ApplicationId != app.Id)
                return NotFound();

            return Ok(config);
        }

        [HttpPost("smtp-configs")]
        public async Task<IActionResult> CreateSmtpConfig([FromBody] SmtpConfig smtpConfig, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid)
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
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid)
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            if (id != smtpConfig.Id.ToString() || smtpConfig.ApplicationId != app.Id)
                return BadRequest();

            await _smtpConfigService.UpdateSmtpConfigAsync(new Guid(id), smtpConfig);
            return NoContent();
        }

        [HttpDelete("smtp-configs/{id}")]
        public async Task<IActionResult> DeleteSmtpConfig(string id, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid)
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            var config = await _smtpConfigService.GetSmtpConfigAsync(new Guid(id));
            if (config == null || config.ApplicationId != app.Id)
                return NotFound();

            await _smtpConfigService.DeleteSmtpConfigAsync(new Guid(id));
            return NoContent();
        }

        // Subscriber Management

        [HttpPost("subscribe")]
        public async Task<IActionResult> Subscribe([FromBody] SubscriberRequest request, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid)
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            var subscriber = await _subscriberService.SubscribeAsync(request.Email, app.Id.ToString());
            return Ok(subscriber);
        }


        // Contact with us

        [HttpPost("contactwithus")]
        public async Task<IActionResult> ContactWithUS([FromBody] ContactWithUSReqest request, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid)
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            var subscriber = await _subscriberService.ContactWithUSAsync(request, app.Id.ToString());

            //string message = $"Name: {request.Name}\nPhone: {request.PhoneNumber}\nEmail: {request.Email}\nMessage: {request.Body}";
            //string subject = request.Subject ?? "New Contact Us Message";
            //List<string> emailList=new List<string>();
            //if (!string.IsNullOrEmpty(app.ContactEmail))
            //{
            //    foreach (var item in app.ContactEmail.Split(';'))
            //    {
            //        emailList.Add(item); 
            //    }
            //}
            //await _emailService.SendEmailAsync(app.Id, subject, message, emailList);
            var subject = string.IsNullOrWhiteSpace(request.Subject)
    ? "New Contact Us Message"
    : request.Subject;

            var message = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
</head>
<body style='margin:0; padding:0; font-family: Arial, Helvetica, sans-serif; background-color:#f5f5f5;'>

    <div style='max-width:600px; margin:20px auto; background-color:#ffffff; padding:20px; border-radius:6px;'>

        <h2 style='margin-top:0; color:#333;'>New Contact Request</h2>

        <p style='margin:8px 0;'>
            <strong>Name:</strong>
            {request.Name}
        </p>

        <p style='margin:8px 0;'>
            <strong>Phone:</strong>
            {request.PhoneNumber}
        </p>

        <p style='margin:8px 0;'>
            <strong>Email:</strong>
            {request.Email}
        </p>

        {(string.IsNullOrWhiteSpace(request.Company)
                        ? string.Empty
                        : $@"
        <p style='margin:8px 0;'>
            <strong>Company:</strong>
            {request.Company}
        </p>")}

        <hr style='margin:16px 0; border:none; border-top:1px solid #ddd;' />

        <p style='margin:8px 0;'>
            <strong>Message:</strong>
        </p>

        <div style='background-color:#f9f9f9; padding:12px; border-radius:4px; white-space:pre-line;'>
            {request.Body}
        </div>

    </div>

</body>
</html>";

            var emailList = new List<string>();

            if (!string.IsNullOrWhiteSpace(app.ContactEmail))
            {
                emailList = app.ContactEmail
                    .Split(';', StringSplitOptions.RemoveEmptyEntries)
                    .Select(e => e.Trim())
                    .ToList();
            }

            await _emailService.SendEmailAsync(app.Id, subject, message, emailList);

            return Ok(subscriber);
        }

        [HttpPost("contactwithattachment")]
        public async Task<IActionResult> ContactWithAttachment([FromForm] ContactWithAttachmentRequest request, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid)
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            // Using the same service method for tracking/subscription
            var subscriber = await _subscriberService.ContactWithUSAsync(new ContactWithUSReqest
            {
                Name = request.Name,
                Email = request.Email,
                PhoneNumber = request.PhoneNumber,
                Company = request.Company,
                Subject = request.Subject,
                Body = request.Body
            }, app.Id.ToString());

            //string message = $"Name: {request.Name}\nPhone: {request.PhoneNumber}\nEmail: {request.Email}\nMessage: {request.Body}";
            var message = $@"
<!DOCTYPE html>
<html>
<body style='font-family: Arial, Helvetica, sans-serif; line-height:1.6; color:#333;'>

    <p>You have received a new contact request.</p>

    <p>
        <strong>Name:</strong>
        {request.Name}
    </p>

    <p>
        <strong>Phone:</strong>
        {request.PhoneNumber}
    </p>

    <p>
        <strong>Email:</strong>
        {request.Email}
    </p>

    {(string.IsNullOrWhiteSpace(request.Company)
         ? string.Empty
         : $@"
    <p>
        <strong>Company:</strong>
        {request.Company}
    </p>")}

    <p>
        <strong>Message:</strong>
    </p>

    <div style='background-color:#f7f7f7; padding:12px; border-radius:4px; white-space:pre-line;'>
        {request.Body}
    </div>

</body>
</html>
".Trim();

            string subject = request.Subject ?? "New Contact Us Message (with attachment)";
            List<string> emailList = new List<string>();
            if (!string.IsNullOrEmpty(app.ContactEmail))
            {
                foreach (var item in app.ContactEmail.Split(';'))
                {
                    emailList.Add(item);
                }
            }

            List<Attachment> attachments = new List<Attachment>();
            if (request.Attachments != null)
            {
                foreach (var file in request.Attachments)
                {
                    if (file.Length > 0)
                    {
                        var stream = file.OpenReadStream();
                        attachments.Add(new Attachment(stream, file.FileName, file.ContentType));
                    }
                }
            }

            try
            {
                await _emailService.SendEmailAsync(app.Id, subject, message, emailList, attachments);
            }
            finally
            {
                // Ensure streams are disposed if necessary, though System.Net.Mail.Attachment usually handles it if we don't dispose early.
                // However, Attachment doesn't automatically close the stream until it's disposed.
                // MailMessage.Dispose() will dispose attachments.
            }

            return Ok(subscriber);
        }

        [HttpPost("unsubscribe")]
        public async Task<IActionResult> Unsubscribe([FromBody] SubscriberRequest request, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid)
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            await _subscriberService.UnsubscribeAsync(request.Email, app.Id.ToString());
            return Ok(new { message = "Unsubscribed successfully." });
        }

        [HttpGet("subscribers")]
        public async Task<IActionResult> GetSubscribers([FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid)
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            var subscribers = await _subscriberService.GetSubscribersAsync(app.Id.ToString());
            return Ok(subscribers);
        }

        // Email Sending

        [HttpPost("send-to-subscribers")]
        public async Task<IActionResult> SendToSubscribers([FromBody] EmailContentRequest request, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid)
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            var subscribers = await _subscriberService.GetSubscribersAsync(app.Id.ToString());
            var recipientList = subscribers.Select(s => s.Email).ToList();

            if (recipientList.Count == 0)
                return Ok(new { message = "No subscribers to send to." });

            await _emailService.SendEmailAsync(app.Id, request.Subject, request.Body, recipientList);

            return Ok(new { message = "Email sent successfully to subscribers." });
        }

        [HttpPost("send-by-group/{groupName}")]
        public async Task<IActionResult> SendByGroup(string groupName, [FromBody] EmailContentRequest request, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid)
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

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
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid)
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            var history = await _emailService.GetEmailHistoryAsync(app.Id);
            return Ok(history);
        }

        private async Task<(bool, Application)> IsValidAppKey(string appKey)
        {
            var applications = await _applicationService.GetApplicationsAsync(appKey);
            var app = applications.FirstOrDefault(a => a.AppKey == appKey);
            if (app == null) return (false, null);

            var isValid = await _applicationService.ValidateAppKeyAndSecretAsync(appKey, app.AppSecret);
            return (isValid, app);
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

    public class ContactWithAttachmentRequest
    {
        public string Name { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string Company { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }
        public IFormFileCollection Attachments { get; set; }
    }
}
