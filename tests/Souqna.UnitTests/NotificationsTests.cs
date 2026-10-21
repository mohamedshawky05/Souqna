using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Souqna.Core.Entities;
using Souqna.Infrastructure.Data;
using Souqna.Web.Hubs;
using Souqna.Web.Services;

namespace Souqna.UnitTests
{
    public class NotificationsTests
    {
        [Fact]
        public async Task RecentNotifications_AreScopedToTheRequestedUser()
        {
            var options = new DbContextOptionsBuilder<SouqnaDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var db = new SouqnaDbContext(options);
            db.Notifications.AddRange(
                new Notification
                {
                    UserId = "user-a",
                    Title = "Order",
                    Message = "Order received",
                    IsRead = false
                },
                new Notification
                {
                    UserId = "user-b",
                    Title = "Private",
                    Message = "Private notification",
                    IsRead = false
                });
            await db.SaveChangesAsync();

            var hub = new Mock<IHubContext<NotificationHub>>();
            var service = new NotificationService(db, hub.Object);

            var notifications = await service.GetRecentAsync("user-a");
            int unreadCount = await service.GetUnreadCountAsync("user-a");

            Assert.Single(notifications);
            Assert.Equal("Order", notifications[0].Title);
            Assert.Equal(1, unreadCount);
        }
    }
}
