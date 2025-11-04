import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import './Example5.css';
import { uploadDocuments, searchDocuments, browseDocuments, getDocument, getDocumentContentUrl, deleteDocument, type SearchResultItem, type BrowseResultItem } from '../api/example5';

interface DocMeta {
  id: string;
  betterName: string;
  docType: string;
  mimeType: string;
}

type ViewMode = 'search' | 'browse';

export default function Example5() {
  const [dragActive, setDragActive] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [uploadResults, setUploadResults] = useState<{ originalFileName: string; status: string; docId?: string; error?: string }[]>([]);
  const [viewMode, setViewMode] = useState<ViewMode>('search');
  const [query, setQuery] = useState('');
  const [searching, setSearching] = useState(false);
  const [results, setResults] = useState<(SearchResultItem | BrowseResultItem)[]>([]);
  const [currentPage, setCurrentPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [browsing, setBrowsing] = useState(false);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [selectedMeta, setSelectedMeta] = useState<any | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const onDrop = useCallback(async (e: React.DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    e.stopPropagation();
    setDragActive(false);
    const files = Array.from(e.dataTransfer.files || []);
    if (!files.length) return;
    await handleUpload(files);
  }, []);

  const handleUpload = async (files: File[]) => {
    setUploading(true);
    try {
      const res = await uploadDocuments(files);
      setUploadResults(res);
    } catch (e: any) {
      setUploadResults(files.map(f => ({ originalFileName: f.name, status: 'Failed', error: e?.message })));
    } finally {
      setUploading(false);
    }
  };

  const handleSearch = async () => {
    if (!query.trim()) return;
    setSearching(true);
    try {
      const res = await searchDocuments({ query, topK: 50 });
      setResults(res.results);
    } finally {
      setSearching(false);
    }
  };

  const handleBrowse = useCallback(async (page: number = 1) => {
    setBrowsing(true);
    try {
      const res = await browseDocuments(page, 50);
      setResults(res.results);
      setCurrentPage(res.pagination.page);
      setTotalPages(res.pagination.totalPages);
      setTotalCount(res.pagination.totalCount);
    } finally {
      setBrowsing(false);
    }
  }, []);

  const handleDelete = useCallback(async (id: string, e: React.MouseEvent) => {
    e.stopPropagation(); // Prevent opening the modal
    if (!confirm('Are you sure you want to delete this document?')) {
      return;
    }
    
    try {
      await deleteDocument(id);
      // Refresh browse view
      if (viewMode === 'browse') {
        await handleBrowse(currentPage);
      } else {
        // If in search mode, remove from results using functional update
        setResults(prev => prev.filter(item => item.docId !== id));
      }
    } catch (e: any) {
      alert(`Failed to delete document: ${e?.message}`);
    }
  }, [viewMode, currentPage, handleBrowse]);

  useEffect(() => {
    if (viewMode === 'browse') {
      handleBrowse(1);
    } else {
      setResults([]);
      setCurrentPage(1);
      setTotalPages(1);
      setTotalCount(0);
    }
  }, [viewMode, handleBrowse]);

  const openResult = async (id: string) => {
    setSelectedId(id);
    try {
      const meta = await getDocument(id);
      setSelectedMeta(meta);
    } catch (e) {
      setSelectedMeta(null);
    }
  };

  const previewUrl = useMemo(() => (selectedId ? getDocumentContentUrl(selectedId) : ''), [selectedId]);

  return (
    <div className="example5-container">
      <h1>Example #5: Semantic Search</h1>
      <p className="description">Upload documents (images or PDFs), then search semantically across them.</p>

      <div
        className={`upload-dropzone ${dragActive ? 'active' : ''}`}
        onDragOver={(e) => { e.preventDefault(); e.stopPropagation(); setDragActive(true); }}
        onDragLeave={(e) => { e.preventDefault(); e.stopPropagation(); setDragActive(false); }}
        onDrop={onDrop}
      >
        <input ref={fileInputRef} type="file" accept="image/*,.pdf" multiple style={{ display: 'none' }} onChange={(e) => {
          const files = Array.from(e.target.files || []);
          if (files.length) handleUpload(files);
        }} />
        <p>Drag & drop files here, or</p>
        <button className="upload-button" onClick={() => fileInputRef.current?.click()} disabled={uploading}>
          {uploading ? 'Uploading...' : 'Select Files'}
        </button>
      </div>

      {uploadResults.length > 0 && (
        <div className="upload-results">
          <h3>Upload Results</h3>
          <ul>
            {uploadResults.map((r, i) => (
              <li key={i} className={r.status === 'Failed' ? 'failed' : r.status.toLowerCase()}>
                <span className="file-name">{r.originalFileName}</span>
                <span className="status">{r.status}</span>
                {r.error && <span className="error">{r.error}</span>}
              </li>
            ))}
          </ul>
        </div>
      )}

      <div className="view-toggle">
        <button
          className={`toggle-button ${viewMode === 'search' ? 'active' : ''}`}
          onClick={() => setViewMode('search')}
        >
          Search
        </button>
        <button
          className={`toggle-button ${viewMode === 'browse' ? 'active' : ''}`}
          onClick={() => setViewMode('browse')}
        >
          Browse
        </button>
      </div>

      {viewMode === 'search' && (
        <div className="search-bar">
          <input
            type="text"
            placeholder="Search documents (any language)…"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && handleSearch()}
          />
          <button onClick={handleSearch} disabled={searching || !query.trim()}>
            {searching ? 'Searching…' : 'Search'}
          </button>
        </div>
      )}

      {viewMode === 'browse' && (
        <div className="browse-info">
          <p>Showing {results.length} of {totalCount} documents</p>
        </div>
      )}

      {(viewMode === 'browse' && browsing) || (viewMode === 'search' && searching) ? (
        <div className="loading">Loading...</div>
      ) : results.length === 0 ? (
        <div className="no-results">
          {viewMode === 'search' ? 'No results found. Try a different search query.' : 'No documents found.'}
        </div>
      ) : (
        <>
          <div className="results-list">
            {results.map((item) => (
              <div key={item.docId} className="result-item" onClick={() => openResult(item.docId)}>
                <div className="thumb">
                  {item.mimeType === 'application/pdf' ? (
                    <div className="pdf-icon">PDF</div>
                  ) : (
                    <img src={getDocumentContentUrl(item.docId)} alt={item.betterName} />
                  )}
                </div>
                <div className="meta">
                  <div className="title">{item.betterName || item.docId}</div>
                  <div className="badges">
                    <span className="doctype">{item.docType}</span>
                    {'similarity' in item && (
                      <>
                        <span className="score">sim {item.similarity.toFixed(3)}</span>
                        <span className="score">rerank {item.rerank.toFixed(3)}</span>
                      </>
                    )}
                  </div>
                </div>
                {viewMode === 'browse' && (
                  <div className="actions">
                    <button
                      className="delete-button"
                      onClick={(e) => handleDelete(item.docId, e)}
                      title="Delete document"
                    >
                      Delete
                    </button>
                  </div>
                )}
              </div>
            ))}
          </div>
          {viewMode === 'browse' && totalPages > 1 && (
            <div className="pagination">
              <button
                onClick={() => handleBrowse(currentPage - 1)}
                disabled={currentPage === 1 || browsing}
              >
                Previous
              </button>
              <span className="page-info">
                Page {currentPage} of {totalPages}
              </span>
              <button
                onClick={() => handleBrowse(currentPage + 1)}
                disabled={currentPage === totalPages || browsing}
              >
                Next
              </button>
            </div>
          )}
        </>
      )}

      {selectedId && selectedMeta && (
        <div className="modal-overlay" onClick={() => setSelectedId(null)}>
          <div className="modal" onClick={(e) => e.stopPropagation()}>
            <button className="close" onClick={() => setSelectedId(null)}>×</button>
            <div className="modal-grid">
              <div className="left">
                {selectedMeta.mimeType === 'application/pdf' ? (
                  <iframe src={previewUrl} title="Document" className="doc-preview" />
                ) : (
                  <img src={previewUrl} alt={selectedMeta.betterName || 'Document'} className="doc-image" />
                )}
              </div>
              <div className="right">
                <h3>{selectedMeta.betterName || selectedMeta.originalFileName}</h3>
                <div className="json-view">
                  <pre>{selectedMeta.extractedDataJson || '{}'}
                  </pre>
                </div>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}


