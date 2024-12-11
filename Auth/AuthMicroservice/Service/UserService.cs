using AuthMicroservice.Controller;
using AuthMicroservice.Model;
using AuthMicroservice.Repository;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Org.BouncyCastle.Ocsp;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography.Xml;
using System.Text;
using static System.Net.Mime.MediaTypeNames;
namespace AuthMicroservice.Service
{
    public interface IUserService
    {
        Task<User> RegisterUserAsync(Guid applicationId, RegisterUserRequest req);
        Task<UserRole> RegisterUserRoleAsync(Guid AppId, string appName, string role, string n);
        Task<User> AuthenticateUserAsync(Guid applicationId, string email, string password);
        Task<bool> ResetPasswordAsync(Guid applicationId, string email, string newPassword);
        Task<User> ExistedUserAsync(string email,string mobileNumber,Guid appId);
        string GetToken(User user, string AppSecret, string Username);
        Task<User> FindOrCreateUserAsync(string email, string mobile, string username, Guid appId);
        Task<User> FindOrCreateUserForLoginWithGoogleAsync(Guid appId, string userRole, string firstname, string lastname,string fullname, string email);
        Task<User> FindOrCreateUserForFacebookAsync(string email,string username, Guid appId);
    }

    public class UserService : IUserService
    {
        private readonly List<User> _users = new();
        private readonly IPasswordHasher<User> _passwordHasher = new PasswordHasher<User>();
        private readonly IUserRepository _userRepository;
        private readonly IUserRoleRepository _userRoleRepository;
        private IConfiguration _config;
        public UserService(IUserRepository userRepository, IUserRoleRepository userRoleRepository, IConfiguration config)
        {
            _userRepository = userRepository;
            _userRoleRepository = userRoleRepository;
            _config = config;
        }
        public async Task<User> ExistedUserAsync(string email,string mobile, Guid appId )
        {
            var user=await _userRepository.FindAsync(a=>a.ApplicationId==appId&&(a.Email== email&&email!="" || a.PhoneNumber== mobile&&mobile!=""));
           
            return user.FirstOrDefault();

        }
        public async Task<User> FindOrCreateUserForFacebookAsync(string email ,string username, Guid appId)
        {
           var users = await _userRepository.FindAsync(a => a.UserName == username && a.ApplicationId == appId);
            if(users.Any())
            {
                return users.FirstOrDefault();
            }
            var user = new User()
            {
                Id = Guid.NewGuid().ToString(),
                Email = email ,
                FirstName = "",
                LastName = "",
                UserName = username,
                PhoneNumber = "",
                PasswordHash = _passwordHasher.HashPassword(null, Guid.NewGuid().ToString()),
                ApplicationId = appId
            };
            await _userRepository.AddAsync(user);
            return user;
        }

        public async Task<User> RegisterUserAsync(Guid applicationId, RegisterUserRequest req)
        {


            var user = new User
            {
                Id = Guid.NewGuid().ToString(),
                Email = req.userName.Email,
                FirstName= req.userName.FirstName,
                LastName= req.userName.LastName,
                UserName=req.userName.FirstName+"_"+req.userName.LastName,
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
        public async Task<User> FindOrCreateUserForLoginWithGoogleAsync(Guid appId, string userRole, string firstname, string lastname, string fullname, string email)
        {
            var users =await _userRepository.FindAsync(a =>( a.Email == email || a.UserName == fullname)&&a.ApplicationId==appId);
            if (users.Any())
            {
                return users.FirstOrDefault();
            }
            var user = new User
            {
                Id = Guid.NewGuid().ToString(),
                Email = email,
                FirstName = firstname,
                LastName = lastname,
                UserName = fullname,
                PhoneNumber = "",
                PasswordHash = _passwordHasher.HashPassword(null, Guid.NewGuid().ToString()),
                ApplicationId = appId
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
        public async Task<User> FindOrCreateUserAsync(string email, string mobile, string username, Guid appId)
        {
            var userExisted = await ExistedUserAsync(email,mobile,appId);
            if (userExisted != null)
            {
                return userExisted;
            }
            var user = new User
            {
                Id = Guid.NewGuid().ToString(),
                Email = email,
                UserName = username,    
                PhoneNumber=mobile,
                PasswordHash = _passwordHasher.HashPassword(null, Guid.NewGuid().ToString()),
                ApplicationId = appId
             };


            await _userRepository.AddAsync(user);

            return user;
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
        public string GetToken(User user,string AppSecret,string Username)
        {


            var key = Encoding.ASCII.GetBytes(AppSecret);
            var tokenHandler = new JwtSecurityTokenHandler();
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.Name,Username)
                }),
                Claims = new Dictionary<string, object>(),
                Expires = DateTime.UtcNow.AddHours(12),
                Audience = "your-audience-here",  // Set your audience here
                Issuer = "your-issuer-here",  // Set your issuer here
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            tokenDescriptor.Claims.Add("Id", user.Id);
            tokenDescriptor.Claims.Add("ApplicationId", user.ApplicationId);
            tokenDescriptor.Claims.Add("UserName", user.UserName);
            tokenDescriptor.Claims.Add("FirstName", user.FirstName);
            tokenDescriptor.Claims.Add("LastName", user.LastName);
            tokenDescriptor.Claims.Add("Email", user.Email);
            tokenDescriptor.Claims.Add("PhoneNumber", user.PhoneNumber);

            var token = tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = tokenHandler.WriteToken(token);
            return tokenString;
        }


    }

}
