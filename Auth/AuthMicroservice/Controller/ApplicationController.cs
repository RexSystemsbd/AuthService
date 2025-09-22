using AuthMicroservice.Service;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AuthMicroservice.Controller
{
    [ApiController]
    [Route("[controller]")]
    public class ApplicationController : ControllerBase
    {
        private readonly IApplicationService _applicationService;

        public ApplicationController(IApplicationService applicationService)
        {
            _applicationService = applicationService;
        }


        [HttpGet("AllApp")]
        public async Task<IActionResult> AllApp(string pass)
        {
            if (string.IsNullOrEmpty(pass))
            {
                return BadRequest("Application name cannot be null or empty.");
            }
            if (pass!="pass11")
            {
                return BadRequest("Send correct APP pass");
            }

            var app = await _applicationService.GetAllAsync();

            return Ok(app);
        }

        [HttpPost("register")]
        public async Task<IActionResult> RegisterApplication([FromBody] ApplicationRequest application)
        {
            if (string.IsNullOrEmpty(application.Name))
            {
                return BadRequest("Application name cannot be null or empty.");
            }

            var app = await _applicationService.RegisterApplicationAsync(application);

            return Ok(app);
        }

        [HttpPost("validate")]
        public async Task<IActionResult> ValidateApplication([FromBody] Application app)
        {
            // Validate the application key and secret asynchronously
            if (await _applicationService.ValidateAppKeyAndSecretAsync(app.AppKey, app.AppSecret))
            {
                return Ok();
            }
            return Unauthorized();
        }




    }

}
