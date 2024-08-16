using AuthMicroservice.Model;
using AuthMicroservice.Repository;
using Microsoft.AspNetCore.Identity;
namespace AuthMicroservice.Service
{
    public interface ILoginService
    {
        Task<User> AuthenticateLoginUserAsync(string username, string password);
    }
    public class LoginService : ILoginService
    {
        //private readonly List<User> _users = new();
        private readonly IPasswordHasher<User> _passwordHasher = new PasswordHasher<User>();
        private readonly IUserRepository _userRepository;
        public LoginService(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }
        public async Task<User>AuthenticateLoginUserAsync(string username, string password)
        {
            // Fetch the user asynchronously
            var user = (await _userRepository.FindAsync(u => u.UserName == username)).FirstOrDefault();

            if (user != null && _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password) == PasswordVerificationResult.Success)
            {
                return user;
            }

            return null;
        }
    }
}
