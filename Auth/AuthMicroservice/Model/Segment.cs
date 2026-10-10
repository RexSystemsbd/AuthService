using System;

namespace AuthMicroservice.Model
{
    // A saved contact filter ("VIP customers in Dhaka", "Leads with no activity in 30 days")
    // that campaigns and feedback surveys can target. Membership is evaluated live on each
    // use rather than stored, so it always reflects the current data bank.
    public class Segment : BaseEntity
    {
        public string Name { get; set; }
        public string? Description { get; set; }
        // Serialized Service.SegmentFilter. JSON rather than columns so new criteria don't
        // each need a migration.
        public string FilterJson { get; set; } = "{}";
        public Guid ApplicationId { get; set; }
    }
}
