using System;

namespace AuthMicroservice.Model
{
    // A customer feedback request sent by email. Each send creates one FeedbackResponse per
    // recipient holding a private link to the public rating page.
    public class FeedbackSurvey : BaseEntity
    {
        public string Name { get; set; }
        public string RatingType { get; set; } = "nps"; // "nps" (0-10) | "csat" (1-5)
        public string Question { get; set; }
        public string? FollowUpQuestion { get; set; }
        public string EmailSubject { get; set; }
        // HTML. Supports the usual {{firstName}}-style variables plus {{ratingButtons}}
        // (one-click score links) and {{feedbackLink}} (the bare response URL).
        public string EmailBody { get; set; }
        public string? ThankYouMessage { get; set; }
        public string TargetGroup { get; set; } = "contacts"; // "contacts" | "segment"
        public Guid? SegmentId { get; set; }
        public string Status { get; set; } = "draft"; // "draft" | "scheduled"
        public DateTime? NextSendDate { get; set; }
        public string? RecurrenceInterval { get; set; } // null | "weekly" | "monthly" | "quarterly"
        public DateTime? LastSentDate { get; set; }
        public int TotalSent { get; set; }
        public Guid ApplicationId { get; set; }
    }
}
