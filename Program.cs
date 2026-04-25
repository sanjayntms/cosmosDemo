using Microsoft.Azure.Cosmos;

var builder = WebApplication.CreateBuilder(args);

// 1. Read Infrastructure Configuration (Set via Azure App Settings)
var endpoint = builder.Configuration["COSMOS_ENDPOINT"];
var key = builder.Configuration["COSMOS_KEY"];
var appRegion = builder.Configuration["APP_REGION"] ?? "Local";
var databaseId = builder.Configuration["DATABASE_NAME"] ?? "DemoDB";
var containerId = builder.Configuration["CONTAINER_NAME"] ?? "DemoContainer";

// 2. Configure Cosmos Client with Automatic Regional Failover (Circuit Breaker)
var cosmosClientOptions = new CosmosClientOptions 
{ 
    // The SDK will attempt the regions in this exact order.
    // If the local region goes down, it automatically falls back to the next one in the list.
    ApplicationPreferredRegions = new List<string> 
    { 
        appRegion,          // Priority 1: Always try the local datacenter first
        "Central India",    // Priority 2: Fallback to India if local is down
        "East US"           // Priority 3: Ultimate fallback (if you added a 3rd region)
    },
    
    // Optional but recommended for demos: Lower the timeout so it fails over faster
    RequestTimeout = TimeSpan.FromSeconds(5),
    
    // The SDK's internal circuit breaker will handle the retries across regions
    MaxRetryAttemptsOnRateLimitedRequests = 3
};
var cosmosClient = new CosmosClient(endpoint, key, cosmosClientOptions);

var app = builder.Build();

// 3. Serve Frontend UI
app.UseStaticFiles();
app.MapGet("/", () => Results.Content(File.ReadAllText("wwwroot/index.html"), "text/html"));
app.MapGet("/api/info", () => Results.Ok(new { AppServiceRegion = appRegion }));

// 4. API: Submit a Vote (Dynamic Partition Key)
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

// 5. API: Get Aggregated Results (Dynamic Partition Key)
app.MapGet("/api/results/{matchName}", async (CosmosClient client, string matchName) =>
{
    var container = client.GetContainer(databaseId, containerId);
    
    // Parameterized query for safety
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