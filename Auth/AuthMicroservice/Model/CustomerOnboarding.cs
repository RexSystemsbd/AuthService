using System;

namespace AuthMicroservice.Model
{
    // One customer working through an onboarding playbook. Steps are copied from the
    // template when onboarding starts, so later template edits don't rewrite history.
    public class CustomerOnboarding : BaseEntity
    {
        public Guid ContactId { get; set; }
        public Guid? TemplateId { get; set; }
        public string TemplateName { get; set; }
        public string Status { get; set; } = "in-progress"; // "in-progress" | "completed" | "cancelled"
        public DateTime StartDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public string? Owner { get; set; }
        // Serialized List<Service.OnboardingStepState>.
        public string StepsJson { get; set; } = "[]";
        // Due date of the earliest automated step not yet attempted, so the scheduler can
        // find work with an indexed query instead of parsing every onboarding's steps.
        public DateTime? NextAutomationDate { get; set; }
        public Guid ApplicationId { get; set; }
    }
}
