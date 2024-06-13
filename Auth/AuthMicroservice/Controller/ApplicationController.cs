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
        public IActionResult RegisterApplication([FromBody] string name)
        {
            var app = _applicationService.RegisterApplication(name);
            return Ok(app.Result);
        }

        [HttpPost("validate")]
        public IActionResult ValidateApplication([FromBody] Application app)
        {
            if (_applicationService.ValidateAppKeyAndSecret(app.AppKey, app.AppSecret))
            {
                return Ok();
            }
            return Unauthorized();
        }
    }

}
