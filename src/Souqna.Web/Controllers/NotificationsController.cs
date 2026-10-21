using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Souqna.Core.Interfaces;

namespace Souqna.Web.Controllers
{
    [Authorize]
    [Route("notifications")]
    public class NotificationsController : Controller
    {
        private readonly INotificationService _notificationService;

        public NotificationsController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var notifications = await _notificationService.GetRecentAsync(userId);
            int unreadCount = await _notificationService.GetUnreadCountAsync(userId);

            return Json(new { notifications, unreadCount });
        }

        [HttpPost("{id:int}/read")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            bool updated = await _notificationService.MarkAsReadAsync(id, userId);

            return updated ? NoContent() : NotFound();
        }

        [HttpPost("read-all")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllAsRead()
        {
            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            int updated = await _notificationService.MarkAllAsReadAsync(userId);

            return Ok(new { updated });
        }
    }
}
