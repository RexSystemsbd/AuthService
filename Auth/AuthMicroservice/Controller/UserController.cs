using AuthMicroservice.Model;
using AuthMicroservice.Repository;
using AuthMicroservice.Service;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Net.Http;
using System.Reflection.Metadata;
using Google.Apis.Auth;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Identity; // For JsonConvert
namespace AuthMicroservice.Controller
{
    [ApiController]
    [Route("[controller]")]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IApplicationService _applicationService;
        private readonly ILogger _logger;
        private readonly HttpClient _httpClient;

        public UserController(IUserService userService, IApplicationService applicationService, HttpClient httpClient, ILogger<UserController> logger)
        {
            _userService = userService;
            _applicationService = applicationService;
            _httpClient = httpClient;
            _logger = logger;

        }
        [HttpPost("GoogleLogin")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginViewModel request)
        {
            // Check if the request is null
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Google login attempt with missing request data.");
                return BadRequest(new Messages
                {
                    IsAuthorized = false,
                    Message = "Invalid request.Data is not valid required valid format.",
                    MessageType = "LoginFailed",
                    Status = false
                });
            }

            // Log the validated Google token details
            _logger.LogInformation("Google token validated. Email: {Email}, Name: {Name}", request.email, request.display_name);

            // Retrieve the application by ID
            var application = await _applicationService.GetApplicationByIdAsync(Guid.Parse(request.appId));

            // Validate the application key and secret
            if (application == null || !await _applicationService.ValidateAppKeyAndSecretAsync(application.AppKey, application.AppSecret))
            {
                _logger.LogWarning("Application not registered: {AppId}", request.appId);
                return BadRequest(new Messages
                {
                    IsAuthorized = false,
                    Message = "Invalid request. Application has not been registered yet.",
                    MessageType = "LoginFailed",
                    Status = false
                });
            }

            // Find or create the user in the system
            var user = await _userService.FindOrCreateUserAsync(request.email, request.mobile, request.display_name, Guid.Parse(request.appId));

            if (user != null)
            {
                if (user.ApplicationId.ToString() != request.appId)
                {
                    _logger.LogWarning("Application is mismatched: {ApplicationId}", user.ApplicationId);
                    return BadRequest(new Messages
                    {
                        IsAuthorized = false,
                        Message = "Invalid request. Application is mismatched.",
                        MessageType = "LoginFailed",
                        Status = false
                    });
                }
                // Return success response with user info and JWT token
                return Ok(new
                {
                    IsAuthorized = true,
                    user.Id,
                    idname =request.id_name,
                    user.Email,
                    user.PhoneNumber,
                    //user.EmailConfirmed,
                    //user.Address,
                    user.UserName,
                    user.ApplicationId,
                    ApplicationName=application.Name,
                    firebasetoken=request.firebase_token,
                    token = _userService.GetToken(user,application.AppSecret,request.email) // Assuming you have a GetToken method to generate JWT token
                });
            }

            // Handle case where user creation failed
            return BadRequest(new Messages
            {
                IsAuthorized = false,
                Message = "Failed to create or find user.",
                MessageType = "LoginFailed",
                Status = false
            });
        }



        [HttpPost("register")]
        public async Task<IActionResult> RegisterUser([FromBody] RegisterUserRequest request,
       [FromHeader(Name = "AppKey")] string appKey,
       [FromHeader(Name = "AppSecret")] string appSecret)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var applications = await _applicationService.GetApplicationsAsync(appKey);
            var app = applications.FirstOrDefault(a => a.AppKey == appKey);

            if (app == null || !await _applicationService.ValidateAppKeyAndSecretAsync(appKey, appSecret))
            {
                return Unauthorized();
            }

            try
            {
                
                string email = request.userName.Email;
                string mobileNumber = request.userName.MobileNumber;
                if (email == null&& mobileNumber==null)
                {
                    throw new Exception("Invalid Input..Write correct userName");
                }

                var name=email==null?mobileNumber:email; 

                var userExist = await _userService.ExistedUserAsync(email,mobileNumber);

                if(userExist!=null) { throw new Exception("This user already exist"); }

                var user = await _userService.RegisterUserAsync(app.Id, request);
               
                var userRole = await _userService.RegisterUserRoleAsync(app.Id,app.Name, request.UserRole,name);
                var userWithUserRole = new
                {
                    user=user,
                    role=userRole,  
                };
                return Ok(userWithUserRole);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }



        [HttpPost("login")]
        public async Task<IActionResult> LoginUser([FromBody] LoginRequest request,
    [FromHeader(Name = "AppKey")] string appKey,
    [FromHeader(Name = "AppSecret")] string appSecret)
        {
            // Fetch applications asynchronously
            var applications = await _applicationService.GetApplicationsAsync(appKey);
            var app = applications.FirstOrDefault(a => a.AppKey == appKey);

            // Validate the application key and secret asynchronously
            if (app == null || !await _applicationService.ValidateAppKeyAndSecretAsync(appKey, appSecret))
            {
                return Unauthorized();
            }

            // Authenticate the user asynchronously
            var user = await _userService.AuthenticateUserAsync(app.Id, request.Email, request.Password);
            if (user != null)
            {
                // Generate JWT token here (assuming you have a method for this)
                // var token = GenerateJwtToken(user);

                return Ok(user);
            }

            return Unauthorized();
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request,
            [FromHeader(Name = "AppKey")] string appKey,
            [FromHeader(Name = "AppSecret")] string appSecret)
        {
            // Fetch applications asynchronously
            var applications = await _applicationService.GetApplicationsAsync(appKey);
            var app = applications.FirstOrDefault(a => a.AppKey == appKey);

            // Validate the application key and secret asynchronously
            if (app == null || !await _applicationService.ValidateAppKeyAndSecretAsync(appKey, appSecret))
            {
                return Unauthorized();
            }

            // Reset the password asynchronously
            var success = await _userService.ResetPasswordAsync(app.Id, request.Email, request.NewPassword);
            if (success)
            {
                return Ok();
            }

            return BadRequest("Password reset failed.");
        }

    }

    public class RegisterUserRequest
    {
        [Required]
        public UserName userName { get; set; }

        [Required]
        public string Password { get; set; }

        [Required]
        public string UserRole { get; set; }
    }

    [AtLeastOneRequired] // Apply the custom validation attribute here
    public class UserName
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string MobileNumber { get; set; }
       
    }
    public class GoogleLoginViewModel
    {
        public string id_name { get; set; } 
        public string display_name { get; set; }
        public string email { get; set; }
        public string mobile { get; set; }
        public string firebase_token { get; set; }
        public string appId { get; set; }
    }

    public class LoginRequest
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }

    public class ResetPasswordRequest
    {
        public string Email { get; set; }
        public string NewPassword { get; set; }
    }
    public class GoogleTokenResponse
    {
        [JsonProperty("access_token")]
        public string AccessToken { get; set; }

        [JsonProperty("refresh_token")]
        public string RefreshToken { get; set; }

         [JsonProperty("id_token")]
        public string IdToken { get; set; }
    }
    public class Messages
    {
        public bool Status { get; set; }

        public bool IsAuthorized { get; set; } = true;

        public string Message { get; set; }

        public string MessageType { get; set; }

        public object Result { get; set; }

    }

}
