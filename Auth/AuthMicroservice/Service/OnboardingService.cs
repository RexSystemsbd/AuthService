using AuthMicroservice.Model;
using AuthMicroservice.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace AuthMicroservice.Service
{
    public class OnboardingStepDefinition
    {
        public string Title { get; set; }
        public string? Description { get; set; }
        // Days after the onboarding start that this step falls due.
        public int DayOffset { get; set; }
        // "task": done by hand from the checklist. "email": sent automatically when due.
        // Automation is email-only: WhatsApp needs pre-approved templates for messages the
        // customer didn't just prompt, so a scheduled free-text WhatsApp would mostly fail.
        public string Channel { get; set; } = "task";
        public string? Subject { get; set; }
        public string? Message { get; set; }
    }

    public class OnboardingStepState : OnboardingStepDefinition
    {
        public DateTime DueDate { get; set; }
        public bool Completed { get; set; }
        public DateTime? CompletedDate { get; set; }
        // Set once an automated step has been attempted: "sent" | "failed".
        public string? AutoStatus { get; set; }
        public string? Note { get; set; }
    }

    public class OnboardingTemplateInput
    {
        public string Name { get; set; }
        public string? Description { get; set; }
        public string? WelcomeSubject { get; set; }
        public string? WelcomeBody { get; set; }
        public List<OnboardingStepDefinition>? Steps { get; set; }
    }

    public class OnboardingTemplateView
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string? Description { get; set; }
        public string? WelcomeSubject { get; set; }
        public string? WelcomeBody { get; set; }
        public List<OnboardingStepDefinition> Steps { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class StartOnboardingInput
    {
        public Guid ContactId { get; set; }
        public Guid TemplateId { get; set; }
        public string? Owner { get; set; }
        public bool SendWelcome { get; set; } = true;
    }

    public class OnboardingView
    {
        public Guid Id { get; set; }
        public Guid ContactId { get; set; }
        public string? ContactName { get; set; }
        public string? ContactEmail { get; set; }
        public string? ContactPhone { get; set; }
        public string? ContactCompany { get; set; }
        public Guid? TemplateId { get; set; }
        public string TemplateName { get; set; }
        public string Status { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public string? Owner { get; set; }
        public List<OnboardingStepState> Steps { get; set; }
        public int TotalSteps { get; set; }
        public int CompletedSteps { get; set; }
        public int OverdueSteps { get; set; }
        public DateTime? NextDueDate { get; set; }
    }

    public class StartOnboardingResult
    {
        public OnboardingView Onboarding { get; set; }
        public string? Error { get; set; }
        public bool Conflict { get; set; }
        // Welcome email outcome: "sent" | "failed" | "skipped".
        public string WelcomeStatus { get; set; } = "skipped";
        public string? WelcomeError { get; set; }
    }

    public interface IOnboardingService
    {
        Task<List<OnboardingTemplateView>> GetTemplatesAsync(Guid applicationId);
        Task<OnboardingTemplateView> CreateTemplateAsync(OnboardingTemplateInput input, Guid applicationId);
        Task<OnboardingTemplateView> UpdateTemplateAsync(Guid id, OnboardingTemplateInput input, Guid applicationId);
        Task DeleteTemplateAsync(Guid id, Guid applicationId);
        Task<List<OnboardingView>> GetOnboardingsAsync(Guid applicationId, Guid? contactId);
        Task<OnboardingView> GetOnboardingAsync(Guid id, Guid applicationId);
        Task<StartOnboardingResult> StartAsync(StartOnboardingInput input, Guid applicationId);
        Task<OnboardingView> UpdateStepAsync(Guid id, int stepIndex, bool completed, string note, Guid applicationId);
        Task<OnboardingView> SetStatusAsync(Guid id, string status, Guid applicationId);
        Task<OnboardingView> SetOwnerAsync(Guid id, string owner, Guid applicationId);
        Task DeleteAsync(Guid id, Guid applicationId);
        Task ProcessDueAutomationAsync();
    }

    public class OnboardingService : IOnboardingService
    {
        private const int MaxSteps = 50;
        private const int MaxDayOffset = 365;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
        };

        private readonly IOnboardingTemplateRepository _templateRepository;
        private readonly ICustomerOnboardingRepository _onboardingRepository;
        private readonly IContactRepository _contactRepository;
        private readonly IEmailService _emailService;
        private readonly IContactActivityService _contactActivityService;

        public OnboardingService(
            IOnboardingTemplateRepository templateRepository,
            ICustomerOnboardingRepository onboardingRepository,
            IContactRepository contactRepository,
            IEmailService emailService,
            IContactActivityService contactActivityService)
        {
            _templateRepository = templateRepository;
            _onboardingRepository = onboardingRepository;
            _contactRepository = contactRepository;
            _emailService = emailService;
            _contactActivityService = contactActivityService;
        }

        private static List<T> ParseList<T>(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<T>();
            try
            {
                return JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? new List<T>();
            }
            catch (JsonException)
            {
                return new List<T>();
            }
        }

        private static List<OnboardingStepDefinition> NormalizeSteps(IEnumerable<OnboardingStepDefinition> steps) =>
            (steps ?? Enumerable.Empty<OnboardingStepDefinition>())
                .Where(s => s != null && !string.IsNullOrWhiteSpace(s.Title))
                .Take(MaxSteps)
                .Select(s => new OnboardingStepDefinition
                {
                    Title = s.Title.Trim(),
                    Description = string.IsNullOrWhiteSpace(s.Description) ? null : s.Description.Trim(),
                    DayOffset = Math.Clamp(s.DayOffset, 0, MaxDayOffset),
                    Channel = string.Equals(s.Channel, "email", StringComparison.OrdinalIgnoreCase) ? "email" : "task",
                    Subject = s.Subject,
                    Message = s.Message,
                })
                .ToList();

        private static OnboardingTemplateView ToView(OnboardingTemplate template) => new OnboardingTemplateView
        {
            Id = template.Id,
            Name = template.Name,
            Description = template.Description,
            WelcomeSubject = template.WelcomeSubject,
            WelcomeBody = template.WelcomeBody,
            Steps = ParseList<OnboardingStepDefinition>(template.StepsJson),
            CreatedDate = template.CreatedDate,
        };

        private static void ApplyTemplateInput(OnboardingTemplate template, OnboardingTemplateInput input)
        {
            template.Name = input.Name.Trim();
            template.Description = input.Description;
            template.WelcomeSubject = string.IsNullOrWhiteSpace(input.WelcomeSubject) ? null : input.WelcomeSubject;
            template.WelcomeBody = string.IsNullOrWhiteSpace(input.WelcomeBody) ? null : input.WelcomeBody;
            template.StepsJson = JsonSerializer.Serialize(NormalizeSteps(input.Steps), JsonOptions);
        }

        private async Task<OnboardingTemplate> GetTemplateEntityAsync(Guid id, Guid applicationId)
        {
            var template = await _templateRepository.GetByIdAsync(id);
            return template == null || template.ApplicationId != applicationId ? null : template;
        }

        public async Task<List<OnboardingTemplateView>> GetTemplatesAsync(Guid applicationId)
        {
            var templates = await _templateRepository.FindAsync(t => t.ApplicationId == applicationId);
            return templates.OrderBy(t => t.Name).Select(ToView).ToList();
        }

        public async Task<OnboardingTemplateView> CreateTemplateAsync(OnboardingTemplateInput input, Guid applicationId)
        {
            var template = new OnboardingTemplate { ApplicationId = applicationId };
            ApplyTemplateInput(template, input);
            await _templateRepository.AddAsync(template);
            return ToView(template);
        }

        public async Task<OnboardingTemplateView> UpdateTemplateAsync(Guid id, OnboardingTemplateInput input, Guid applicationId)
        {
            var template = await GetTemplateEntityAsync(id, applicationId);
            if (template == null) return null;

            ApplyTemplateInput(template, input);
            await _templateRepository.UpdateAsync(template);
            return ToView(template);
        }

        // Onboardings already started keep their own copy of the steps, so they're unaffected.
        public async Task DeleteTemplateAsync(Guid id, Guid applicationId)
        {
            var template = await GetTemplateEntityAsync(id, applicationId);
            if (template != null)
                await _templateRepository.RemoveAsync(template);
        }

        private async Task<CustomerOnboarding> GetEntityAsync(Guid id, Guid applicationId)
        {
            var onboarding = await _onboardingRepository.GetByIdAsync(id);
            return onboarding == null || onboarding.ApplicationId != applicationId ? null : onboarding;
        }

        private static OnboardingView ToView(CustomerOnboarding onboarding, Contact contact)
        {
            var steps = ParseList<OnboardingStepState>(onboarding.StepsJson);
            var now = DateTime.UtcNow;
            var pending = steps.Where(s => !s.Completed).ToList();
            return new OnboardingView
            {
                Id = onboarding.Id,
                ContactId = onboarding.ContactId,
                ContactName = contact == null ? null : $"{contact.FirstName} {contact.LastName}".Trim(),
                ContactEmail = contact?.Email,
                ContactPhone = contact?.Phone,
                ContactCompany = contact?.Company,
                TemplateId = onboarding.TemplateId,
                TemplateName = onboarding.TemplateName,
                Status = onboarding.Status,
                StartDate = onboarding.StartDate,
                CompletedDate = onboarding.CompletedDate,
                Owner = onboarding.Owner,
                Steps = steps,
                TotalSteps = steps.Count,
                CompletedSteps = steps.Count - pending.Count,
                OverdueSteps = onboarding.Status == "in-progress" ? pending.Count(s => s.DueDate < now) : 0,
                NextDueDate = pending.Count == 0 ? null : pending.Min(s => s.DueDate),
            };
        }

        private async Task<OnboardingView> ToViewAsync(CustomerOnboarding onboarding)
        {
            var contact = await _contactRepository.GetByIdAsync(onboarding.ContactId);
            return ToView(onboarding, contact);
        }

        public async Task<List<OnboardingView>> GetOnboardingsAsync(Guid applicationId, Guid? contactId)
        {
            var onboardings = await _onboardingRepository.FindAsync(o =>
                o.ApplicationId == applicationId && (!contactId.HasValue || o.ContactId == contactId.Value));
            var contacts = (await _contactRepository.FindAsync(c => c.ApplicationId == applicationId)).ToDictionary(c => c.Id);

            return onboardings
                .OrderByDescending(o => o.StartDate)
                .Select(o => ToView(o, contacts.TryGetValue(o.ContactId, out var contact) ? contact : null))
                .ToList();
        }

        public async Task<OnboardingView> GetOnboardingAsync(Guid id, Guid applicationId)
        {
            var onboarding = await GetEntityAsync(id, applicationId);
            return onboarding == null ? null : await ToViewAsync(onboarding);
        }

        public async Task<StartOnboardingResult> StartAsync(StartOnboardingInput input, Guid applicationId)
        {
            var contact = await _contactRepository.GetByIdAsync(input.ContactId);
            if (contact == null || contact.ApplicationId != applicationId)
                return new StartOnboardingResult { Error = "Contact not found." };

            var template = await GetTemplateEntityAsync(input.TemplateId, applicationId);
            if (template == null)
                return new StartOnboardingResult { Error = "Onboarding template not found." };

            var active = await _onboardingRepository.FindAsync(o =>
                o.ApplicationId == applicationId && o.ContactId == contact.Id && o.Status == "in-progress");
            if (active.Any())
                return new StartOnboardingResult { Conflict = true, Error = "This customer already has an onboarding in progress." };

            var start = DateTime.UtcNow;
            var steps = ParseList<OnboardingStepDefinition>(template.StepsJson)
                .Select(s => new OnboardingStepState
                {
                    Title = s.Title,
                    Description = s.Description,
                    DayOffset = s.DayOffset,
                    Channel = s.Channel,
                    Subject = s.Subject,
                    Message = s.Message,
                    DueDate = start.AddDays(s.DayOffset),
                })
                .ToList();

            var onboarding = new CustomerOnboarding
            {
                ContactId = contact.Id,
                TemplateId = template.Id,
                TemplateName = template.Name,
                Status = "in-progress",
                StartDate = start,
                Owner = string.IsNullOrWhiteSpace(input.Owner) ? null : input.Owner.Trim(),
                ApplicationId = applicationId,
            };
            SaveSteps(onboarding, steps);
            await _onboardingRepository.AddAsync(onboarding);

            await _contactActivityService.LogActivityForContactAsync(applicationId, contact.Id, "onboarding",
                $"Onboarding started: {template.Name}", $"{steps.Count} step(s)", "Logged");

            var result = new StartOnboardingResult();
            if (input.SendWelcome && !string.IsNullOrWhiteSpace(template.WelcomeSubject) && !string.IsNullOrWhiteSpace(template.WelcomeBody))
            {
                if (string.IsNullOrWhiteSpace(contact.Email))
                {
                    result.WelcomeStatus = "failed";
                    result.WelcomeError = "This customer has no email address on file.";
                }
                else
                {
                    var (sent, error) = await SendEmailAsync(applicationId, contact, template.WelcomeSubject, template.WelcomeBody);
                    result.WelcomeStatus = sent ? "sent" : "failed";
                    result.WelcomeError = error;
                }
            }

            result.Onboarding = ToView(onboarding, contact);
            return result;
        }

        public async Task<OnboardingView> UpdateStepAsync(Guid id, int stepIndex, bool completed, string note, Guid applicationId)
        {
            var onboarding = await GetEntityAsync(id, applicationId);
            if (onboarding == null) return null;

            var steps = ParseList<OnboardingStepState>(onboarding.StepsJson);
            if (stepIndex < 0 || stepIndex >= steps.Count) return null;

            var step = steps[stepIndex];
            if (step.Completed != completed)
            {
                step.Completed = completed;
                step.CompletedDate = completed ? DateTime.UtcNow : null;
            }
            if (note != null)
                step.Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

            // Ticking the last box finishes the onboarding; unticking one reopens it.
            var wasCompleted = onboarding.Status == "completed";
            if (onboarding.Status != "cancelled")
            {
                var allDone = steps.Count > 0 && steps.All(s => s.Completed);
                onboarding.Status = allDone ? "completed" : "in-progress";
                onboarding.CompletedDate = allDone ? onboarding.CompletedDate ?? DateTime.UtcNow : null;
            }
            SaveSteps(onboarding, steps);
            await _onboardingRepository.UpdateAsync(onboarding);

            if (!wasCompleted && onboarding.Status == "completed")
                await LogCompletedAsync(onboarding);

            return await ToViewAsync(onboarding);
        }

        public async Task<OnboardingView> SetStatusAsync(Guid id, string status, Guid applicationId)
        {
            var onboarding = await GetEntityAsync(id, applicationId);
            if (onboarding == null) return null;

            var wasCompleted = onboarding.Status == "completed";
            onboarding.Status = status;
            onboarding.CompletedDate = status == "completed" ? DateTime.UtcNow : null;
            if (status != "in-progress")
                onboarding.NextAutomationDate = null;
            else
                SaveSteps(onboarding, ParseList<OnboardingStepState>(onboarding.StepsJson));
            await _onboardingRepository.UpdateAsync(onboarding);

            if (!wasCompleted && status == "completed")
                await LogCompletedAsync(onboarding);
            else if (status == "cancelled")
                await _contactActivityService.LogActivityForContactAsync(applicationId, onboarding.ContactId, "onboarding",
                    $"Onboarding cancelled: {onboarding.TemplateName}", "Cancelled before completion", "Logged");

            return await ToViewAsync(onboarding);
        }

        public async Task<OnboardingView> SetOwnerAsync(Guid id, string owner, Guid applicationId)
        {
            var onboarding = await GetEntityAsync(id, applicationId);
            if (onboarding == null) return null;

            onboarding.Owner = string.IsNullOrWhiteSpace(owner) ? null : owner.Trim();
            await _onboardingRepository.UpdateAsync(onboarding);
            return await ToViewAsync(onboarding);
        }

        public async Task DeleteAsync(Guid id, Guid applicationId)
        {
            var onboarding = await GetEntityAsync(id, applicationId);
            if (onboarding != null)
                await _onboardingRepository.RemoveAsync(onboarding);
        }

        // Called by CampaignSchedulerService's poll. Sends every automated email step that
        // has come due. Each step is attempted once: a failure (no email on file, SMTP
        // down) is recorded on the step for someone to follow up by hand, not retried
        // every 30 seconds.
        public async Task ProcessDueAutomationAsync()
        {
            var now = DateTime.UtcNow;
            var due = await _onboardingRepository.FindAsync(o =>
                o.Status == "in-progress" && o.NextAutomationDate != null && o.NextAutomationDate <= now);

            foreach (var onboarding in due)
            {
                var contact = await _contactRepository.GetByIdAsync(onboarding.ContactId);
                var steps = ParseList<OnboardingStepState>(onboarding.StepsJson);

                foreach (var step in steps.Where(s => IsAutomated(s) && s.AutoStatus == null && s.DueDate <= now))
                {
                    if (contact == null || contact.ApplicationId != onboarding.ApplicationId || string.IsNullOrWhiteSpace(contact.Email))
                    {
                        step.AutoStatus = "failed";
                        step.Note ??= "Not sent: no email address on file.";
                        continue;
                    }

                    var (sent, error) = await SendEmailAsync(onboarding.ApplicationId, contact,
                        string.IsNullOrWhiteSpace(step.Subject) ? step.Title : step.Subject,
                        string.IsNullOrWhiteSpace(step.Message) ? step.Description ?? step.Title : step.Message);
                    step.AutoStatus = sent ? "sent" : "failed";
                    if (sent)
                    {
                        step.Completed = true;
                        step.CompletedDate = DateTime.UtcNow;
                    }
                    else
                    {
                        step.Note ??= $"Not sent: {error}";
                    }
                }

                SaveSteps(onboarding, steps);
                var allDone = steps.Count > 0 && steps.All(s => s.Completed);
                if (allDone)
                {
                    onboarding.Status = "completed";
                    onboarding.CompletedDate = DateTime.UtcNow;
                }
                await _onboardingRepository.UpdateAsync(onboarding);
                if (allDone)
                    await LogCompletedAsync(onboarding);
            }
        }

        private static bool IsAutomated(OnboardingStepState step) => step.Channel == "email";

        private static void SaveSteps(CustomerOnboarding onboarding, List<OnboardingStepState> steps)
        {
            onboarding.StepsJson = JsonSerializer.Serialize(steps, JsonOptions);
            var pendingAutomation = steps.Where(s => IsAutomated(s) && s.AutoStatus == null && !s.Completed).ToList();
            onboarding.NextAutomationDate = onboarding.Status == "in-progress" && pendingAutomation.Count > 0
                ? pendingAutomation.Min(s => s.DueDate)
                : null;
        }

        private async Task LogCompletedAsync(CustomerOnboarding onboarding)
        {
            await _contactActivityService.LogActivityForContactAsync(onboarding.ApplicationId, onboarding.ContactId, "onboarding",
                $"Onboarding completed: {onboarding.TemplateName}", "All steps done", "Logged");
        }

        private async Task<(bool sent, string error)> SendEmailAsync(Guid applicationId, Contact contact, string subject, string body)
        {
            try
            {
                var recipient = new EmailRecipient { Email = contact.Email.Trim(), FirstName = contact.FirstName, LastName = contact.LastName, Company = contact.Company };
                var result = await _emailService.SendPersonalizedEmailAsync(applicationId, subject, body, new List<EmailRecipient> { recipient });
                return result.SentCount > 0 ? (true, null) : (false, "The email could not be delivered.");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }
    }
}
