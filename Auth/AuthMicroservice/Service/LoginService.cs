using AuthMicroservice.Model;
using AuthMicroservice.Repository;
using Microsoft.AspNetCore.Identity;
namespace AuthMicroservice.Service
{
    public interface ILoginService
    {
        Task<User> AuthenticateLoginUserAsync(string username, string password);
        Task<UserRole> GetUserRoleAsync(string name, Guid appId);
    }
    public class LoginService : ILoginService
    {
        //private readonly List<User> _users = new();
        private readonly IPasswordHasher<User> _passwordHasher = new PasswordHasher<User>();
        private readonly IUserRepository _userRepository;
        private readonly IUserRoleRepository _userRoleRepository;   
        public LoginService(IUserRepository userRepository,IUserRoleRepository userRoleRepository)
        {
            _userRepository = userRepository;
            _userRoleRepository= userRoleRepository;    
        }
        public async Task<User> AuthenticateLoginUserAsync(string username, string password)
        {
            // Determine the type of username: Email, Phone Number, or Username
            // Fetch the user asynchronously based on the type
            User user = null;

            if (IsValidEmail(username))
            {
                user = (await _userRepository.FindAsync(u => u.Email == username)).FirstOrDefault();
            }
            else if (IsValidPhoneNumber(username))
            {
                user = (await _userRepository.FindAsync(u => u.PhoneNumber == username)).FirstOrDefault();
            }
            else
            {
                user = (await _userRepository.FindAsync(u => u.UserName == username)).FirstOrDefault();
            }

            // Verify the password
            if (user != null && _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password) == PasswordVerificationResult.Success)
            {
                return user;
            }

            return null;
        }
       public async Task<UserRole> GetUserRoleAsync(string username,Guid appId)
        {
           var user=await _userRoleRepository.FindAsync(a=>a.UserName==username||a.ApplicationId==appId);
            if (user==null)
            {
                throw new Exception("No such userRole exist");
            }
            return user.FirstOrDefault();
        }

        // Helper method to validate email
        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        // Helper method to validate phone number
        private bool IsValidPhoneNumber(string number)
        {
            return System.Text.RegularExpressions.Regex.IsMatch(number, @"^0\d{10}$");
        }


    }
}
