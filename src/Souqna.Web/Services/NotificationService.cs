using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Souqna.Core.DTOs;
using Souqna.Core.Entities;
using Souqna.Core.Interfaces;
using Souqna.Infrastructure.Data;
using Souqna.Web.Hubs;

namespace Souqna.Web.Services
{
    public class NotificationService : INotificationService
    {
        private readonly SouqnaDbContext _dbContext;
        private readonly IHubContext<NotificationHub> _hubContext;

        public NotificationService(
            SouqnaDbContext dbContext,
            IHubContext<NotificationHub> hubContext)
        {
            _dbContext = dbContext;
            _hubContext = hubContext;
        }

        public async Task<IReadOnlyList<NotificationDto>> GetRecentAsync(
            string userId,
            int count = 10)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new ArgumentException("User ID is required.", nameof(userId));

            if (count < 1 || count > 50)
                throw new ArgumentOutOfRangeException(nameof(count));

            var notifications = await _dbContext.Notifications
                .AsNoTracking()
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Take(count)
                .Select(n => new NotificationDto
                {
                    Id = n.Id,
                    Title = n.Title,
                    Message = n.Message,
                    ActionUrl = n.LinkUrl,
                    Type = n.Type,
                    IsRead = n.IsRead,
                    CreatedAt = n.CreatedAt
                })
                .ToListAsync();

            return notifications;
        }

        public Task<int> GetUnreadCountAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new ArgumentException("User ID is required.", nameof(userId));

            return _dbContext.Notifications
                .CountAsync(n => n.UserId == userId && !n.IsRead);
        }

        public async Task<bool> MarkAsReadAsync(int notificationId, string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new ArgumentException("User ID is required.", nameof(userId));

            var notification = await _dbContext.Notifications
                .FirstOrDefaultAsync(n =>
                    n.Id == notificationId &&
                    n.UserId == userId);

            if (notification == null)
                return false;

            if (!notification.IsRead)
            {
                notification.IsRead = true;
                await _dbContext.SaveChangesAsync();
                await PublishAsync(notification);
            }

            return true;
        }

        public async Task<int> MarkAllAsReadAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new ArgumentException("User ID is required.", nameof(userId));

            int updated = await _dbContext.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ExecuteUpdateAsync(updates =>
                    updates.SetProperty(n => n.IsRead, true));

            await SendUnreadCountAsync(userId);
            return updated;
        }

        public async Task<Notification> CreateNotificationAsync(
            string userId,
            string title,
            string message,
            string type,
            string? actionUrl = null)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new ArgumentException("User ID is required.", nameof(userId));
            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("Title is required.", nameof(title));
            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("Message is required.", nameof(message));
            if (string.IsNullOrWhiteSpace(type))
                throw new ArgumentException("Type is required.", nameof(type));

            var notification = new Notification
            {
                UserId = userId,
                Title = title,
                Message = message,
                Type = type,
                LinkUrl = actionUrl,
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            };

            _dbContext.Notifications.Add(notification);
            await _dbContext.SaveChangesAsync();
            await PublishAsync(notification);

            return notification;
        }

        public async Task PublishAsync(Notification notification)
        {
            ArgumentNullException.ThrowIfNull(notification);

            int unreadCount = await GetUnreadCountAsync(notification.UserId);
            await _hubContext.Clients
                .Group(NotificationHub.UserGroup(notification.UserId))
                .SendAsync("ReceiveNotification", new NotificationDto
                {
                    Id = notification.Id,
                    Title = notification.Title,
                    Message = notification.Message,
                    ActionUrl = notification.LinkUrl,
                    Type = notification.Type,
                    IsRead = notification.IsRead,
                    CreatedAt = notification.CreatedAt
                });
            await SendUnreadCountAsync(notification.UserId, unreadCount);
        }

        private Task SendUnreadCountAsync(string userId, int? count = null)
        {
            var unreadCountTask = count.HasValue
                ? Task.FromResult(count.Value)
                : GetUnreadCountAsync(userId);

            return SendUnreadCountCoreAsync(userId, unreadCountTask);
        }

        private async Task SendUnreadCountCoreAsync(
            string userId,
            Task<int> unreadCountTask)
        {
            int unreadCount = await unreadCountTask;
            await _hubContext.Clients
                .Group(NotificationHub.UserGroup(userId))
                .SendAsync("UnreadCountChanged", unreadCount);
        }
    }
}
