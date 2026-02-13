using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ParkKnowledgeAPI.Models;
using ParkKnowledgeAPI.Services.Interfaces;

namespace ParkKnowledgeAPI.Functions;

public class IngestFunction
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly IVectorStoreService _vectorStoreService;
    private readonly ILogger<IngestFunction> _logger;

    public IngestFunction(
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        IVectorStoreService vectorStoreService,
        ILogger<IngestFunction> logger)
    {
        _embeddingGenerator = embeddingGenerator;
        _vectorStoreService = vectorStoreService;
        _logger = logger;
    }

    [Function("Ingest")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/ingest")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Ingest endpoint called");

        // Determine document source: request body or local files
        var documents = await GetDocumentsAsync(req, cancellationToken);

        if (documents.Count == 0)
        {
            return new BadRequestObjectResult(new ErrorResponse("No documents found to ingest."));
        }

        _logger.LogInformation("Processing {Count} documents for ingestion", documents.Count);

        try
        {
            // Ensure the Qdrant collection exists
            await _vectorStoreService.CreateCollectionIfNotExistsAsync(cancellationToken);

            // Parse each document and extract the description for embedding
            var parkData = new List<(string ParkCode, string ParkName, string State, string Content, string Description)>();

            foreach (var (fileName, content) in documents)
            {
                var parkCode = Path.GetFileNameWithoutExtension(fileName);
                var lines = content.Split('\n', StringSplitOptions.None);

                if (lines.Length < 2)
                {
                    _logger.LogWarning("Skipping {File}: insufficient content", fileName);
                    continue;
                }

                var parkName = lines[0].Trim();
                var state = lines.Length > 1 ? lines[1].Replace("State(s):", "").Trim() : "Unknown";
                var description = ExtractDescription(content);

                parkData.Add((parkCode, parkName, state, content, description));
            }

            if (parkData.Count == 0)
            {
                return new BadRequestObjectResult(new ErrorResponse("No valid documents to ingest after parsing."));
            }

            // Generate embeddings for all descriptions in one batch
            var descriptions = parkData.Select(p => p.Description).ToList();
            var embeddings = await _embeddingGenerator.GenerateAsync(descriptions, cancellationToken: cancellationToken);

            // Pair parks with their embeddings and upsert
            var parksWithEmbeddings = parkData.Zip(embeddings, (park, embedding) =>
                (park.ParkCode, park.ParkName, park.State, park.Content, (ReadOnlyMemory<float>)embedding.Vector));

            await _vectorStoreService.UpsertParksAsync(parksWithEmbeddings, cancellationToken);

            _logger.LogInformation("Successfully ingested {Count} parks", parkData.Count);

            return new OkObjectResult(new IngestResponse($"Successfully ingested {parkData.Count} parks.", parkData.Count));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ingest documents");
            return new ObjectResult(new ErrorResponse("An error occurred during ingestion."))
            {
                StatusCode = StatusCodes.Status500InternalServerError
            };
        }
    }

    private async Task<List<(string FileName, string Content)>> GetDocumentsAsync(
        HttpRequest req, CancellationToken cancellationToken)
    {
        // Try reading from request body first
        try
        {
            if (req.ContentLength > 0)
            {
                var body = await req.ReadFromJsonAsync<IngestRequest>(cancellationToken);
                if (body?.Documents is { Count: > 0 })
                {
                    _logger.LogInformation("Using {Count} documents from request body", body.Documents.Count);
                    return body.Documents.Select(d => (d.FileName, d.Content)).ToList();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not parse request body, falling back to local files");
        }

        // Fall back to reading from local Data/parks/ directory
        var parksDir = Path.Combine(AppContext.BaseDirectory, "Data", "parks");

        if (!Directory.Exists(parksDir))
        {
            _logger.LogWarning("Parks directory not found at {Path}", parksDir);
            return [];
        }

        var files = Directory.GetFiles(parksDir, "*.txt");
        _logger.LogInformation("Found {Count} park files in {Path}", files.Length, parksDir);

        var documents = new List<(string FileName, string Content)>();

        foreach (var file in files)
        {
            var content = await File.ReadAllTextAsync(file, cancellationToken);
            documents.Add((Path.GetFileName(file), content));
        }

        return documents;
    }

    internal static string ExtractDescription(string content)
    {
        const string descriptionHeader = "Description:";

        var descIndex = content.IndexOf(descriptionHeader, StringComparison.OrdinalIgnoreCase);
        if (descIndex < 0)
            return content; // fallback: embed entire content

        var start = descIndex + descriptionHeader.Length;

        // Find the next section header (a line ending with ":")
        var remaining = content[start..];
        var lines = remaining.Split('\n');
        var descriptionLines = new List<string>();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            // Stop at the next section header (non-empty line ending with ":")
            if (descriptionLines.Count > 0 && trimmed.Length > 0 && trimmed.EndsWith(':') && !trimmed.Contains(' '))
            {
                break;
            }

            // Also stop at common section headers with spaces like "Operating Hours:"
            if (descriptionLines.Count > 0 && trimmed.Length > 0 && trimmed.EndsWith(':')
                && (trimmed.StartsWith("Directions", StringComparison.OrdinalIgnoreCase)
                    || trimmed.StartsWith("Operating", StringComparison.OrdinalIgnoreCase)
                    || trimmed.StartsWith("Weather", StringComparison.OrdinalIgnoreCase)))
            {
                break;
            }

            descriptionLines.Add(trimmed);
        }

        var description = string.Join(" ", descriptionLines).Trim();
        return string.IsNullOrEmpty(description) ? content : description;
    }
}
