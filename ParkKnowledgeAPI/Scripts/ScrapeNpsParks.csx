#!/usr/bin/env dotnet-script
// Simple C# script to scrape park data from the NPS API.
// Run with: dotnet script ParkKnowledgeAPI/Scripts/ScrapeNpsParks.csx
//   or:     dotnet script ScrapeNpsParks.csx   (from Scripts folder)
//
// Requires dotnet-script: dotnet tool install -g dotnet-script

using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

var apiKey = "REMOVED_KEY";
var outputDir = Path.Combine(AppContext.BaseDirectory, "..", "Data", "parks");

// Resolve output path relative to the script's location if running via dotnet-script
var scriptDir = Path.GetDirectoryName(GetScriptPath()) ?? ".";
outputDir = Path.GetFullPath(Path.Combine(scriptDir, "..", "Data", "parks"));

Directory.CreateDirectory(outputDir);

var http = new HttpClient();
http.DefaultRequestHeaders.Add("accept", "application/json");

var baseUrl = "https://developer.nps.gov/api/v1/parks";
int start = 0;
int limit = 50; // NPS API max per page
int total = int.MaxValue;
var allParks = new List<JsonElement>();

Console.WriteLine("Fetching parks from NPS API...");

while (start < total)
{
    var url = $"{baseUrl}?api_key={apiKey}&limit={limit}&start={start}";
    Console.WriteLine($"  Requesting start={start}, limit={limit}...");

    var response = await http.GetAsync(url);
    response.EnsureSuccessStatusCode();

    var json = await response.Content.ReadAsStringAsync();
    var doc = JsonDocument.Parse(json);

    total = doc.RootElement.GetProperty("total").GetString() is string t ? int.Parse(t) : 0;
    var data = doc.RootElement.GetProperty("data");

    foreach (var park in data.EnumerateArray())
    {
        allParks.Add(park.Clone());
    }

    Console.WriteLine($"  Got {data.GetArrayLength()} parks (total: {total})");
    start += limit;
}

Console.WriteLine($"\nFetched {allParks.Count} parks total. Saving to {outputDir}...");

// Save each park as a .txt with selected fields
foreach (var park in allParks)
{
    var parkCode = park.GetProperty("parkCode").GetString() ?? "unknown";
    var fullName = park.GetProperty("fullName").GetString() ?? "";
    var states = park.GetProperty("states").GetString() ?? "";
    var description = park.GetProperty("description").GetString() ?? "";
    var directionsInfo = park.GetProperty("directionsInfo").GetString() ?? "";
    var weatherInfo = park.GetProperty("weatherInfo").GetString() ?? "";

    // Collect all operating hours descriptions
    var hours = new List<string>();
    foreach (var oh in park.GetProperty("operatingHours").EnumerateArray())
    {
        var name = oh.GetProperty("name").GetString() ?? "";
        var desc = oh.GetProperty("description").GetString() ?? "";
        hours.Add(string.IsNullOrEmpty(name) ? desc : $"{name}: {desc}");
    }

    var text = $"""
                {fullName}
                State(s): {states}

                Description:
                {description}

                Directions:
                {directionsInfo}

                Operating Hours:
                {string.Join("\n", hours)}

                Weather:
                {weatherInfo}
                """;

    var filePath = Path.Combine(outputDir, $"{parkCode}.txt");
    File.WriteAllText(filePath, text);
}

Console.WriteLine($"Done! Saved {allParks.Count} park .txt files to {outputDir}");

// Helper to get the script file path (dotnet-script specific)
static string GetScriptPath([System.Runtime.CompilerServices.CallerFilePath] string path = "") => path;
