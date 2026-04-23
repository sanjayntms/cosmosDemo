using Microsoft.Azure.Cosmos;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// 1. Read Infrastructure Configuration (Set these in App Service Settings via Terraform)
var endpoint = builder.Configuration["COSMOS_ENDPOINT"];
var key = builder.Configuration["COSMOS_KEY"];
var appRegion = builder.Configuration["APP_REGION"]; // e.g., "Australia East" or "Central India"
var databaseId = builder.Configuration["DATABASE_NAME"] ?? "DemoDB";
var containerId = builder.Configuration["CONTAINER_NAME"] ?? "DemoContainer";

// 2. Configure Cosmos Client for Local Region
var cosmosClientOptions = new CosmosClientOptions
{
    // THIS IS CRITICAL: It forces the App Service to read/write to its local Cosmos replica
    ApplicationRegion = appRegion, 
};

var cosmosClient = new CosmosClient(endpoint, key, cosmosClientOptions);
builder.Services.AddSingleton(cosmosClient);

var app = builder.Build();

// Serve the frontend UI
app.UseStaticFiles();
app.MapGet("/", () => Results.Content(File.ReadAllText("wwwroot/index.html"), "text/html"));

// API: Get Current Region Info
app.MapGet("/api/info", () =>
{
    return Results.Ok(new { AppServiceRegion = appRegion });
});

// API: Write Document & Measure Latency
app.MapPost("/api/write", async (CosmosClient client) =>
{
    var container = client.GetContainer(databaseId, containerId);
    var id = Guid.NewGuid().ToString();
    
    // Create a dummy payload
    var doc = new { id = id, message = $"Written from {appRegion}", timestamp = DateTime.UtcNow };

    var stopwatch = Stopwatch.StartNew();
    var response = await container.CreateItemAsync(doc, new PartitionKey(id));
    stopwatch.Stop();

    return Results.Ok(new {
        Id = id,
        LatencyMs = stopwatch.ElapsedMilliseconds,
        Region = appRegion
    });
});

// API: Read Document & Measure Latency
app.MapGet("/api/read/{id}", async (CosmosClient client, string id) =>
{
    var container = client.GetContainer(databaseId, containerId);
    var stopwatch = Stopwatch.StartNew();
    try 
    {
        var response = await container.ReadItemAsync<dynamic>(id, new PartitionKey(id));
        stopwatch.Stop();
        
        return Results.Ok(new {
            Id = id,
            LatencyMs = stopwatch.ElapsedMilliseconds,
            Region = appRegion,
            Data = response.Resource
        });
    } 
    catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound) 
    {
        stopwatch.Stop();
        return Results.NotFound(new { 
            Message = "Document not found or not replicated yet.", 
            LatencyMs = stopwatch.ElapsedMilliseconds 
        });
    }
});

app.Run();