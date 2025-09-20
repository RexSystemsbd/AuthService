using System;
using System.Collections.Generic;

namespace AuthMicroservice.Model
{
    public class EmailHistory : BaseEntity
    {
        public string Subject { get; set; }
        public string Body { get; set; }
        public string Recipients { get; set; } // Storing as a comma-separated string
        public DateTime SentDate { get; set; }
        public string Status { get; set; }
        public Guid ApplicationId { get; set; }
    }
}
