using Microsoft.Azure.Cosmos;

var builder = WebApplication.CreateBuilder(args);

// 1. Read Infrastructure Configuration (Set via Azure App Settings)
var endpoint = builder.Configuration["COSMOS_ENDPOINT"];
var key = builder.Configuration["COSMOS_KEY"];
var appRegion = builder.Configuration["APP_REGION"] ?? "Local";
var databaseId = builder.Configuration["DATABASE_NAME"] ?? "DemoDB";
var containerId = builder.Configuration["CONTAINER_NAME"] ?? "DemoContainer";

// 2. Configure Cosmos Client for Local Region
var cosmosClientOptions = new CosmosClientOptions { ApplicationRegion = appRegion };
var cosmosClient = new CosmosClient(endpoint, key, cosmosClientOptions);
builder.Services.AddSingleton(cosmosClient);

var app = builder.Build();

// 3. Serve Frontend UI
app.UseStaticFiles();
app.MapGet("/", () => Results.Content(File.ReadAllText("wwwroot/index.html"), "text/html"));
app.MapGet("/api/info", () => Results.Ok(new { AppServiceRegion = appRegion }));

// 4. API: Submit a Vote
app.MapPost("/api/vote/{player}", async (CosmosClient client, string player) =>
{
    var container = client.GetContainer(databaseId, containerId);
    
    // Using a static partition key for the final poll to group all votes
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

// 5. API: Get Aggregated Results (Using Strongly Typed Class)
app.MapGet("/api/results", async (CosmosClient client) =>
{
    var container = client.GetContainer(databaseId, containerId);
    
    var query = new QueryDefinition(
        "SELECT c.playerName, COUNT(1) as votes FROM c WHERE c.partitionKey = 'IndVsNzFinal' GROUP BY c.playerName"
    );
    
    var iterator = container.GetItemQueryIterator<VoteResult>(
        query, 
        requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey("IndVsNzFinal") }
    );
    
    var results = new List<VoteResult>(); 
    while (iterator.HasMoreResults)
    {
        var response = await iterator.ReadNextAsync();
        results.AddRange(response);
    }
    
    return Results.Ok(results);
});

app.Run();

// 6. Strongly Typed Model to fix System.Text.Json serialization
public class VoteResult
{
    public string playerName { get; set; }
    public int votes { get; set; }
}