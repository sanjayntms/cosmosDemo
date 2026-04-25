using Microsoft.Azure.Cosmos;

var builder = WebApplication.CreateBuilder(args);

// 1. Read Infrastructure Configuration (Set via Azure App Settings)
var endpoint = builder.Configuration["COSMOS_ENDPOINT"];
var key = builder.Configuration["COSMOS_KEY"];
var appRegion = builder.Configuration["APP_REGION"] ?? "Local";
var databaseId = builder.Configuration["DATABASE_NAME"] ?? "DemoDB";
var containerId = builder.Configuration["CONTAINER_NAME"] ?? "DemoContainer";

// 2. Build a safe region list for the Circuit Breaker (Prevents duplicate region crashes)
var preferredRegions = new List<string>();
if (appRegion != "Local") 
{
    preferredRegions.Add(appRegion); // Priority 1: Always try local datacenter first
}
// Only add Central India as fallback if it isn't already the primary region
if (!preferredRegions.Contains("Central India")) 
{
    preferredRegions.Add("Central India"); // Priority 2: Regional Fallback
}

// 3. Configure Cosmos Client with Automatic Regional Failover
var cosmosClientOptions = new CosmosClientOptions 
{ 
    ApplicationPreferredRegions = preferredRegions,
    RequestTimeout = TimeSpan.FromSeconds(5),
    MaxRetryAttemptsOnRateLimitedRequests = 3
};
var cosmosClient = new CosmosClient(endpoint, key, cosmosClientOptions);

// THE FIX: Register the CosmosClient into memory so the APIs can find it!
builder.Services.AddSingleton(cosmosClient);

var app = builder.Build();

// 4. Serve Frontend UI (Optimized for Azure Linux App Services)
app.UseDefaultFiles(); // Automatically resolves index.html at the root URL
app.UseStaticFiles();  // Safely serves static assets from the wwwroot folder

// 5. API: Get Server Info (Used by UI to show active region)
app.MapGet("/api/info", () => Results.Ok(new { AppServiceRegion = appRegion }));

// 6. API: Submit a Vote (Dynamic Partition Key)
app.MapPost("/api/vote/{matchName}/{player}", async (CosmosClient client, string matchName, string player) =>
{
    var container = client.GetContainer(databaseId, containerId);
    
    var doc = new { 
        id = Guid.NewGuid().ToString(), 
        partitionKey = matchName, // The dynamic logical folder!
        playerName = player, 
        region = appRegion, 
        timestamp = DateTime.UtcNow 
    };
    
    await container.CreateItemAsync(doc, new PartitionKey(matchName));
    return Results.Ok();
});

// 7. API: Get Aggregated Results (Dynamic Partition Key)
app.MapGet("/api/results/{matchName}", async (CosmosClient client, string matchName) =>
{
    var container = client.GetContainer(databaseId, containerId);
    
    // Parameterized query for SQL injection safety
    var query = new QueryDefinition(
        "SELECT c.playerName, COUNT(1) as votes FROM c WHERE c.partitionKey = @match GROUP BY c.playerName"
    ).WithParameter("@match", matchName);
    
    var iterator = container.GetItemQueryIterator<VoteResult>(
        query, 
        requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(matchName) }
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

// 8. Strongly Typed Model for JSON Serialization
public class VoteResult
{
    public string playerName { get; set; }
    public int votes { get; set; }
}