using ArchitectCentral.AgentStudio.Models.Codebase;
using ArchitectCentral.AgentStudio.Models.Conversation;
using ArchitectCentral.AgentStudio.Models.RAG;
using ArchitectCentral.AgentStudio.Services;
using Spectre.Console;

namespace ArchitectCentral.AgentStudio.UI;

/// <summary>
/// Spectre.Console-based interactive menu for Agent Studio
/// </summary>
public class InteractiveMenu
{
    private readonly RAGQueryService _ragService;
    private readonly ChatService _chatService;
    private readonly AgentGeneratorService _agentGenerator;

    public InteractiveMenu(
        RAGQueryService ragService,
        ChatService chatService,
        AgentGeneratorService agentGenerator)
    {
        _ragService = ragService;
        _chatService = chatService;
        _agentGenerator = agentGenerator;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        ShowWelcomeBanner();

        while (!cancellationToken.IsCancellationRequested)
        {
            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("\n[cyan]What would you like to do?[/]")
                    .PageSize(10)
                    .AddChoices(new[]
                    {
                        "Generate Agent Artifacts",
                        "Query Documentation (RAG)",
                        "Chat with AI",
                        "Help",
                        "Exit"
                    }));

            try
            {
                switch (choice)
                {
                    case "Generate Agent Artifacts":
                        await GenerateAgentArtifactsAsync();
                        break;
                    case "Query Documentation (RAG)":
                        await QueryDocumentationAsync();
                        break;
                    case "Chat with AI":
                        await ChatWithAIAsync();
                        break;

                    case "Help":
                        ShowHelp();
                        break;
                    case "Exit":
                        AnsiConsole.MarkupLine("[yellow]Goodbye![/]");
                        return;
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error: {ex.Message.EscapeMarkup()}[/]");
                AnsiConsole.WriteLine();
            }
        }
    }

    private void ShowWelcomeBanner()
    {
        AnsiConsole.Clear();

        var rule = new Rule("[cyan bold] ARCHITECT CENTRAL - AGENT STUDIO [/]")
        {
            Justification = Justify.Center
        };
        AnsiConsole.Write(rule);
        AnsiConsole.WriteLine();

        var panel = new Panel(
            new Markup(
                "[dim]AI-Powered Agent Generation for Your Codebase[/]\n\n" +
                "This tool uses your architecture documentation (ADRs, Guidelines) to\n" +
                "generate GitHub Copilot agents, skills, and instructions tailored to\n" +
                "your specific codebase."))
        {
            Border = BoxBorder.Rounded,
            Padding = new Padding(2, 1)
        };
        AnsiConsole.Write(panel);
    }

    private async Task GenerateAgentArtifactsAsync()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[yellow] Agent Artifact Generator[/]").LeftJustified());
        AnsiConsole.WriteLine();

        // Collect codebase information
        var codebaseName = AnsiConsole.Ask<string>("[cyan]Codebase Name:[/]");
        var description = AnsiConsole.Ask<string>("[cyan]Description:[/]");
        var runtime = AnsiConsole.Ask<string>("[cyan]Runtime (e.g., .NET 8.0):[/]");
        var framework = AnsiConsole.Ask<string>("[cyan]Framework (e.g., ASP.NET Core):[/]");

        // Collect dependencies
        var dependencies = new List<Dependency>();
        AnsiConsole.MarkupLine("\n[cyan]Add key dependencies (press Enter on empty name to finish):[/]");

        while (true)
        {
            var depName = AnsiConsole.Ask<string>($"[dim]Dependency #{dependencies.Count + 1} Name (or Enter to finish):[/]", string.Empty);
            if (string.IsNullOrWhiteSpace(depName)) break;

            var version = AnsiConsole.Ask<string>($"[dim]  Version:[/]");
            var role = AnsiConsole.Ask<string>($"[dim]  Role (e.g., ORM, CQRS, Validation):[/]");

            dependencies.Add(new Dependency
            {
                Name = depName,
                Version = version,
                Role = role
            });
        }

        var request = new CodebaseRequest
        {
            Name = codebaseName,
            Description = description,
            Runtime = runtime,
            Framework = framework,
            Dependencies = dependencies
        };

        // Generate artifacts
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("[yellow]⏳ Generating artifacts...[/]", async ctx =>
            {
                ctx.Status("[yellow]Querying relevant architecture patterns...[/]");
                var artifacts = await _agentGenerator.GenerateArtifactsAsync(request);

                ctx.Status("[yellow]Saving files...[/]");
                var outputDir = Path.Combine("generated-agents", codebaseName.ToLowerInvariant().Replace(" ", "-"));
                Directory.CreateDirectory(outputDir);

                // Save .agent file
                var agentFile = Path.Combine(outputDir, $"{codebaseName.ToLowerInvariant().Replace(" ", "-")}.agent");
                await File.WriteAllTextAsync(agentFile, artifacts.AgentFile);

                // Save skills
                foreach (var skill in artifacts.Skills)
                {
                    var skillFile = Path.Combine(outputDir, skill.FileName);
                    await File.WriteAllTextAsync(skillFile, skill.Content);
                }

                // Save instructions
                foreach (var instruction in artifacts.Instructions)
                {
                    var instructionFile = Path.Combine(outputDir, instruction.FileName);
                    await File.WriteAllTextAsync(instructionFile, instruction.Content);
                }
            });

        AnsiConsole.MarkupLine($"\n[green][SUCCESS] Artifacts generated successfully![/]");
        AnsiConsole.MarkupLine($"[dim]Location: generated-agents/{codebaseName.ToLowerInvariant().Replace(" ", "-")}/[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.Prompt(new TextPrompt<string>("[dim]Press Enter to continue...[/]").AllowEmpty());
    }

    private async Task QueryDocumentationAsync()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[blue]Query Architecture Documentation[/]").LeftJustified());
        AnsiConsole.WriteLine();

        var query = AnsiConsole.Ask<string>("[cyan]Enter your query:[/]");

        List<RAGResult> results = null!;
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("[yellow]⏳ Searching...[/]", async ctx =>
            {
                results = await _ragService.QueryAsync(query);
            });

        if (results.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No results found above the similarity threshold.[/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"\n[green]Found {results.Count} relevant documents:[/]");
            AnsiConsole.WriteLine();

            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("[cyan]Type[/]")
                .AddColumn("[cyan]Section[/]")
                .AddColumn("[cyan]Similarity[/]")
                .AddColumn("[cyan]Content Preview[/]");

            foreach (var result in results)
            {
                var contentPreview = result.Content.Length > 100
                    ? string.Concat(result.Content.AsSpan(0, 100), "...")
                    : result.Content;

                table.AddRow(
                    result.DocumentType,
                    result.Section ?? "[dim]N/A[/]",
                    $"[green]{result.Similarity:F3}[/]",
                    contentPreview.EscapeMarkup());
            }

            AnsiConsole.Write(table);
        }

        AnsiConsole.WriteLine();
        AnsiConsole.Prompt(new TextPrompt<string>("[dim]Press Enter to continue...[/]").AllowEmpty());
    }

    private async Task ChatWithAIAsync()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[magenta]Chat with AI[/]").LeftJustified());
        AnsiConsole.MarkupLine("[dim]Type 'exit' to return to main menu[/]");
        AnsiConsole.WriteLine();

        var conversationHistory = new List<ConversationMessage>();

        while (true)
        {
            var userMessage = AnsiConsole.Prompt(
                new TextPrompt<string>("[green]You>[/]")
                    .AllowEmpty());

            if (string.IsNullOrWhiteSpace(userMessage))
                continue;

            if (userMessage.Equals("exit", StringComparison.OrdinalIgnoreCase))
                break;

            // Add user message to history
            conversationHistory.Add(new ConversationMessage
            {
                Role = MessageRole.User,
                Content = userMessage,
                Timestamp = DateTime.UtcNow
            });

            // Query RAG for context
            var ragResults = await _ragService.QueryAsync(userMessage);

            string response = string.Empty;
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("[yellow]Thinking...[/]", async ctx =>
                {
                    response = await _chatService.SendMessageAsync(
                        userMessage,
                        systemPrompt: "You are an expert software architect. Answer questions based on the provided architecture documentation.",
                        ragContext: ragResults,
                        conversationHistory: conversationHistory);
                });

            // Add assistant response to history
            conversationHistory.Add(new ConversationMessage
            {
                Role = MessageRole.Assistant,
                Content = response,
                Timestamp = DateTime.UtcNow
            });

            AnsiConsole.MarkupLine($"[blue]Assistant>[/] {response.EscapeMarkup()}");
            AnsiConsole.WriteLine();
        }
    }

    private void ShowHelp()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[yellow]Help[/]").LeftJustified());
        AnsiConsole.WriteLine();

        var helpTable = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("[cyan]Feature[/]")
            .AddColumn("[cyan]Description[/]");

        helpTable.AddRow(
            " Generate Agent Artifacts",
            "Create GitHub Copilot agents, skills, and instructions\ntailored to your codebase using RAG-powered AI");

        helpTable.AddRow(
            "Query Documentation",
            "Search architecture documentation using semantic search\n(RAG with pgvector)");

        helpTable.AddRow(
            "Chat with AI",
            "Interactive Q&A about architecture with RAG context\ninjection");

        helpTable.AddRow(
            "Clear Chat History",
            "Reset the conversation history");

        AnsiConsole.Write(helpTable);
        AnsiConsole.WriteLine();
        AnsiConsole.Prompt(new TextPrompt<string>("[dim]Press Enter to continue...[/]").AllowEmpty());
    }
}
