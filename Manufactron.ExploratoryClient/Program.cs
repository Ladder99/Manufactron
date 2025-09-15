using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Spectre.Console;
using Manufactron.I3X.Shared.Models;
using Manufactron.I3X.Shared.Models.Manufacturing;

namespace Manufactron.ExploratoryClient;

class Program
{
    private static readonly HttpClient httpClient = new HttpClient();
    private static string aggregatorUrl = "http://localhost:7000"; // Default, will be overridden by config
    private static readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    static async Task Main(string[] args)
    {
        // Load configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        // Get aggregator URL from configuration
        aggregatorUrl = configuration["AggregatorService:BaseUrl"] ?? "http://localhost:7000";

        AnsiConsole.Write(
            new FigletText("I3X Explorer")
                .Centered()
                .Color(Color.Cyan1));

        AnsiConsole.MarkupLine("[bold yellow]Manufacturing Intelligence Explorer[/]");
        AnsiConsole.MarkupLine($"[dim]Connected to: {aggregatorUrl}[/]");
        AnsiConsole.WriteLine();

        while (true)
        {
            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[green]What would you like to explore?[/]")
                    .AddChoices(new[] {
                        "📊 Discover Namespaces",
                        "🏭 Browse Object Types",
                        "📦 List All Objects",
                        "🔍 Search for Object",
                        "🌐 Build Manufacturing Context",
                        "📈 Visualize Object Graph",
                        "🔗 Explore Relationships",
                        "📜 View Object History",
                        "🎯 Interactive Object Navigator",
                        "❌ Exit"
                    }));

            try
            {
                switch (choice)
                {
                    case "📊 Discover Namespaces":
                        await DiscoverNamespaces();
                        break;
                    case "🏭 Browse Object Types":
                        await BrowseObjectTypes();
                        break;
                    case "📦 List All Objects":
                        await ListAllObjects();
                        break;
                    case "🔍 Search for Object":
                        await SearchForObject();
                        break;
                    case "🌐 Build Manufacturing Context":
                        await BuildManufacturingContext();
                        break;
                    case "📈 Visualize Object Graph":
                        await VisualizeObjectGraph();
                        break;
                    case "🔗 Explore Relationships":
                        await ExploreRelationships();
                        break;
                    case "📜 View Object History":
                        await ViewObjectHistory();
                        break;
                    case "🎯 Interactive Object Navigator":
                        await InteractiveObjectNavigator();
                        break;
                    case "❌ Exit":
                        AnsiConsole.MarkupLine("[yellow]Goodbye![/]");
                        return;
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.WriteException(ex);
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim]Press any key to continue...[/]");
            Console.ReadKey(true);
            AnsiConsole.Clear();
        }
    }

    static async Task DiscoverNamespaces()
    {
        await AnsiConsole.Status()
            .StartAsync("Discovering namespaces...", async ctx =>
            {
                var response = await httpClient.GetAsync($"{aggregatorUrl}/api/i3x/namespaces");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var namespaces = JsonSerializer.Deserialize<List<Namespace>>(json, jsonOptions);

                    if (namespaces != null && namespaces.Count > 0)
                    {
                        var table = new Table();
                        table.AddColumn("URI");
                        table.AddColumn("Name");
                        table.AddColumn("Version");
                        table.AddColumn("Source");

                        foreach (var ns in namespaces)
                        {
                            var source = "Unknown";
                            table.AddRow(
                                ns.Uri ?? "",
                                ns.Name ?? "",
                                ns.Version ?? "",
                                source ?? ""
                            );
                        }

                        AnsiConsole.Write(table);
                    }
                    else
                    {
                        AnsiConsole.MarkupLine("[yellow]No namespaces found[/]");
                    }
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]Error: {response.StatusCode}[/]");
                }
            });
    }

    static async Task BrowseObjectTypes()
    {
        await AnsiConsole.Status()
            .StartAsync("Fetching object types...", async ctx =>
            {
                var response = await httpClient.GetAsync($"{aggregatorUrl}/api/i3x/types");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var types = JsonSerializer.Deserialize<List<ObjectType>>(json, jsonOptions);

                    if (types != null && types.Count > 0)
                    {
                        var root = new Tree("[yellow]Object Types[/]");

                        // Group by namespace
                        var grouped = types.GroupBy(t => t.NamespaceUri ?? "Default");

                        foreach (var group in grouped)
                        {
                            var nsNode = root.AddNode($"[cyan]{group.Key}[/]");
                            foreach (var type in group)
                            {
                                var typeNode = nsNode.AddNode($"[green]{type.Name}[/] ({type.ElementId})");
                                if (type.Attributes != null && type.Attributes.Count > 0)
                                {
                                    foreach (var attr in type.Attributes.Take(3))
                                    {
                                        typeNode.AddNode($"[dim]{attr.Name}: {attr.DefaultValue}[/]");
                                    }
                                }
                            }
                        }

                        AnsiConsole.Write(root);
                    }
                    else
                    {
                        AnsiConsole.MarkupLine("[yellow]No object types found[/]");
                    }
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]Error: {response.StatusCode}[/]");
                }
            });
    }

    static async Task ListAllObjects()
    {
        var includeMetadata = AnsiConsole.Confirm("Include metadata?", false);

        await AnsiConsole.Status()
            .StartAsync("Fetching...", async ctx =>
            {
                var response = await httpClient.GetAsync($"{aggregatorUrl}/api/i3x/objects?includeMetadata={includeMetadata}");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var objects = JsonSerializer.Deserialize<List<Instance>>(json, jsonOptions);

                    if (objects != null && objects.Count > 0)
                    {
                        // Group by type
                        var grouped = objects.GroupBy(o => o.TypeId ?? "Unknown");

                        foreach (var group in grouped)
                        {
                            AnsiConsole.MarkupLine($"\n[bold yellow]{group.Key}           [/]");

                            var table = new Table();
                            table.AddColumn("ID");
                            table.AddColumn("Name");

                            if (includeMetadata)
                            {
                                // Add I3X required metadata columns
                                table.AddColumn("ParentId");
                                table.AddColumn("HasChildren");
                                table.AddColumn("NamespaceUri");
                                table.AddColumn("Other Attributes");
                                table.AddColumn("Relationships");
                            }

                            foreach (var obj in group.Take(10)) // Limit display
                            {
                                var row = new List<string> { obj.ElementId ?? "", obj.Name ?? "" };

                                if (includeMetadata)
                                {
                                    // Add I3X required metadata values
                                    row.Add(obj.ParentId ?? "");
                                    row.Add(obj.HasChildren.ToString());
                                    row.Add(obj.NamespaceUri ?? "");

                                    // Add all other attributes
                                    var otherAttrs = "";
                                    if (obj.Attributes != null && obj.Attributes.Any())
                                    {
                                        var attrs = obj.Attributes.Select(kvp => $"{kvp.Key}={kvp.Value}");
                                        otherAttrs = string.Join(", ", attrs);
                                    }
                                    row.Add(otherAttrs);

                                    // Add relationships
                                    var rels = obj.Relationships != null ? string.Join(", ", obj.Relationships.Keys.Take(3)) : "";
                                    row.Add(rels);
                                }

                                table.AddRow(row.ToArray());
                            }

                            AnsiConsole.Write(table);

                            if (group.Count() > 10)
                            {
                                AnsiConsole.MarkupLine($"[dim]... and {group.Count() - 10} more[/]");
                            }
                        }

                        AnsiConsole.MarkupLine($"\n[green]Total objects: {objects.Count}[/]");
                    }
                    else
                    {
                        AnsiConsole.MarkupLine("[yellow]No objects found[/]");
                    }
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]Error: {response.StatusCode}[/]");
                }
            });
    }

    static async Task SearchForObject()
    {
        var searchTerm = AnsiConsole.Ask<string>("Enter search term (ID or partial name):");

        await AnsiConsole.Status()
            .StartAsync($"Searching for '{searchTerm}'...", async ctx =>
            {
                var response = await httpClient.GetAsync($"{aggregatorUrl}/api/i3x/objects?includeMetadata=true");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var objects = JsonSerializer.Deserialize<List<Instance>>(json, jsonOptions);

                    if (objects != null)
                    {
                        var matches = objects.Where(o =>
                            (o.ElementId?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false) ||
                            (o.Name?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false) ||
                            (o.TypeId?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false)
                        ).ToList();

                        if (matches.Count > 0)
                        {
                            AnsiConsole.MarkupLine($"[green]Found {matches.Count} matches:[/]");

                            foreach (var match in matches)
                            {
                                DisplayObjectDetails(match);
                            }
                        }
                        else
                        {
                            AnsiConsole.MarkupLine($"[yellow]No objects matching '{searchTerm}' found[/]");
                        }
                    }
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]Error: {response.StatusCode}[/]");
                }
            });
    }

    static async Task BuildManufacturingContext()
    {
        var elementId = AnsiConsole.Ask<string>("Enter element ID to build context from:");

        await AnsiConsole.Status()
            .StartAsync($"Building manufacturing context for '{elementId}'...", async ctx =>
            {
                var response = await httpClient.GetAsync($"{aggregatorUrl}/api/i3x/context/{Uri.EscapeDataString(elementId)}");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var context = JsonSerializer.Deserialize<ManufacturingContext>(json, jsonOptions);

                    if (context != null)
                    {
                        DisplayManufacturingContext(context);
                    }
                    else
                    {
                        AnsiConsole.MarkupLine("[yellow]No context data returned[/]");
                    }
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]Error: {response.StatusCode}[/]");
                    var error = await response.Content.ReadAsStringAsync();
                    AnsiConsole.MarkupLine($"[red]{error}[/]");
                }
            });
    }

    static async Task VisualizeObjectGraph()
    {
        var elementId = AnsiConsole.Ask<string>("Enter element ID to visualize:");

        await AnsiConsole.Status()
            .StartAsync($"Building graph for '{elementId}'...", async ctx =>
            {
                // Get the object
                var response = await httpClient.GetAsync($"{aggregatorUrl}/api/i3x/objects/{Uri.EscapeDataString(elementId)}?includeMetadata=true");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var obj = JsonSerializer.Deserialize<Instance>(json, jsonOptions);

                    if (obj != null)
                    {
                        await VisualizeObjectAsGraph(obj);
                    }
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]Error fetching object: {response.StatusCode}[/]");
                }
            });
    }

    static async Task VisualizeObjectAsGraph(Instance obj)
    {
        var tree = new Tree($"[bold yellow]{obj.Name ?? obj.ElementId}[/] ([cyan]{obj.TypeId}[/])");

        // Add attributes
        if (obj.Attributes != null && obj.Attributes.Count > 0)
        {
            var attrNode = tree.AddNode("[green]Attributes[/]");
            foreach (var attr in obj.Attributes)
            {
                attrNode.AddNode($"{attr.Key}: [dim]{attr.Value}[/]");
            }
        }

        // Add relationships and fetch related objects
        if (obj.Relationships != null && obj.Relationships.Count > 0)
        {
            var relNode = tree.AddNode("[blue]Relationships[/]");

            foreach (var rel in obj.Relationships)
            {
                var relTypeNode = relNode.AddNode($"[yellow]{rel.Key}[/]");

                foreach (var targetId in rel.Value.Take(5)) // Limit to 5 per relationship type
                {
                    // Try to fetch the related object
                    var response = await httpClient.GetAsync($"{aggregatorUrl}/api/i3x/objects/{Uri.EscapeDataString(targetId)}");
                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        var related = JsonSerializer.Deserialize<Instance>(json, jsonOptions);
                        if (related != null)
                        {
                            relTypeNode.AddNode($"→ {related.Name ?? related.ElementId} ([dim]{related.TypeId}[/])");
                        }
                    }
                    else
                    {
                        relTypeNode.AddNode($"→ {targetId} [dim](not found)[/]");
                    }
                }

                if (rel.Value.Count > 5)
                {
                    relTypeNode.AddNode($"[dim]... and {rel.Value.Count - 5} more[/]");
                }
            }
        }

        // Try to get parent
        var parentResponse = await httpClient.GetAsync($"{aggregatorUrl}/api/i3x/objects/{Uri.EscapeDataString(obj.ElementId)}/parent");
        if (parentResponse.IsSuccessStatusCode)
        {
            var parentJson = await parentResponse.Content.ReadAsStringAsync();
            var parent = JsonSerializer.Deserialize<Instance>(parentJson, jsonOptions);
            if (parent != null)
            {
                tree.AddNode($"[magenta]Parent:[/] {parent.Name ?? parent.ElementId} ([dim]{parent.TypeId}[/])");
            }
        }

        // Try to get children
        var childrenResponse = await httpClient.GetAsync($"{aggregatorUrl}/api/i3x/objects/{Uri.EscapeDataString(obj.ElementId)}/children");
        if (childrenResponse.IsSuccessStatusCode)
        {
            var childrenJson = await childrenResponse.Content.ReadAsStringAsync();
            var children = JsonSerializer.Deserialize<List<Instance>>(childrenJson, jsonOptions);
            if (children != null && children.Count > 0)
            {
                var childNode = tree.AddNode("[magenta]Children[/]");
                foreach (var child in children.Take(10))
                {
                    childNode.AddNode($"↓ {child.Name ?? child.ElementId} ([dim]{child.TypeId}[/])");
                }
                if (children.Count > 10)
                {
                    childNode.AddNode($"[dim]... and {children.Count - 10} more[/]");
                }
            }
        }

        AnsiConsole.Write(tree);
    }

    static async Task ExploreRelationships()
    {
        var elementId = AnsiConsole.Ask<string>("Enter element ID:");
        var relationshipType = AnsiConsole.Ask<string>("Enter relationship type (or 'all' for all):");

        await AnsiConsole.Status()
            .StartAsync($"Exploring relationships...", async ctx =>
            {
                if (relationshipType.ToLower() == "all")
                {
                    // Get the object with all relationships
                    var response = await httpClient.GetAsync($"{aggregatorUrl}/api/i3x/objects/{Uri.EscapeDataString(elementId)}?includeMetadata=true");
                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        var obj = JsonSerializer.Deserialize<Instance>(json, jsonOptions);

                        if (obj?.Relationships != null && obj.Relationships.Count > 0)
                        {
                            var table = new Table();
                            table.AddColumn("Relationship Type");
                            table.AddColumn("Target Count");
                            table.AddColumn("Target IDs");

                            foreach (var rel in obj.Relationships)
                            {
                                var targetIds = string.Join(", ", rel.Value.Take(3));
                                if (rel.Value.Count > 3)
                                {
                                    targetIds += $" ... (+{rel.Value.Count - 3})";
                                }
                                table.AddRow(rel.Key, rel.Value.Count.ToString(), targetIds);
                            }

                            AnsiConsole.Write(table);
                        }
                        else
                        {
                            AnsiConsole.MarkupLine("[yellow]No relationships found[/]");
                        }
                    }
                }
                else
                {
                    // Get specific relationship type
                    var response = await httpClient.GetAsync($"{aggregatorUrl}/api/i3x/objects/{Uri.EscapeDataString(elementId)}/relationships/{Uri.EscapeDataString(relationshipType)}");
                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        var related = JsonSerializer.Deserialize<List<Instance>>(json, jsonOptions);

                        if (related != null && related.Count > 0)
                        {
                            AnsiConsole.MarkupLine($"[green]Found {related.Count} objects with relationship '{relationshipType}':[/]");

                            foreach (var obj in related)
                            {
                                DisplayObjectDetails(obj);
                            }
                        }
                        else
                        {
                            AnsiConsole.MarkupLine($"[yellow]No objects found with relationship '{relationshipType}'[/]");
                        }
                    }
                }
            });
    }

    static async Task ViewObjectHistory()
    {
        var elementId = AnsiConsole.Ask<string>("Enter element ID:");
        var includeTimeRange = AnsiConsole.Confirm("Specify time range?", false);

        string url = $"{aggregatorUrl}/api/i3x/history/{Uri.EscapeDataString(elementId)}";

        if (includeTimeRange)
        {
            var days = AnsiConsole.Ask<int>("How many days back?", 7);
            var endTime = DateTime.UtcNow;
            var startTime = endTime.AddDays(-days);
            url += $"?startTime={Uri.EscapeDataString(startTime.ToString("O"))}&endTime={Uri.EscapeDataString(endTime.ToString("O"))}";
        }

        await AnsiConsole.Status()
            .StartAsync($"Fetching history...", async ctx =>
            {
                var response = await httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var history = JsonSerializer.Deserialize<List<HistoricalValue>>(json, jsonOptions);

                    if (history != null && history.Count > 0)
                    {
                        // Create a chart
                        AnsiConsole.MarkupLine($"[green]Found {history.Count} historical values[/]");

                        var table = new Table();
                        table.AddColumn("Timestamp");
                        table.AddColumn("Values");
                        table.AddColumn("Quality");

                        foreach (var h in history.Take(20))
                        {
                            var values = h.Values != null ? string.Join(", ", h.Values.Select(kv => $"{kv.Key}={kv.Value}")) : "";
                            table.AddRow(
                                h.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                                values,
                                h.Quality ?? "Good"
                            );
                        }

                        AnsiConsole.Write(table);

                        if (history.Count > 20)
                        {
                            AnsiConsole.MarkupLine($"[dim]... and {history.Count - 20} more entries[/]");
                        }
                    }
                    else
                    {
                        AnsiConsole.MarkupLine("[yellow]No historical data found[/]");
                    }
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]Error: {response.StatusCode}[/]");
                }
            });
    }

    static async Task InteractiveObjectNavigator()
    {
        var currentId = AnsiConsole.Ask<string>("Enter starting element ID:");
        Instance? current = null;

        while (true)
        {
            // Fetch current object
            var response = await httpClient.GetAsync($"{aggregatorUrl}/api/i3x/objects/{Uri.EscapeDataString(currentId)}?includeMetadata=true");
            if (!response.IsSuccessStatusCode)
            {
                AnsiConsole.MarkupLine($"[red]Error fetching object: {response.StatusCode}[/]");
                break;
            }

            var json = await response.Content.ReadAsStringAsync();
            current = JsonSerializer.Deserialize<Instance>(json, jsonOptions);

            if (current == null)
            {
                AnsiConsole.MarkupLine("[red]Object not found[/]");
                break;
            }

            // Display current object
            AnsiConsole.Clear();
            AnsiConsole.Write(new Rule($"[yellow]{current.Name ?? current.ElementId}[/]"));
            DisplayObjectDetails(current);

            // Build navigation options
            var choices = new List<string> { "🔙 Back to main menu" };

            // Add parent option
            var parentResp = await httpClient.GetAsync($"{aggregatorUrl}/api/i3x/objects/{Uri.EscapeDataString(currentId)}/parent");
            if (parentResp.IsSuccessStatusCode)
            {
                choices.Add("⬆️ Navigate to Parent");
            }

            // Add children option
            var childrenResp = await httpClient.GetAsync($"{aggregatorUrl}/api/i3x/objects/{Uri.EscapeDataString(currentId)}/children");
            if (childrenResp.IsSuccessStatusCode)
            {
                var childrenJson = await childrenResp.Content.ReadAsStringAsync();
                var children = JsonSerializer.Deserialize<List<Instance>>(childrenJson, jsonOptions);
                if (children != null && children.Count > 0)
                {
                    choices.Add("⬇️ Navigate to Child");
                }
            }

            // Add relationship navigation
            if (current.Relationships != null && current.Relationships.Count > 0)
            {
                choices.Add("➡️ Follow Relationship");
            }

            choices.Add("🌐 Build Context from Here");
            choices.Add("📊 Visualize as Graph");

            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[green]Navigate:[/]")
                    .AddChoices(choices));

            switch (choice)
            {
                case "🔙 Back to main menu":
                    return;

                case "⬆️ Navigate to Parent":
                    var parentJson = await parentResp.Content.ReadAsStringAsync();
                    var parent = JsonSerializer.Deserialize<Instance>(parentJson, jsonOptions);
                    if (parent != null)
                    {
                        currentId = parent.ElementId;
                    }
                    break;

                case "⬇️ Navigate to Child":
                    var cJson = await childrenResp.Content.ReadAsStringAsync();
                    var c = JsonSerializer.Deserialize<List<Instance>>(cJson, jsonOptions);
                    if (c != null && c.Count > 0)
                    {
                        var selected = AnsiConsole.Prompt(
                            new SelectionPrompt<Instance>()
                                .Title("Select child:")
                                .AddChoices(c)
                                .UseConverter(i => $"{i.Name ?? i.ElementId} ({i.TypeId})"));
                        currentId = selected.ElementId;
                    }
                    break;

                case "➡️ Follow Relationship":
                    if (current.Relationships != null && current.Relationships.Count > 0)
                    {
                        var relType = AnsiConsole.Prompt(
                            new SelectionPrompt<string>()
                                .Title("Select relationship type:")
                                .AddChoices(current.Relationships.Keys));

                        var targetId = AnsiConsole.Prompt(
                            new SelectionPrompt<string>()
                                .Title("Select target:")
                                .AddChoices(current.Relationships[relType]));

                        currentId = targetId;
                    }
                    break;

                case "🌐 Build Context from Here":
                    await BuildManufacturingContextForId(currentId);
                    AnsiConsole.MarkupLine("[dim]Press any key to continue...[/]");
                    Console.ReadKey(true);
                    break;

                case "📊 Visualize as Graph":
                    await VisualizeObjectAsGraph(current);
                    AnsiConsole.MarkupLine("[dim]Press any key to continue...[/]");
                    Console.ReadKey(true);
                    break;
            }
        }
    }

    static async Task BuildManufacturingContextForId(string elementId)
    {
        var response = await httpClient.GetAsync($"{aggregatorUrl}/api/i3x/context/{Uri.EscapeDataString(elementId)}");
        if (response.IsSuccessStatusCode)
        {
            var json = await response.Content.ReadAsStringAsync();
            var context = JsonSerializer.Deserialize<ManufacturingContext>(json, jsonOptions);
            if (context != null)
            {
                DisplayManufacturingContext(context);
            }
        }
    }

    static void DisplayObjectDetails(Instance obj)
    {
        var panel = new Panel(new Markup($@"
[bold]ID:[/] {obj.ElementId}
[bold]Name:[/] {obj.Name ?? "N/A"}
[bold]Type:[/] {obj.TypeId ?? "N/A"}
[bold]ParentId:[/] {obj.ParentId ?? "N/A"}
[bold]HasChildren:[/] {obj.HasChildren}
[bold]Namespace:[/] {obj.NamespaceUri ?? "N/A"}"))
        {
            Header = new PanelHeader($"[cyan]{obj.Name ?? obj.ElementId}[/]"),
            Border = BoxBorder.Rounded
        };

        AnsiConsole.Write(panel);

        if (obj.Attributes != null && obj.Attributes.Count > 0)
        {
            var table = new Table();
            table.Title = new TableTitle("[green]Attributes[/]");
            table.AddColumn("Key");
            table.AddColumn("Value");

            foreach (var attr in obj.Attributes.Take(10))
            {
                table.AddRow(attr.Key, attr.Value?.ToString() ?? "null");
            }

            if (obj.Attributes.Count > 10)
            {
                table.AddRow("[dim]...[/]", $"[dim]+{obj.Attributes.Count - 10} more[/]");
            }

            AnsiConsole.Write(table);
        }

        if (obj.Relationships != null && obj.Relationships.Count > 0)
        {
            var relTable = new Table();
            relTable.Title = new TableTitle("[blue]Relationships[/]");
            relTable.AddColumn("Type");
            relTable.AddColumn("Targets");

            foreach (var rel in obj.Relationships.Take(10))
            {
                var targets = string.Join(", ", rel.Value.Take(3));
                if (rel.Value.Count > 3)
                {
                    targets += $" ... (+{rel.Value.Count - 3})";
                }
                relTable.AddRow(rel.Key, targets);
            }

            AnsiConsole.Write(relTable);
        }
    }

    static void DisplayManufacturingContext(ManufacturingContext context)
    {
        AnsiConsole.Write(new Rule("[bold yellow]Manufacturing Context[/]"));

        var grid = new Grid();
        grid.AddColumn();
        grid.AddColumn();

        // Equipment
        if (context.Equipment != null)
        {
            grid.AddRow(
                new Panel($"[cyan]Equipment[/]\n{context.Equipment.Name ?? context.Equipment.ElementId}"),
                new Panel($"Type: {context.Equipment.TypeId}")
            );
        }

        // Line
        if (context.Line != null)
        {
            grid.AddRow(
                new Panel($"[green]Production Line[/]\n{context.Line.Name ?? context.Line.ElementId}"),
                new Panel($"Type: {context.Line.TypeId}")
            );
        }

        // Job
        if (context.Job != null)
        {
            grid.AddRow(
                new Panel($"[yellow]Job[/]\n{context.Job.Name ?? context.Job.ElementId}"),
                new Panel($"Type: {context.Job.TypeId}")
            );
        }

        // Order
        if (context.Order != null)
        {
            grid.AddRow(
                new Panel($"[magenta]Order[/]\n{context.Order.Name ?? context.Order.ElementId}"),
                new Panel($"Type: {context.Order.TypeId}")
            );
        }

        // Material Batch
        if (context.MaterialBatch != null)
        {
            grid.AddRow(
                new Panel($"[blue]Material Batch[/]\n{context.MaterialBatch.Name ?? context.MaterialBatch.ElementId}"),
                new Panel($"Type: {context.MaterialBatch.TypeId}")
            );
        }

        // Operator
        if (context.Operator != null)
        {
            grid.AddRow(
                new Panel($"[red]Operator[/]\n{context.Operator.Name ?? context.Operator.ElementId}"),
                new Panel($"Type: {context.Operator.TypeId}")
            );
        }

        AnsiConsole.Write(grid);

        // Display all relationships
        if (context.AllRelationships != null && context.AllRelationships.Count > 0)
        {
            AnsiConsole.Write(new Rule("[dim]All Relationships[/]"));

            var relTree = new Tree("[yellow]Relationships[/]");
            foreach (var relType in context.AllRelationships)
            {
                var typeNode = relTree.AddNode($"[cyan]{relType.Key}[/] ({relType.Value.Count})");
                foreach (var rel in relType.Value.Take(5))
                {
                    typeNode.AddNode($"{rel.SubjectId} → {rel.ObjectId}");
                }
                if (relType.Value.Count > 5)
                {
                    typeNode.AddNode($"[dim]... +{relType.Value.Count - 5} more[/]");
                }
            }
            AnsiConsole.Write(relTree);
        }
    }

}