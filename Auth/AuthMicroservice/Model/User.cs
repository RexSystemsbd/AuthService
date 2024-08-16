using Microsoft.AspNetCore.Identity;

namespace AuthMicroservice.Model
{
    public class User:IdentityUser
    {
        public string? Email { get; set; }
        public string? EmailConfirmationToken { get; set; }
        public string? ResetPasswordToken { get; set; }
        public DateTime? ResetPasswordTokenExpiry { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? Address { get; set; }
        public string? PhoneNumber {  get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Country { get; set; }
        public string? PostalCode { get; set; }
        public Guid ApplicationId { get; set; }
        public string? UserName { get; set; }

    }

}
