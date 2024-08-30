using Microsoft.AspNetCore.Identity;

//using System.ComponentModel.DataAnnotations;

//public class AtLeastOneRequiredAttribute : ValidationAttribute
//{
//    protected override ValidationResult IsValid(object value, ValidationContext validationContext)
//    {
//        var emailProperty = validationContext.ObjectType.GetProperty("Email");
//        var firstNameProperty = validationContext.ObjectType.GetProperty("FirstName");
//        var lastNameProperty = validationContext.ObjectType.GetProperty("LastName");
//        var phoneNumberProperty = validationContext.ObjectType.GetProperty("PhoneNumber");

//        var emailValue = emailProperty.GetValue(validationContext.ObjectInstance) as string;
//        var firstNameValue = firstNameProperty.GetValue(validationContext.ObjectInstance) as string;
//        var lastNameValue = lastNameProperty.GetValue(validationContext.ObjectInstance) as string;
//        var phoneNumberValue = phoneNumberProperty.GetValue(validationContext.ObjectInstance) as string;

//        if (!string.IsNullOrWhiteSpace(emailValue) ||
//            (!string.IsNullOrWhiteSpace(firstNameValue) && !string.IsNullOrWhiteSpace(lastNameValue)) ||
//            !string.IsNullOrWhiteSpace(phoneNumberValue))
//        {
//            return ValidationResult.Success;
//        }

//        return new ValidationResult("At least one of the following fields is required: Email, FirstName and LastName, or PhoneNumber.");
//    }
//}
namespace AuthMicroservice.Model
{
   // [AtLeastOneRequired]
    public class User:IdentityUser
    {

        public string? Email { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? EmailConfirmationToken { get; set; }
        public string? ResetPasswordToken { get; set; }
        public DateTime? ResetPasswordTokenExpiry { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? Address { get; set; }
        
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Country { get; set; }
        public string? PostalCode { get; set; }
        public Guid ApplicationId { get; set; }
        public string? UserName { get; set; }

    }

}
