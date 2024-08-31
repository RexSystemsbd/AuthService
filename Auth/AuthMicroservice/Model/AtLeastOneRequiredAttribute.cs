
using AuthMicroservice.Controller;
using System.ComponentModel.DataAnnotations;
namespace AuthMicroservice.Model
{
   

    public class AtLeastOneRequiredAttribute : ValidationAttribute
    {
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            var userName = value as UserName;
            if (userName == null)
            {
                return new ValidationResult("UserName is required.");
            }

            bool isEmailProvided = !string.IsNullOrEmpty(userName.Email);
            bool isMobileNumberProvided = !string.IsNullOrEmpty(userName.MobileNumber);
            bool isFirstNameProvided = !string.IsNullOrEmpty(userName.FirstName);
            bool isLastNameProvided = !string.IsNullOrEmpty(userName.LastName);

            if (isEmailProvided || isMobileNumberProvided || (isFirstNameProvided && isLastNameProvided))
            {
                return ValidationResult.Success;
            }

            return new ValidationResult("Either Email, MobileNumber, or both FirstName and LastName are required.");
        }
    }

}
