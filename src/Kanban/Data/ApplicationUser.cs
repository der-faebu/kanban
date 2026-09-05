using Microsoft.AspNetCore.Identity;
using Kanban.Data.Entities;

namespace Kanban.Data;

// Add profile data for application users by adding properties to the ApplicationUser class
public class ApplicationUser : IdentityUser
{
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<CardAssignee> CardAssignments { get; set; } = [];
}

