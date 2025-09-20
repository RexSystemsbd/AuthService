using System;

namespace AuthMicroservice.Model
{
    public class Contact : BaseEntity
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string Email { get; set; }
        public string? Phone { get; set; }
        public string? Company { get; set; }
        public string? JobTitle { get; set; }
        public string? Department { get; set; }
        public string? TopicsOfInterest { get; set; }
        public string? Source { get; set; }
        public string? Notes { get; set; }
        public Guid ApplicationId { get; set; }
        public string? Category { get; set; }
        public string? Grade { get; set; }
        public string? Industry { get; set; }
        public string? Country { get; set; }
        public string? City { get; set; }
        public string? TimeZone { get; set; }
        public bool IsSubscribed { get; set; }
        public DateTime? SubscribedDate { get; set; }
        public DateTime? UnsubscribedDate { get; set; }
        public int? EngagementScore { get; set; }
        public DateTime? LastInteractionDate { get; set; }
        public string? PreferredLanguage { get; set; }
    }
}
