#!/usr/bin/env bash
# test-endpoints.sh
# Integration tests for ParkKnowledgeAPI endpoints.
#
# Setup:
#   1. Start Qdrant:          docker compose up -d
#   2. Build the API:         dotnet build ParkKnowledgeAPI
#   3. Run the API:           cd ParkKnowledgeAPI && func start
#   4. Run this script:       ./test-endpoints.sh
#
# The API runs at http://localhost:7071/api/v1/
# Qdrant dashboard: http://localhost:6333/dashboard

BASE_URL="http://localhost:7071/api/v1"

# Colors
CYAN='\033[0;36m'
GREEN='\033[0;32m'
NC='\033[0m' # No Color

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
echo -e "\n${CYAN}===== GET /health =====${NC}"
curl -s "$BASE_URL/health" | python3 -m json.tool 2>/dev/null || curl -s "$BASE_URL/health"
echo ""

# ── POST /ask — validation (empty question) ─────────────────
# Sending an empty question should return HTTP 400.
# Expected: {"error":"Question is required."}
echo -e "${CYAN}===== POST /ask - empty question (expect 400) =====${NC}"
curl -s -X POST "$BASE_URL/ask" -H "Content-Type: application/json" -d '{"question": ""}'
echo -e "\n"

# ── POST /ingest — single document via request body ─────────
# Sends one park document inline. The API parses fileName (park code),
# line 1 (park name), line 2 (state), and extracts the Description section
# to generate an ONNX embedding, then upserts to Qdrant.
# Expected: {"message":"Successfully ingested 1 parks.","count":1}
echo -e "${CYAN}===== POST /ingest - single document =====${NC}"
curl -s -X POST "$BASE_URL/ingest" -H "Content-Type: application/json" \
  -d '{"documents": [{"fileName": "test.txt", "content": "Test Park\nState(s): CA\n\nDescription:\nA test park for verification.\n\nDirections:\nNone."}]}'
echo -e "\n"

# ── POST /ingest — all local parks (no body) ────────────────
# When no request body is provided, the API falls back to reading
# all .txt files from Data/parks/ (~474 park files from NPS scrape).
# Expected: {"message":"Successfully ingested 474 parks.","count":474}
echo -e "${CYAN}===== POST /ingest - all local parks =====${NC}"
curl -s -X POST "$BASE_URL/ingest"
echo -e "\n"

# ── POST /ask — RAG query (run after ingest) ────────────────
# Uses the search_parks MCP tool to query Qdrant before answering.
# Response should reference Mount Rainier with specific ingested details:
#   - Active volcano, most glaciated peak in contiguous US (14,410 ft)
#   - Located in Washington state
#   - Subalpine wildflower meadows, ancient forests, diverse wildlife
#   - Feeds five major rivers
#   - Entrances: Nisqually (year-round), Carbon River, White River
#   - Peak season: July–August for wildflowers
echo -e "${CYAN}===== POST /ask - RAG query (after ingest) =====${NC}"
curl -s -X POST "$BASE_URL/ask" -H "Content-Type: application/json" \
  -d '{"question": "What can you tell me about Mount Rainier?"}'
echo -e "\n"

# ── POST /ask — general question ─────────────────────────────
# Another RAG query to verify broad knowledge retrieval.
echo -e "${CYAN}===== POST /ask - general question =====${NC}"
curl -s -X POST "$BASE_URL/ask" -H "Content-Type: application/json" \
  -d '{"question": "What is Yellowstone known for?"}'
echo -e "\n"

# ── POST /ask/stream — Server-Sent Events ───────────────────
# Returns text/event-stream with chunks: data: {"content":"..."}
# Stream ends with: data: [DONE]
# The -N flag disables curl buffering for real-time output.
echo -e "${CYAN}===== POST /ask/stream - streaming query =====${NC}"
curl -s -N -X POST "$BASE_URL/ask/stream" -H "Content-Type: application/json" \
  -d '{"question": "Tell me about Acadia National Park"}'
echo -e "\n"

echo -e "${GREEN}===== Done =====${NC}"
