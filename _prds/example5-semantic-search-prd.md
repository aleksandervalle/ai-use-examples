 # Example 5: Semantic Search — PRD
 
 Version: 1.0  
 Owner: Engineering  
 Status: Draft  
 Target Stack: React 19 + TypeScript (Vite) / ASP.NET Core 8 (C#) / SQLite / ChromaDB / Google Gemini
 
 ## 1) Summary
 Build a new example that demonstrates end-to-end semantic search across user-uploaded documents (images and PDFs). The flow includes document ingestion, LLM-based classification and renaming, structured data extraction, embedding generation, vector indexing in a local ChromaDB, and a search experience with LLM-powered query expansion and reranking.
 
 ## 2) Goals & Non‑Goals
 - Goals:
   - Enable multi-file drag-and-drop upload in the UI (images + PDFs).
   - Store files locally on disk (configurable root folder) and metadata in SQLite.
   - Classify each document and generate a better filename (LLM), then extract structured data (LLM).
   - Create Gemini embeddings from a canonical text (better name + doc type + extracted data JSON) and index in local ChromaDB.
   - Provide a search input that performs LLM query expansion + translation to English, vector search (top 50), and LLM reranking.
   - Display results with preview/icon, generated name, doc type, similarity score, and rerank score. Full-screen modal shows document and extracted data side-by-side (like Example 4).
 - Non‑Goals:
   - No cloud storage or managed vector DB; local-only.
   - No authentication/authorization.
   - No OCR/PDF text extraction beyond Gemini’s vision capabilities for images/PDFs (future).
 
 ## 3) User Stories
 - As a user, I can drag-and-drop multiple documents and see them process to searchable status.
 - As a user, I can search in any language; the system translates/expands my query.
 - As a user, I get relevant results ordered by meaning, with clear scores and quick preview.
 - As a user, I can open a result to view the original file and the extracted data together.
 
 ## 4) UX / UI Requirements
 - New page: Example 5 — Semantic Search (`/example5`).
 - Upload Area:
   - Drag-and-drop multiple files, plus “Select files” button.
   - Accept: .pdf, .jpg, .jpeg, .png, .gif, .webp.
   - Show per-file progress/status: Pending → Processing → Completed/Failed.
 - Search:
   - Single input with placeholder: “Search documents (any language)…”.
   - Optional filter: Doc Type (Invoice, Receipt, Flight Ticket, Order Confirmation, Other) — optional for MVP.
   - On submit: show loading state; after results: show count and latency badges.
 - Results List:
   - Each item: preview (thumbnail if image; PDF icon otherwise), generated name, doc type, similarity score (vector), rerank score (LLM).
   - Click item → full-screen modal: left panel document, right panel extracted data JSON (formatted) — reuse layout from Example 4.
 - Error/Empty States:
   - Friendly messages for no results, processing, and failures.
 
 ## 5) Functional Requirements
 ### 5.1 Ingestion Pipeline
 1) Upload
 - Endpoint accepts multiple files (multipart/form-data).
 - For each file:
   - Generate `DocumentId` (GUID).
   - Persist to disk using a safe temporary name in the configured folder.
   - Insert SQLite row with initial metadata and `ProcessingStatus=Pending`.
 
 2) Classification & Better Name (LLM)
 - Use Gemini (same completion model as Example 4, `gemini-2.5-flash-lite`, temperature 0.0).
 - Classify into one of: "Invoice", "Receipt", "Flight Ticket", "Order Confirmation", "Other".
 - Generate a human-friendly English filename stem (no extension), safe for filesystem.
 - Update SQLite with `DocType`, `BetterName`, `ClassificationConfidence`.
 - Rename file on disk to `{DocType}-{yyyyMMdd}-{slug}-{shortId}.{ext}` (slug from BetterName).
 
 3) Structured Data Extraction (LLM)
 - Use doc-type-specific prompts like Example 4; return JSON (no markdown).
 - Store raw JSON string in SQLite `ExtractedDataJson`.
 
 4) Embedding & Indexing
 - Build canonical embedding input (English):
   - `BetterName + "\nDocType: " + DocType + "\nExtractedData: " + ExtractedDataJson`
 - Call Gemini Embeddings model `text-embedding-004` (or latest available) to get vector.
 - Upsert into local ChromaDB collection `documents` with:
   - id: `DocumentId` (GUID)
   - vector: embedding
   - metadata: `{ docType, betterName, filePath, mimeType, fileSize, createdAt }`
 - Mark `EmbeddedAt` in SQLite and set `ProcessingStatus=Completed`.
 
 ### 5.2 Search Pipeline
 1) Query Expansion (LLM)
 - Endpoint receives `{ query: string, topK?: number=50, docType?: string|null }`.
 - Call Gemini to produce structured JSON (no markdown):
 ```json
 {
   "language": "<detected language>",
   "englishQuery": "<translated to English>",
   "expandedEnglishQuery": "<expanded/phrased variations>",
   "docType": "Invoice|Receipt|Flight Ticket|Order Confirmation|Other|null"
 }
 ```
 - If user provided `docType`, prefer that; otherwise use inferred `docType`.
 
 2) Vector Search (Chroma)
 - Embed `expandedEnglishQuery` with `text-embedding-004`.
 - Query ChromaDB `documents` collection for topK (default 50).
 - Return candidate list with similarity scores.
 
 3) LLM Reranking
 - For each candidate (in parallel with bounded concurrency), call Gemini to return a single score `[0.0..1.0]` given:
   - Original user query (raw)
   - Candidate’s `ExtractedDataJson` from SQLite
 - Response schema per candidate:
 ```json
 { "docId": "<GUID>", "relevancy": 0.0 }
 ```
 - If multiple top candidates are tied (e.g., >=2 with `relevancy >= 0.99`), run a tie-break LLM call with the query and the tied candidates’ `{ docId, ExtractedDataJson }` to get an ordered list of `docId`s.
 
 4) Response to Client
 - Return ordered results with:
 ```json
 {
   "results": [
     {
       "docId": "<GUID>",
       "betterName": "<string>",
       "docType": "<string>",
       "similarity": <number>,
       "rerank": <number>,
       "previewUrl": "/api/Example5/documents/<id>/content",
       "mimeType": "<string>"
     }
   ]
 }
 ```
 
 ## 6) Data Model (SQLite)
 Table: `Documents`
 - `Id` TEXT (GUID, PK)
 - `OriginalFileName` TEXT
 - `StoredFileName` TEXT
 - `FilePath` TEXT
 - `MimeType` TEXT
 - `FileSize` INTEGER
 - `DocType` TEXT CHECK (DocType IN ('Invoice','Receipt','Flight Ticket','Order Confirmation','Other'))
 - `ClassificationConfidence` REAL
 - `BetterName` TEXT
 - `ExtractedDataJson` TEXT
 - `ProcessingStatus` TEXT CHECK (ProcessingStatus IN ('Pending','Processing','Completed','Failed'))
 - `ErrorMessage` TEXT NULL
 - `EmbeddedAt` TEXT NULL (ISO timestamp)
 - `CreatedAt` TEXT
 - `UpdatedAt` TEXT
 
 Indexes:
 - `IX_Documents_DocType`
 - `IX_Documents_ProcessingStatus`
 
 ## 7) API Design (Server)
 - `POST /api/Example5/upload-documents` (multipart/form-data)
   - Form field: `files[]`
   - Response: `{ documents: [{ docId, originalFileName, status }] }`
 - `GET /api/Example5/documents/{id}`
   - Returns document metadata + extracted data.
 - `GET /api/Example5/documents/{id}/content`
   - Streams the original file.
 - `POST /api/Example5/search`
   - Body: `{ query: string, topK?: number, docType?: string|null }`
   - Response: search results array (see 5.2.4).
 
 Notes:
 - Processing may happen synchronously after upload for MVP. If latency is high, consider background queued jobs.
 
 ## 8) Configuration (appsettings.json)
 ```json
 {
   "DocumentStorage": {
     "RootPath": "C:\\dev\\ai-use-examples\\data\\documents"
   },
   "ChromaDb": {
     "Endpoint": "http://localhost:8000",
     "Collection": "documents"
   },
   "Gemini": {
     "ApiKey": "<your-key>",
     "CompletionModel": "gemini-2.5-flash-lite",
     "EmbeddingModel": "text-embedding-004",
     "Temperature": 0.0
   },
   "Limits": {
     "MaxUploadSizeMb": 20,
     "MaxFilesPerBatch": 20,
     "RerankConcurrency": 10
   }
 }
 ```
 
 ## 9) Implementation Plan (High Level)
 - Server
   - Add `Example5Controller.cs` with endpoints.
   - Services:
     - `DocumentIngestionService` (persist + pipeline orchestration)
     - `GeminiApiService` (reuse) — add embeddings method
     - `ExtractionService` (doc-type prompts + parsing)
     - `ChromaDbService` (index/search)
     - `SearchService` (query expansion, vector search, rerank, tie-break)
   - Data Access:
     - `DocumentsRepository` (SQLite)
   - Utilities:
     - Slugify filenames; MIME detection; image thumbnail generation (optional).
 - Client
   - Add `pages/Example5.tsx` and `Example5.css`.
   - Components: Upload dropzone, ResultsList, ResultItem, FullscreenModal (reuse Example 4 layout).
 
 ## 10) Prompts (Specifications)
 - Classification + Better Name (no markdown):
   - Input: image/PDF; Instruction: classify into one of the allowed doc types; propose concise English filename stem; output JSON `{ docType, betterName, confidence }`.
 - Extraction (doc-type aware):
   - Separate schemas per type, e.g., Invoice: invoiceNumber, dates, vendor, customer, lineItems[], totals, currency, orgNumbers, etc. Output JSON only.
 - Query Expansion (no markdown):
 ```json
 {
   "language": "<auto-detected>",
   "englishQuery": "<translation to English>",
   "expandedEnglishQuery": "<short expanded variant>",
   "docType": "Invoice|Receipt|Flight Ticket|Order Confirmation|Other|null"
 }
 ```
 - Rerank (single score): Given user query (original) and `ExtractedDataJson`, return JSON `{ "docId": "<GUID>", "relevancy": <0..1> }` only.
 
 ## 11) Performance & Costs
 - Parallel reranking up to `Limits.RerankConcurrency`.
 - TopK default 50.
 - Embedding model `text-embedding-004` (~3072 dims) — plan for memory.
 - Log timing for ingestion steps and search stages.
 
 ## 12) Validation & Acceptance Criteria
 - Upload 5 mixed documents; all reach `Completed` with `DocType`, `BetterName`, `ExtractedDataJson`, and `EmbeddedAt` set.
 - Search in Norwegian for a receipt; top result is the receipt with `rerank >= 0.9`.
 - Query expansion always returns English fields as specified.
 - Results include both scores; ordering respects rerank with tie-breaking when needed.
 - Full-screen modal matches Example 4’s split view.
 
 ## 13) Risks & Mitigations
 - High latency from parallel LLM calls → cap concurrency; show UI spinners and partial results if needed (future).
 - Inconsistent class labels → hard constraints in prompts; validate against known set; fallback to "Other".
 - File system collisions/unsafe names → slug + GUID suffix; sanitize.
 - Chroma service not running → detect on startup, show actionable error.
 
 ## 14) Open Questions
 - Should we generate thumbnails for PDFs for previews? (Out of scope MVP.)
 - Should we chunk long PDFs for better recall later? (Future.)
 - Do we need per-user isolation? (Out of scope.)
 
 ## 15) Future Enhancements
 - PDF text extraction + chunked embeddings.
 - Filters for date ranges, amounts, vendors.
 - Evals dashboard for search quality.
 - Batch uploads with background jobs and web sockets for live status.
 

