using System.Net;
using System.Text;
using AuthMicroservice.Model;
using AuthMicroservice.Service;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace AuthMicroservice.Controller
{
    [ApiController]
    [Route("[controller]")]
    public class SmsController : ControllerBase
    {
       // private readonly IMemoryCache _cache;
        private const string ApiKey = "zzTXYcqGRBpmZJCKKifM";  // Replace with your actual API key
        private const string SenderId = "8809617624929";  // Your Sender ID
        private readonly IMemoryCache _cache;
        private readonly IUserService _userService;
        private readonly IApplicationService _applicationService;
        private readonly ILoginService _loginService;
        public SmsController(ILoginService loginService, IMemoryCache memoryCache, IUserService userService, IApplicationService applicationService)
        {
            _cache = memoryCache;
            _applicationService = applicationService;
            _userService = userService; 
            _loginService = loginService;   
        }
        string result = "";
            WebRequest request = null;
            HttpWebResponse response = null;
        // Endpoint to send OTP
        [HttpPost("sendotp")]
        public async Task<IActionResult> SendOtp([FromBody] SendOtpRequest request, [FromHeader(Name = "AppKey")] string appKey)
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
            var userExist = await _userService.ExistedUserAsync("", request.PhoneNumber, app.Id);
            if (userExist == null)
            {
                return Ok(new { message = "Please register the application first"});
            }
            //If any userRole exist?

            //var userRole = await _loginService.GetUserRoleAsync(userExist.Email, userExist.PhoneNumber, app.Id);

           
             var tokenString = _userService.GetToken(userExist, app.AppSecret, request.PhoneNumber);
           
              
              
            
            try
            {
                // Generate a random OTP
                var otp = new Random().Next(100000, 999999).ToString();

                // Store OTP in memory cache for 5 minutes
                _cache.Set(request.PhoneNumber, otp, TimeSpan.FromMinutes(5));
                // Your unique hash key for SMS Retriever
                string appHashKey = "1oE89+1peG6"; // Replace with actual hash key

                // Construct the OTP message in the required format
                string message = Uri.EscapeDataString($"<#> Your OTP is: {otp}\n{appHashKey}");


                // Send the OTP via SMS
                //string message = Uri.EscapeDataString($"Your OTP is: {otp}");
               //http://bulksmsbd.net/api/smsapi?api_key=zzTXYcqGRBpmZJCKKifM&type=text&number=Receiver&senderid=8809617624929&message=TestSMS
                string url = $"http://bulksmsbd.net/api/smsapi?api_key={ApiKey}&type=text&number={request.PhoneNumber}&senderid={SenderId}&message={message}";

                // Create the WebRequest to send the SMS
                WebRequest requestSms = WebRequest.Create(url);
                HttpWebResponse response = (HttpWebResponse)requestSms.GetResponse();
                Stream stream = response.GetResponseStream();
                StreamReader reader = new StreamReader(stream, Encoding.UTF8);

                string result = reader.ReadToEnd();

                reader.Close();
                stream.Close();

                // Return the response from the SMS service
                return Ok(new { message = "OTP sent successfully", result,tokenString});
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Error sending OTP", error = ex.Message });
            }
        }
        // Endpoint to verify OTP
        [HttpPost("verifyotp")]
        public IActionResult VerifyOtp([FromBody] VerifyOtpRequest request)
        {
            try
            {
                // Check if OTP exists for the given phone number
                if (!_cache.TryGetValue(request.PhoneNumber, out string storedOtp))
                {
                    return BadRequest(new { message = "OTP has expired or was never sent" });
                }

                // Verify the OTP
                if (storedOtp == request.Otp)
                {
                    return Ok(new { message = "OTP verified successfully" });
                }
                else
                {
                    return BadRequest(new { message = "Invalid OTP" });
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Error verifying OTP", error = ex.Message });
            }
        }
    
    
    
    }

    // DTO for sending OTP request
    public class SendOtpRequest
    {
        public string PhoneNumber { get; set; }
    }

    // DTO for verifying OTP request
    public class VerifyOtpRequest
    {
        public string PhoneNumber { get; set; }
        public string Otp { get; set; }
    }
}