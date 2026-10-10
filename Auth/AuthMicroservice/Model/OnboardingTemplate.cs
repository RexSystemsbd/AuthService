using System;

namespace AuthMicroservice.Model
{
    // A reusable onboarding playbook: an optional welcome email plus an ordered list of
    // steps (manual tasks, or messages sent automatically N days after the start).
    public class OnboardingTemplate : BaseEntity
    {
        public string Name { get; set; }
        public string? Description { get; set; }
        public string? WelcomeSubject { get; set; }
        public string? WelcomeBody { get; set; }
        // Serialized List<Service.OnboardingStepDefinition>.
        public string StepsJson { get; set; } = "[]";
        public Guid ApplicationId { get; set; }
    }
}
