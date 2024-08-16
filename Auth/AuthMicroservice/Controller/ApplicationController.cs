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


        [HttpPost("register")]
        public async Task<IActionResult> RegisterApplication([FromBody] string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return BadRequest("Application name cannot be null or empty.");
            }

            var app = await _applicationService.RegisterApplicationAsync(name);

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
