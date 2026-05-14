using System.Diagnostics;
using System.Threading.RateLimiting;
using Equibles.Core.AutoWiring;
using McpManager.Core.Data.Contexts;
using McpManager.Core.Data.Extensions;
using McpManager.Core.Data.Models.Identity;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Identity.Extensions;
using McpManager.Core.Mcp;
using McpManager.Core.Mcp.Extensions;
using McpManager.Core.Repositories;
using McpManager.Core.Repositories.ApiKeys;
using McpManager.Core.Repositories.Extensions;
using McpManager.Core.Repositories.Identity;
using McpManager.Core.Repositories.Mcp;
using McpManager.Core.Repositories.Notifications;
using McpManager.Web.Portal.Authentication;
using McpManager.Web.Portal.Authorization;
using McpManager.Web.Portal.Mcp;
using McpManager.Web.Portal.Services.FlashMessage;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Serilog;
using Serilog.Exceptions;
using Serilog.Exceptions.Core;
using Serilog.Exceptions.EntityFrameworkCore.Destructurers;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .Enrich.WithExceptionDetails(
        new DestructuringOptionsBuilder()
            .WithDefaultDestructurers()
            .WithDestructurers([new DbUpdateExceptionDestructurer()])
    )
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddLogging(loggingBuilder =>
{
    loggingBuilder.ClearProviders();
    loggingBuilder.AddSerilog(Log.Logger);
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddData(configuration);
builder.Services.AddRepositories();
builder.Services.AddMcp();
builder.Services.AddHttpContextAccessor();

// Configure MCP server (HTTP transport for API key-authenticated proxy)
builder
    .Services.AddMcpServer(options =>
    {
        options.ServerInfo = new Implementation { Name = "MCP Manager Proxy", Version = "1.0.0" };
        options.ServerInstructions =
            "MCP proxy server that aggregates tools from multiple upstream MCP servers.";
    })
    .WithHttpTransport(httpOptions =>
    {
        httpOptions.Stateless = true;
        httpOptions.ConfigureSessionOptions = (httpContext, mcpOptions, _) =>
        {
            mcpOptions.Capabilities ??= new ServerCapabilities();
            mcpOptions.Capabilities.Tools = new ToolsCapability { ListChanged = false };

            // Route to namespace proxy if slug is present, otherwise global proxy
            var slug = httpContext.Request.RouteValues["slug"] as string;
            if (!string.IsNullOrEmpty(slug))
            {
                var nsProxy =
                    httpContext.RequestServices.GetRequiredService<McpNamespaceProxyServer>();
                mcpOptions.Handlers.ListToolsHandler = nsProxy.ListToolsHandler;
                mcpOptions.Handlers.CallToolHandler = nsProxy.CallToolHandler;
            }
            else
            {
                var proxyServer = httpContext.RequestServices.GetRequiredService<McpProxyServer>();
                mcpOptions.Handlers.ListToolsHandler = proxyServer.ListToolsHandler;
                mcpOptions.Handlers.CallToolHandler = proxyServer.CallToolHandler;
            }

            return Task.CompletedTask;
        };
    });

builder
    .Services.AddIdentity<User, McpManager.Core.Data.Models.Identity.Role>(options =>
    {
        options.SignIn.RequireConfirmedEmail = true;
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 6;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = false;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddIdentity();

builder.Services.AddFlashMessage();

// API key authentication for MCP endpoint
builder
    .Services.AddAuthentication()
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthHandler>(ApiKeyAuthHandler.SchemeName, null);

builder
    .Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo("data/DataProtectionKeys"))
    .SetApplicationName("McpManager.Web.Portal");

builder.Services.AddAuthorization(options =>
{
    foreach (var claim in ClaimStore.ClaimList())
    {
        options.AddPolicy(claim.Type, policy => policy.RequireClaim(claim.Type));
    }
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddPolicy(
        "NamespaceRateLimit",
        httpContext =>
        {
            var slug = httpContext.Request.RouteValues["slug"] as string;
            if (string.IsNullOrEmpty(slug))
            {
                return RateLimitPartition.GetNoLimiter(string.Empty);
            }

            var nsRepo = httpContext.RequestServices.GetRequiredService<McpNamespaceRepository>();
            var ns = nsRepo.GetBySlug(slug).FirstOrDefault();
            if (ns == null || !ns.RateLimitEnabled)
            {
                return RateLimitPartition.GetNoLimiter(slug);
            }

            var partitionKey = ns.RateLimitStrategy switch
            {
                RateLimitStrategy.PerApiKey => $"ns:{slug}:key:{httpContext.Items["ApiKeyId"]}",
                RateLimitStrategy.PerIp => $"ns:{slug}:ip:{httpContext.Connection.RemoteIpAddress}",
                _ => $"ns:{slug}:global",
            };

            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey,
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = ns.RateLimitRequestsPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                }
            );
        }
    );
});

builder.Services.AutoWireServicesFrom<Program>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "Identity.Application";
    options.Cookie.MaxAge = TimeSpan.FromDays(90);
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
    options.Events.OnRedirectToLogin = ctx =>
    {
        var back = ctx.Request.GetEncodedPathAndQuery();
        var linkGenerator =
            ctx.Request.HttpContext.RequestServices.GetRequiredService<LinkGenerator>();
        var loginLink = linkGenerator.GetPathByAction("Login", "Auth", new { ReturnUrl = back });
        Debug.Assert(loginLink != null, nameof(loginLink) + " != null");
        ctx.Response.Redirect(loginLink);
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = ctx =>
    {
        var linkGenerator =
            ctx.Request.HttpContext.RequestServices.GetRequiredService<LinkGenerator>();
        var accessDeniedLink = linkGenerator.GetPathByAction("AccessDenied", "Auth");
        ctx.Response.Redirect(accessDeniedLink ?? "/");
        return Task.CompletedTask;
    };
});

builder.Services.AddAntiforgery(opts =>
{
    opts.FormFieldName = "AntiForgery";
    opts.Cookie.Name = "Antiforgery";
});

builder.Services.AddRouting(options => options.LowercaseUrls = true);

builder
    .Services.AddControllersWithViews()
    .AddNewtonsoftJson(options =>
    {
        options.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
        options.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
        options.SerializerSettings.Converters.Add(new StringEnumConverter());
    })
    .AddRazorRuntimeCompilation();

var app = builder.Build();

// Ensure data directory exists
Directory.CreateDirectory("data");

// Migrate database on startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        await context.Database.MigrateAsync();
        logger.LogInformation("Database migrated successfully");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while migrating the database");
    }
}

app.UseForwardedHeaders();

app.UseExceptionHandler("/Home/Error");

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseSerilogRequestLogging();

app.UseHttpsRedirection();
app.MapStaticAssets();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers().WithStaticAssets();
app.MapMcpEndpoint();
app.MapMcpNamespaceEndpoint();

app.Run();
