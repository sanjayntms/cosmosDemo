using Microsoft.Azure.Cosmos;

var builder = WebApplication.CreateBuilder(args);

// 1. Read Infrastructure Configuration
var endpoint = builder.Configuration["COSMOS_ENDPOINT"];
var key = builder.Configuration["COSMOS_KEY"];
var appRegion = builder.Configuration["APP_REGION"] ?? "Local";
var databaseId = builder.Configuration["DATABASE_NAME"] ?? "DemoDB";
var containerId = builder.Configuration["CONTAINER_NAME"] ?? "DemoContainer";

// 2. Build a safe region list (Prevents duplicates and invalid names)
var preferredRegions = new List<string>();
if (appRegion != "Local") 
{
    preferredRegions.Add(appRegion);
}
// Only add Central India as fallback if it isn't already the primary region
if (!preferredRegions.Contains("Central India")) 
{
    preferredRegions.Add("Central India");
}

// 3. Configure Cosmos Client with Automatic Regional Failover
var cosmosClientOptions = new CosmosClientOptions 
{ 
    ApplicationPreferredRegions = preferredRegions,
    RequestTimeout = TimeSpan.FromSeconds(5),
    MaxRetryAttemptsOnRateLimitedRequests = 3
};
var cosmosClient = new CosmosClient(endpoint, key, cosmosClientOptions);

var app = builder.Build();

// 4. Serve Frontend UI
app.UseStaticFiles();
app.MapGet("/", () => Results.Content(File.ReadAllText("wwwroot/index.html"), "text/html"));
app.MapGet("/api/info", () => Results.Ok(new { AppServiceRegion = appRegion }));

// 5. API: Submit a Vote (Dynamic Partition Key)
app.MapPost("/api/vote/{matchName}/{player}", async (CosmosClient client, string matchName, string player) =>
{
    var container = client.GetContainer(databaseId, containerId);
    
    var doc = new { 
        id = Guid.NewGuid().ToString(), 
        partitionKey = matchName, 
        playerName = player, 
        region = appRegion, 
        timestamp = DateTime.UtcNow 
    };
    
    await container.CreateItemAsync(doc, new PartitionKey(matchName));
    return Results.Ok();
});

// 6. API: Get Aggregated Results (Dynamic Partition Key)
app.MapGet("/api/results/{matchName}", async (CosmosClient client, string matchName) =>
{
    var container = client.GetContainer(databaseId, containerId);
    
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

// 7. Strongly Typed Model
public class VoteResult
{
    public string playerName { get; set; }
    public int votes { get; set; }
}