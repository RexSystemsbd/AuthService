using System;
using System.ComponentModel.DataAnnotations;

namespace AuthMicroservice.Model
{
    public class SmtpConfig : BaseEntity
    {
        [Required]
        public string Host { get; set; }
        [Required]
        public int Port { get; set; }
        [Required]
        public string Username { get; set; }
        [Required]
        public string Password { get; set; }
        public bool EnableSsl { get; set; }
        public string FromAddress { get; set; }
        public string FromName { get; set; }

        [Required]
        public Guid ApplicationId { get; set; }
    }
}
