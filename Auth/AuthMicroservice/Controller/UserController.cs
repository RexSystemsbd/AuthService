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
        public IActionResult RegisterUser([FromBody] RegisterUserRequest request,
        [FromHeader(Name = "AppKey")] string appKey,
        [FromHeader(Name = "AppSecret")] string appSecret)
        {

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var app = _applicationService.GetApplication(appKey).Result.FirstOrDefault();
            if (app == null || !_applicationService.ValidateAppKeyAndSecret(appKey, appSecret))
            {
                return Unauthorized();
            }

            try
            {
                var user = _userService.RegisterUser(app.Id, request.Email, request.MobileNumber, request.Password);
                return Ok(user);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("login")]
        public IActionResult LoginUser([FromBody] LoginRequest request,
            [FromHeader(Name = "AppKey")] string appKey,
            [FromHeader(Name = "AppSecret")] string appSecret)
        {

            var app = _applicationService.GetApplication(appKey).Result.FirstOrDefault();
            if (app == null || !_applicationService.ValidateAppKeyAndSecret(appKey, appSecret))
            {
                return Unauthorized();
            }

            var user = _userService.AuthenticateUser(app.Id, request.Email, request.Password);
            if (user != null)
            {
                // Generate JWT token here
                return Ok(user);
            }
            return Unauthorized();
        }

        [HttpPost("reset-password")]
        public IActionResult ResetPassword([FromBody] ResetPasswordRequest request,
            [FromHeader(Name = "AppKey")] string appKey,
            [FromHeader(Name = "AppSecret")] string appSecret)
        {

            var app = _applicationService.GetApplication(appKey).Result.FirstOrDefault();
            if (app == null || !_applicationService.ValidateAppKeyAndSecret(appKey, appSecret))
            {
                return Unauthorized();
            }

            if (_userService.ResetPassword(app.Id, request.Email, request.NewPassword))
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
