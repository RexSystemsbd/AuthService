using AuthMicroservice.Model;
using AuthMicroservice.Repository;
using Microsoft.AspNetCore.Identity;

namespace AuthMicroservice.Service
{
    public interface IUserService
    {
        Task<User> RegisterUserAsync(Guid applicationId, string email, string mobileNumber, string password);
        Task<User> AuthenticateUserAsync(Guid applicationId, string email, string password);
        Task<bool> ResetPasswordAsync(Guid applicationId, string email, string newPassword);    }

    public class UserService : IUserService
    {
        private readonly List<User> _users = new();
        private readonly IPasswordHasher<User> _passwordHasher = new PasswordHasher<User>();
        private readonly IUserRepository _userRepository;
        public UserService(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }
        public async Task<User> RegisterUserAsync(Guid applicationId, string email, string mobileNumber, string password)
        {
            if (_userRepository.FindAsync(a => a.Email == email || a.PhoneNumber == mobileNumber).Result.Any())
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

            await _userRepository.AddAsync(user);
            return user;
        }


        public async Task<User> AuthenticateUserAsync(Guid applicationId, string email, string password)
        {
            // Fetch the user asynchronously
            var user = (await _userRepository.FindAsync(u => u.Email == email && u.ApplicationId == applicationId)).FirstOrDefault();

            if (user != null && _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password) == PasswordVerificationResult.Success)
            {
                return user;
            }

            return null;
        }


        public async Task<bool> ResetPasswordAsync(Guid applicationId, string email, string newPassword)
        {
            // Fetch the user asynchronously
            var user = (await _userRepository.FindAsync(u => u.Email == email && u.ApplicationId == applicationId)).FirstOrDefault();

            if (user != null)
            {
                // Hash the new password and update the user
                user.PasswordHash = _passwordHasher.HashPassword(null, newPassword);
                await _userRepository.UpdateAsync(user); // Ensure this method is asynchronous
                return true;
            }

            return false;
        }

    }

}
