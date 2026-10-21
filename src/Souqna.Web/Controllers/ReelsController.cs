using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Souqna.Core.Entities;
using Souqna.Core.Enums;
using Souqna.Infrastructure.Data;
using Souqna.Web.ViewModels;

namespace Souqna.Web.Controllers
{
    public class ReelsController : Controller
    {
        private readonly SouqnaDbContext _dbContext;

        public ReelsController(SouqnaDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IActionResult> Index(int? id)
        {
            var query = _dbContext.Videos
                .Include(v => v.Store)
                .Include(v => v.Product)
                    .ThenInclude(p => p!.Images)
                .Where(v => v.Status == VideoStatus.Published);

            var reels = await query
                .OrderByDescending(v => v.CreatedAt)
                .ToListAsync();

            // If a specific reel was requested, move it to the front of the feed
            if (id.HasValue)
            {
                var requested = reels.FirstOrDefault(v => v.Id == id.Value);
                if (requested != null)
                {
                    reels.Remove(requested);
                    reels.Insert(0, requested);
                }
            }

            var vm = new ReelsFeedViewModel
            {
                Videos = reels,
                InitialVideoId = id ?? 0
            };

            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> Like(int id)
        {
            var video = await _dbContext.Videos.FindAsync(id);
            if (video == null) return NotFound(new { success = false, message = "الفيديو غير موجود" });

            string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Json(new { success = false, message = "يرجى تسجيل الدخول للإعجاب بالفيديو" });
            }

            var existingLike = await _dbContext.VideoLikes
                .FirstOrDefaultAsync(vl => vl.VideoId == id && vl.UserId == userId);

            bool isLiked;
            if (existingLike != null)
            {
                _dbContext.VideoLikes.Remove(existingLike);
                video.LikesCount = System.Math.Max(0, video.LikesCount - 1);
                isLiked = false;
            }
            else
            {
                _dbContext.VideoLikes.Add(new VideoLike
                {
                    VideoId = id,
                    UserId = userId
                });
                video.LikesCount++;
                isLiked = true;
            }

            await _dbContext.SaveChangesAsync();
            return Json(new { success = true, isLiked, likesCount = video.LikesCount });
        }

        [HttpPost]
        public async Task<IActionResult> RecordView(int id)
        {
            var video = await _dbContext.Videos.FindAsync(id);
            if (video == null) return NotFound();

            // Unique view check per IP / User
            string viewerKey = User.FindFirstValue(ClaimTypes.NameIdentifier) 
                ?? HttpContext.Connection.RemoteIpAddress?.ToString() 
                ?? "anonymous";

            bool viewedRecently = await _dbContext.VideoViews
                .AnyAsync(v => v.VideoId == id && v.ViewerKey == viewerKey && v.ViewedAt > System.DateTime.UtcNow.AddMinutes(-30));

            if (!viewedRecently)
            {
                video.ViewsCount++;
                _dbContext.VideoViews.Add(new VideoView
                {
                    VideoId = id,
                    ViewerKey = viewerKey,
                    ViewedAt = System.DateTime.UtcNow
                });
                await _dbContext.SaveChangesAsync();
            }

            return Json(new { success = true, viewsCount = video.ViewsCount });
        }
    }
}
