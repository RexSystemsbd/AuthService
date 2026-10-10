using AuthMicroservice.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AuthMicroservice.Controller
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class OnboardingController : TenantControllerBase
    {
        private readonly IOnboardingService _onboardingService;

        public OnboardingController(IOnboardingService onboardingService, IApplicationService applicationService)
            : base(applicationService)
        {
            _onboardingService = onboardingService;
        }

        [HttpGet("templates")]
        public async Task<IActionResult> GetTemplates([FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid) return InvalidAppKey();

            return Ok(await _onboardingService.GetTemplatesAsync(app.Id));
        }

        [HttpPost("templates")]
        public async Task<IActionResult> CreateTemplate([FromBody] OnboardingTemplateInput input, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid) return InvalidAppKey();

            var error = Validate(input);
            if (error != null) return BadRequest(new { message = error });

            return Ok(await _onboardingService.CreateTemplateAsync(input, app.Id));
        }

        [HttpPut("templates/{id}")]
        public async Task<IActionResult> UpdateTemplate(Guid id, [FromBody] OnboardingTemplateInput input, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid) return InvalidAppKey();

            var error = Validate(input);
            if (error != null) return BadRequest(new { message = error });

            var template = await _onboardingService.UpdateTemplateAsync(id, input, app.Id);
            return template == null ? NotFound() : Ok(template);
        }

        [HttpDelete("templates/{id}")]
        public async Task<IActionResult> DeleteTemplate(Guid id, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid) return InvalidAppKey();

            await _onboardingService.DeleteTemplateAsync(id, app.Id);
            return NoContent();
        }

        [HttpGet]
        public async Task<IActionResult> GetOnboardings([FromQuery] Guid? contactId, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid) return InvalidAppKey();

            return Ok(await _onboardingService.GetOnboardingsAsync(app.Id, contactId));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetOnboarding(Guid id, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid) return InvalidAppKey();

            var onboarding = await _onboardingService.GetOnboardingAsync(id, app.Id);
            return onboarding == null ? NotFound() : Ok(onboarding);
        }

        [HttpPost]
        public async Task<IActionResult> StartOnboarding([FromBody] StartOnboardingInput input, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid) return InvalidAppKey();

            if (input == null || input.ContactId == Guid.Empty || input.TemplateId == Guid.Empty)
                return BadRequest(new { message = "Choose a customer and an onboarding template." });

            var result = await _onboardingService.StartAsync(input, app.Id);
            if (result.Conflict) return Conflict(new { message = result.Error });
            if (result.Onboarding == null) return BadRequest(new { message = result.Error });
            return Ok(new { onboarding = result.Onboarding, result.WelcomeStatus, result.WelcomeError });
        }

        [HttpPut("{id}/steps/{index}")]
        public async Task<IActionResult> UpdateStep(Guid id, int index, [FromBody] OnboardingStepUpdateRequest request, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid) return InvalidAppKey();

            if (request == null) return BadRequest(new { message = "Step update is required." });

            var onboarding = await _onboardingService.UpdateStepAsync(id, index, request.Completed, request.Note, app.Id);
            return onboarding == null ? NotFound() : Ok(onboarding);
        }

        [HttpPost("{id}/complete")]
        public Task<IActionResult> Complete(Guid id, [FromHeader(Name = "AppKey")] string appKey) => SetStatus(id, "completed", appKey);

        [HttpPost("{id}/cancel")]
        public Task<IActionResult> Cancel(Guid id, [FromHeader(Name = "AppKey")] string appKey) => SetStatus(id, "cancelled", appKey);

        [HttpPost("{id}/reopen")]
        public Task<IActionResult> Reopen(Guid id, [FromHeader(Name = "AppKey")] string appKey) => SetStatus(id, "in-progress", appKey);

        [HttpPut("{id}/owner")]
        public async Task<IActionResult> SetOwner(Guid id, [FromBody] OnboardingOwnerRequest request, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid) return InvalidAppKey();

            var onboarding = await _onboardingService.SetOwnerAsync(id, request?.Owner, app.Id);
            return onboarding == null ? NotFound() : Ok(onboarding);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid) return InvalidAppKey();

            await _onboardingService.DeleteAsync(id, app.Id);
            return NoContent();
        }

        private async Task<IActionResult> SetStatus(Guid id, string status, string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid) return InvalidAppKey();

            var onboarding = await _onboardingService.SetStatusAsync(id, status, app.Id);
            return onboarding == null ? NotFound() : Ok(onboarding);
        }

        private static string Validate(OnboardingTemplateInput input)
        {
            if (input == null || string.IsNullOrWhiteSpace(input.Name)) return "Template name is required.";
            if (input.Steps == null || !input.Steps.Any(s => !string.IsNullOrWhiteSpace(s?.Title)))
                return "Add at least one step.";
            var emailWithoutBody = input.Steps.FirstOrDefault(s =>
                string.Equals(s?.Channel, "email", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(s.Message));
            if (emailWithoutBody != null)
                return $"The automated email step \"{emailWithoutBody.Title}\" needs a message.";
            return null;
        }
    }

    public class OnboardingStepUpdateRequest
    {
        public bool Completed { get; set; }
        public string? Note { get; set; }
    }

    public class OnboardingOwnerRequest
    {
        public string? Owner { get; set; }
    }
}
