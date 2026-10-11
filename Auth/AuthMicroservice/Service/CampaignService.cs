using AuthMicroservice.Model;
using AuthMicroservice.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AuthMicroservice.Service
{
    public interface ICampaignService
    {
        Task<Campaign> CreateCampaignAsync(Campaign campaign, Guid applicationId);
        Task<IEnumerable<Campaign>> GetCampaignsAsync(Guid applicationId);
        Task<Campaign> GetCampaignByIdAsync(string id, Guid applicationId);
        Task UpdateCampaignAsync(string id, Campaign campaign, Guid applicationId);
        Task DeleteCampaignAsync(string id, Guid applicationId);
        Task<Campaign> SendCampaignAsync(string id, Guid applicationId);
        Task<Campaign> CancelScheduleAsync(string id, Guid applicationId);
        Task<IEnumerable<Campaign>> GetDueScheduledCampaignsAsync();
    }

    public class CampaignService : ICampaignService
    {
        private readonly ICampaignRepository _campaignRepository;
        private readonly ISubscriberService _subscriberService;
        private readonly IUserService _userService;
        private readonly IEmailService _emailService;
        private readonly IContactRepository _contactRepository;
        private readonly IContactActivityService _contactActivityService;
        private readonly IFacebookConfigService _facebookConfigService;
        private readonly IWhatsappConfigService _whatsappConfigService;
        private readonly IFacebookService _facebookService;
        private readonly IWhatsappService _whatsappService;
        private readonly ISegmentService _segmentService;
        private readonly ISubscriberRepository _subscriberRepository;

        public CampaignService(
            ICampaignRepository campaignRepository,
            ISubscriberService subscriberService,
            IUserService userService,
            IEmailService emailService,
            IContactRepository contactRepository,
            IContactActivityService contactActivityService,
            IFacebookConfigService facebookConfigService,
            IWhatsappConfigService whatsappConfigService,
            IFacebookService facebookService,
            IWhatsappService whatsappService,
            ISegmentService segmentService,
            ISubscriberRepository subscriberRepository)
        {
            _campaignRepository = campaignRepository;
            _subscriberService = subscriberService;
            _userService = userService;
            _emailService = emailService;
            _contactRepository = contactRepository;
            _contactActivityService = contactActivityService;
            _facebookConfigService = facebookConfigService;
            _whatsappConfigService = whatsappConfigService;
            _facebookService = facebookService;
            _whatsappService = whatsappService;
            _segmentService = segmentService;
            _subscriberRepository = subscriberRepository;
        }

        private static bool TargetsFilter(Campaign campaign) =>
            string.Equals(campaign.TargetGroup, "filter", StringComparison.OrdinalIgnoreCase);

        private static bool TargetsSegment(Campaign campaign) =>
            string.Equals(campaign.TargetGroup, "segment", StringComparison.OrdinalIgnoreCase);

        // Keeps only the audience settings the chosen TargetGroup actually uses.
        private static void NormalizeAudience(Campaign campaign)
        {
            if (!TargetsSegment(campaign)) campaign.SegmentId = null;
            if (!TargetsFilter(campaign)) campaign.AudienceFilter = null;
        }

        // Contacts picked by tag/category/status/... filter or by a saved segment, for every
        // channel. Null when the campaign targets the subscriber list or a user role instead.
        // A deleted segment resolves to nobody, so the send fails with 0 recipients.
        private async Task<List<Contact>> ResolveContactAudienceAsync(Campaign campaign, Guid applicationId)
        {
            if (TargetsFilter(campaign))
                return await _segmentService.GetMatchingContactsAsync(_segmentService.ParseFilter(campaign.AudienceFilter), applicationId);
            if (TargetsSegment(campaign))
                return await _segmentService.ResolveAudienceAsync("segment", campaign.SegmentId, applicationId) ?? new List<Contact>();
            return null;
        }

        private async Task<HashSet<string>> GetUnsubscribedEmailsAsync(Guid applicationId)
        {
            var appId = applicationId.ToString();
            var unsubscribed = await _subscriberRepository.FindAsync(s => s.ApplicationId == appId && !s.IsSubscribed);
            return unsubscribed
                .Where(s => !string.IsNullOrWhiteSpace(s.Email))
                .Select(s => s.Email.Trim().ToLowerInvariant())
                .ToHashSet();
        }

        private async Task<Dictionary<string, Contact>> GetContactsByEmailAsync(Guid applicationId)
        {
            var contacts = await _contactRepository.FindAsync(c => c.ApplicationId == applicationId);
            return contacts
                .Where(c => !string.IsNullOrWhiteSpace(c.Email))
                .GroupBy(c => c.Email.Trim().ToLowerInvariant())
                .ToDictionary(g => g.Key, g => g.First());
        }

        private static bool IsFutureSchedule(DateTime? scheduledDate) =>
            scheduledDate.HasValue && scheduledDate.Value > DateTime.UtcNow;

        // Recurrence without a scheduled anchor time is meaningless, so it only
        // ever applies alongside a ScheduledDate.
        private static string NormalizeRecurrence(DateTime? scheduledDate, string recurrenceInterval) =>
            scheduledDate.HasValue ? recurrenceInterval : null;

        public async Task<Campaign> CreateCampaignAsync(Campaign campaign, Guid applicationId)
        {
            campaign.ApplicationId = applicationId;
            NormalizeAudience(campaign);
            campaign.Status = IsFutureSchedule(campaign.ScheduledDate) ? "scheduled" : "draft";
            campaign.RecurrenceInterval = NormalizeRecurrence(campaign.ScheduledDate, campaign.RecurrenceInterval);
            await _campaignRepository.AddAsync(campaign);
            return campaign;
        }

        public async Task<IEnumerable<Campaign>> GetCampaignsAsync(Guid applicationId)
        {
            var all = await _campaignRepository.GetAllAsync();
            return all.Where(c => c.ApplicationId == applicationId);
        }

        public async Task<Campaign> GetCampaignByIdAsync(string id, Guid applicationId)
        {
            var campaign = await _campaignRepository.GetByIdAsync(new Guid(id));
            if (campaign == null || campaign.ApplicationId != applicationId)
                return null;
            return campaign;
        }

        public async Task UpdateCampaignAsync(string id, Campaign campaign, Guid applicationId)
        {
            var existing = await GetCampaignByIdAsync(id, applicationId);
            if (existing == null) return;

            existing.Name = campaign.Name;
            existing.Subject = campaign.Subject;
            existing.Channel = string.IsNullOrWhiteSpace(campaign.Channel) ? existing.Channel : campaign.Channel;
            existing.Body = campaign.Body;
            existing.TargetGroup = campaign.TargetGroup;
            existing.SegmentId = campaign.SegmentId;
            existing.AudienceFilter = campaign.AudienceFilter;
            NormalizeAudience(existing);
            existing.VerifiedOnly = campaign.VerifiedOnly;
            existing.ScheduledDate = campaign.ScheduledDate;
            existing.RecurrenceInterval = NormalizeRecurrence(existing.ScheduledDate, campaign.RecurrenceInterval);

            // Draft/scheduled/failed campaigns are still editable (failed so a 0-recipient
            // send can be fixed and retried); sending/completed are left alone. Re-derive
            // status from the (possibly changed) schedule, and clear the previous attempt's
            // stats/SentDate when reviving a failed campaign so the details page doesn't show
            // stale results for a send that hasn't happened yet.
            if (existing.Status == "draft" || existing.Status == "scheduled" || existing.Status == "failed")
            {
                if (existing.Status == "failed")
                {
                    existing.TotalRecipients = 0;
                    existing.TotalSent = 0;
                    existing.TotalFailed = 0;
                    existing.SentDate = null;
                }

                existing.Status = IsFutureSchedule(existing.ScheduledDate) ? "scheduled" : "draft";
            }

            await _campaignRepository.UpdateAsync(existing);
        }

        public async Task DeleteCampaignAsync(string id, Guid applicationId)
        {
            var campaign = await GetCampaignByIdAsync(id, applicationId);
            if (campaign != null)
            {
                await _campaignRepository.RemoveAsync(campaign);
            }
        }

        public async Task<Campaign> CancelScheduleAsync(string id, Guid applicationId)
        {
            var campaign = await GetCampaignByIdAsync(id, applicationId);
            if (campaign == null || campaign.Status != "scheduled")
                return campaign;

            campaign.ScheduledDate = null;
            campaign.RecurrenceInterval = null;
            campaign.Status = "draft";
            await _campaignRepository.UpdateAsync(campaign);
            return campaign;
        }

        public async Task<IEnumerable<Campaign>> GetDueScheduledCampaignsAsync()
        {
            var now = DateTime.UtcNow;
            return await _campaignRepository.FindAsync(c =>
                c.Status == "scheduled" && c.ScheduledDate != null && c.ScheduledDate <= now);
        }

        public async Task<Campaign> SendCampaignAsync(string id, Guid applicationId)
        {
            var campaign = await GetCampaignByIdAsync(id, applicationId);
            if (campaign == null) return null;

            if (campaign.Status != "draft" && campaign.Status != "scheduled")
                return campaign;

            var channel = string.IsNullOrWhiteSpace(campaign.Channel) ? "email" : campaign.Channel.ToLowerInvariant();
            Campaign result = channel switch
            {
                "facebook" => await SendFacebookCampaignAsync(campaign, applicationId),
                "whatsapp" => await SendWhatsappCampaignAsync(campaign, applicationId),
                _ => await SendEmailCampaignAsync(campaign, applicationId),
            };

            await AdvanceRecurrenceAsync(result);
            return result;
        }

        // Bumps a recurring campaign's ScheduledDate to its next occurrence and puts it
        // back into "scheduled" status so the existing due-campaign poll in
        // CampaignSchedulerService picks it up again later. Looping forward from the
        // previous ScheduledDate (rather than just now + interval) keeps a fixed
        // time-of-day cadence even if the scheduler was briefly down, without firing a
        // burst of catch-up sends.
        private async Task AdvanceRecurrenceAsync(Campaign campaign)
        {
            if (campaign == null || string.IsNullOrWhiteSpace(campaign.RecurrenceInterval) || !campaign.ScheduledDate.HasValue)
                return;

            var now = DateTime.UtcNow;
            var next = campaign.ScheduledDate.Value;
            do { next = AddRecurrenceInterval(next, campaign.RecurrenceInterval); } while (next <= now);

            campaign.ScheduledDate = next;
            campaign.Status = "scheduled";
            await _campaignRepository.UpdateAsync(campaign);
        }

        private static DateTime AddRecurrenceInterval(DateTime date, string interval) => interval.ToLowerInvariant() switch
        {
            "daily" => date.AddDays(1),
            "weekly" => date.AddDays(7),
            "monthly" => date.AddMonths(1),
            _ => date,
        };

        private async Task<Campaign> SendEmailCampaignAsync(Campaign campaign, Guid applicationId)
        {
            List<EmailRecipient> recipients;
            var audience = await ResolveContactAudienceAsync(campaign, applicationId);
            if (audience != null)
            {
                // Same opt-out rule as feedback surveys: skip anyone who unsubscribed, either
                // from the subscriber list or on the contact itself.
                var unsubscribed = await GetUnsubscribedEmailsAsync(applicationId);
                recipients = audience
                    .Where(c => !campaign.VerifiedOnly || c.IsVerified)
                    .Where(c => !string.IsNullOrWhiteSpace(c.Email))
                    .Where(c => !unsubscribed.Contains(c.Email.Trim().ToLowerInvariant()) && !(!c.IsSubscribed && c.UnsubscribedDate.HasValue))
                    .GroupBy(c => c.Email.Trim().ToLowerInvariant())
                    .Select(g => g.First())
                    .Select(c => new EmailRecipient { Email = c.Email, FirstName = c.FirstName, LastName = c.LastName, Company = c.Company })
                    .ToList();
            }
            else if (!string.IsNullOrWhiteSpace(campaign.TargetGroup) && campaign.TargetGroup != "all")
            {
                var users = await _userService.GetUsersByGroupAsync(campaign.TargetGroup, applicationId);
                recipients = users
                    .Where(u => !campaign.VerifiedOnly || u.EmailConfirmed)
                    .Where(u => !string.IsNullOrWhiteSpace(u.Email))
                    .Select(u => new EmailRecipient { Email = u.Email, FirstName = u.FirstName, LastName = u.LastName })
                    .ToList();
            }
            else
            {
                var subscribers = await _subscriberService.GetSubscribersAsync(applicationId.ToString());
                var contactsByEmail = await GetContactsByEmailAsync(applicationId);
                recipients = subscribers
                    .Where(s => !campaign.VerifiedOnly || s.IsVerified)
                    .Where(s => !string.IsNullOrWhiteSpace(s.Email))
                    .Select(s =>
                    {
                        contactsByEmail.TryGetValue(s.Email.Trim().ToLowerInvariant(), out var contact);
                        return new EmailRecipient
                        {
                            Email = s.Email,
                            FirstName = contact?.FirstName,
                            LastName = contact?.LastName,
                            Company = contact?.Company
                        };
                    })
                    .ToList();
            }

            campaign.Status = "sending";
            campaign.TotalRecipients = recipients.Count;
            await _campaignRepository.UpdateAsync(campaign);

            if (recipients.Count == 0)
            {
                campaign.Status = "failed";
                campaign.TotalSent = 0;
                campaign.TotalFailed = 0;
                campaign.SentDate = DateTime.UtcNow;
                await _campaignRepository.UpdateAsync(campaign);
                return campaign;
            }

            var globalVariables = new Dictionary<string, string> { ["campaignName"] = campaign.Name ?? "" };
            var result = await _emailService.SendPersonalizedEmailAsync(applicationId, campaign.Subject, campaign.Body, recipients, globalVariables);

            campaign.Status = result.FailedCount == 0 ? "completed" : result.SentCount == 0 ? "failed" : "completed";
            campaign.TotalSent = result.SentCount;
            campaign.TotalFailed = result.FailedCount;
            campaign.SentDate = DateTime.UtcNow;

            await _campaignRepository.UpdateAsync(campaign);
            return campaign;
        }

        // Facebook Page Send API can only reach contacts who have previously messaged the Page
        // (Facebook does not allow cold-messaging arbitrary users), so recipients are contacts
        // with a stored FacebookPsid — not the role-based TargetGroup used for email, since that
        // identifier only exists on Contact, not User. A filter/segment audience narrows them.
        private async Task<Campaign> SendFacebookCampaignAsync(Campaign campaign, Guid applicationId)
        {
            var config = await _facebookConfigService.GetFacebookConfigByApplicationIdAsync(applicationId);
            if (config == null || string.IsNullOrWhiteSpace(config.PageAccessToken))
            {
                campaign.Status = "failed";
                campaign.TotalRecipients = 0;
                campaign.TotalSent = 0;
                campaign.TotalFailed = 0;
                campaign.SentDate = DateTime.UtcNow;
                await _campaignRepository.UpdateAsync(campaign);
                return campaign;
            }

            var contacts = await ResolveContactAudienceAsync(campaign, applicationId)
                ?? await _contactRepository.FindAsync(c => c.ApplicationId == applicationId);
            var recipients = contacts
                .Where(c => !campaign.VerifiedOnly || c.IsVerified)
                .Where(c => !string.IsNullOrWhiteSpace(c.FacebookPsid))
                .ToList();

            campaign.Status = "sending";
            campaign.TotalRecipients = recipients.Count;
            await _campaignRepository.UpdateAsync(campaign);

            if (recipients.Count == 0)
            {
                campaign.Status = "failed";
                campaign.TotalSent = 0;
                campaign.TotalFailed = 0;
                campaign.SentDate = DateTime.UtcNow;
                await _campaignRepository.UpdateAsync(campaign);
                return campaign;
            }

            int sentCount = 0;
            int failedCount = 0;
            foreach (var contact in recipients)
            {
                var sent = await _facebookService.SendMessageAsync(config.PageAccessToken, contact.FacebookPsid, campaign.Body);
                if (sent) sentCount++; else failedCount++;
                await _contactActivityService.LogActivityForContactAsync(applicationId, contact.Id, "facebook-message", campaign.Name, campaign.Body, sent ? "Sent" : "Failed");
            }

            campaign.Status = failedCount == 0 ? "completed" : sentCount == 0 ? "failed" : "completed";
            campaign.TotalSent = sentCount;
            campaign.TotalFailed = failedCount;
            campaign.SentDate = DateTime.UtcNow;
            await _campaignRepository.UpdateAsync(campaign);
            return campaign;
        }

        // WhatsApp recipients are drawn from Contact.Phone directly, same reasoning as Facebook:
        // the channel identifier lives on Contact, not on the role-based User group used for email.
        // A filter/segment audience narrows them.
        private async Task<Campaign> SendWhatsappCampaignAsync(Campaign campaign, Guid applicationId)
        {
            var config = await _whatsappConfigService.GetWhatsappConfigByApplicationIdAsync(applicationId);
            if (config == null || string.IsNullOrWhiteSpace(config.AccessToken))
            {
                campaign.Status = "failed";
                campaign.TotalRecipients = 0;
                campaign.TotalSent = 0;
                campaign.TotalFailed = 0;
                campaign.SentDate = DateTime.UtcNow;
                await _campaignRepository.UpdateAsync(campaign);
                return campaign;
            }

            var contacts = await ResolveContactAudienceAsync(campaign, applicationId)
                ?? await _contactRepository.FindAsync(c => c.ApplicationId == applicationId);
            var recipients = contacts
                .Where(c => !campaign.VerifiedOnly || c.IsVerified)
                .Where(c => !string.IsNullOrWhiteSpace(c.Phone))
                .ToList();

            campaign.Status = "sending";
            campaign.TotalRecipients = recipients.Count;
            await _campaignRepository.UpdateAsync(campaign);

            if (recipients.Count == 0)
            {
                campaign.Status = "failed";
                campaign.TotalSent = 0;
                campaign.TotalFailed = 0;
                campaign.SentDate = DateTime.UtcNow;
                await _campaignRepository.UpdateAsync(campaign);
                return campaign;
            }

            int sentCount = 0;
            int failedCount = 0;
            foreach (var contact in recipients)
            {
                var sent = await _whatsappService.SendMessageAsync(config.AccessToken, config.PhoneNumberId, contact.Phone, campaign.Body);
                if (sent) sentCount++; else failedCount++;
                await _contactActivityService.LogActivityForContactAsync(applicationId, contact.Id, "whatsapp-message", campaign.Name, campaign.Body, sent ? "Sent" : "Failed");
            }

            campaign.Status = failedCount == 0 ? "completed" : sentCount == 0 ? "failed" : "completed";
            campaign.TotalSent = sentCount;
            campaign.TotalFailed = failedCount;
            campaign.SentDate = DateTime.UtcNow;
            await _campaignRepository.UpdateAsync(campaign);
            return campaign;
        }
    }
}
