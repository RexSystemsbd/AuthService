using AuthMicroservice.Model;
using AuthMicroservice.Repository;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace AuthMicroservice.Service
{
    public class FeedbackSurveyInput
    {
        public string Name { get; set; }
        public string? RatingType { get; set; }
        public string Question { get; set; }
        public string? FollowUpQuestion { get; set; }
        public string EmailSubject { get; set; }
        public string EmailBody { get; set; }
        public string? ThankYouMessage { get; set; }
        public string? TargetGroup { get; set; }
        public Guid? SegmentId { get; set; }
        public DateTime? NextSendDate { get; set; }
        public string? RecurrenceInterval { get; set; }
    }

    public class FeedbackSendResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public int Recipients { get; set; }
        public int Sent { get; set; }
        public int Failed { get; set; }
    }

    public class FeedbackResponseView
    {
        public Guid Id { get; set; }
        public Guid SurveyId { get; set; }
        public string? SurveyName { get; set; }
        public string? RatingType { get; set; }
        public Guid ContactId { get; set; }
        public string? ContactName { get; set; }
        public string? ContactEmail { get; set; }
        public string Status { get; set; }
        public DateTime SentDate { get; set; }
        public DateTime? RespondedDate { get; set; }
        public int? Score { get; set; }
        public string? Comment { get; set; }
    }

    public class PublicFeedbackView
    {
        public string? OrganizationName { get; set; }
        public string? FirstName { get; set; }
        public string RatingType { get; set; }
        public string Question { get; set; }
        public string? FollowUpQuestion { get; set; }
        public string? ThankYouMessage { get; set; }
        public bool AlreadyResponded { get; set; }
        public int? Score { get; set; }
        public string? Comment { get; set; }
    }

    public interface IFeedbackService
    {
        Task<IEnumerable<FeedbackSurvey>> GetSurveysAsync(Guid applicationId);
        Task<FeedbackSurvey> GetSurveyAsync(Guid id, Guid applicationId);
        Task<FeedbackSurvey> CreateSurveyAsync(FeedbackSurveyInput input, Guid applicationId);
        Task<FeedbackSurvey> UpdateSurveyAsync(Guid id, FeedbackSurveyInput input, Guid applicationId);
        Task DeleteSurveyAsync(Guid id, Guid applicationId);
        Task<FeedbackSendResult> SendSurveyAsync(Guid id, Guid applicationId);
        Task<List<FeedbackResponseView>> GetResponsesAsync(Guid applicationId, Guid? surveyId, Guid? contactId);
        Task<PublicFeedbackView> GetPublicViewAsync(string token, Application app);
        Task<(bool ok, string error)> SubmitResponseAsync(string token, int score, string comment, Application app);
        Task<IEnumerable<FeedbackSurvey>> GetDueSurveysAsync();
        Task SendDueSurveyAsync(FeedbackSurvey survey);
    }

    public class FeedbackService : IFeedbackService
    {
        private const int CommentMaxLength = 2000;
        private static readonly string[] RecurrenceOptions = { "weekly", "monthly", "quarterly" };

        private readonly IFeedbackSurveyRepository _surveyRepository;
        private readonly IFeedbackResponseRepository _responseRepository;
        private readonly IContactRepository _contactRepository;
        private readonly ISubscriberRepository _subscriberRepository;
        private readonly ISegmentService _segmentService;
        private readonly IEmailService _emailService;
        private readonly IContactActivityService _contactActivityService;
        private readonly IConfiguration _configuration;

        public FeedbackService(
            IFeedbackSurveyRepository surveyRepository,
            IFeedbackResponseRepository responseRepository,
            IContactRepository contactRepository,
            ISubscriberRepository subscriberRepository,
            ISegmentService segmentService,
            IEmailService emailService,
            IContactActivityService contactActivityService,
            IConfiguration configuration)
        {
            _surveyRepository = surveyRepository;
            _responseRepository = responseRepository;
            _contactRepository = contactRepository;
            _subscriberRepository = subscriberRepository;
            _segmentService = segmentService;
            _emailService = emailService;
            _contactActivityService = contactActivityService;
            _configuration = configuration;
        }

        public static (int min, int max) ScoreRange(string ratingType) =>
            string.Equals(ratingType, "csat", StringComparison.OrdinalIgnoreCase) ? (1, 5) : (0, 10);

        private static string NormalizeRatingType(string ratingType) =>
            string.Equals(ratingType, "csat", StringComparison.OrdinalIgnoreCase) ? "csat" : "nps";

        private static string NormalizeRecurrence(DateTime? nextSendDate, string interval)
        {
            if (!nextSendDate.HasValue || string.IsNullOrWhiteSpace(interval)) return null;
            var normalized = interval.Trim().ToLowerInvariant();
            return RecurrenceOptions.Contains(normalized) ? normalized : null;
        }

        private static DateTime AddInterval(DateTime date, string interval) => interval switch
        {
            "weekly" => date.AddDays(7),
            "monthly" => date.AddMonths(1),
            "quarterly" => date.AddMonths(3),
            _ => date,
        };

        private static void ApplyInput(FeedbackSurvey survey, FeedbackSurveyInput input)
        {
            survey.Name = input.Name.Trim();
            survey.RatingType = NormalizeRatingType(input.RatingType);
            survey.Question = input.Question.Trim();
            survey.FollowUpQuestion = string.IsNullOrWhiteSpace(input.FollowUpQuestion) ? null : input.FollowUpQuestion.Trim();
            survey.EmailSubject = input.EmailSubject;
            survey.EmailBody = input.EmailBody;
            survey.ThankYouMessage = string.IsNullOrWhiteSpace(input.ThankYouMessage) ? null : input.ThankYouMessage.Trim();
            survey.TargetGroup = string.Equals(input.TargetGroup, "segment", StringComparison.OrdinalIgnoreCase) ? "segment" : "contacts";
            survey.SegmentId = survey.TargetGroup == "segment" ? input.SegmentId : null;
            survey.NextSendDate = input.NextSendDate;
            survey.RecurrenceInterval = NormalizeRecurrence(input.NextSendDate, input.RecurrenceInterval);
            survey.Status = input.NextSendDate.HasValue && input.NextSendDate.Value > DateTime.UtcNow ? "scheduled" : "draft";
        }

        public async Task<IEnumerable<FeedbackSurvey>> GetSurveysAsync(Guid applicationId)
        {
            var surveys = await _surveyRepository.FindAsync(s => s.ApplicationId == applicationId);
            return surveys.OrderByDescending(s => s.CreatedDate);
        }

        public async Task<FeedbackSurvey> GetSurveyAsync(Guid id, Guid applicationId)
        {
            var survey = await _surveyRepository.GetByIdAsync(id);
            return survey == null || survey.ApplicationId != applicationId ? null : survey;
        }

        public async Task<FeedbackSurvey> CreateSurveyAsync(FeedbackSurveyInput input, Guid applicationId)
        {
            var survey = new FeedbackSurvey { ApplicationId = applicationId };
            ApplyInput(survey, input);
            await _surveyRepository.AddAsync(survey);
            return survey;
        }

        public async Task<FeedbackSurvey> UpdateSurveyAsync(Guid id, FeedbackSurveyInput input, Guid applicationId)
        {
            var survey = await GetSurveyAsync(id, applicationId);
            if (survey == null) return null;

            ApplyInput(survey, input);
            await _surveyRepository.UpdateAsync(survey);
            return survey;
        }

        // Responses are kept: they're the feedback history, and other reports reference them.
        public async Task DeleteSurveyAsync(Guid id, Guid applicationId)
        {
            var survey = await GetSurveyAsync(id, applicationId);
            if (survey != null)
                await _surveyRepository.RemoveAsync(survey);
        }

        public async Task<IEnumerable<FeedbackSurvey>> GetDueSurveysAsync()
        {
            var now = DateTime.UtcNow;
            return await _surveyRepository.FindAsync(s => s.Status == "scheduled" && s.NextSendDate != null && s.NextSendDate <= now);
        }

        public async Task SendDueSurveyAsync(FeedbackSurvey survey)
        {
            await SendSurveyAsync(survey.Id, survey.ApplicationId);
        }

        public async Task<FeedbackSendResult> SendSurveyAsync(Guid id, Guid applicationId)
        {
            var survey = await GetSurveyAsync(id, applicationId);
            if (survey == null)
                return new FeedbackSendResult { Error = "Survey not found." };

            var contacts = await _segmentService.ResolveAudienceAsync(survey.TargetGroup, survey.SegmentId, applicationId);
            if (contacts == null)
            {
                await AdvanceScheduleAsync(survey);
                return new FeedbackSendResult { Error = "The segment this survey targets no longer exists." };
            }

            var unsubscribed = await GetUnsubscribedEmailsAsync(applicationId);
            var recipients = contacts
                .Where(c => !string.IsNullOrWhiteSpace(c.Email))
                .Where(c => !unsubscribed.Contains(c.Email.Trim().ToLowerInvariant()) && !(!c.IsSubscribed && c.UnsubscribedDate.HasValue))
                .GroupBy(c => c.Email.Trim().ToLowerInvariant())
                .Select(g => g.First())
                .ToList();

            if (recipients.Count == 0)
            {
                await AdvanceScheduleAsync(survey);
                return new FeedbackSendResult { Error = "No contacts with an email address match this survey's audience." };
            }

            // Persist the requests before emailing so a link clicked the moment it arrives
            // already resolves.
            var now = DateTime.UtcNow;
            var requests = recipients.Select(c => new FeedbackResponse
            {
                SurveyId = survey.Id,
                ContactId = c.Id,
                ApplicationId = applicationId,
                Token = NewToken(),
                Status = "sent",
                SentDate = now,
            }).ToList();
            await _responseRepository.AddRangeAsync(requests);

            var requestByContact = requests.ToDictionary(r => r.ContactId);
            var emailRecipients = recipients.Select(c =>
            {
                var link = BuildResponseLink(requestByContact[c.Id].Token);
                return new EmailRecipient
                {
                    Email = c.Email.Trim(),
                    FirstName = c.FirstName,
                    LastName = c.LastName,
                    Company = c.Company,
                    Variables = new Dictionary<string, string>
                    {
                        ["feedbackLink"] = link,
                        ["ratingButtons"] = BuildRatingButtons(link, survey.RatingType),
                        ["question"] = WebUtility.HtmlEncode(survey.Question),
                    },
                };
            }).ToList();

            PersonalizedSendResult sendResult;
            try
            {
                sendResult = await _emailService.SendPersonalizedEmailAsync(applicationId, survey.EmailSubject, survey.EmailBody, emailRecipients);
            }
            catch (Exception ex)
            {
                // Typically "SMTP configuration not found". Nothing was delivered, so the
                // requests are recorded as failed rather than left looking pending.
                foreach (var request in requests) request.Status = "failed";
                await _responseRepository.UpdateRangeAsync(requests);
                await AdvanceScheduleAsync(survey);
                return new FeedbackSendResult { Error = ex.Message, Recipients = recipients.Count, Failed = recipients.Count };
            }

            var failedEmails = sendResult.FailedEmails.Select(e => e.Trim().ToLowerInvariant()).ToHashSet();
            var failedRequests = recipients
                .Where(c => failedEmails.Contains(c.Email.Trim().ToLowerInvariant()))
                .Select(c => requestByContact[c.Id])
                .ToList();
            if (failedRequests.Count > 0)
            {
                foreach (var request in failedRequests) request.Status = "failed";
                await _responseRepository.UpdateRangeAsync(failedRequests);
            }

            survey.LastSentDate = now;
            survey.TotalSent += sendResult.SentCount;
            await AdvanceScheduleAsync(survey);

            return new FeedbackSendResult
            {
                Success = sendResult.SentCount > 0,
                Error = sendResult.SentCount > 0 ? null : "Every email failed to send. Check the SMTP settings.",
                Recipients = recipients.Count,
                Sent = sendResult.SentCount,
                Failed = sendResult.FailedCount,
            };
        }

        // After a send (or a send that couldn't happen), move a recurring survey to its next
        // slot; a one-off schedule goes back to draft so it isn't retried every poll.
        private async Task AdvanceScheduleAsync(FeedbackSurvey survey)
        {
            if (!string.IsNullOrWhiteSpace(survey.RecurrenceInterval) && survey.NextSendDate.HasValue)
            {
                var now = DateTime.UtcNow;
                var next = survey.NextSendDate.Value;
                do { next = AddInterval(next, survey.RecurrenceInterval); } while (next <= now);
                survey.NextSendDate = next;
                survey.Status = "scheduled";
            }
            else if (survey.Status == "scheduled" && survey.NextSendDate.HasValue && survey.NextSendDate.Value <= DateTime.UtcNow)
            {
                survey.NextSendDate = null;
                survey.Status = "draft";
            }

            await _surveyRepository.UpdateAsync(survey);
        }

        public async Task<List<FeedbackResponseView>> GetResponsesAsync(Guid applicationId, Guid? surveyId, Guid? contactId)
        {
            var responses = await _responseRepository.FindAsync(r =>
                r.ApplicationId == applicationId &&
                (!surveyId.HasValue || r.SurveyId == surveyId.Value) &&
                (!contactId.HasValue || r.ContactId == contactId.Value));

            var surveys = (await _surveyRepository.FindAsync(s => s.ApplicationId == applicationId)).ToDictionary(s => s.Id);
            var contacts = (await _contactRepository.FindAsync(c => c.ApplicationId == applicationId)).ToDictionary(c => c.Id);

            return responses
                .OrderByDescending(r => r.RespondedDate ?? r.SentDate)
                .Select(r =>
                {
                    surveys.TryGetValue(r.SurveyId, out var survey);
                    contacts.TryGetValue(r.ContactId, out var contact);
                    return new FeedbackResponseView
                    {
                        Id = r.Id,
                        SurveyId = r.SurveyId,
                        SurveyName = survey?.Name,
                        RatingType = survey?.RatingType,
                        ContactId = r.ContactId,
                        ContactName = contact == null ? null : $"{contact.FirstName} {contact.LastName}".Trim(),
                        ContactEmail = contact?.Email,
                        Status = r.Status,
                        SentDate = r.SentDate,
                        RespondedDate = r.RespondedDate,
                        Score = r.Score,
                        Comment = r.Comment,
                    };
                })
                .ToList();
        }

        // The public page is reached with the tenant's own AppKey; a token from another
        // tenant is treated as not found rather than revealing that it exists.
        private async Task<(FeedbackResponse response, FeedbackSurvey survey)> FindForPublicAsync(string token, Application app)
        {
            if (string.IsNullOrWhiteSpace(token) || token.Length > 64 || app == null)
                return (null, null);

            var response = await _responseRepository.GetByTokenAsync(token);
            if (response == null || response.ApplicationId != app.Id || response.Status == "failed")
                return (null, null);

            var survey = await _surveyRepository.GetByIdAsync(response.SurveyId);
            return (response, survey);
        }

        public async Task<PublicFeedbackView> GetPublicViewAsync(string token, Application app)
        {
            var (response, survey) = await FindForPublicAsync(token, app);
            if (response == null || survey == null)
                return null;

            var contact = await _contactRepository.GetByIdAsync(response.ContactId);
            return new PublicFeedbackView
            {
                OrganizationName = app.Name,
                FirstName = contact?.FirstName,
                RatingType = survey.RatingType,
                Question = survey.Question,
                FollowUpQuestion = survey.FollowUpQuestion,
                ThankYouMessage = survey.ThankYouMessage,
                AlreadyResponded = response.Status == "responded",
                Score = response.Score,
                Comment = response.Comment,
            };
        }

        // Re-submitting the same link updates the answer (e.g. a customer who clicked a
        // score in the email, then came back to add a comment).
        public async Task<(bool ok, string error)> SubmitResponseAsync(string token, int score, string comment, Application app)
        {
            var (response, survey) = await FindForPublicAsync(token, app);
            if (response == null || survey == null)
                return (false, "This feedback link is invalid or has expired.");

            var (min, max) = ScoreRange(survey.RatingType);
            if (score < min || score > max)
                return (false, $"Score must be between {min} and {max}.");

            var trimmedComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
            if (trimmedComment != null && trimmedComment.Length > CommentMaxLength)
                trimmedComment = trimmedComment.Substring(0, CommentMaxLength);

            var isFirstAnswer = response.Status != "responded";
            response.Score = score;
            response.Comment = trimmedComment;
            response.Status = "responded";
            response.RespondedDate = DateTime.UtcNow;
            await _responseRepository.UpdateAsync(response);

            if (isFirstAnswer)
            {
                await _contactActivityService.LogActivityForContactAsync(
                    response.ApplicationId,
                    response.ContactId,
                    "feedback",
                    $"{survey.Name}: {score}/{max}",
                    trimmedComment ?? "(no comment)",
                    "Received");
            }

            return (true, null);
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

        private static string NewToken() =>
            Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();

        private string BuildResponseLink(string token)
        {
            var baseUrl = _configuration["Frontend:BaseUrl"]?.TrimEnd('/') ?? "http://localhost:5173";
            return $"{baseUrl}/feedback/respond/{token}";
        }

        // A row of score links for the email body: one click opens the response page with
        // that score preselected. Inline styles only, since most mail clients strip <style>.
        private static string BuildRatingButtons(string link, string ratingType)
        {
            var (min, max) = ScoreRange(ratingType);
            var isNps = min == 0;
            var html = new StringBuilder();
            html.Append("<table role=\"presentation\" cellspacing=\"0\" cellpadding=\"0\" style=\"margin:16px 0;border-collapse:separate;border-spacing:4px;\"><tr>");
            for (var score = min; score <= max; score++)
            {
                var (background, color) = isNps
                    ? score <= 6 ? ("#fee2e2", "#991b1b") : score <= 8 ? ("#fef3c7", "#92400e") : ("#dcfce7", "#166534")
                    : score <= 2 ? ("#fee2e2", "#991b1b") : score == 3 ? ("#fef3c7", "#92400e") : ("#dcfce7", "#166534");
                html.Append("<td><a href=\"").Append(WebUtility.HtmlEncode($"{link}?score={score}"))
                    .Append("\" style=\"display:inline-block;min-width:30px;padding:9px 4px;border-radius:6px;text-align:center;text-decoration:none;font:600 15px Arial,sans-serif;background:")
                    .Append(background).Append(";color:").Append(color).Append(";\">")
                    .Append(score).Append("</a></td>");
            }
            html.Append("</tr></table>");
            html.Append("<div style=\"font:12px Arial,sans-serif;color:#64748b;\">")
                .Append(isNps ? "0 = Not at all likely &nbsp;&middot;&nbsp; 10 = Extremely likely" : "1 = Very unsatisfied &nbsp;&middot;&nbsp; 5 = Very satisfied")
                .Append("</div>");
            return html.ToString();
        }
    }
}
