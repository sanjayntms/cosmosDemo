using Microsoft.Azure.Cosmos;

var builder = WebApplication.CreateBuilder(args);

// Configuration
var endpoint = builder.Configuration["COSMOS_ENDPOINT"];
var key = builder.Configuration["COSMOS_KEY"];
var appRegion = builder.Configuration["APP_REGION"] ?? "Local";
var databaseId = builder.Configuration["DATABASE_NAME"] ?? "DemoDB";
var containerId = builder.Configuration["CONTAINER_NAME"] ?? "DemoContainer";

// Cosmos Client Setup with Region Affinity
var cosmosClientOptions = new CosmosClientOptions { ApplicationRegion = appRegion };
var cosmosClient = new CosmosClient(endpoint, key, cosmosClientOptions);
builder.Services.AddSingleton(cosmosClient);

var app = builder.Build();

app.UseStaticFiles();
app.MapGet("/", () => Results.Content(File.ReadAllText("wwwroot/index.html"), "text/html"));
app.MapGet("/api/info", () => Results.Ok(new { AppServiceRegion = appRegion }));

// API: Submit a Vote
app.MapPost("/api/vote/{player}", async (CosmosClient client, string player) =>
{
    var container = client.GetContainer(databaseId, containerId);
    
    // We use a static partition key for this PoC so we can easily query and count all votes
    var doc = new { 
        id = Guid.NewGuid().ToString(), 
        partitionKey = "IndVsNzFinal", 
        playerName = player, 
        region = appRegion, 
        timestamp = DateTime.UtcNow 
    };
    
    await container.CreateItemAsync(doc, new PartitionKey("IndVsNzFinal"));
    return Results.Ok();
});

// API: Get Aggregated Results
app.MapGet("/api/results", async (CosmosClient client) =>
{
    var container = client.GetContainer(databaseId, containerId);
    
    // Aggregate votes directly in the database
    var query = new QueryDefinition(
        "SELECT c.playerName, COUNT(1) as votes FROM c WHERE c.partitionKey = 'IndVsNzFinal' GROUP BY c.playerName"
    );
    
    var iterator = container.GetItemQueryIterator<dynamic>(
        query, 
        requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey("IndVsNzFinal") }
    );
    
    var results = new List<dynamic>();
    while (iterator.HasMoreResults)
    {
        var response = await iterator.ReadNextAsync();
        results.AddRange(response);
    }
    
    return Results.Ok(results);
});

app.Run();