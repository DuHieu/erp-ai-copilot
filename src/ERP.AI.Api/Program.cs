using ERP.AI.Api.Middleware;
using ERP.AI.Copilot.Providers;
using ERP.AI.Copilot.Services;
using ERP.AI.Core.Interfaces;
using ERP.AI.Infrastructure.Data;
using ERP.AI.Infrastructure.Repositories;
using ERP.AI.Infrastructure.Security;
using ERP.AI.Knowledge.Interfaces;
using ERP.AI.Tools.Definitions;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Fail-Fast Startup Configuration Validation
ValidateConfiguration(builder.Configuration);

// Add Services to Container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ERP AI Copilot API",
        Version = "v1",
        Description = "Open-Source AI Copilot for ERP Systems (.NET 8 & Safe Tool Calling)"
    });
});

// Configure CORS for Local Web Client
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Configure SQLite Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                       ?? "Data Source=/app/data/erp-demo.db";

// Ensure database directory exists
var dbFilePath = connectionString.Replace("Data Source=", "").Trim();
var dbDir = Path.GetDirectoryName(dbFilePath);
if (!string.IsNullOrWhiteSpace(dbDir) && !Directory.Exists(dbDir))
{
    Directory.CreateDirectory(dbDir);
}

builder.Services.AddDbContext<ErpDbContext>(options =>
    options.UseSqlite(connectionString));

// Register Security Mocks
builder.Services.AddScoped<ICurrentUser, MockCurrentUser>();
builder.Services.AddScoped<IErpPermissionService, MockErpPermissionService>();

// Register Repositories
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<IInvoiceRepository, InvoiceRepository>();
builder.Services.AddScoped<ISalesRepository, SalesRepository>();
builder.Services.AddScoped<IInventoryRepository, InventoryRepository>();
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();

// Register ERP Tools
builder.Services.AddScoped<IErpTool, GetTopDebtorsTool>();
builder.Services.AddScoped<IErpTool, GetCustomerReceivableTool>();
builder.Services.AddScoped<IErpTool, GetRevenueSummaryTool>();
builder.Services.AddScoped<IErpTool, GetInventoryAlertsTool>();
builder.Services.AddScoped<IErpTool, GetProjectBudgetAlertsTool>();

// Register Knowledge Ingestion & Base Engine
builder.Services.AddScoped<IKnowledgeDocumentRepository, KnowledgeDocumentRepository>();
builder.Services.AddScoped<IKnowledgeChunkRepository, KnowledgeChunkRepository>();
builder.Services.AddSingleton<ERP.AI.Knowledge.Storage.IDocumentStorage, ERP.AI.Knowledge.Storage.LocalDocumentStorage>();
builder.Services.AddSingleton<ERP.AI.Knowledge.Services.IDocumentTextNormalizer, ERP.AI.Knowledge.Services.DocumentTextNormalizer>();
builder.Services.AddSingleton<ERP.AI.Knowledge.Chunking.IDocumentChunker, ERP.AI.Knowledge.Chunking.StructureAwareChunker>();

builder.Services.AddSingleton<ERP.AI.Knowledge.Parsers.IDocumentParser, ERP.AI.Knowledge.Parsers.PlainTextDocumentParser>();
builder.Services.AddSingleton<ERP.AI.Knowledge.Parsers.IDocumentParser, ERP.AI.Knowledge.Parsers.MarkdownDocumentParser>();
builder.Services.AddHttpClient<ERP.AI.Knowledge.Parsers.IDocumentParser, ERP.AI.Knowledge.Parsers.DoclingServiceDocumentParser>();
builder.Services.AddSingleton<ERP.AI.Knowledge.Parsers.IDocumentParserRegistry, ERP.AI.Knowledge.Parsers.DocumentParserRegistry>();

builder.Services.AddScoped<ERP.AI.Knowledge.Services.IDocumentIngestionService, ERP.AI.Knowledge.Services.DocumentIngestionService>();

// Register Vector Embedding, Qdrant & Semantic Search Engine (Phase 2.2)
builder.Services.AddHttpClient<IEmbeddingService, ERP.AI.Knowledge.Services.LocalEmbeddingServiceClient>();
builder.Services.AddHttpClient<IKnowledgeVectorStore, ERP.AI.Knowledge.Services.QdrantKnowledgeVectorStore>();
builder.Services.AddScoped<IKnowledgeIndexingService, ERP.AI.Knowledge.Services.KnowledgeIndexingService>();
builder.Services.AddScoped<IKnowledgeSearchService, ERP.AI.Knowledge.Services.KnowledgeSearchService>();

// Register Grounded RAG Engine (Phase 2.3)
builder.Services.AddSingleton<ERP.AI.Knowledge.Interfaces.IGroundingContextBuilder, ERP.AI.Knowledge.Services.GroundingContextBuilder>();
builder.Services.AddSingleton<ERP.AI.Knowledge.Interfaces.ICitationValidator, ERP.AI.Knowledge.Services.CitationValidator>();
builder.Services.AddSingleton<ERP.AI.Knowledge.Services.KnowledgeRagConversationStore>();
builder.Services.AddScoped<ERP.AI.Knowledge.Interfaces.IKnowledgeRagService, ERP.AI.Knowledge.Services.KnowledgeRagService>();

// Register Copilot Core Engine
builder.Services.AddScoped<IErpToolRegistry, ErpToolRegistry>();
builder.Services.AddHttpClient<ILlmProvider, OllamaLlmProvider>();
builder.Services.AddScoped<ICopilotService, CopilotService>();

var app = builder.Build();

// Auto-Initialize SQLite Database & Seed Data
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        logger.LogInformation("ERP AI Copilot API starting...");
        logger.LogInformation("Initializing SQLite ERP Demo Database at {DbPath}...", dbFilePath);
        await DbInitializer.InitializeAsync(dbContext);
        logger.LogInformation("SQLite ERP Demo Database initialized successfully.");
        logger.LogInformation("Configured LLM provider: {Provider}", builder.Configuration["AI:Provider"]);
        logger.LogInformation("Configured LLM model: {Model}", builder.Configuration["AI:Model"]);
        logger.LogInformation("ERP AI Copilot API ready.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while initializing the SQLite database.");
    }
}

// Middleware Pipeline
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "ERP AI Copilot API v1");
    });
}

app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

app.Run();

static void ValidateConfiguration(IConfiguration config)
{
    var provider = config["AI:Provider"];
    if (string.IsNullOrWhiteSpace(provider))
    {
        throw new InvalidOperationException("AI:Provider configuration is required (e.g. 'Ollama').");
    }

    var endpoint = config["AI:Endpoint"];
    if (string.IsNullOrWhiteSpace(endpoint))
    {
        throw new InvalidOperationException("AI:Endpoint configuration is required (e.g. 'http://ollama:11434').");
    }

    var model = config["AI:Model"];
    if (string.IsNullOrWhiteSpace(model))
    {
        throw new InvalidOperationException("AI:Model configuration is required (e.g. 'qwen3').");
    }

    var connStr = config.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(connStr))
    {
        throw new InvalidOperationException("ConnectionStrings:DefaultConnection configuration is required.");
    }
}
