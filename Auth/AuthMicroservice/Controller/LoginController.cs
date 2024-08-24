﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AuthMicroservice.Service;
namespace AuthMicroservice.Controller
{
    [ApiController]
    [Route("[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly IApplicationService _applicationService;
        private readonly ILoginService _loginService;
        public AuthController(IConfiguration configuration, IApplicationService applicationService,ILoginService loginService)
        {
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
            var user=await _loginService.AuthenticateLoginUserAsync(model.Username, model.Password);
            if (user!=null)
            {
                var key = Encoding.ASCII.GetBytes(app.AppSecret);
                var tokenHandler = new JwtSecurityTokenHandler();
                var tokenDescriptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(new[]
                    {
                    new Claim(ClaimTypes.Name, model.Username)
                }),
                    Expires = DateTime.UtcNow.AddHours(12),
                    Audience = "your-audience-here",  // Set your audience here
                    Issuer = "your-issuer-here",  // Set your issuer here
                    SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
                };

                var token = tokenHandler.CreateToken(tokenDescriptor);
                var tokenString = tokenHandler.WriteToken(token);

                return Ok(new
                {
                    UserId = user.Id,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Token = tokenString
                });
            }

            return Unauthorized();
        }
    }

    public class LoginModel
    {
     
        public string Username { get; set; }
        public string Password { get; set; }
    }
}
