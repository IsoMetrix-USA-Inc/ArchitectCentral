using ArchitectCentral.AgentStudio.API;
using ArchitectCentral.AgentStudio.Configuration;
using ArchitectCentral.AgentStudio.Services;
using ArchitectCentral.AgentStudio.UI;
using ArchitectCentral.AgentStudio.Workflows;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.AI;
using Npgsql;
using Scalar.AspNetCore;
using System.Diagnostics;

// Check for --web argument
var runWebMode = args.Contains("--web");

var builder = WebApplication.CreateBuilder(args);

// Add Aspire ServiceDefaults (includes OpenTelemetry, health checks, service discovery)
builder.AddServiceDefaults();

// Load configuration
var appSettings = new AppSettings();
builder.Configuration.Bind(appSettings);

// Register NpgsqlDataSource as a singleton with pgvector support
// This is the modern approach - GlobalTypeMapper is deprecated in Npgsql 8.0+
var dataSourceBuilder = new NpgsqlDataSourceBuilder(appSettings.PostgreSQL.ConnectionString);
dataSourceBuilder.UseVector();
var dataSource = dataSourceBuilder.Build();
builder.Services.AddSingleton(dataSource);

const string SourceName = "ArchitectCentral.AgentStudio";

// Enhance Aspire's default OTEL configuration with Agent Framework sources
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddSource("Microsoft.Agents.AI.*");
        tracing.AddSource("Microsoft.Extensions.AI.*");
        tracing.AddSource(SourceName);
        tracing.AddSource("ArchitectCentral.AgentStudio.Workflows");
    })
    .WithMetrics(metrics =>
    {
        metrics.AddMeter("Microsoft.Agents.AI.*");
        metrics.AddMeter("Microsoft.Extensions.AI.*");
        metrics.AddMeter(SourceName);
        metrics.AddMeter("ArchitectCentral.AgentStudio.Workflows");
    });



// Register services
builder.Services.AddSingleton(appSettings);
builder.Services.AddSingleton<RAGQueryService>();
builder.Services.AddSingleton<IRAGQueryService>(sp => sp.GetRequiredService<RAGQueryService>());
builder.Services.AddSingleton<EnhancedRAGQueryService>();
builder.Services.AddSingleton<KnowledgeSynthesizerService>();
builder.Services.AddSingleton<ChatService>();
builder.Services.AddSingleton<AgentGeneratorService>();
builder.Services.AddSingleton<InteractiveMenu>();
builder.Services.AddSingleton<ProgramAgentTools>();
builder.Services.AddSingleton<AgentGenerationTools>();
builder.Services.AddOpenApi();
// builder.Services.AddSingleton<ConfluenceMcpTools>(); // Commented out for testing - needs implementation

// Configure AI chat client for Agent Framework with OpenTelemetry
var chatClient = new AzureOpenAIClient(
    new Uri(appSettings.AzureOpenAI.Endpoint),
    new AzureKeyCredential(appSettings.AzureOpenAI.ApiKey))
    .GetChatClient(appSettings.AzureOpenAI.ChatDeploymentName)
    .AsIChatClient()
    .AsBuilder()
    .UseOpenTelemetry(sourceName: SourceName, configure: cfg => cfg.EnableSensitiveData = false)  // Enable telemetry
    .Build();

builder.Services.AddChatClient(chatClient);

// Register all AI agents using orchestrator
StartupUI.ShowWorkflowCreation();
AgentRegistrationOrchestrator.RegisterAgents(builder);
StartupUI.ShowWorkflowRegistered();

// Enable OpenAI-compatible API endpoints for DevUI
builder.Services.AddOpenAIResponses();
builder.Services.AddOpenAIConversations();

// Configure API behavior options for validation
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.SuppressModelStateInvalidFilter = false; // Enable automatic validation
    });

var app = builder.Build();

app.MapOpenApi();
app.MapScalarApiReference();
app.MapGet("/", () => Results.Redirect("/scalar"));

// Configure exception handling middleware
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
}

app.MapGet("/error", (HttpContext context) =>
{
    var exceptionFeature = context.Features.Get<IExceptionHandlerFeature>();
    var exception = exceptionFeature?.Error;

    var problemDetails = new
    {
        type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
        title = "An error occurred",
        status = 500,
        detail = exception?.Message ?? "An unexpected error occurred",
        traceId = Activity.Current?.Id ?? context.TraceIdentifier
    };

    return Results.Json(problemDetails, statusCode: 500);
});

// Run in selected mode
if (runWebMode)
{
    // Web mode - API + DevUI
    StartupUI.ShowWebModeBanner();

    // Map Aspire health check endpoints
    app.MapDefaultEndpoints();

    // Map all API endpoints
    EndpointConfiguration.MapEndpoints(app, appSettings);

    await app.RunAsync();
}
else
{
    // Interactive TUI mode (Spectre.Console)
    await app.StartAsync();

    var menu = app.Services.GetRequiredService<InteractiveMenu>();
    await menu.RunAsync();

    await app.StopAsync();
}
