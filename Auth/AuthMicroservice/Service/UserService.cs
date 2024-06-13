using AuthMicroservice.Model;
using Microsoft.AspNetCore.Identity;

namespace AuthMicroservice.Service
{
    public interface IUserService
    {
        User RegisterUser(Guid applicationId, string email, string mobileNumber, string password);
        User AuthenticateUser(Guid applicationId, string email, string password);
        bool ResetPassword(Guid applicationId, string email, string newPassword);    }

    public class UserService : IUserService
    {
        private readonly List<User> _users = new();
        private readonly IPasswordHasher<User> _passwordHasher = new PasswordHasher<User>();

        public User RegisterUser(Guid applicationId, string email, string mobileNumber, string password)
        {
            if (_users.Any(u => u.Email == email && u.ApplicationId == applicationId))
            {
                throw new Exception("User already exists.");
            }

            var user = new User
            {
                Id = Guid.NewGuid().ToString(),
                Email = email,
                PhoneNumber = mobileNumber,
                PasswordHash = _passwordHasher.HashPassword(null, password),
                ApplicationId = applicationId
            };

            _users.Add(user);
            return user;
        }

        public User AuthenticateUser(Guid applicationId, string email, string password)
        {
            var user = _users.FirstOrDefault(u => u.Email == email && u.ApplicationId == applicationId);
            if (user != null && _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password) == PasswordVerificationResult.Success)
            {
                return user;
            }

            return null;
        }

        public bool ResetPassword(Guid applicationId, string email, string newPassword)
        {
            var user = _users.FirstOrDefault(u => u.Email == email && u.ApplicationId == applicationId);
            if (user != null)
            {
                user.PasswordHash = _passwordHasher.HashPassword(null, newPassword);
                return true;
            }

            return false;
        }
    }

}
