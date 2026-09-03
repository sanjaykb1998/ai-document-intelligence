# DocAI — Document Intelligence & RAG Chat

A full-stack document intelligence application that lets users securely upload PDFs and images, automatically extracts their text, and answers natural-language questions about the uploaded documents using Retrieval-Augmented Generation (RAG).

## Overview

DocAI enables:

* Secure user authentication (JWT-based login/signup)
* Uploading documents (PDF and image formats) per logged-in user
* Automatic text extraction (PDF parsing + OCR for images)
* Chunking and embedding extracted text for semantic search
* Asking natural-language questions and getting AI-generated answers, grounded in your own documents (RAG)
* Viewing, downloading, and deleting uploaded documents
* A modern, dark glassmorphism UI with animations

---

## Tech Stack

### Backend

* ASP.NET Core Web API (.NET 8)
* Entity Framework Core (SQLite/SQL Server via `DefaultConnection`)
* JWT Bearer authentication
* PDF text extraction via PdfPig
* OCR for images via Tesseract
* Local JSON-based chunk/embedding index (`App_Data/document-chunks.json`)
* LLM integration: Groq / OpenAI-compatible Chat Completions API (primary), Ollama (local fallback), heuristic text extraction (last-resort fallback)
* Swagger / OpenAPI

### Frontend

* Angular (standalone components)
* RxJS (Observables, BehaviorSubject)
* JWT-based auth guard + HTTP interceptor
* Chat-style Q&A UI with source citations
* Responsive, animated dark UI theme

---

## Features

### Authentication

* Signup / login with JWT tokens
* Auth guard protecting the document dashboard route
* Logged-in username displayed from the JWT payload

### Document Management

* Upload PDF and image documents (`.pdf`, `.png`, `.jpg`, `.jpeg`, `.bmp`, `.tif`, `.tiff`)
* Per-user document storage and ownership checks
* Background text extraction with status tracking: `Uploaded` → `Processing` → `Processed` / `Failed`
* View extracted text, download the original file, or delete a document (removes file, DB record, and its search-index chunks)

### RAG Chat ("Ask a question")

* Ask questions like ChatGPT about your uploaded documents
* Retrieval: chunks are embedded and ranked by cosine similarity (+ lexical boosting) against the query
* Generation priority:
  1. Groq / OpenAI-compatible Chat Completions API (if `Llm:ApiKey` is configured)
  2. Ollama (local LLM), if reachable
  3. Heuristic regex-based extraction (amounts, dates/durations) as a last resort
  4. Fallback: shortest relevant excerpt from the best-matching source
* Answers include the source file name(s) used, deduplicated

---

## Architecture

```
User (Angular UI)
   │  JWT auth
   ▼
ASP.NET Core API ──► Local file storage (Uploads/)
   │
   ├─► Text extraction (PdfPig / Tesseract OCR) ──► Status tracking
   │
   ├─► Chunking + embeddings ──► App_Data/document-chunks.json
   │
   └─► RAG query: retrieve top chunks ──► Groq/OpenAI (or Ollama) ──► Answer + Sources
```

---

## Project Structure

```
DocAI
│
├── backend
│   └── DocAI/DocAI.Api
│       ├── Controllers      (Auth, Documents)
│       ├── Services         (RagService, DocumentChunkService, DocumentTextService, DocumentProcessorService, BlobService)
│       ├── Data             (EF Core DbContext)
│       ├── Models
│       └── Dockerfile
│
├── frontend
│   └── doc-ai-ui
│       ├── src/app
│       │   ├── auth         (login, signup, entry)
│       │   ├── upload       (document dashboard + chat)
│       │   └── services     (document, auth)
│       └── vercel.json
```

---

## Setup Instructions

### 1. Clone repository

```
git clone https://github.com/sanjaykb1998/ai-document-intelligence.git
```

### 2. Backend Setup

Configure `backend/DocAI/DocAI.Api/appsettings.json` (or user-secrets / environment variables — env vars use double underscore, e.g. `Llm__ApiKey`):

```json
{
  "Jwt": {
    "Key": "<a long random secret>",
    "Issuer": "DocIntelApp",
    "Audience": "DocIntelUsers"
  },
  "Llm": {
    "BaseUrl": "https://api.groq.com/openai/v1",
    "Model": "openai/gpt-oss-120b",
    "ApiKey": "<your Groq or OpenAI API key>"
  },
  "Ollama": {
    "BaseUrl": "http://localhost:11434",
    "Model": "llama3.2:3b",
    "EmbeddingModel": "nomic-embed-text"
  }
}
```

> `Llm.ApiKey` is required for Groq/OpenAI-quality answers. If left empty, the app falls back to Ollama, then to a much cruder heuristic. Groq's available models can change — check `GET https://api.groq.com/openai/v1/models` with your key to confirm a valid model name.

Run the API:

```
cd backend/DocAI/DocAI.Api
dotnet run
```

### 3. Frontend Setup

```
cd frontend/doc-ai-ui
npm install
ng serve
```

Open:

```
http://localhost:4200
```

The frontend points to the backend URL configured in `src/environments/environment.ts` (local) and `environment.prod.ts` (production build, auto-swapped via Angular's `fileReplacements`).

---

## API Endpoints

| Method | Endpoint                     | Description                                          |
| ------ | ----------------------------- | ----------------------------------------------------- |
| POST   | /api/Auth/register            | Register a new user                                   |
| POST   | /api/Auth/login               | Log in, returns a JWT                                  |
| POST   | /api/Documents/upload         | Upload a document (PDF/image)                          |
| GET    | /api/Documents                | Get the current user's documents                        |
| GET    | /api/Documents/{id}/download  | Download a document's original file                     |
| DELETE | /api/Documents/{id}           | Delete a document (file + chunks + record)              |
| POST   | /api/Rag/ask                  | Ask a question, get an AI-generated answer + sources    |

---

## Deployment

* **Backend**: Dockerized ASP.NET Core app, deployed on [Render](https://render.com). Swagger is enabled in all environments for easy API inspection.
* **Frontend**: Angular app deployed on [Vercel](https://vercel.com), with `vercel.json` handling SPA route rewrites.
* Environment variables (Render) use double-underscore notation to map to nested config, e.g. `Llm__BaseUrl`, `Llm__Model`, `Llm__ApiKey`, `Jwt__Key`.

---

## Key Implementation Highlights

* RAG pipeline: chunking → embeddings → cosine-similarity retrieval → LLM generation, with a layered fallback chain (Groq/OpenAI → Ollama → heuristic → excerpt)
* JWT-based authentication with per-user document ownership checks
* Clean layered architecture (Controller → Service → Data)
* Reactive Angular UI (RxJS observables, standalone components)
* Modern, animated dark glassmorphism theme with custom branding/favicon

---

## Future Enhancements

* `.docx` and other document format support
* Upfront file-type validation with clear user-facing errors
* Vector database (e.g. pgvector, Qdrant) instead of a local JSON index for larger-scale search
* Document summarization
* Pagination & advanced filters on the dashboard

---

## Author

**Sanjay B**
GitHub: https://github.com/sanjaykb1998
