using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Souqna.Web.Hubs
{
    [Authorize]
    public class NotificationHub : Hub
    {
        public static string UserGroup(string userId) => $"user_{userId}";

        public override async Task OnConnectedAsync()
        {
            string? userId = Context.UserIdentifier;
            if (!string.IsNullOrEmpty(userId))
            {
                await Groups.AddToGroupAsync(
                    Context.ConnectionId,
                    UserGroup(userId));
            }

            await base.OnConnectedAsync();
        }
    }
}
