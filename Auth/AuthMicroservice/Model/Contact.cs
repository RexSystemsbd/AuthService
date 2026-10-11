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
        public string? Status { get; set; }
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
        public bool IsVerified { get; set; }
        public DateTime? VerifiedDate { get; set; }
        public string? VerificationToken { get; set; }
        public DateTime? VerificationTokenExpiry { get; set; }
        public string? Nid { get; set; }
        public string? HomeAddress { get; set; }
        public string? WorkAddress { get; set; }
        public string? District { get; set; }
        public string? Division { get; set; }
        public string? Thana { get; set; }
        public string? Gps { get; set; }
        public string? HealthData { get; set; }
        public string? ExtraData { get; set; }
        public DateTime? NextContactDate { get; set; }
        public DateTime? LastEmailDate { get; set; }
        public DateTime? LastSmsDate { get; set; }
        public DateTime? LastFacebookMessageDate { get; set; }
        public DateTime? LastWhatsappDate { get; set; }
        // Facebook Messenger page-scoped user id. Only obtainable once this contact has
        // messaged the connected Page (Meta's Send API cannot target arbitrary users);
        // populated by a webhook receiver on first inbound message, not by this API.
        public string? FacebookPsid { get; set; }
        // Comma-separated free-form labels ("vip, wholesale") used to segment the data bank.
        public string? Tags { get; set; }
        public string? Website { get; set; }

        // Personal details
        public string? Salutation { get; set; } // "Mr.", "Ms.", "Dr.", ...
        public string? Gender { get; set; }
        public DateTime? DateOfBirth { get; set; } // date only
        public DateTime? Anniversary { get; set; } // date only
        public string? Nationality { get; set; }

        // Additional ways to reach the contact; Email and Phone stay the primary ones.
        public string? PersonalEmail { get; set; }
        public string? WorkEmail { get; set; }
        public string? AlternatePhone { get; set; }
        public string? WorkPhone { get; set; }
        public string? WhatsappNumber { get; set; }

        // Social profiles (URL or handle)
        public string? LinkedInUrl { get; set; }
        public string? FacebookUrl { get; set; }
        public string? TwitterUrl { get; set; }
        public string? InstagramUrl { get; set; }

        public string? PostalCode { get; set; }
        public string? CompanySize { get; set; } // e.g. "11-50"
        public string? ReferredBy { get; set; }
        public string? Owner { get; set; } // team member responsible for this contact
    }
}
