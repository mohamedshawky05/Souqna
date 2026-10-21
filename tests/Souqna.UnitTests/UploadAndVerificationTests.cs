using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Souqna.Core.DTOs;
using Souqna.Core.Entities;
using Souqna.Core.Enums;
using Souqna.Core.Interfaces;
using Souqna.Infrastructure.Data;
using Souqna.Infrastructure.Services;
using Xunit;

namespace Souqna.UnitTests
{
    public class UploadAndVerificationTests
    {
        private SouqnaDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<SouqnaDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new SouqnaDbContext(options);
        }

        [Fact]
        public async Task VideoUpload_RejectsUnsupportedFileFormat()
        {
            // Arrange
            var storageMock = new Mock<IStorageService>();
            var loggerMock = new Mock<ILogger<VideoProcessingService>>();
            var service = new VideoProcessingService(storageMock.Object, loggerMock.Object);

            using var stream = new MemoryStream(new byte[] { 1, 2, 3 });

            // Act: Attempt to upload an .exe or .avi file
            var result = await service.ValidateAndProcessVideoAsync(stream, "dangerous.exe", maxAllowedSeconds: 60);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains("غير مدعومة", result.ErrorMessage);
        }

        [Fact]
        public async Task PhoneVerification_GeneratesOtp_AndVerifiesCorrectly()
        {
            // Arrange
            using var db = CreateInMemoryDbContext();
            string capturedOtp = string.Empty;

            var smsMock = new Mock<ISmsService>();
            smsMock.Setup(s => s.SendOtpAsync(It.IsAny<string>(), It.IsAny<string>()))
                   .Callback<string, string>((phone, otp) => capturedOtp = otp)
                   .ReturnsAsync(true);

            var loggerMock = new Mock<ILogger<PhoneVerificationService>>();
            var service = new PhoneVerificationService(db, smsMock.Object, loggerMock.Object);

            string egyptPhone = "01012345678";

            // Act 1: Request OTP
            bool requestSuccess = await service.RequestOtpAsync(egyptPhone);
            Assert.True(requestSuccess);
            Assert.False(string.IsNullOrEmpty(capturedOtp));
            Assert.Equal(6, capturedOtp.Length);

            // Act 2: Verify with incorrect OTP
            bool failVerify = await service.VerifyOtpAsync(egyptPhone, "000000");
            Assert.False(failVerify);

            // Act 3: Verify with correct OTP
            bool successVerify = await service.VerifyOtpAsync(egyptPhone, capturedOtp);
            Assert.True(successVerify);

            // Assert DB record updated
            var record = await db.PhoneVerifications.FirstOrDefaultAsync(p => p.PhoneNumber == "+201012345678");
            Assert.NotNull(record);
            Assert.True(record.IsVerified);
        }

        [Fact]
        public async Task IdentityVerification_AdminApproval_GrantsBadgeAndUpdatesStatus()
        {
            // Arrange
            using var db = CreateInMemoryDbContext();
            var storageMock = new Mock<IStorageService>();
            var loggerMock = new Mock<ILogger<IdentityVerificationService>>();
            var notificationMock = new Mock<INotificationService>();
            notificationMock
                .Setup(service => service.PublishAsync(It.IsAny<Notification>()))
                .Returns(Task.CompletedTask);
            var service = new IdentityVerificationService(
                db,
                storageMock.Object,
                loggerMock.Object,
                notificationMock.Object);

            var store = new Store
            {
                Id = 77,
                SellerId = "seller-77",
                StoreName = "متجر الحرف",
                Slug = "crafts",
                IsVerified = false
            };
            db.Stores.Add(store);

            var verification = new IdentityVerification
            {
                Id = 501,
                StoreId = 77,
                NationalIdNumber = "29001011234567",
                FullLegalName = "كريم سامي",
                Status = VerificationStatus.Pending
            };
            db.IdentityVerifications.Add(verification);
            await db.SaveChangesAsync();

            // Act: Admin approves the verification
            bool result = await service.ReviewVerificationAsync(501, reviewerId: "admin-1", VerificationStatus.Approved, "مستندات الهوية سليمة ومطابقة");

            // Assert
            Assert.True(result);
            var updatedVer = await db.IdentityVerifications.FindAsync(501);
            Assert.Equal(VerificationStatus.Approved, updatedVer!.Status);
            Assert.Equal("admin-1", updatedVer.ReviewerId);

            var updatedStore = await db.Stores.FindAsync(77);
            Assert.True(updatedStore!.IsVerified);
            Assert.Equal("Identity Verified", updatedStore.VerificationBadge);

            // Check notification generated for seller
            var notif = await db.Notifications.FirstOrDefaultAsync(n => n.UserId == "seller-77");
            Assert.NotNull(notif);
            Assert.Contains("تم توثيق هويتك", notif.Title);
        }
    }
}
