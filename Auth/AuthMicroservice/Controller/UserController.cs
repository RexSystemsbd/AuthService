using AuthMicroservice.Service;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AuthMicroservice.Controller
{
    [ApiController]
    [Route("[controller]")]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IApplicationService _applicationService;

        public UserController(IUserService userService, IApplicationService applicationService)
        {
            _userService = userService;
            _applicationService = applicationService;
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
                var user = await _userService.RegisterUserAsync(app.Id, request.Email, request.MobileNumber, request.Password);
                return Ok(user);
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
        public string Email { get; set; }
        public string MobileNumber { get; set; }
        public string Password { get; set; }
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

}
