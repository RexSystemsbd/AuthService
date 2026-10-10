using AuthMicroservice.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AuthMicroservice.Controller
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class FeedbackController : TenantControllerBase
    {
        private readonly IFeedbackService _feedbackService;

        public FeedbackController(IFeedbackService feedbackService, IApplicationService applicationService)
            : base(applicationService)
        {
            _feedbackService = feedbackService;
        }

        [HttpGet("surveys")]
        public async Task<IActionResult> GetSurveys([FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid) return InvalidAppKey();

            return Ok(await _feedbackService.GetSurveysAsync(app.Id));
        }

        [HttpGet("surveys/{id}")]
        public async Task<IActionResult> GetSurvey(Guid id, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid) return InvalidAppKey();

            var survey = await _feedbackService.GetSurveyAsync(id, app.Id);
            return survey == null ? NotFound() : Ok(survey);
        }

        [HttpPost("surveys")]
        public async Task<IActionResult> CreateSurvey([FromBody] FeedbackSurveyInput input, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid) return InvalidAppKey();

            var error = Validate(input);
            if (error != null) return BadRequest(new { message = error });

            var survey = await _feedbackService.CreateSurveyAsync(input, app.Id);
            return CreatedAtAction(nameof(GetSurvey), new { id = survey.Id }, survey);
        }

        [HttpPut("surveys/{id}")]
        public async Task<IActionResult> UpdateSurvey(Guid id, [FromBody] FeedbackSurveyInput input, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid) return InvalidAppKey();

            var error = Validate(input);
            if (error != null) return BadRequest(new { message = error });

            var survey = await _feedbackService.UpdateSurveyAsync(id, input, app.Id);
            return survey == null ? NotFound() : Ok(survey);
        }

        [HttpDelete("surveys/{id}")]
        public async Task<IActionResult> DeleteSurvey(Guid id, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid) return InvalidAppKey();

            await _feedbackService.DeleteSurveyAsync(id, app.Id);
            return NoContent();
        }

        [HttpPost("surveys/{id}/send")]
        public async Task<IActionResult> SendSurvey(Guid id, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid) return InvalidAppKey();

            var result = await _feedbackService.SendSurveyAsync(id, app.Id);
            if (result.Error == "Survey not found.") return NotFound();
            if (!result.Success) return BadRequest(new { message = result.Error, result.Recipients, result.Sent, result.Failed });
            return Ok(result);
        }

        [HttpGet("responses")]
        public async Task<IActionResult> GetResponses([FromQuery] Guid? surveyId, [FromQuery] Guid? contactId, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid) return InvalidAppKey();

            return Ok(await _feedbackService.GetResponsesAsync(app.Id, surveyId, contactId));
        }

        // Public: the customer-facing response page. No login; the unguessable token in the
        // emailed link is the credential, scoped to the tenant named by the AppKey header.
        [AllowAnonymous]
        [HttpGet("public/{token}")]
        public async Task<IActionResult> GetPublicSurvey(string token, [FromHeader(Name = "AppKey")] string appKey)
        {
            var app = await ResolveApplicationAsync(appKey);
            var view = await _feedbackService.GetPublicViewAsync(token, app);
            return view == null ? NotFound(new { message = "This feedback link is invalid or has expired." }) : Ok(view);
        }

        [AllowAnonymous]
        [HttpPost("public/{token}")]
        public async Task<IActionResult> SubmitPublicResponse(string token, [FromBody] PublicFeedbackRequest request, [FromHeader(Name = "AppKey")] string appKey)
        {
            if (request?.Score == null)
                return BadRequest(new { message = "Please choose a score." });

            var app = await ResolveApplicationAsync(appKey);
            var (ok, error) = await _feedbackService.SubmitResponseAsync(token, request.Score.Value, request.Comment, app);
            return ok ? Ok(new { message = "Thank you for your feedback!" }) : BadRequest(new { message = error });
        }

        private static string Validate(FeedbackSurveyInput input)
        {
            if (input == null) return "Survey details are required.";
            if (string.IsNullOrWhiteSpace(input.Name)) return "Survey name is required.";
            if (string.IsNullOrWhiteSpace(input.Question)) return "A question is required.";
            if (string.IsNullOrWhiteSpace(input.EmailSubject)) return "An email subject is required.";
            if (string.IsNullOrWhiteSpace(input.EmailBody)) return "An email body is required.";
            // Without one of these the email gives customers no way to answer.
            if (!Regex.IsMatch(input.EmailBody, @"\{\{\s*(ratingButtons|feedbackLink)\s*\}\}", RegexOptions.IgnoreCase))
                return "The email body must include {{ratingButtons}} or {{feedbackLink}} so customers can respond.";
            if (string.Equals(input.TargetGroup, "segment", StringComparison.OrdinalIgnoreCase) && !input.SegmentId.HasValue)
                return "Choose a segment to send this survey to.";
            if (input.NextSendDate.HasValue && input.NextSendDate.Value <= DateTime.UtcNow)
                return "The scheduled send time must be in the future.";
            return null;
        }
    }

    public class PublicFeedbackRequest
    {
        public int? Score { get; set; }
        public string? Comment { get; set; }
    }
}
