using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Souqna.Core.Interfaces;

namespace Souqna.Infrastructure.Services
{
    public class SmsService : ISmsService
    {
        private readonly ILogger<SmsService> _logger;

        public SmsService(ILogger<SmsService> logger)
        {
            _logger = logger;
        }

        public Task<bool> SendOtpAsync(string phoneNumber, string otpCode)
        {
            string normalizedPhone = NormalizePhoneNumber(phoneNumber);
            
            // Development Mock SMS Provider:
            // Logs the OTP securely to the application log without transmitting over third-party network.
            // Ready for integration with SMSMisr, Twilio, or Vodafone SMS Gateway.
            _logger.LogInformation("[DEV SMS MOCK] Sending OTP to {PhoneNumber}: >>> {OtpCode} <<< (Expires in 5 minutes)", normalizedPhone, otpCode);
            
            return Task.FromResult(true);
        }

        public static string NormalizePhoneNumber(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
            
            string cleaned = Regex.Replace(phone.Trim(), @"[^\d+]", "");
            
            // Standardize Egyptian numbers
            if (cleaned.StartsWith("01"))
            {
                cleaned = "+2" + cleaned;
            }
            else if (cleaned.StartsWith("201"))
            {
                cleaned = "+" + cleaned;
            }
            
            return cleaned;
        }

        public static bool IsValidEgyptianPhoneNumber(string phone)
        {
            string normalized = NormalizePhoneNumber(phone);
            // Matches +2010xxxxxxxx, +2011xxxxxxxx, +2012xxxxxxxx, +2015xxxxxxxx
            return Regex.IsMatch(normalized, @"^\+201[0125]\d{8}$");
        }
    }
}
