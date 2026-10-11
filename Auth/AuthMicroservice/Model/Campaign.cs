using System;

namespace AuthMicroservice.Model
{
    public class Campaign : BaseEntity
    {
        public string Name { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; } = string.Empty;
        public string Channel { get; set; } = "email";
        public string Status { get; set; } = "draft";
        // "all" (subscriber list) | "filter" (contacts matching AudienceFilter) |
        // "segment" (contacts in SegmentId) | a user role name (legacy).
        public string TargetGroup { get; set; } = "all";
        public Guid? SegmentId { get; set; }
        public string? AudienceFilter { get; set; } // SegmentFilter JSON
        public bool VerifiedOnly { get; set; }
        public DateTime? ScheduledDate { get; set; }
        public string? RecurrenceInterval { get; set; } // null | "daily" | "weekly" | "monthly"
        public DateTime? SentDate { get; set; }
        public Guid ApplicationId { get; set; }
        public int TotalRecipients { get; set; }
        public int TotalSent { get; set; }
        public int TotalFailed { get; set; }
    }
}
