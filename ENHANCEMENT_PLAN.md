# DocAI Enhancement Plan

## Current state

### Done
- Local file storage for uploads and downloads
- SQLite backend for single-instance deployment
- JWT auth
- Local OCR/text extraction with Tesseract + PdfPig
- Document chunking with overlap
- Local chunk index with hashed vectors + cosine similarity
- Chat-style Q&A endpoint through Ollama
- Angular chat-only UI

### Planned
- Better answer formatting
- Streaming responses
- Source citations in answers
- Prompt tuning
- Optional real vector DB upgrade
- Guardrails and evaluation metrics
- Better observability/logging

---

## Phase 1: Current retrieval stack

### 1.1 Chunking
- Split extracted text into fixed windows with overlap
- Store chunk data in `App_Data/document-chunks.json`
- Keep chunk metadata per document and per user

### 1.2 Retrieval
- Build a query vector from the question
- Score chunks with cosine similarity
- Keep a small lexical boost for exact phrase matches

### 1.3 Chat answers
- Retrieve top chunks for the user question
- Send question + context to local Ollama
- Return a natural-language answer with source chunks

---

## Phase 2: Improve answer quality

- Add citations in the answer body
- Add streaming token output in the UI
- Add better fallback text when Ollama is unavailable
- Tune prompts for short, direct answers

---

## Phase 3: Optional upgrades

- Replace the JSON chunk index with SQLite FTS or a real vector DB
- Add summarization
- Add guardrails
- Add evaluation/monitoring

---

## Deployment target

- Single-instance deployment
- Local files on a persistent disk
- Ollama on the same host as the .NET backend
- Angular frontend on a free static host

---

## Next steps

1. Add answer streaming
2. Improve source citations
3. Tune the Ollama prompt
4. Decide if a real vector DB is needed later
