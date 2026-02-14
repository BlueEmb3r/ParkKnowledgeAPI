# Test-Endpoints.ps1
# Integration tests for ParkKnowledgeAPI endpoints.
#
# Setup:
#   1. Start Qdrant:          docker compose up -d
#   2. Build the API:         dotnet build ParkKnowledgeAPI
#   3. Run the API:           cd ParkKnowledgeAPI; func start
#   4. Run this script:       .\Test-Endpoints.ps1
#
# The API runs at http://localhost:7071/api/v1/
# Qdrant dashboard: http://localhost:6333/dashboard

$BaseUrl = "http://localhost:7071/api/v1"

# ── GET /health ──────────────────────────────────────────────
# Checks all modules: Qdrant (gRPC ping), DeepSeek (config check), ONNX model (file exists).
# Expected: status "healthy" when all modules are up, "degraded" if any are down.
# Example response:
#   {
#     "status": "healthy",
#     "modules": {
#       "qdrant":        { "status": "healthy", "details": "1 collection(s)" },
#       "deepSeek":      { "status": "healthy", "details": "model=deepseek-chat, endpoint=https://api.deepseek.com" },
#       "onnxEmbedding": { "status": "healthy", "details": "model.onnx=86MB" }
#     }
#   }
Write-Host "`n===== GET /health =====" -ForegroundColor Cyan
curl.exe -s "$BaseUrl/health" | ConvertFrom-Json | ConvertTo-Json -Depth 5
Write-Host ""

# ── POST /ask — validation (empty question) ─────────────────
# Sending an empty question should return HTTP 400.
# Expected: {"error":"Question is required."}
Write-Host "===== POST /ask - empty question (expect 400) =====" -ForegroundColor Cyan
curl.exe -s -X POST "$BaseUrl/ask" -H "Content-Type: application/json" -d '{"question": ""}'
Write-Host "`n"

# ── POST /ingest — single document via request body ─────────
# Sends one park document inline. The API parses fileName (park code),
# line 1 (park name), line 2 (state), and extracts the Description section
# to generate an ONNX embedding, then upserts to Qdrant.
# Expected: {"message":"Successfully ingested 1 parks.","count":1}
Write-Host "===== POST /ingest - single document =====" -ForegroundColor Cyan
curl.exe -s -X POST "$BaseUrl/ingest" -H "Content-Type: application/json" -d '{"documents": [{"fileName": "test.txt", "content": "Test Park\nState(s): CA\n\nDescription:\nA test park for verification.\n\nDirections:\nNone."}]}'
Write-Host "`n"

# ── POST /ingest — all local parks (no body) ────────────────
# When no request body is provided, the API falls back to reading
# all .txt files from Data/parks/ (~474 park files from NPS scrape).
# Expected: {"message":"Successfully ingested 474 parks.","count":474}
Write-Host "===== POST /ingest - all local parks =====" -ForegroundColor Cyan
curl.exe -s -X POST "$BaseUrl/ingest"
Write-Host "`n"

# ── POST /ask — RAG query (run after ingest) ────────────────
# Uses the search_parks MCP tool to query Qdrant before answering.
# Response should reference Mount Rainier with specific ingested details:
#   - Active volcano, most glaciated peak in contiguous US (14,410 ft)
#   - Located in Washington state
#   - Subalpine wildflower meadows, ancient forests, diverse wildlife
#   - Feeds five major rivers
#   - Entrances: Nisqually (year-round), Carbon River, White River
#   - Peak season: July–August for wildflowers
Write-Host "===== POST /ask - RAG query (after ingest) =====" -ForegroundColor Cyan
curl.exe -s -X POST "$BaseUrl/ask" -H "Content-Type: application/json" -d '{"question": "What can you tell me about Mount Rainier?"}'
Write-Host "`n"

# ── POST /ask — general question ─────────────────────────────
# Another RAG query to verify broad knowledge retrieval.
Write-Host "===== POST /ask - general question =====" -ForegroundColor Cyan
curl.exe -s -X POST "$BaseUrl/ask" -H "Content-Type: application/json" -d '{"question": "What is Yellowstone known for?"}'
Write-Host "`n"

# ── POST /ask/stream — Server-Sent Events ───────────────────
# Returns text/event-stream with chunks: data: {"content":"..."}
# Stream ends with: data: [DONE]
# The -N flag disables curl buffering for real-time output.
Write-Host "===== POST /ask/stream - streaming query =====" -ForegroundColor Cyan
curl.exe -s -N -X POST "$BaseUrl/ask/stream" -H "Content-Type: application/json" -d '{"question": "Tell me about Acadia National Park"}'
Write-Host "`n"

Write-Host "===== Done =====" -ForegroundColor Green
