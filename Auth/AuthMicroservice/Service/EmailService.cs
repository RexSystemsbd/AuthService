using AuthMicroservice.Model;
using AuthMicroservice.Repository;
using System.Collections.Generic;
using System.Net;
using System.Net.Mail;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System;
using System.Linq;

namespace AuthMicroservice.Service
{
    public class EmailRecipient
    {
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Company { get; set; }
        // Extra per-recipient {{variables}}, e.g. a feedback survey's private response link.
        public Dictionary<string, string>? Variables { get; set; }
    }

    public class PersonalizedSendResult
    {
        public int SentCount { get; set; }
        public int FailedCount { get; set; }
        public string Status { get; set; }
        public List<string> FailedEmails { get; set; } = new List<string>();
    }

    public interface IEmailService
    {
        Task<string> SendEmailAsync(Guid applicationId, string subject, string body, List<string> to);
        Task<string> SendEmailAsync(Guid applicationId, string subject, string body, List<string> to, List<Attachment> attachments);
        Task<PersonalizedSendResult> SendPersonalizedEmailAsync(Guid applicationId, string subjectTemplate, string bodyTemplate, List<EmailRecipient> recipients, Dictionary<string, string> globalVariables = null);
        Task<IEnumerable<EmailHistory>> GetEmailHistoryAsync(Guid applicationId);
        Task SendTransactionalEmailAsync(Guid applicationId, string subject, string body, string to);
    }

    public class EmailService : IEmailService
    {
        private readonly ISmtpConfigService _smtpConfigService;
        private readonly IEmailHistoryRepository _emailHistoryRepository;
        private readonly IContactActivityService _contactActivityService;

        public EmailService(ISmtpConfigService smtpConfigService, IEmailHistoryRepository emailHistoryRepository, IContactActivityService contactActivityService)
        {
            _smtpConfigService = smtpConfigService;
            _emailHistoryRepository = emailHistoryRepository;
            _contactActivityService = contactActivityService;
        }

        // For security emails (password reset links): sent through the tenant's SMTP but
        // deliberately NOT written to EmailHistory or the contact activity log, since both
        // are readable by every user of the tenant and the body carries a live credential.
        // Throws if SMTP isn't configured or delivery fails.
        public async Task SendTransactionalEmailAsync(Guid applicationId, string subject, string body, string to)
        {
            var smtpConfig = await _smtpConfigService.GetSmtpConfigByApplicationIdAsync(applicationId);
            if (smtpConfig == null)
                throw new Exception("SMTP configuration not found for this application.");

            using var client = new SmtpClient(smtpConfig.Host, smtpConfig.Port)
            {
                UseDefaultCredentials = false,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = 10000,
                Credentials = new NetworkCredential(smtpConfig.Username, smtpConfig.Password),
                EnableSsl = smtpConfig.EnableSsl,
            };
            using var mailMessage = new MailMessage
            {
                From = new MailAddress(smtpConfig.FromAddress, smtpConfig.FromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true,
            };
            mailMessage.To.Add(to);
            await client.SendMailAsync(mailMessage);
        }

        public async Task<string> SendEmailAsync(Guid applicationId, string subject, string body, List<string> to)
        {
            return await SendEmailAsync(applicationId, subject, body, to, null);
        }

        public async Task<string> SendEmailAsync(Guid applicationId, string subject, string body, List<string> to, List<Attachment> attachments)
        {
            var smtpConfig = await _smtpConfigService.GetSmtpConfigByApplicationIdAsync(applicationId);
            if (smtpConfig == null)
                throw new System.Exception("SMTP configuration not found for this application.");

            string status = "Sent";
            try
            {
                using (var client = new SmtpClient(smtpConfig.Host, smtpConfig.Port))
                {
                    client.UseDefaultCredentials = false;
                    client.DeliveryMethod = SmtpDeliveryMethod.Network;
                    client.Timeout = 10000;
                    client.Credentials = new NetworkCredential(smtpConfig.Username, smtpConfig.Password);
                    client.EnableSsl = smtpConfig.EnableSsl;

                    var mailMessage = new MailMessage
                    {
                        From = new MailAddress(smtpConfig.FromAddress, smtpConfig.FromName),
                        Subject = subject,
                        Body = body,
                        IsBodyHtml = true,
                    };

                    foreach (var email in to)
                    {
                        mailMessage.To.Add(email);
                    }

                    if (attachments != null && attachments.Count > 0)
                    {
                        foreach (var attachment in attachments)
                        {
                            mailMessage.Attachments.Add(attachment);
                        }
                    }

                    await client.SendMailAsync(mailMessage);
                }
            }
            catch (Exception ex)
            { 
                status = "Failed: " + ex.Message;
            }

            var emailHistory = new EmailHistory
            {
                Subject = subject,
                Body = body,
                Recipients = string.Join(",", to),
                SentDate = DateTime.UtcNow,
                Status = status,
                ApplicationId = applicationId
            };

            await _emailHistoryRepository.AddAsync(emailHistory);

            var activityStatus = status == "Sent" ? "Sent" : "Failed";
            foreach (var recipientEmail in to)
            {
                await _contactActivityService.LogActivityAsync(applicationId, recipientEmail, "Email", subject, body, activityStatus);
            }

            return status;
        }

        public async Task<PersonalizedSendResult> SendPersonalizedEmailAsync(Guid applicationId, string subjectTemplate, string bodyTemplate, List<EmailRecipient> recipients, Dictionary<string, string> globalVariables = null)
        {
            var smtpConfig = await _smtpConfigService.GetSmtpConfigByApplicationIdAsync(applicationId);
            if (smtpConfig == null)
                throw new Exception("SMTP configuration not found for this application.");

            int sentCount = 0;
            int failedCount = 0;
            var failedEmails = new List<string>();

            using (var client = new SmtpClient(smtpConfig.Host, smtpConfig.Port))
            {
                client.UseDefaultCredentials = false;
                client.DeliveryMethod = SmtpDeliveryMethod.Network;
                client.Timeout = 10000;
                client.Credentials = new NetworkCredential(smtpConfig.Username, smtpConfig.Password);
                client.EnableSsl = smtpConfig.EnableSsl;

                foreach (var recipient in recipients)
                {
                    if (string.IsNullOrWhiteSpace(recipient?.Email))
                        continue;

                    var variables = new Dictionary<string, string>(globalVariables ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase)
                    {
                        ["firstName"] = recipient.FirstName ?? "",
                        ["lastName"] = recipient.LastName ?? "",
                        ["company"] = recipient.Company ?? "",
                        ["email"] = recipient.Email ?? "",
                    };
                    if (!variables.ContainsKey("date"))
                        variables["date"] = DateTime.Now.ToString("MMMM d, yyyy");
                    if (recipient.Variables != null)
                    {
                        foreach (var pair in recipient.Variables)
                            variables[pair.Key] = pair.Value;
                    }

                    var renderedSubject = MessageTemplate.Apply(subjectTemplate, variables);
                    var renderedBody = MessageTemplate.Apply(bodyTemplate, variables);
                    var recipientSucceeded = true;

                    try
                    {
                        using var mailMessage = new MailMessage
                        {
                            From = new MailAddress(smtpConfig.FromAddress, smtpConfig.FromName),
                            Subject = renderedSubject,
                            Body = renderedBody,
                            IsBodyHtml = true,
                        };
                        mailMessage.To.Add(recipient.Email);

                        await client.SendMailAsync(mailMessage);
                        sentCount++;
                    }
                    catch
                    {
                        failedCount++;
                        failedEmails.Add(recipient.Email);
                        recipientSucceeded = false;
                    }

                    await _contactActivityService.LogActivityAsync(applicationId, recipient.Email, "Email", renderedSubject, renderedBody, recipientSucceeded ? "Sent" : "Failed");
                }
            }

            var status = failedCount == 0
                ? "Sent"
                : sentCount == 0
                    ? "Failed"
                    : $"Partially sent ({sentCount}/{sentCount + failedCount})";

            var emailHistory = new EmailHistory
            {
                Subject = subjectTemplate,
                Body = bodyTemplate,
                Recipients = string.Join(",", recipients.Where(r => !string.IsNullOrWhiteSpace(r?.Email)).Select(r => r.Email)),
                SentDate = DateTime.UtcNow,
                Status = status,
                ApplicationId = applicationId
            };

            await _emailHistoryRepository.AddAsync(emailHistory);

            return new PersonalizedSendResult { SentCount = sentCount, FailedCount = failedCount, Status = status, FailedEmails = failedEmails };
        }

        public async Task<IEnumerable<EmailHistory>> GetEmailHistoryAsync(Guid applicationId)
        {
            var allHistory = await _emailHistoryRepository.GetAllAsync();
            return allHistory.Where(h => h.ApplicationId == applicationId);
        }

    }
}
