# ParkKnowledgeAPI

Azure Functions v4 API powered by Semantic Kernel, Qdrant vector search, and local ONNX embeddings for national park information.

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Azure Functions Core Tools v4](https://learn.microsoft.com/en-us/azure/azure-functions/functions-run-local)
- [Docker](https://www.docker.com/) (for Qdrant)

## Getting Started

1. **Start Qdrant**
   ```
   docker compose up -d
   ```

2. **Download the embedding model** (~87 MB)
   ```
   curl -L -o ParkKnowledgeAPI/Models/onnx/model.onnx https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/onnx/model.onnx
   ```
   > On Windows PowerShell, use `curl.exe` instead of `curl`.

3. **Build**
   ```
   cd ParkKnowledgeAPI
   dotnet build
   ```

4. **Run**
   ```
   cd ParkKnowledgeAPI
   func start
   ```

> `local.settings.json` is committed with the shared DeepSeek API key provided for this assessment — no configuration needed.

## Project Structure

```
ParkKnowledgeAPI/
  Functions/           # HTTP-triggered Azure Functions (API endpoints)
  Models/              # Domain models and DTOs
    onnx/              # all-MiniLM-L6-v2 ONNX model for local embeddings
  Services/            # Business logic and external service clients
    Interfaces/        # Service contracts
  Mcp/                 # Model Context Protocol server/tools
  Orchestration/       # Semantic Kernel agent orchestration
  Data/
    parks/             # Park knowledge base source files
  Program.cs           # App startup and DI configuration
  host.json            # Azure Functions host config
  local.settings.json  # Local environment variables (committed for assessment)
docker-compose.yml     # Qdrant vector database
```

## Design Decisions

### One vector per park, no chunking

The embedding model (all-MiniLM-L6-v2) has a 256-token context window. We only embed the **Description** section of each park file, which is 36-67 words across the dataset -- well within that limit. There is no text to split, so chunking and overlap logic would be dead code.

The full file content (directions, weather, hours) is stored as payload in Qdrant and returned to the LLM at query time, so no information is lost.

### Embed description, store everything

Descriptions are the most **semantically meaningful** section for similarity search — a user asking "parks with glaciers" should match on description content, not driving directions or weather. Embedding the full file would dilute the signal with boilerplate (operating hours, route numbers) that adds noise to cosine similarity.

### No custom embedding wrapper

`Microsoft.SemanticKernel.Connectors.Onnx` provides `BertOnnxTextEmbeddingGenerationService` which handles tokenization, ONNX inference, mean pooling, and L2 normalization internally. Wrapping it in a custom `IEmbeddingService` would add indirection with no benefit — the SK `IEmbeddingGenerator<string, Embedding<float>>` interface is injected directly where needed.

## Key Dependencies

| Package | Purpose |
|---|---|
| Microsoft.SemanticKernel | LLM orchestration and plugin system |
| Microsoft.SemanticKernel.Agents.Core | ChatCompletionAgent for single-agent orchestration |
| Microsoft.SemanticKernel.Connectors.OpenAI | OpenAI-compatible LLM connector (DeepSeek) |
| ModelContextProtocol | MCP server for tool exposure |
| Qdrant.Client | Vector database client |
| Microsoft.ML.OnnxRuntime | Local embedding generation (all-MiniLM-L6-v2) |
