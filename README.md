# DocAI — Document Intelligence & RAG Chat

A full-stack document intelligence platform that lets users securely upload documents across multiple formats (`.pdf`, `.docx`, `.txt`, images), automatically extracts structured text via native parsers and OCR, and answers natural-language questions using a high-precision Retrieval-Augmented Generation (RAG) pipeline.

## Overview

DocAI enables:

* **Zero-Trust Security**: Secure authentication via `HttpOnly`, `Secure`, `SameSite=None` JWT cookies (shielding against XSS and token tampering).
* **Multi-Format Document Ingestion**: Supports `.pdf`, `.docx` (OpenXML), `.txt` (UTF-8), and image formats (`.png`, `.jpg`, `.jpeg`, `.bmp`, `.tif`, `.tiff`).
* **Automated Text Extraction**: Native high-speed parsing for digital formats and Tesseract OCR for scanned images.
* **3-Tier Vector Embedding Pipeline**: Cloud Hugging Face Inference embeddings (`sentence-transformers/all-MiniLM-L6-v2`), local Ollama (`nomic-embed-text`), and deterministic 256-D FNV-1a hashed fallback.
* **Semantic Search & RAG Q&A**: Context-grounded Q&A via Groq / OpenAI LLMs with cosine similarity ranking and accurate source document attribution.
* **Modern Dashboard**: 2-column responsive layout featuring full-width AI chat response, document status tracking, text preview, download, and delete actions.

---

## Tech Stack

### Backend

* **Framework**: ASP.NET Core Web API (.NET 8)
* **Database & ORM**: SQLite / SQL Server via Entity Framework Core (`AppDbContext`)
* **Security & Auth**: JWT authentication with `HttpOnly` cookie-based session management and BCrypt password hashing
* **Document Parsing**: 
  * PDF text extraction via `PdfPig`
  * Word document (`.docx`) extraction via `System.IO.Compression` & `XDocument` (OpenXML)
  * Plain text (`.txt`) extraction via UTF-8 streams
  * Image OCR via `Tesseract`
* **Embeddings & Vector Index**: 
  * Cloud: Hugging Face Inference API (`sentence-transformers/all-MiniLM-L6-v2`)
  * Local: Ollama API (`nomic-embed-text`)
  * Deterministic fallback: FNV-1a bigram hashed vector embeddings
  * Local chunk index: `App_Data/document-chunks.json`
* **LLM Integration**: Groq API / OpenAI-compatible Chat Completions (`openai/gpt-oss-120b`), Ollama fallback, and rule-based fallback
* **API Documentation**: Swagger / OpenAPI

### Frontend

* **Framework**: Angular 17 (Standalone Components)
* **State & Data Streams**: RxJS (`Observables`, `BehaviorSubject`, `async` pipe)
* **HTTP & Security**: Interceptor configured with `withCredentials: true` for cross-domain cookie exchange; Route Auth Guard
* **UI/UX**: Custom glassmorphism dark theme, animated CSS gradients, responsive 2-column grid layout, and code/text overflow handling

---

## Architecture

```
User (Angular 17 on Vercel)
   │  HttpOnly JWT Cookie (Cross-Origin CORS withCredentials)
   ▼
ASP.NET Core 8 Web API (on Render) ──► Local file storage (uploads/)
   │
   ├─► Text Extraction:
   │     ├── .pdf  ──► PdfPig
   │     ├── .docx ──► OpenXML (System.IO.Compression)
   │     ├── .txt  ──► UTF-8 Reader
   │     └── Images ──► Tesseract OCR
   │
   ├─► Chunking (1200 chars / 200 overlap) + Vector Embeddings:
   │     ├── Tier 1: Hugging Face Cloud API (384-D)
   │     ├── Tier 2: Local Ollama API (768-D)
   │     └── Tier 3: Deterministic Hashed Fallback (256-D)
   │     └── Storage ──► App_Data/document-chunks.json
   │
   └─► RAG Retrieval & Generation:
         ├── Query Embedding + Cosine Similarity Ranking (Top-5 chunks)
         ├── Context Augmentation + Strict Prompting
         └── Groq / OpenAI API (gpt-oss-120b) ──► Answer + Sources Cited
```

---

## Project Structure

```
DocAI
│
├── backend
│   └── DocAI/DocAI.Api
│       ├── Controllers      (AuthController, DocumentsController, RagController, SearchController)
│       ├── Services         (RagService, DocumentChunkService, DocumentTextService, DocumentProcessorService, BlobService)
│       ├── Data             (AppDbContext, EF Core Migrations)
│       ├── Models           (Document, DocumentChunk, User, AskRequest/Response, SearchResult)
│       └── Dockerfile
│
├── frontend
│   └── doc-ai-ui
│       ├── src/app
│       │   ├── auth         (login, signup)
│       │   ├── upload       (document dashboard + RAG chat)
│       │   ├── services     (auth, document)
│       │   ├── guards       (auth-guard)
│       │   └── interceptors (auth-interceptor)
│       └── vercel.json
```

---

## Setup Instructions

### 1. Clone repository

```bash
git clone https://github.com/sanjaykb1998/ai-document-intelligence.git
```

### 2. Backend Setup

Configure `backend/DocAI/DocAI.Api/appsettings.json` (or set environment variables in cloud hosting using double underscores, e.g. `Llm__ApiKey`, `Embedding__ApiKey`):

```json
{
  "Jwt": {
    "Key": "<a long random secret key>",
    "Issuer": "DocIntelApp",
    "Audience": "DocIntelUsers",
    "DurationInMinutes": 60
  },
  "Llm": {
    "BaseUrl": "https://api.groq.com/openai/v1",
    "Model": "openai/gpt-oss-120b",
    "ApiKey": "<your Groq or OpenAI API key>"
  },
  "Embedding": {
    "BaseUrl": "https://router.huggingface.co/hf-inference/v1/embeddings",
    "Model": "sentence-transformers/all-MiniLM-L6-v2",
    "ApiKey": "<your HuggingFace Read token>"
  },
  "Ollama": {
    "BaseUrl": "http://localhost:11434",
    "Model": "llama3.2:3b",
    "EmbeddingModel": "nomic-embed-text"
  }
}
```

Run the API:

```bash
cd backend/DocAI/DocAI.Api
dotnet run
```

### 3. Frontend Setup

```bash
cd frontend/doc-ai-ui
npm install
ng serve
```

Open `http://localhost:4200` in your browser.

---

## API Endpoints

| Method | Endpoint                     | Description                                            |
| ------ | ---------------------------- | ------------------------------------------------------ |
| POST   | `/api/auth/signup`           | Register a new user account                            |
| POST   | `/api/auth/login`            | Authenticate and issue secure `HttpOnly` JWT cookie    |
| POST   | `/api/auth/logout`           | Clear authentication cookie                            |
| GET    | `/api/auth/me`               | Retrieve currently authenticated user profile          |
| POST   | `/api/documents/upload`      | Upload a document (`.pdf`, `.docx`, `.txt`, images)    |
| GET    | `/api/documents`             | List all uploaded documents for current user           |
| GET    | `/api/documents/{id}/download` | Download original uploaded document file             |
| DELETE | `/api/documents/{id}`        | Delete document, local storage file, and search chunks |
| POST   | `/api/rag/ask`               | Ask a question and receive AI answer with cited sources|

---

## Deployment

* **Backend**: Dockerized ASP.NET Core API deployed on [Render](https://render.com).
  * Configure environment variables in Render: `Llm__ApiKey`, `Embedding__ApiKey`, `Jwt__Key`.
* **Frontend**: Angular 17 SPA deployed on [Vercel](https://vercel.com).
  * `vercel.json` provides client-side route rewrites for seamless page refreshes.

---

## Key Implementation Highlights

* **End-to-End RAG Pipeline**: Combines sliding-window chunking, vector embeddings, cosine-similarity ranking, hybrid keyword boosting, and LLM inference.
* **Zero-Trust Client Authentication**: Stores JWT tokens exclusively in `HttpOnly`, `SameSite=None`, `Secure` cookies with dynamic server-side origin validation to mitigate XSS and CSRF.
* **Cross-Format Parsing**: Unified extraction service seamlessly routing between `PdfPig`, `OpenXML`, `Tesseract OCR`, and UTF-8 readers.
* **Reliable Fallback Chains**: Graceful degradations across both embeddings (Hugging Face $\rightarrow$ Ollama $\rightarrow$ Hashed vector) and generative LLMs (Groq $\rightarrow$ Ollama $\rightarrow$ Heuristic extractor).

---

## Author

**Sanjay B**  
GitHub: [https://github.com/sanjaykb1998](https://github.com/sanjaykb1998)
