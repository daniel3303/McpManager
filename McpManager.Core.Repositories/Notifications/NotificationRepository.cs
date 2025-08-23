using McpManager.Core.Data.Contexts;
using McpManager.Core.Data.Models.Identity;
using McpManager.Core.Data.Models.Notifications;
using McpManager.Core.Repositories.Contracts;

namespace McpManager.Core.Repositories.Notifications;

public class NotificationRepository : BaseRepository<Notification> {
    public NotificationRepository(ApplicationDbContext dbContext) : base(dbContext) { }

    public IQueryable<Notification> GetByUser(User user) {
        return GetAll().Where(n => n.User.Id == user.Id);
    }

    public IQueryable<Notification> GetUnreadByUser(User user) {
        return GetByUser(user).Where(n => !n.IsRead);
    }
}
