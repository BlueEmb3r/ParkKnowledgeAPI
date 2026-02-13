# Testing Endpoints

## Setup

Build and run the API from the `ParkKnowledgeAPI/` directory:

```bash
cd ParkKnowledgeAPI
dotnet build
func start
```

Ensure Qdrant is running (needed for `/ingest`):

```bash
docker compose up -d
```

The API will be available at `http://localhost:7071/api/v1/`.

## GET /health

**Check all modules:**
```bash
curl -s http://localhost:7071/api/v1/health
```

```powershell
curl.exe -s http://localhost:7071/api/v1/health
```

Expected (all healthy):
```json
{
  "status": "healthy",
  "modules": {
    "qdrant": { "status": "healthy", "details": "1 collection(s)" },
    "deepSeek": { "status": "healthy", "details": "model=deepseek-chat, endpoint=https://api.deepseek.com" },
    "onnxEmbedding": { "status": "healthy", "details": "model.onnx=86MB" }
  }
}
```

When a module is down, `status` changes to `"degraded"` and the failing module shows `"unhealthy"` with an error message.

## POST /ask

The ask endpoint uses RAG — it calls the `search_parks` MCP tool to query Qdrant before answering. **Ingest data first** (see `/ingest` below) for grounded responses.

**RAG query (after ingesting):**
```bash
curl -s -X POST http://localhost:7071/api/v1/ask -H "Content-Type: application/json" -d "{\"question\": \"What can you tell me about Mount Rainier?\"}"
```

```powershell
curl.exe -s -X POST http://localhost:7071/api/v1/ask -H "Content-Type: application/json" -d '{"question": "What can you tell me about Mount Rainier?"}'
```

Expected: Response should reference Mount Rainier National Park with specific details from the ingested data, such as:
- It's an active volcano and the most glaciated peak in the contiguous US at 14,410 feet
- Located in Washington state
- Features subalpine wildflower meadows, ancient forests, and diverse wildlife
- Feeds five major rivers
- Park entrances include Nisqually (year-round), Carbon River, and White River
- Peak season is July and August for wildflowers

**General question:**
```bash
curl -s -X POST http://localhost:7071/api/v1/ask -H "Content-Type: application/json" -d "{\"question\": \"What is Yellowstone known for?\"}"
```

```powershell
curl.exe -s -X POST http://localhost:7071/api/v1/ask -H "Content-Type: application/json" -d '{"question": "What is Yellowstone known for?"}'
```

**Validation (empty question):**
```bash
curl -s -X POST http://localhost:7071/api/v1/ask -H "Content-Type: application/json" -d "{\"question\": \"\"}"
```

```powershell
curl.exe -s -X POST http://localhost:7071/api/v1/ask -H "Content-Type: application/json" -d '{"question": ""}'
```

Expected: `{"error":"Question is required."}`

## POST /ingest

**Ingest all parks from local files (no body):**
```bash
curl -s -X POST http://localhost:7071/api/v1/ingest
```

```powershell
curl.exe -s -X POST http://localhost:7071/api/v1/ingest
```

Expected: `{"message":"Successfully ingested 474 parks.","count":474}`

**Ingest specific documents (with body):**
```bash
curl -s -X POST http://localhost:7071/api/v1/ingest -H "Content-Type: application/json" -d "{\"documents\": [{\"fileName\": \"test.txt\", \"content\": \"Test Park\nState(s): CA\n\nDescription:\nA test park for verification.\n\nDirections:\nNone.\"}]}"
```

```powershell
curl.exe -s -X POST http://localhost:7071/api/v1/ingest -H "Content-Type: application/json" -d '{"documents": [{"fileName": "test.txt", "content": "Test Park\nState(s): CA\n\nDescription:\nA test park for verification.\n\nDirections:\nNone."}]}'
```

Expected: `{"message":"Successfully ingested 1 parks.","count":1}`

**Verify in Qdrant dashboard:**
Open `http://localhost:6333/dashboard` and check the `parks` collection has points.
