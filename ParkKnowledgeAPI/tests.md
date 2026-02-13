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

The API will be available at `http://localhost:7071/api/`.

## GET /health

**Check all modules:**
```bash
curl -s http://localhost:7071/api/health
```

```powershell
curl.exe -s http://localhost:7071/api/health
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

**Success:**
```bash
curl -s -X POST http://localhost:7071/api/ask -H "Content-Type: application/json" -d "{\"question\": \"What is Yellowstone known for?\"}"
```

```powershell
curl.exe -s -X POST http://localhost:7071/api/ask -H "Content-Type: application/json" -d '{"question": "What is Yellowstone known for?"}'
```

**Validation (empty question):**
```bash
curl -s -X POST http://localhost:7071/api/ask -H "Content-Type: application/json" -d "{\"question\": \"\"}"
```

```powershell
curl.exe -s -X POST http://localhost:7071/api/ask -H "Content-Type: application/json" -d '{"question": ""}'
```

Expected: `{"error":"Question is required."}`

## POST /ingest

**Ingest all parks from local files (no body):**
```bash
curl -s -X POST http://localhost:7071/api/ingest
```

```powershell
curl.exe -s -X POST http://localhost:7071/api/ingest
```

Expected: `{"message":"Successfully ingested 474 parks.","count":474}`

**Ingest specific documents (with body):**
```bash
curl -s -X POST http://localhost:7071/api/ingest -H "Content-Type: application/json" -d "{\"documents\": [{\"fileName\": \"test.txt\", \"content\": \"Test Park\nState(s): CA\n\nDescription:\nA test park for verification.\n\nDirections:\nNone.\"}]}"
```

```powershell
curl.exe -s -X POST http://localhost:7071/api/ingest -H "Content-Type: application/json" -d '{"documents": [{"fileName": "test.txt", "content": "Test Park\nState(s): CA\n\nDescription:\nA test park for verification.\n\nDirections:\nNone."}]}'
```

Expected: `{"message":"Successfully ingested 1 parks.","count":1}`

**Verify in Qdrant dashboard:**
Open `http://localhost:6333/dashboard` and check the `parks` collection has points.
