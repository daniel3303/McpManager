namespace McpManager.Web.Portal.Services;

public static class Sidebar {
    private static readonly List<MenuSection> Sections = [
        new MenuSection() {
            Name = "Overview",
            Items = [
                new MenuItem() {
                    Controller = "Home",
                    Action = "Index",
                    Menu = "Home",
                    Icon = "home",
                    Name = "Home",
                },
            ]
        },
        new MenuSection() {
            Name = "MCP",
            Items = [
                new MenuItem() {
                    Controller = "McpServers",
                    Action = "Index",
                    Menu = "McpServers",
                    Icon = "server-stack",
                    Name = "Servers",
                },
                new MenuItem() {
                    Controller = "McpNamespaces",
                    Action = "Index",
                    Menu = "McpNamespaces",
                    Icon = "rectangle-group",
                    Name = "Namespaces",
                },
                new MenuItem() {
                    Controller = "McpPlayground",
                    Action = "Index",
                    Menu = "McpPlayground",
                    Icon = "command-line",
                    Name = "Playground",
                },
                new MenuItem() {
                    Controller = "McpRequests",
                    Action = "Index",
                    Menu = "McpRequests",
                    Icon = "document-text",
                    Name = "Request Log",
                },
                new MenuItem() {
                    Controller = "LiveLogs",
                    Action = "Index",
                    Menu = "LiveLogs",
                    Icon = "signal",
                    Name = "Live Logs",
                },
            ]
        },
        new MenuSection() {
            Name = "Administration",
            Items = [
                new MenuItem() {
                    Controller = "ApiKeys",
                    Action = "Index",
                    Menu = "ApiKeys",
                    Icon = "key",
                    Name = "API Keys",
                },
                new MenuItem() {
                    Controller = "Users",
                    Action = "Index",
                    Menu = "Users",
                    Icon = "users",
                    Name = "Users",
                },
                new MenuItem() {
                    Controller = "AdminSettings",
                    Action = "Index",
                    Menu = "AdminSettings",
                    Icon = "cog-6-tooth",
                    Name = "Settings",
                },
            ]
        },
    ];

    public static List<MenuSection> GetSections() {
        return Sections;
    }

    public class MenuSection {
        public string Name { get; set; }
        public List<MenuItem> Items { get; set; } = [];
    }

    public class MenuItem {
        public string Controller { get; set; }
        public string Action { get; set; }
        public string Menu { get; set; }
        public string Name { get; set; }
        public string Icon { get; set; }
        public string Target { get; set; }
        public Func<IServiceProvider, Task<int>> Badge { get; set; }
        public List<MenuItem> SubMenus { get; } = [];
        public Func<IServiceProvider, bool> Shown { get; set; }
    }
}
