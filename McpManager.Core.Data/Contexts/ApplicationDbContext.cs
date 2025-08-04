using McpManager.Core.Data.Converters;
using McpManager.Core.Data.Models;
using McpManager.Core.Data.Models.ApiKeys;
using McpManager.Core.Data.Models.Identity;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Data.Models.Notifications;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace McpManager.Core.Data.Contexts;

public class ApplicationDbContext : IdentityDbContext<User, Role, Guid, UserClaim, UserRole, UserLogin, RoleClaim, UserToken> {
    public DbSet<McpServer> McpServers { get; set; }
    public DbSet<McpTool> McpTools { get; set; }
    public DbSet<McpToolRequest> McpToolRequests { get; set; }
    public DbSet<McpServerLog> McpServerLogs { get; set; }
    public DbSet<McpNamespace> McpNamespaces { get; set; }
    public DbSet<McpNamespaceServer> McpNamespaceServers { get; set; }
    public DbSet<McpNamespaceTool> McpNamespaceTools { get; set; }
    public DbSet<ApiKey> ApiKeys { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<AppSettings> AppSettings { get; set; }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        base.OnModelCreating(modelBuilder);

        ConfigureIdentity(modelBuilder);
        ConfigureMcp(modelBuilder);
        SeedData(modelBuilder);
    }

    private void ConfigureMcp(ModelBuilder modelBuilder) {
        modelBuilder.Entity<McpServer>(b => {
            b.OwnsOne(s => s.Auth);
            b.Property(s => s.CustomHeaders).HasJsonConversion();
            b.Property(s => s.Arguments).HasJsonConversion();
            b.Property(s => s.EnvironmentVariables).HasJsonConversion();
        });

        modelBuilder.Entity<ApiKey>()
            .HasMany(k => k.AllowedNamespaces)
            .WithMany(n => n.ApiKeys)
            .UsingEntity(j => j.ToTable("ApiKeyNamespaces"));
    }

    private void ConfigureIdentity(ModelBuilder modelBuilder) {
        modelBuilder.Entity<User>(b => {
            b.HasMany(e => e.Claims).WithOne(e => e.User).HasForeignKey(uc => uc.UserId).IsRequired();
            b.HasMany(e => e.Logins).WithOne(e => e.User).HasForeignKey(ul => ul.UserId).IsRequired();
            b.HasMany(e => e.Tokens).WithOne(e => e.User).HasForeignKey(ut => ut.UserId).IsRequired();
            b.HasMany(u => u.Roles).WithOne(r => r.User).HasForeignKey(r => r.UserId);
            b.ToTable("Users");
        });

        modelBuilder.Entity<Role>(b => {
            b.HasMany(r => r.User).WithOne(r => r.Role).HasForeignKey(r => r.RoleId);
            b.ToTable("Roles");
        });

        modelBuilder.Entity<RoleClaim>(b => b.ToTable("RoleClaims"));
        modelBuilder.Entity<UserClaim>(b => b.ToTable("UserClaims"));
        modelBuilder.Entity<UserLogin>(b => b.ToTable("UserLogins"));
        modelBuilder.Entity<UserToken>(b => b.ToTable("UserTokens"));
        modelBuilder.Entity<UserRole>(b => b.ToTable("UserRoles"));
    }

    private void SeedData(ModelBuilder modelBuilder) {
        modelBuilder.Entity<User>().HasData(new User() {
            Id = IntToGuid(1),
            IsActive = true,
            GivenName = "Daniel",
            Surname = "Oliveira",
            UserName = "admin@mcpmanager.local",
            NormalizedUserName = "ADMIN@MCPMANAGER.LOCAL",
            Email = "admin@mcpmanager.local",
            NormalizedEmail = "ADMIN@MCPMANAGER.LOCAL",
            EmailConfirmed = true,
            PasswordHash = "AQAAAAIAAYagAAAAEOqOiuA0bHdat2FXMTttZDTVksgYKQ9t5Pzc92oGjnNRcGUSiPUQOM00wFjo1eeblQ==",
            SecurityStamp = "U7LXVLANYGF3IKRETLA5YF4SJ4Z3FEA3",
            ConcurrencyStamp = "1d979655-fed0-411b-b2ad-66345adceab3",
            CreationTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        });

        modelBuilder.Entity<UserClaim>().HasData(new UserClaim {
            Id = 1,
            UserId = IntToGuid(1),
            ClaimType = "Admin",
            ClaimValue = "Admin"
        });

        modelBuilder.Entity<AppSettings>().HasData(new AppSettings {
            Id = 1,
            McpConnectionTimeoutSeconds = 120,
            McpRetryAttempts = 3
        });
    }

    private Guid IntToGuid(int value) {
        var bytes = new byte[16];
        BitConverter.GetBytes(value).CopyTo(bytes, 0);
        return new Guid(bytes);
    }
}
