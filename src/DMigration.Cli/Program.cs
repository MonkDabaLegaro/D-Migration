using DMigration.Application;
using DMigration.Domain;
using DMigration.Infrastructure.Windows;
using DMigration.Providers;
using Spectre.Console;

var destinationDrive = Environment.GetEnvironmentVariable("DMIGRATION_DESTINATION_DRIVE") ?? "D:";
var providers = new IInventoryProvider[]
{
    new WindowsKnownFolderProvider(destinationDrive),
    new DeveloperToolProvider(destinationDrive)
};

var inventoryService = new InventoryService(providers);
var planningService = new PlanningService();
var journal = new JsonJournalStore(destinationDrive);
var executionService = new ExecutionService(journal);
var doctorService = new DoctorService();

var command = args.FirstOrDefault()?.ToLowerInvariant() ?? "interactive";

try
{
    return command switch
    {
        "scan" => await ScanAsync(),
        "plan" => await PlanAsync(),
        "doctor" => await DoctorAsync(),
        "apply" => await ApplyAsync(args.Skip(1).ToArray()),
        "rollback" => await RollbackAsync(args.Skip(1).ToArray()),
        "interactive" => await InteractiveAsync(),
        "help" or "--help" or "-h" => ShowHelp(),
        _ => UnknownCommand(command)
    };
}
catch (Exception ex)
{
    AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(ex.Message)}");
    return 1;
}

async Task<int> InteractiveAsync()
{
    AnsiConsole.Write(new FigletText("D-Migration"));
    AnsiConsole.MarkupLine("[grey]Windows Storage Migration Manager[/]");

    var choice = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("¿Qué deseas hacer?")
            .AddChoices("Analizar sistema", "Crear plan", "Diagnóstico", "Salir"));

    return choice switch
    {
        "Analizar sistema" => await ScanAsync(),
        "Crear plan" => await PlanAsync(),
        "Diagnóstico" => await DoctorAsync(),
        _ => 0
    };
}

async Task<int> ScanAsync()
{
    var items = await inventoryService.ScanAsync();
    RenderInventory(items);
    return 0;
}

async Task<int> PlanAsync()
{
    var items = await inventoryService.ScanAsync();
    var plan = planningService.Create(items);
    await executionService.DryRunAsync(plan);

    var table = new Table().Border(TableBorder.Rounded)
        .AddColumn("Elemento")
        .AddColumn("Estrategia")
        .AddColumn("Destino")
        .AddColumn("Estado");

    foreach (var step in plan.Steps)
        table.AddRow(
            Markup.Escape(step.Item.Name),
            step.Item.Strategy.ToString(),
            Markup.Escape(step.Destination ?? "-") ,
            step.Item.CanExecute ? "[green]Ejecutable[/]" : "[yellow]Plan/manual[/]");

    AnsiConsole.Write(table);
    AnsiConsole.MarkupLine($"Plan guardado: [bold]{plan.Id}[/]");
    AnsiConsole.MarkupLine("[grey]No se movió ni eliminó ningún archivo.[/]");
    return 0;
}

async Task<int> DoctorAsync()
{
    var items = await inventoryService.ScanAsync();
    var findings = doctorService.Inspect(items);
    if (findings.Count == 0)
    {
        AnsiConsole.MarkupLine("[green]No se detectaron herramientas de desarrollo que requieran revisión.[/]");
        return 0;
    }

    foreach (var finding in findings)
    {
        var state = finding.Healthy ? "[green]OK[/]" : "[yellow]REVISAR[/]";
        AnsiConsole.MarkupLine($"{state} [bold]{Markup.Escape(finding.Component)}[/] — {Markup.Escape(finding.Message)}");
    }
    return 0;
}

async Task<int> ApplyAsync(string[] commandArgs)
{
    var id = commandArgs.FirstOrDefault(x => !x.StartsWith('-'));
    var execute = commandArgs.Contains("--execute", StringComparer.OrdinalIgnoreCase);
    if (string.IsNullOrWhiteSpace(id))
    {
        AnsiConsole.MarkupLine("Uso: dmigrate apply <plan-id> [--execute]");
        return 2;
    }

    var plan = await journal.LoadAsync(id);
    if (plan is null)
    {
        AnsiConsole.MarkupLine("[red]Plan no encontrado.[/]");
        return 2;
    }

    if (!execute)
    {
        AnsiConsole.MarkupLine("[yellow]Dry-run:[/] el plan es válido y permanece sin ejecutar. Usa --execute sólo cuando todos sus providers sean ejecutables.");
        return 0;
    }

    await executionService.ExecuteAsync(plan);
    return 0;
}

async Task<int> RollbackAsync(string[] commandArgs)
{
    var id = commandArgs.FirstOrDefault();
    if (string.IsNullOrWhiteSpace(id) || await journal.LoadAsync(id) is null)
    {
        AnsiConsole.MarkupLine("Uso: dmigrate rollback <plan-id> (el plan debe existir)");
        return 2;
    }

    AnsiConsole.MarkupLine("[yellow]Rollback no ejecutado:[/] este incremento aún no habilita mutaciones, por lo tanto no existe estado del sistema que revertir.");
    return 0;
}

void RenderInventory(IReadOnlyList<InventoryItem> items)
{
    var table = new Table().Border(TableBorder.Rounded)
        .AddColumn("Elemento")
        .AddColumn("Tamaño")
        .AddColumn("Riesgo")
        .AddColumn("Estrategia")
        .AddColumn("Origen");

    foreach (var item in items)
        table.AddRow(
            Markup.Escape(item.Name),
            FormatBytes(item.SizeBytes),
            item.Risk.ToString(),
            item.Strategy.ToString(),
            Markup.Escape(item.SourcePath));

    AnsiConsole.Write(table);
    AnsiConsole.MarkupLine($"Detectados [bold]{items.Count}[/] elementos. El escaneo es de solo lectura.");
}

static string FormatBytes(long bytes)
{
    string[] units = ["B", "KB", "MB", "GB", "TB"];
    double value = bytes;
    var unit = 0;
    while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
    return $"{value:0.##} {units[unit]}";
}

static int ShowHelp()
{
    AnsiConsole.WriteLine("dmigrate [scan|plan|doctor|apply <plan-id> [--execute]|rollback <plan-id>]");
    return 0;
}

static int UnknownCommand(string command)
{
    AnsiConsole.MarkupLine($"[red]Comando desconocido:[/] {Markup.Escape(command)}");
    return 2;
}
