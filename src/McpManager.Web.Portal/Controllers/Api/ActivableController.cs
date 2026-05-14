using McpManager.Core.Data.Contexts;
using McpManager.Core.Data.Models.Contracts;
using McpManager.Core.Data.Models.Identity;
using Microsoft.AspNetCore.Mvc;

namespace McpManager.Web.Portal.Controllers.Api;

public class ActivableController : ApiController
{
    public ActivableController(ApplicationDbContext dbContext)
        : base(dbContext) { }

    [HttpPost]
    public async Task<IActionResult> Index(string modelName, Guid key)
    {
        if (!(User?.Identity?.IsAuthenticated ?? false))
            return Forbid();

        var modelType = Type.GetType(modelName);
        if (modelType == null)
            return BadRequest("Model type not found.");

        var dbType = DbContext.GetType();
        var dbsetProp = dbType
            .GetProperties()
            .FirstOrDefault(p => p.PropertyType.GenericTypeArguments.Any(t => t == modelType));
        if (dbsetProp == null)
            return BadRequest("DbSet not found.");

        if (!typeof(IActivable).IsAssignableFrom(modelType))
            return BadRequest("The model must implement the IActivable interface.");

        var dbSet = (dynamic)dbsetProp.GetValue(DbContext);
        var model = (IActivable)dbSet.Find(key);
        if (model == null)
            return NotFound($"{modelType.Name} with primary key {key} was not found.");

        // Prevent deactivating the last active user or yourself
        if (modelType.IsAssignableTo(typeof(User)) && model.IsActive)
        {
            if (DbContext.Users.Count(c => c.IsActive) <= 1)
            {
                return BadRequest("The platform must keep at least one active user.");
            }

            if (model.Id == GetAuthenticatedUserId())
            {
                return BadRequest("You cannot deactivate your own user.");
            }
        }

        // Get the IActivableExecutor for the model type
        var executorType = typeof(IActivableExecutor<>).MakeGenericType(modelType);
        var executor = HttpContext.RequestServices.GetService(executorType);

        if (executor != null)
        {
            try
            {
                if (model.IsActive)
                {
                    var deactivate = executorType.GetMethod(
                        nameof(IActivableExecutor<>.Deactivate)
                    );
                    var task = deactivate?.Invoke(executor, [model]);
                    if (task is Task t)
                        await t;
                }
                else
                {
                    var activate = executorType.GetMethod(nameof(IActivableExecutor<>.Activate));
                    var task = activate?.Invoke(executor, [model]);
                    if (task is Task t)
                        await t;
                }
            }
            catch (Exception ex)
            {
                return BadRequest(ex.InnerException?.Message ?? ex.Message);
            }
        }
        else
        {
            model.IsActive = !model.IsActive;
        }

        await DbContext.SaveChangesAsync();
        return Ok(model.IsActive);
    }
}
