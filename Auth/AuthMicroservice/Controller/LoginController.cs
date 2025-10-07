using AuthMicroservice.Model;
using AuthMicroservice.Service;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
namespace AuthMicroservice.Controller
{
    [ApiController]
    [Route("[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly IApplicationService _applicationService;
        private readonly ILoginService _loginService;
        private readonly IUserService _userService; 
        public AuthController(IUserService userService,IConfiguration configuration, IApplicationService applicationService,ILoginService loginService)
        {
            _userService = userService; 
            _configuration = configuration;
            _applicationService = applicationService;
            _loginService = loginService;
        }

        [HttpPost("login")]
        public async  Task<IActionResult> Login([FromBody] LoginModel model, [FromHeader(Name = "AppKey")] string appKey)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            // Verify the appKey
            // Fetch applications asynchronously
            var applications = await _applicationService.GetApplicationsAsync(appKey);
            var app = applications.FirstOrDefault();


            if (app == null)
            {
                return Unauthorized();
            }
            // Validate the application key 
            //if (model.AppKey != _configuration["Jwt:AppKey"])
            //{
            //    return Unauthorized("Invalid appKey.");
            //}

            // Replace this with your user authentication logic
            var user=await _loginService.AuthenticateLoginUserAsync(model.Username, model.Password,app.Id);
            if (user == null)
            {
                return Unauthorized();
            }
            //If any userRole exist?
           
             var  userRole = await _loginService.GetUserRoleAsync(user.Email, user.PhoneNumber, app.Id);
            var tokenString = _userService.GetToken(user, app.AppSecret, model.Username);

            if (userRole!=null)
            {
                var role = userRole.RoleName;
                //userName may be Email or MobileNumber or FistName&LastName
                return Ok(new
                {
                    UserId = user.Id,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Email= user.Email,
                    Role= role, 
                    MobileNumber=user.PhoneNumber, 
                    Token = tokenString
                });
            }

            return Ok(new
            {
                UserId = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                MobileNumber = user.PhoneNumber,
                Token = tokenString
            });
        }
   
    }

    public class LoginModel
    {
     
        public string Username { get; set; }
        public string Password { get; set; }
    }
}
