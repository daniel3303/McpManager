using McpManager.Core.Data.Models.Contracts;
using McpManager.Web.Portal.TagHelpers;
using Microsoft.AspNetCore.Antiforgery;
using NonFactors.Mvc.Grid;

namespace McpManager.Web.Portal.Extensions;

public static class GridColumnsOfExtensions {
    public static void AddId<T>(this IGridColumnsOf<T> gridColumn) {
        gridColumn.Add()
            .Titled("Id")
            .RenderedAs(m => m.GetType().GetProperty("Id")?.GetValue(m))
            .CssClasses = "w-1 p-2";
    }


    public static void AddActivating<T>(this IGridColumnsOf<T> gridColumn, string title = "Status") where T : IActivable {
        gridColumn.Add(m => m.IsActive)
            .RenderedAs(m => {
                var type = (m.GetType().Namespace ?? "").StartsWith("Castle.Proxies")
                    ? m.GetType().BaseType
                    : m.GetType();
                return string.Format(
                    "<input type=\"checkbox\" data-key=\"" + m.Id + "\" data-model=\"" + type?.AssemblyQualifiedName +
                    "\" class=\"toggle toggle-success activable\" {0} />",
                    m.IsActive ? "checked" : "");
            })
            .Titled(title)
            .Encoded(false).CssClasses = "w-1 p-2";
    }

    public static void AddActions<T>(this IGridColumnsOf<T> gridColumn, Action<IActionBuilder<T>> action, string title = "") {
        var httpContext = gridColumn.Grid.HttpContext;
        gridColumn.Add().RenderedAs(m => {
            var builder = new ActionBuilder<T>(m, httpContext, httpContext.RequestServices.GetService<LinkGenerator>(), httpContext.GetController(), gridColumn);
            action(builder);
            return $@"<div class=""grid-actions"">{builder.Html}</div>";
        }).Titled(title).Encoded(false).CssClasses = "p-2 w-1 white-space-nowrap align-middle";
    }
}

public interface IActionBuilder<TEntity> {
    public string Html { get; set; }
    public LinkGenerator LinkGenerator { get; set; }
    public HttpContext HttpContext { get; set; }
    public TEntity Model { get; set; }
    public string Controller { get; set; }
    public IGridColumnsOf<TEntity> GridColumn { get; set; }

    public IActionBuilder<TEntity> AddShow(string action = null, string controller = null,
        object routeValues = null, string text = "View", Func<TEntity, bool> when = null, bool targetBlank = false);

    public IActionBuilder<TEntity> AddInfo(string action = null, string controller = null,
        object routeValues = null, string text = "Info");

    public IActionBuilder<TEntity> AddDelete(string action = default, string controller = default,
        object routeValues = null, string text = "Delete", bool goBack = true, string title = "Delete", bool verification = true, string confirmMessage = null);

    public IActionBuilder<TEntity> AddCustomHtml(Func<TEntity, string> action, bool verification = true, Func<TEntity, bool> when = null);

    public IActionBuilder<TEntity> AddCustom(
        string text,
        string icon = null,
        string action = null,
        string controller = null,
        Func<TEntity, object> routeValues = null,
        string cssClass = "",
        Func<TEntity, bool> when = null,
        string confirmMessage = null,
        bool isPost = false,
        Func<TEntity, Dictionary<string, string>> dataAttributes = null);
}

public class ActionBuilder<TEntity> : IActionBuilder<TEntity> {
    // SVG icons for grid actions (outline style)
    private static readonly string SvgEye = HeroIcons.Render("eye", HeroIcons.IconStyle.Outline, 4);
    private static readonly string SvgInfo = HeroIcons.Render("information-circle", HeroIcons.IconStyle.Outline, 4);
    private static readonly string SvgTrash = HeroIcons.Render("trash", HeroIcons.IconStyle.Outline, 4);

    // Icon name to SVG mapping for AddCustom
    private static string GetIcon(string name) => HeroIcons.Render(name, HeroIcons.IconStyle.Outline, 4);

    public string Html { get; set; }
    public LinkGenerator LinkGenerator { get; set; }
    public HttpContext HttpContext { get; set; }
    public TEntity Model { get; set; }
    public string Controller { get; set; }
    public IGridColumnsOf<TEntity> GridColumn { get; set; }

    public ActionBuilder(TEntity model, HttpContext context, LinkGenerator linkGenerator, string controller, IGridColumnsOf<TEntity> gridColumn) {
        Model = model;
        HttpContext = context;
        LinkGenerator = linkGenerator;
        Controller = controller;
        Html = "";
        GridColumn = gridColumn;
    }

    public IActionBuilder<TEntity> AddShow(string action = null, string controller = null,
        object routeValues = null, string text = "View", Func<TEntity, bool> when = null, bool targetBlank = false) {
        if (when != null && when(Model) == false)
            return this;
        var id = Model.GetType().GetProperty("Id")?.GetValue(Model) ?? "";
        action ??= "Show";
        controller ??= Controller;
        routeValues ??= new { id };
        var link = LinkGenerator.GetPathByAction(HttpContext, action, controller, routeValues);
        Html += $"<a href=\"{link}\" class=\"grid-action-btn\" data-tooltip=\"{text}\" {(targetBlank ? "target=\"_blank\"" : "")}>{SvgEye}</a>";
        return this;
    }

    public IActionBuilder<TEntity> AddInfo(string action = null, string controller = null,
        object routeValues = null, string text = "Info") {
        var id = Model.GetType().GetProperty("Id")?.GetValue(Model) ?? "";
        action ??= "Edit";
        controller ??= Controller;
        routeValues ??= new { id };
        var link = LinkGenerator.GetPathByAction(HttpContext, action, controller, routeValues);
        Html += $"<a href=\"{link}\" class=\"grid-action-btn\" data-tooltip=\"{text}\">{SvgInfo}</a>";
        return this;
    }

    public IActionBuilder<TEntity> AddDelete(string action = default, string controller = default,
        object routeValues = null, string text = "Delete", bool goBack = true, string title = "Delete", bool verification = true, string confirmMessage = null) {
        if (!verification)
            return this;

        var id = Model.GetType().GetProperty("Id")?.GetValue(Model) ?? "";
        action ??= "Delete";
        controller ??= Controller;
        routeValues ??= new { id };
        var name = goBack ? "GoBack" : "";
        var link = LinkGenerator.GetPathByAction(HttpContext, action, controller, routeValues);
        var confirm = string.IsNullOrEmpty(confirmMessage)
            ? "Are you sure you want to delete this item?"
            : confirmMessage;

        var antiforgery = HttpContext.RequestServices.GetRequiredService<IAntiforgery>();
        var tokenSet = antiforgery.GetAndStoreTokens(HttpContext);
        var antiforgeryInput = $@"<input type=""hidden"" name=""{tokenSet.FormFieldName}"" value=""{tokenSet.RequestToken}"" />";

        Html += $@"<form method=""post"" class=""form-confirm inline"" data-message=""{confirm}"" action=""{link}"">
            {antiforgeryInput}
            <button type=""submit"" name=""{name}"" class=""grid-action-btn grid-action-danger"" data-tooltip=""{text}"">{SvgTrash}</button>
        </form>";
        return this;
    }

    public IActionBuilder<TEntity> AddCustomHtml(Func<TEntity, string> action, bool verification = true, Func<TEntity, bool> when = null) {
        if (when != null && when(Model) == false)
            return this;
        if (!verification)
            return this;
        Html += action(Model);
        return this;
    }

    public IActionBuilder<TEntity> AddCustom(
        string text,
        string icon = null,
        string action = null,
        string controller = null,
        Func<TEntity, object> routeValues = null,
        string cssClass = "",
        Func<TEntity, bool> when = null,
        string confirmMessage = null,
        bool isPost = false,
        Func<TEntity, Dictionary<string, string>> dataAttributes = null) {

        if (when != null && !when(Model))
            return this;

        var iconHtml = "";
        if (!string.IsNullOrEmpty(icon))
            iconHtml = GetIcon(icon);

        var dataAttrs = "";
        var attrs = dataAttributes?.Invoke(Model);
        if (attrs != null) {
            foreach (var attr in attrs)
                dataAttrs += $" data-{attr.Key}=\"{attr.Value}\"";
        }

        // Button without action (client-side only)
        if (string.IsNullOrEmpty(action)) {
            Html += $"<button type=\"button\" class=\"grid-action-btn {cssClass}\" data-tooltip=\"{text}\"{dataAttrs}>{iconHtml}</button>";
            return this;
        }

        var id = Model.GetType().GetProperty("Id")?.GetValue(Model) ?? "";
        controller ??= Controller;
        var routes = routeValues?.Invoke(Model) ?? new { id };
        var link = LinkGenerator.GetPathByAction(HttpContext, action, controller, routes);

        if (isPost) {
            var antiforgery = HttpContext.RequestServices.GetRequiredService<IAntiforgery>();
            var tokenSet = antiforgery.GetAndStoreTokens(HttpContext);
            var antiforgeryInput = $@"<input type=""hidden"" name=""{tokenSet.FormFieldName}"" value=""{tokenSet.RequestToken}"" />";
            var confirmClass = !string.IsNullOrEmpty(confirmMessage) ? "form-confirm" : "";
            var confirmAttr = !string.IsNullOrEmpty(confirmMessage) ? $@"data-message=""{confirmMessage}""" : "";

            Html += $@"<form method=""post"" class=""{confirmClass} inline"" {confirmAttr} action=""{link}"">
                {antiforgeryInput}
                <button type=""submit"" class=""grid-action-btn {cssClass}"" data-tooltip=""{text}""{dataAttrs}>{iconHtml}</button>
            </form>";
        } else {
            Html += $"<a href=\"{link}\" class=\"grid-action-btn {cssClass}\" data-tooltip=\"{text}\"{dataAttrs}>{iconHtml}</a>";
        }

        return this;
    }
}
