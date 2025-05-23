﻿using AuthMicroservice.Model;
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
        private readonly ILoginService loginService;
        public UserController(IUserService userService, IApplicationService applicationService, HttpClient httpClient, ILogger<UserController> logger, ILoginService loginService)
        {
            this.loginService = loginService;
            _userService = userService;
            _applicationService = applicationService;
            _httpClient = httpClient;
            _logger = logger;
            this.loginService = loginService;
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
                    idname = request.id_name,
                    user.Email,
                    user.PhoneNumber,
                    //user.EmailConfirmed,
                    //user.Address,
                    user.UserName,
                    user.ApplicationId,
                    ApplicationName = application.Name,
                    firebasetoken = request.firebase_token,
                    token = _userService.GetToken(user, application.AppSecret, request.email) // Assuming you have a GetToken method to generate JWT token
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

        [HttpPost("FacebookLogin")]
        public async Task<IActionResult> FacebookLogin([FromBody] LoginWithFacebookViewModel request)
        {
            // Check if the request is null
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Facebook login attempt with missing request data.");
                return BadRequest(new Messages
                {
                    IsAuthorized = false,
                    Message = "Invalid request.Data is not valid required valid format.",
                    MessageType = "LoginFailed",
                    Status = false
                });
            }

            // Log the validated Google token details
            _logger.LogInformation("Facebook token validated. Email: {Email}, Name: {Name}", request.UserName, request.PhotoUrl);

            // Retrieve the application by ID
            var application = await _applicationService.GetApplicationByIdAsync(Guid.Parse(request.AppId));

            // Validate the application key and secret
            if (application == null || !await _applicationService.ValidateAppKeyAndSecretAsync(application.AppKey, application.AppSecret))
            {
                _logger.LogWarning("Application not registered: {AppId}", request.AppId);
                return BadRequest(new Messages
                {
                    IsAuthorized = false,
                    Message = "Invalid request. Application has not been registered yet.",
                    MessageType = "LoginFailed",
                    Status = false
                });
            }
           
            // Find or create the user in the system
            var user = await _userService.FindOrCreateUserForFacebookAsync(request.Email,request.UserName, Guid.Parse(request.AppId));

            if (user != null)
            {
                if (user.ApplicationId.ToString() != request.AppId)
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
                    request.PhotoUrl,
                    user.Email,
                    user.PhoneNumber,
                    user.UserName,
                    user.ApplicationId,
                    ApplicationName = application.Name,
                    FacebookToken = request.FacebookToken,
                    token = _userService.GetToken(user, application.AppSecret, request.UserName) // Assuming you have a GetToken method to generate JWT token
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
       [FromHeader(Name = "AppKey")] string appKey)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var applications = await _applicationService.GetApplicationsAsync(appKey);
            var app = applications.FirstOrDefault(a => a.AppKey == appKey);

            if (app == null || !await _applicationService.ValidateAppKeyAndSecretAsync(appKey, app.AppSecret))
            {
                return Unauthorized();
            }

            try
            {

                string email = request.userName.Email;
                string mobileNumber = request.userName.MobileNumber;
                if (email == null && mobileNumber == null)
                {
                    throw new Exception("Invalid Input..Write correct userName");
                }

                var name = email == null ? mobileNumber : email;

                var userExist = await _userService.ExistedUserAsync(email, mobileNumber, app.Id);
                if (userExist != null)
                {
                    return Ok(new
                    {
                        message = "Already existed such user",
                        userExist
                    });
                }
                var userRole = new UserRole();
                if (userExist == null) { userExist = await _userService.RegisterUserAsync(app.Id, request);
                    userRole = await loginService.GetUserRoleAsync(email, mobileNumber, app.Id);
                }
                else
                {
                    userRole = await loginService.GetUserRoleAsync(userExist.Email, userExist.PhoneNumber, app.Id);

                }
                if (userRole == null)
                {
                    userRole = await _userService.RegisterUserRoleAsync(app.Id, app.Name, request.UserRole, email);
                }
                var userWithUserRole = new
                {
                    message="User is added successfully",
                    user = userExist,
                    role = userRole,
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
    [FromHeader(Name = "AppKey")] string appKey)
        {
            // Fetch applications asynchronously
            var applications = await _applicationService.GetApplicationsAsync(appKey);
            var app = applications.FirstOrDefault(a => a.AppKey == appKey);

            // Validate the application key and secret asynchronously
            if (app == null || !await _applicationService.ValidateAppKeyAndSecretAsync(appKey, app.AppSecret))
            {
                return Unauthorized();
            }

            // Authenticate the user asynchronously
            var user = await _userService.AuthenticateUserAsync(app.Id, request.Email, request.Password);

            if (user != null)
            {
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

        

        [HttpPost("LoginWithGoogle")]
        public async Task<IActionResult> LoginWithGoogle([FromBody] GoogleWithLoginViewModel request)
        {
            try
            {
                // Validate the input
                if (string.IsNullOrWhiteSpace(request.appKey) || string.IsNullOrWhiteSpace(request.code) ||
                    string.IsNullOrWhiteSpace(request.clientId) || string.IsNullOrWhiteSpace(request.clientSecret) ||
                    string.IsNullOrWhiteSpace(request.redirectUrl) || string.IsNullOrWhiteSpace(request.role))
                {
                    return BadRequest(new { Message = "Missing required fields." });
                }

                // Validate the application
                var applications = await _applicationService.GetApplicationsAsync(request.appKey);
                var app = applications.FirstOrDefault(a => a.AppKey == request.appKey);

                if (app == null || !await _applicationService.ValidateAppKeyAndSecretAsync(request.appKey, app.AppSecret))
                {
                    return Unauthorized(new { Message = "Invalid application key or secret." });
                }

                // Prepare the request to Google's OAuth2 token endpoint
                const string tokenEndpoint = "https://oauth2.googleapis.com/token";
                // Create the request body
                var requestBody = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("code", request.code),
                    new KeyValuePair<string, string>("client_id", request.clientId),
                    new KeyValuePair<string, string>("client_secret", request.clientSecret),
                    new KeyValuePair<string, string>("redirect_uri", request.redirectUrl),
                    new KeyValuePair<string, string>("grant_type", "authorization_code")
              });


                using var response = await _httpClient.PostAsync(tokenEndpoint, requestBody);

                if (!response.IsSuccessStatusCode)
                {
                    var errorDetails = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning($"Token exchange failed: {errorDetails}");
                    return BadRequest(new
                    {
                        Message = "Failed to exchange authorization code for tokens.",
                        StatusCode = (int)response.StatusCode,
                        Details = errorDetails
                    });
                }

                // Parse the response content
                var jsonResponse = await response.Content.ReadAsStringAsync();
                var tokenResponse = JsonConvert.DeserializeObject<GoogleTokenResponse>(jsonResponse);

                // Validate Google ID token
                var payload = await GoogleJsonWebSignature.ValidateAsync(tokenResponse.IdToken);
                if (payload == null)
                {
                    return Unauthorized(new { Message = "Invalid Google ID token." });
                }

                // Extract user information
                var email = payload.Email;
                var fullName = payload.Name?.Trim() ?? string.Empty;
                var nameParts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var firstName = nameParts.FirstOrDefault() ?? string.Empty;
                var lastName = nameParts.Length > 1 ? string.Join(" ", nameParts.Skip(1)) : string.Empty;

                // Create or find the user
                var user = await _userService.FindOrCreateUserForLoginWithGoogleAsync(
                    app.Id, request.role, firstName, lastName, fullName, email);

                if (user == null)
                {
                    return Unauthorized(new { Message = "Failed to create or retrieve the user." });
                }

                // Retrieve or assign user role
                var userRole = await loginService.GetUserRoleAsync(email,user.PhoneNumber, app.Id) ??
                               await _userService.RegisterUserRoleAsync(app.Id, app.Name, request.role, email);

                if (userRole == null)
                {
                    return Unauthorized(new { Message = "Failed to assign user role." });
                }

                // Generate token
                var tokenString = _userService.GetToken(user, app.AppSecret, email);

                return Ok(new
                {
                    UserId = user.Id,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Email = user.Email,
                    Role = userRole.RoleName,
                    MobileNumber = user.PhoneNumber,
                    Token = tokenString
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Google login.");
                return StatusCode(StatusCodes.Status500InternalServerError, new { Message = "An error occurred while processing your request." });
            }
        }



    }
    public class LoginWithFacebookViewModel
    {
        public string FacebookToken { get; set; }
        public string UserName { get; set; }
        public string Email { get; set; }    
        public string PhotoUrl { get; set; }
        public string AppId { get; set; }

    }
    public class GoogleWithLoginViewModel
    {
        public string code { get; set; }
        public string redirectUrl{ get; set; }
        public string appKey { get; set; }
        public string clientId { get; set; }
        public string clientSecret { get; set; }    
        public string role { get; set; }
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
