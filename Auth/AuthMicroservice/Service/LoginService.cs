using AuthMicroservice.Controller;
using AuthMicroservice.Model;
using AuthMicroservice.Repository;
using Microsoft.AspNetCore.Identity;
namespace AuthMicroservice.Service
{
    public interface ILoginService
    {
        Task<User> AuthenticateLoginUserAsync(string username, string password, Guid appId);
        Task<UserRole> GetUserRoleAsync(string email, string phone, Guid appId);
        
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
     

        public async Task<User> AuthenticateLoginUserAsync(string identifier, string password,Guid appId)
        {
            User user = null;

            // First, try to determine the type of identifier (email, phone number, username)
            if (IsValidEmail(identifier))
            {
                user = (await _userRepository.FindAsync(u => u.Email == identifier&&u.ApplicationId==appId)).FirstOrDefault();
            }
            else if (IsValidPhoneNumber(identifier))
            {
                user = (await _userRepository.FindAsync(u => u.PhoneNumber == identifier && u.ApplicationId == appId)).FirstOrDefault();
            }
            else
            {
                user = (await _userRepository.FindAsync(u => u.UserName == identifier && u.ApplicationId == appId)).FirstOrDefault();
            }

            // Verify the password
            if (user != null && _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password) == PasswordVerificationResult.Success)
            {
                return user;
            }

            return null;
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
            
            // Adjusted regex pattern for 11-digit numbers starting with 0
            return System.Text.RegularExpressions.Regex.IsMatch(number, @"^0\d{10}$");
        }

        public async Task<UserRole> GetUserRoleAsync(string email,string phone,Guid appId)
        {
           var user=await _userRoleRepository.FindAsync(a=>((a.UserName== email)||(a.UserName==phone)) && a.ApplicationId==appId);
            if (user==null)
            {
                throw new Exception("No such userRole exist");
            }
            return user.FirstOrDefault();
        }

      

    }
}
