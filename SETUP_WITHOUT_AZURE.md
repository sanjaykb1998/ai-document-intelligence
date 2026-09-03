# DocAI Setup Guide - Without Azure (Free Alternatives)

## Current Azure Dependencies ❌
Your project currently uses:
1. **Azure Blob Storage** - For document uploads
2. **Azure Form Recognizer (Document Intelligence)** - For OCR/text extraction
3. **Azure SQL Server** - For database

## Free Alternatives ✅
| Azure Service | Free Alternative | Why? |
|---|---|---|
| **Blob Storage** | Local FileSystem + Minio | Free, self-hosted |
| **Form Recognizer** | Tesseract OCR | Free, accurate OCR |
| **SQL Server** | SQLite (dev) / PostgreSQL (prod) | Free, no setup |

---

## Step 1: Replace Azure Blob Storage

### Option A: Local FileSystem (Simplest) ⭐

**File:** `backend/DocAI/DocAI.Api/Services/BlobService.cs` (REPLACE)

```csharp
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Local file system service replacing Azure Blob Storage
/// </summary>
public class BlobService
{
    private readonly string _uploadPath;
    private readonly IConfiguration _config;

    public BlobService(IConfiguration config)
    {
        _config = config;
        // Create uploads folder in project root
        _uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
        
        if (!Directory.Exists(_uploadPath))
        {
            Directory.CreateDirectory(_uploadPath);
        }
    }

    /// <summary>
    /// Upload file to local file system
    /// Returns: file URL for serving locally
    /// </summary>
    public async Task<string> UploadAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("File is empty");

        // Create user-specific folder
        var fileName = $"{Guid.NewGuid()}_{file.FileName}";
        var filePath = Path.Combine(_uploadPath, fileName);

        // Save file to disk
        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        // Return URL (this will be served by your API)
        return $"/api/documents/download/{fileName}";
    }

    /// <summary>
    /// Download file from local storage
    /// </summary>
    public async Task<(Stream, string)> DownloadAsync(string fileName)
    {
        var filePath = Path.Combine(_uploadPath, fileName);

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {fileName}");

        var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        return (stream, fileName);
    }

    /// <summary>
    /// Delete file from local storage
    /// </summary>
    public Task DeleteAsync(string fileName)
    {
        var filePath = Path.Combine(_uploadPath, fileName);

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Get full file path for processing
    /// </summary>
    public string GetFilePath(string fileName)
    {
        return Path.Combine(_uploadPath, fileName);
    }
}
```

### Add Download Endpoint

**File:** `backend/DocAI/DocAI.Api/Controllers/DocumentsController.cs` (ADD)

```csharp
[HttpGet("download/{fileName}")]
public async Task<IActionResult> DownloadDocument(string fileName)
{
    try
    {
        var (stream, originalFileName) = await _blobService.DownloadAsync(fileName);
        return File(stream, "application/octet-stream", originalFileName);
    }
    catch (FileNotFoundException)
    {
        return NotFound("File not found");
    }
}
```

### Update appsettings.json

**File:** `backend/DocAI/DocAI.Api/appsettings.Development.json`

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=docai.db"  // SQLite for local dev
  }
  // REMOVE: AzureBlob, AzureAI sections
}
```

---

## Step 2: Replace Azure Form Recognizer with Tesseract OCR

### Install Tesseract

**Windows:**
```powershell
# Option 1: Using Chocolatey
choco install tesseract

# Option 2: Download installer
# https://github.com/UB-Mannheim/tesseract/wiki
```

**Mac:**
```bash
brew install tesseract
```

**Linux:**
```bash
sudo apt-get install tesseract-ocr
```

### Create New OCR Service

**File:** `backend/DocAI/DocAI.Api/Services/OcrService.cs` (NEW)

```csharp
using System;
using System.IO;
using System.Threading.Tasks;
using Tesseract;

/// <summary>
/// OCR Service using Tesseract (free alternative to Azure Form Recognizer)
/// </summary>
public class OcrService
{
    private readonly IConfiguration _config;

    public OcrService(IConfiguration config)
    {
        _config = config;
    }

    /// <summary>
    /// Extract text from document (PDF, images, etc.)
    /// </summary>
    public async Task<string> ExtractTextAsync(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");

        var extension = Path.GetExtension(filePath).ToLower();

        return extension switch
        {
            ".pdf" => await ExtractFromPdfAsync(filePath),
            ".jpg" or ".jpeg" or ".png" or ".bmp" or ".tiff" => await ExtractFromImageAsync(filePath),
            _ => throw new InvalidOperationException($"Unsupported file type: {extension}")
        };
    }

    private async Task<string> ExtractFromImageAsync(string imagePath)
    {
        return await Task.Run(() =>
        {
            using (var engine = new TesseractEngine(
                       Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata"),
                       "eng",
                       EngineMode.Default))
            {
                using (var img = Pix.LoadFromFile(imagePath))
                {
                    using (var page = engine.Process(img))
                    {
                        return page.GetText();
                    }
                }
            }
        });
    }

    private async Task<string> ExtractFromPdfAsync(string pdfPath)
    {
        // Install: dotnet add package PdfSharpCore
        // or: dotnet add package iTextSharp
        return await Task.Run(() =>
        {
            var text = "";
            using (var document = PdfDocument.Open(pdfPath))
            {
                foreach (var page in document.GetPages())
                {
                    text += page.Text + "\n";
                }
            }
            return text;
        });
    }
}
```

### Update DocumentProcessorService

**File:** `backend/DocAI/DocAI.Api/Services/DocumentProcessorService.cs` (REPLACE)

```csharp
public class DocumentProcessorService
{
    private readonly AppDbContext _context;
    private readonly BlobService _blobService;
    private readonly OcrService _ocrService;  // NEW: Replace AzureDocumentService
    private readonly ILogger<DocumentProcessorService> _logger;

    public DocumentProcessorService(
        AppDbContext context,
        BlobService blobService,
        OcrService ocrService,  // NEW
        ILogger<DocumentProcessorService> logger)
    {
        _context = context;
        _blobService = blobService;
        _ocrService = ocrService;  // NEW
        _logger = logger;
    }

    public async Task ProcessDocumentAsync(Guid documentId)
    {
        try
        {
            var document = await _context.Documents.FindAsync(documentId);
            if (document == null) return;

            document.Status = "Processing";
            await _context.SaveChangesAsync();

            // NEW: Use Tesseract OCR instead of Azure
            var filePath = _blobService.GetFilePath(Path.GetFileName(document.FilePath));
            var extractedText = await _ocrService.ExtractTextAsync(filePath);

            document.ExtractedText = extractedText;
            document.Status = "Processed";
            document.ProcessedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            _logger.LogInformation($"Document {documentId} processed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error processing document {documentId}: {ex.Message}");
            var document = await _context.Documents.FindAsync(documentId);
            if (document != null)
            {
                document.Status = "Failed";
                document.ErrorMessage = ex.Message;
                await _context.SaveChangesAsync();
            }
        }
    }
}
```

### Update Program.cs

**File:** `backend/DocAI/DocAI.Api/Program.cs` (UPDATE)

```csharp
// REMOVE these lines:
// builder.Services.AddScoped<AzureDocumentService>();

// ADD this line:
builder.Services.AddScoped<OcrService>();

// Keep existing:
builder.Services.AddScoped<BlobService>();
builder.Services.AddScoped<DocumentProcessorService>();
```

---

## Step 3: Replace SQL Server with SQLite (Local) or PostgreSQL (Production)

### Option A: SQLite (Development - Easiest) ⭐

**Install:**
```bash
dotnet add package Microsoft.EntityFrameworkCore.Sqlite
```

**File:** `backend/DocAI/DocAI.Api/appsettings.Development.json`

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=docai.db;Cache=Shared"
  }
}
```

**File:** `backend/DocAI/DocAI.Api/Program.cs` (UPDATE)

```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
{
    // Automatically switch based on environment
    if (builder.Environment.IsDevelopment())
    {
        options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"));
    }
    else
    {
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
    }
});
```

**Initialize Database:**
```bash
cd backend/DocAI/DocAI.Api
dotnet ef database update
```

### Option B: PostgreSQL (Production - Free Tier Available)

**Install:**
```bash
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
```

**Sign up for free:**
- **Supabase:** https://supabase.com (500MB free)
- **Railway:** https://railway.app (free tier)
- **Render:** https://render.com (free tier)

**Connection String Example (Supabase):**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=db.xxxxx.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=your_password;SSL Mode=Require;"
  }
}
```

**Update Program.cs:**
```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
);
```

---

## Step 4: Update Frontend to Work Without Azure

### Remove Azure Dependencies

**File:** `frontend/doc-ai-ui/src/app/services/document.service.ts`

```typescript
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class DocumentService {
  private apiUrl = 'http://localhost:5000/api/documents';

  constructor(private http: HttpClient) {}

  uploadDocument(file: File): Observable<any> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post(`${this.apiUrl}/upload`, formData);
  }

  getDocuments(): Observable<any[]> {
    return this.http.get<any[]>(this.apiUrl);
  }

  downloadDocument(fileName: string): Observable<Blob> {
    return this.http.get(`${this.apiUrl}/download/${fileName}`, {
      responseType: 'blob'
    });
  }
}
```

---

## Step 5: Setup Project Structure

```
DocAI/
├── backend/
│   └── DocAI/
│       └── DocAI.Api/
│           ├── uploads/              (NEW - Local storage)
│           ├── tessdata/             (NEW - OCR language files)
│           ├── Services/
│           │   ├── BlobService.cs    (UPDATED - local filesystem)
│           │   ├── OcrService.cs     (NEW - Tesseract)
│           │   ├── DocumentProcessorService.cs  (UPDATED)
│           │   └── [keep others]
│           ├── appsettings.Development.json  (UPDATED - SQLite)
│           ├── Program.cs            (UPDATED - no Azure)
│           ├── docai.db              (Generated on first run)
│           └── [keep other files]
└── frontend/
    └── doc-ai-ui/
        ├── src/app/services/
        │   └── document.service.ts   (UPDATED - no Azure)
        └── [keep other files]
```

---

## Step 6: Install NuGet Packages

```bash
cd backend/DocAI/DocAI.Api

# OCR
dotnet add package Tesseract

# PDF Processing
dotnet add package PdfSharpCore

# Database
dotnet add package Microsoft.EntityFrameworkCore.Sqlite

# Keep existing
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
```

---

## Step 7: Run Locally

### Backend
```bash
cd backend/DocAI/DocAI.Api

# Create database
dotnet ef database update

# Run
dotnet run
# API runs at: http://localhost:5000
```

### Frontend
```bash
cd frontend/doc-ai-ui

npm install
ng serve
# Frontend runs at: http://localhost:4200
```

---

## Complete Setup Checklist

### Backend Setup:
- [ ] Remove all Azure NuGet packages
- [ ] Install Tesseract OCR locally
- [ ] Replace BlobService.cs with local filesystem version
- [ ] Create OcrService.cs with Tesseract
- [ ] Update DocumentProcessorService.cs to use OcrService
- [ ] Update Program.cs to remove Azure services
- [ ] Install SQLite NuGet package
- [ ] Update appsettings.Development.json with SQLite connection
- [ ] Run `dotnet ef database update`
- [ ] Run `dotnet run` and verify API starts

### Frontend Setup:
- [ ] Update DocumentService to use local API URLs
- [ ] Remove any Azure authentication code (if any)
- [ ] Run `npm install`
- [ ] Run `ng serve`
- [ ] Verify UI loads at http://localhost:4200

### Testing:
- [ ] Upload a document through UI
- [ ] Verify file saved to `/uploads` folder
- [ ] Verify OCR extracted text
- [ ] Verify database saved metadata
- [ ] Search documents
- [ ] Download document

---

## Troubleshooting

### Tesseract Not Found
```bash
# Verify installation
which tesseract  # Mac/Linux
where tesseract  # Windows

# If not found, add to PATH
# Windows: C:\Program Files\Tesseract-OCR
# Mac: /usr/local/bin/tesseract
```

### Database Connection Issues
```bash
# Check if docai.db was created
ls backend/DocAI/DocAI.Api/docai.db

# If issues, delete and recreate
rm docai.db
dotnet ef database update
```

### CORS Issues
```bash
# Update Program.cs to allow localhost:4200
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular",
        policy =>
        {
            policy.WithOrigins("http://localhost:4200")
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});

app.UseCors("AllowAngular");
```

### File Upload Path Issues
```bash
# Uploads folder should be created in:
D:\DocAI\backend\DocAI\DocAI.Api\uploads\

# If not created, create manually:
mkdir backend\DocAI\DocAI.Api\uploads
```

---

## Environment Configuration

### appsettings.Development.json
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=docai.db;Cache=Shared"
  },
  "Jwt": {
    "Key": "your-secret-key-change-this-in-production",
    "Issuer": "DocAI",
    "Audience": "DocAI-Users",
    "ExpirationMinutes": 60
  }
}
```

### .gitignore Updates
```gitignore
# Add these
docai.db
docai.db-shm
docai.db-wal
uploads/
tessdata/
```

---

## What You Get Now ✅

| Feature | Before (Azure) | After (Free) |
|---------|---|---|
| **File Storage** | Azure Blob ($) | Local FileSystem + Minio (Free) |
| **OCR** | Azure Form Recognizer ($) | Tesseract (Free) |
| **Database** | SQL Server ($) | SQLite/PostgreSQL (Free) |
| **Authentication** | Azure AD (Paid) | JWT (Already have) |
| **Cost** | $15-30/month | $0 |
| **Local Dev** | Limited | Full offline support |

---

## Next Steps

After this setup is complete and working:
1. ✅ Verify all functionality works locally
2. ✅ Test document upload → OCR → search
3. ✅ Commit changes to GitHub
4. ✅ Then proceed to **Phase 1: Semantic Search** in ENHANCEMENT_PLAN.md

---

**Status:** Ready to setup  
**Effort:** 2-3 hours  
**Result:** Fully working project without Azure costs  

Let me know when you get stuck on any step! 🚀
