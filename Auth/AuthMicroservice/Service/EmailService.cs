using AuthMicroservice.Model;
using AuthMicroservice.Repository;
using System.Collections.Generic;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using System;
using System.Linq;

namespace AuthMicroservice.Service
{
    public interface IEmailService
    {
        Task SendEmailAsync(string applicationId, string subject, string body, List<string> to);
        Task<IEnumerable<EmailHistory>> GetEmailHistoryAsync(string applicationId);
    }

    public class EmailService : IEmailService
    {
        private readonly ISmtpConfigService _smtpConfigService;
        private readonly IEmailHistoryRepository _emailHistoryRepository;

        public EmailService(ISmtpConfigService smtpConfigService, IEmailHistoryRepository emailHistoryRepository)
        {
            _smtpConfigService = smtpConfigService;
            _emailHistoryRepository = emailHistoryRepository;
        }

        public async Task SendEmailAsync(string applicationId, string subject, string body, List<string> to)
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
        }

        public async Task<IEnumerable<EmailHistory>> GetEmailHistoryAsync(string applicationId)
        {
            var allHistory = await _emailHistoryRepository.GetAllAsync();
            return allHistory.Where(h => h.ApplicationId == applicationId);
        }
    }
}
