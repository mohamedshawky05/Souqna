using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Souqna.Core.Entities;
using Souqna.Core.Interfaces;
using Souqna.Infrastructure.Data;

namespace Souqna.Infrastructure.Services
{
    public class PhoneVerificationService : IPhoneVerificationService
    {
        private readonly SouqnaDbContext _dbContext;
        private readonly ISmsService _smsService;
        private readonly ILogger<PhoneVerificationService> _logger;

        public PhoneVerificationService(
            SouqnaDbContext dbContext,
            ISmsService smsService,
            ILogger<PhoneVerificationService> logger)
        {
            _dbContext = dbContext;
            _smsService = smsService;
            _logger = logger;
        }

        public async Task<bool> RequestOtpAsync(string phoneNumber)
        {
            string normalized = SmsService.NormalizePhoneNumber(phoneNumber);
            if (!SmsService.IsValidEgyptianPhoneNumber(normalized))
            {
                _logger.LogWarning("Invalid Egyptian phone number: {Phone}", phoneNumber);
                return false;
            }

            var record = await _dbContext.PhoneVerifications
                .FirstOrDefaultAsync(p => p.PhoneNumber == normalized);

            // Rate-limit resend: must wait at least 60 seconds
            if (record != null && (DateTime.UtcNow - record.LastSentUtc).TotalSeconds < 60)
            {
                _logger.LogWarning("OTP request throttled for {Phone}", normalized);
                return false;
            }

            // Generate cryptographically secure 6-digit OTP
            string otpCode = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
            string codeHash = HashCode(otpCode);

            if (record == null)
            {
                record = new PhoneVerification
                {
                    PhoneNumber = normalized,
                    CodeHash = codeHash,
                    ExpirationUtc = DateTime.UtcNow.AddMinutes(5),
                    AttemptsCount = 0,
                    IsVerified = false,
                    LastSentUtc = DateTime.UtcNow
                };
                _dbContext.PhoneVerifications.Add(record);
            }
            else
            {
                record.CodeHash = codeHash;
                record.ExpirationUtc = DateTime.UtcNow.AddMinutes(5);
                record.AttemptsCount = 0;
                record.IsVerified = false;
                record.LastSentUtc = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync();

            // Send via SMS Service
            await _smsService.SendOtpAsync(normalized, otpCode);
            return true;
        }

        public async Task<bool> VerifyOtpAsync(string phoneNumber, string code)
        {
            if (string.IsNullOrWhiteSpace(code) || code.Length != 6) return false;

            string normalized = SmsService.NormalizePhoneNumber(phoneNumber);
            var record = await _dbContext.PhoneVerifications
                .FirstOrDefaultAsync(p => p.PhoneNumber == normalized);

            if (record == null) return false;

            // Check if expired
            if (DateTime.UtcNow > record.ExpirationUtc)
            {
                _logger.LogWarning("Expired OTP attempted for {Phone}", normalized);
                return false;
            }

            // Check max attempts (limit to 5)
            if (record.AttemptsCount >= 5)
            {
                _logger.LogWarning("Too many invalid OTP attempts for {Phone}", normalized);
                return false;
            }

            record.AttemptsCount++;

            string codeHash = HashCode(code.Trim());
            if (record.CodeHash == codeHash)
            {
                record.IsVerified = true;
                record.VerifiedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();

                // Also update any ApplicationUser with this phone number
                var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.PhoneNumber == normalized);
                if (user != null)
                {
                    user.PhoneNumberConfirmed = true;
                    await _dbContext.SaveChangesAsync();
                }

                _logger.LogInformation("Phone successfully verified: {Phone}", normalized);
                return true;
            }

            await _dbContext.SaveChangesAsync();
            return false;
        }

        private static string HashCode(string code)
        {
            using var sha = SHA256.Create();
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(code));
            return Convert.ToHexString(bytes);
        }
    }
}
