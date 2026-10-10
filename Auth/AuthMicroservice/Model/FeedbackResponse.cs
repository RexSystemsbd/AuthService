using System;

namespace AuthMicroservice.Model
{
    // One survey request to one contact, and their answer once they give it.
    public class FeedbackResponse : BaseEntity
    {
        public Guid SurveyId { get; set; }
        public Guid ContactId { get; set; }
        public Guid ApplicationId { get; set; }
        // Unguessable per-recipient token. The public feedback page is keyed by it, so
        // whoever holds the emailed link can answer, but only for this one request.
        public string Token { get; set; }
        public string Status { get; set; } = "sent"; // "sent" | "failed" | "responded"
        public DateTime SentDate { get; set; }
        public DateTime? RespondedDate { get; set; }
        public int? Score { get; set; }
        public string? Comment { get; set; }
    }
}
