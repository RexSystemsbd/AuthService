using AuthMicroservice.Model;
using AuthMicroservice.Service;
using System.Collections.Generic;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace AuthMicroservice.Service
{
    public class EmailService : IEmailService
    {
        private readonly ISmtpConfigService _smtpConfigService;

        public EmailService(ISmtpConfigService smtpConfigService)
        {
            _smtpConfigService = smtpConfigService;
        }

        public async Task SendEmailAsync(string applicationId, string subject, string body, List<string> to)
        {
            var smtpConfig = await _smtpConfigService.GetSmtpConfigByApplicationIdAsync(applicationId);
            if (smtpConfig == null)
                throw new System.Exception("SMTP configuration not found for this application.");

            using (var client = new SmtpClient(smtpConfig.Host, smtpConfig.Port))
            {
                client.UseDefaultCredentials = false;
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

                await client.SendMailAsync(mailMessage);
            }
        }
    }
}
