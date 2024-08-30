using AuthMicroservice.Controller;
using AuthMicroservice.Model;
using AuthMicroservice.Repository;
using Microsoft.AspNetCore.Identity;
using static System.Net.Mime.MediaTypeNames;
namespace AuthMicroservice.Service
{
    public interface IUserService
    {
        Task<User> RegisterUserAsync(Guid applicationId, RegisterUserRequest req);
        Task<UserRole> RegisterUserRoleAsync(Guid AppId, string appName, string role, string n);
        Task<User> AuthenticateUserAsync(Guid applicationId, string email, string password);
        Task<bool> ResetPasswordAsync(Guid applicationId, string email, string newPassword);
        Task<bool> isExistUserAsync(string u);

    }
    
    public class UserService : IUserService
    {
        private readonly List<User> _users = new();
        private readonly IPasswordHasher<User> _passwordHasher = new PasswordHasher<User>();
        private readonly IUserRepository _userRepository;
        private readonly IUserRoleRepository _userRoleRepository;
        public UserService(IUserRepository userRepository, IUserRoleRepository userRoleRepository)
        {
            _userRepository = userRepository;
            _userRoleRepository = userRoleRepository;   
             
        }
        public async Task<bool> isExistUserAsync(string userName)
        {
            var user=await _userRepository.FindAsync(a=>a.Email== userName || a.PhoneNumber== userName || a.FirstName+" "+a.LastName==userName);
            if(user.Any()) return true;
            return false;
        }
        public async Task<User> RegisterUserAsync(Guid applicationId, RegisterUserRequest req)
        {
           
          

            var user = new User
            {
                Id = Guid.NewGuid().ToString(),
                Email = req.userName.Email,
                FirstName= req.userName.FirstName,
                LastName= req.userName.LastName,
               // UserName=req.FirstName+req.LastName,
                PhoneNumber = req.userName.MobileNumber,
                PasswordHash = _passwordHasher.HashPassword(null, req.Password),
                ApplicationId = applicationId
            };
          

            await _userRepository.AddAsync(user);
           
            return user;
        }

        public async Task<UserRole> RegisterUserRoleAsync(Guid AppId,string appName, string role,string name)
        {
            var userRole = new UserRole
            {
                Id = Guid.NewGuid(),
                ApplicationId = AppId,
                RoleName = role,
                UserName=name,
                ApplicationName = appName,
                CreatedDate = DateTime.Now

            };
            await _userRoleRepository.AddAsync(userRole);
            return userRole;    
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
