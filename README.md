# AI Document Intelligence System

A full-stack cloud application that allows users to upload documents, extract text using Azure AI, and search documents through an intelligent dashboard.

## Overview

AI Document Intelligence System enables:

* Uploading documents to Azure Blob Storage
* Automatic text extraction using Azure AI Document Intelligence (Form Recognizer)
* Storing metadata and extracted text in SQL Server
* Searching and viewing processed documents from an Angular dashboard

This project demonstrates end-to-end cloud integration with asynchronous processing and scalable architecture.

---

## Tech Stack

### Backend

* ASP.NET Core Web API (.NET 8)
* Entity Framework Core
* SQL Server
* Azure Blob Storage
* Azure AI Document Intelligence
* Background processing using `Task.Run` and scoped services

### Frontend

* Angular (Standalone Components)
* RxJS (Observables, BehaviorSubject)
* Search & filter dashboard
* Responsive UI

---

## Features

### Document Management

* Upload documents (PDF, images, supported formats)
* Store files securely in Azure Blob Storage
* Save document metadata in SQL Server

### AI Processing

* Automatic text extraction using Azure AI
* Background processing after upload
* Status tracking:

  * Uploaded
  * Processing
  * Processed
  * Failed

### Search Dashboard

* View all uploaded documents
* Search by:

  * File name
  * Extracted text
* Open document directly from Blob URL
* View extracted content

---

## Architecture

User Upload → ASP.NET API → Azure Blob Storage
→ Save metadata → Background Processor
→ Azure AI Document Intelligence
→ Extract text → Save to SQL → Angular UI

---

## Project Structure

```
DocAI
│
├── backend
│   └── DocAI.Api
│       ├── Controllers
│       ├── Services
│       ├── Data
│       └── Models
│
├── frontend
│   └── doc-ai-ui
│       ├── components
│       ├── services
│       └── models
```

---

## Setup Instructions

### 1. Clone repository

```
git clone https://github.com/sanjaykb1998/ai-document-intelligence.git
```

---

### 2. Backend Setup

Update **appsettings.json**:

```
ConnectionStrings:DefaultConnection
AzureBlob:ConnectionString
AzureBlob:ContainerName
AzureAI:Endpoint
AzureAI:Key
```

Run migrations / database update.

Run API:

```
dotnet run
```

---

### 3. Frontend Setup

Navigate to frontend:

```
cd frontend/doc-ai-ui
npm install
ng serve
```

Open:

```
http://localhost:4200
```

---

## API Endpoints

| Method | Endpoint              | Description       |
| ------ | --------------------- | ----------------- |
| POST   | /api/Documents/upload | Upload document   |
| GET    | /api/Documents        | Get all documents |

---

## Key Implementation Highlights

* Asynchronous background processing using scoped services
* Azure cloud integration (Storage + AI)
* Reactive UI using RxJS
* Search over AI-extracted content
* Clean layered architecture (Controller → Service → Data)

---

## Future Enhancements

* Azure Queue / Background Worker
* Azure Cognitive Search integration
* Document summarization using Azure OpenAI
* Authentication (Azure AD / JWT)
* Pagination & advanced filters

---

## Author

**Sanjay B**
GitHub: https://github.com/sanjaykb1998
