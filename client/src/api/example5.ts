const API_BASE = 'http://localhost:5064';

export interface UploadResult {
  docId?: string;
  originalFileName: string;
  status: string;
  error?: string;
}

export interface SearchRequest {
  query: string;
  topK?: number;
}

export interface SearchResultItem {
  docId: string;
  betterName: string;
  docType: string;
  similarity: number;
  rerank: number;
  previewUrl: string;
  mimeType: string;
}

export interface SearchResponse {
  results: SearchResultItem[];
}

export interface BrowseResultItem {
  docId: string;
  betterName: string;
  docType: string;
  mimeType: string;
  previewUrl: string;
  createdAt: string;
}

export interface BrowseResponse {
  results: BrowseResultItem[];
  pagination: {
    page: number;
    pageSize: number;
    totalCount: number;
    totalPages: number;
  };
}

export async function uploadDocuments(files: File[]): Promise<UploadResult[]> {
  const form = new FormData();
  files.forEach(f => form.append('files', f));
  const res = await fetch(`${API_BASE}/api/Example5/upload-documents`, {
    method: 'POST',
    body: form,
  });
  if (!res.ok) throw new Error(`Upload failed: ${res.status}`);
  const data = await res.json();
  return data.documents as UploadResult[];
}

export async function searchDocuments(req: SearchRequest): Promise<SearchResponse> {
  const res = await fetch(`${API_BASE}/api/Example5/search`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(req),
  });
  if (!res.ok) throw new Error(`Search failed: ${res.status}`);
  return res.json();
}

export async function browseDocuments(page: number = 1, pageSize: number = 50): Promise<BrowseResponse> {
  const res = await fetch(`${API_BASE}/api/Example5/browse?page=${page}&pageSize=${pageSize}`);
  if (!res.ok) throw new Error(`Browse failed: ${res.status}`);
  return res.json();
}

export async function getDocument(id: string) {
  const res = await fetch(`${API_BASE}/api/Example5/documents/${id}`);
  if (!res.ok) throw new Error(`Get document failed: ${res.status}`);
  return res.json();
}

export function getDocumentContentUrl(id: string) {
  return `${API_BASE}/api/Example5/documents/${id}/content`;
}

export async function deleteDocument(id: string): Promise<void> {
  const res = await fetch(`${API_BASE}/api/Example5/documents/${id}`, {
    method: 'DELETE',
  });
  if (!res.ok) throw new Error(`Delete failed: ${res.status}`);
}


