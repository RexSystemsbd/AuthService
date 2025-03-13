using System.Net;
using System.Text;
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
        private const string SenderId = "Random";  // Your Sender ID
        private readonly IMemoryCache _cache;

        public SmsController(IMemoryCache memoryCache)
        {
            _cache = memoryCache;
        }
        string result = "";
            WebRequest request = null;
            HttpWebResponse response = null;
        // Endpoint to send OTP
        [HttpPost("sendotp")]
        public async Task<IActionResult> SendOtp([FromBody] SendOtpRequest request)
        {

            try
            {
                // Generate a random OTP
                var otp = new Random().Next(100000, 999999).ToString();

                // Store OTP in memory cache for 5 minutes
                _cache.Set(request.PhoneNumber, otp, TimeSpan.FromMinutes(5));

                // Send the OTP via SMS
                string message = Uri.EscapeDataString($"Your OTP is: {otp}");

                string url = $"http://bulksmsbd.net/api/smsapi?api_key={ApiKey}&senderid={SenderId}&number={request.PhoneNumber}&message={message}";

                // Create the WebRequest to send the SMS
                WebRequest requestSms = WebRequest.Create(url);
                HttpWebResponse response = (HttpWebResponse)requestSms.GetResponse();
                Stream stream = response.GetResponseStream();
                StreamReader reader = new StreamReader(stream, Encoding.UTF8);

                string result = reader.ReadToEnd();

                reader.Close();
                stream.Close();

                // Return the response from the SMS service
                return Ok(new { message = "OTP sent successfully", result,otp });
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