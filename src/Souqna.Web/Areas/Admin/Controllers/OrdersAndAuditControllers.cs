using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Souqna.Core.Enums;
using Souqna.Infrastructure.Data;

namespace Souqna.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class OrdersController : Controller
    {
        private readonly SouqnaDbContext _dbContext;

        public OrdersController(SouqnaDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IActionResult> Index(OrderStatus? status)
        {
            var query = _dbContext.Orders
                .Include(o => o.StoreOrders)
                    .ThenInclude(so => so.Store)
                .Include(o => o.StoreOrders)
                    .ThenInclude(so => so.Items)
                .AsQueryable();

            if (status.HasValue)
            {
                query = query.Where(o => o.Status == status.Value);
            }

            var orders = await query.OrderByDescending(o => o.CreatedAt).ToListAsync();
            ViewBag.CurrentStatus = status;
            return View(orders);
        }

        public async Task<IActionResult> Details(int id)
        {
            var order = await _dbContext.Orders
                .Include(o => o.StoreOrders)
                    .ThenInclude(so => so.Store)
                .Include(o => o.StoreOrders)
                    .ThenInclude(so => so.Items)
                        .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound();

            return View(order);
        }
    }

    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class AuditLogsController : Controller
    {
        private readonly SouqnaDbContext _dbContext;

        public AuditLogsController(SouqnaDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IActionResult> Index()
        {
            var logs = await _dbContext.AuditLogs
                .OrderByDescending(l => l.Timestamp)
                .Take(100)
                .ToListAsync();

            return View(logs);
        }
    }
}
