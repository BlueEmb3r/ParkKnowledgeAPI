# Testing Endpoints

## Setup

Build and run the API from the `ParkKnowledgeAPI/` directory:

```bash
cd ParkKnowledgeAPI
dotnet build
func start
```

The API will be available at `http://localhost:7071/api/`.

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
