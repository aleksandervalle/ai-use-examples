import { useState, useRef, useEffect } from 'react';
import './Example4.css';
import ShimmerLoader from '../components/ShimmerLoader';

interface ExtractedData {
  [key: string]: any;
}

function formatCurrency(amount: number, currency?: string): string {
  if (!currency) {
    return `$${amount.toFixed(2)}`;
  }
  
  // Try to format with Intl.NumberFormat if currency code is valid
  try {
    return new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency: currency.toUpperCase(),
    }).format(amount);
  } catch (e) {
    // Fallback to currency code + amount
    return `${currency.toUpperCase()} ${amount.toFixed(2)}`;
  }
}

export default function Example4() {
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [filePreview, setFilePreview] = useState<string | null>(null);
  const [classificationResult, setClassificationResult] = useState<string>('');
  const [filenameResult, setFilenameResult] = useState<string>('');
  const [extractionResult, setExtractionResult] = useState<string>('');
  const [isProcessing, setIsProcessing] = useState(false);
  const [currentPhase, setCurrentPhase] = useState<'idle' | 'classifying' | 'extracting'>('idle');
  const [showComparisonModal, setShowComparisonModal] = useState(false);
  const [documentType, setDocumentType] = useState<string>('');
  const fileInputRef = useRef<HTMLInputElement>(null);

  const handleFileSelect = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) {
      setSelectedFile(file);
      setClassificationResult('');
      setFilenameResult('');
      setExtractionResult('');
      setDocumentType('');
      setCurrentPhase('idle');

      // Create preview
      const reader = new FileReader();
      reader.onloadend = () => {
        setFilePreview(reader.result as string);
      };
      reader.readAsDataURL(file);
    }
  };

  const handleUploadClick = () => {
    fileInputRef.current?.click();
  };

  const handleProcess = async () => {
    if (!selectedFile || isProcessing) return;

    setIsProcessing(true);
    setClassificationResult('');
    setFilenameResult('');
    setExtractionResult('');
    setDocumentType('');
    setCurrentPhase('classifying');

    try {
      const formData = new FormData();
      formData.append('file', selectedFile);

      const response = await fetch(`http://localhost:5064/api/Example4/process-document`, {
        method: 'POST',
        body: formData,
      });

      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }

      const reader = response.body?.getReader();
      const decoder = new TextDecoder();

      if (!reader) {
        throw new Error('No response body');
      }

      let accumulatedText = '';
      let classificationComplete = false;
      let filenameComplete = false;

      while (true) {
        const { done, value } = await reader.read();

        if (done) break;

        const chunk = decoder.decode(value, { stream: true });
        accumulatedText += chunk;

        // Check for classification delimiter
        if (!classificationComplete && accumulatedText.includes('CLASSIFICATION_RESULT:')) {
          const delimiterIndex = accumulatedText.indexOf('CLASSIFICATION_RESULT:');
          const afterDelimiter = delimiterIndex + 'CLASSIFICATION_RESULT:'.length;
          const afterNewlines = accumulatedText.indexOf('\n\n', afterDelimiter);

          if (afterNewlines !== -1) {
            // Extract classification (everything between CLASSIFICATION_RESULT: and \n\n)
            const classification = accumulatedText.substring(afterDelimiter, afterNewlines).trim();
            setClassificationResult(classification);
            classificationComplete = true;

            // Parse document type from classification
            try {
              const classificationJson = JSON.parse(classification);
              setDocumentType(classificationJson.type || '');
            } catch (e) {
              // Ignore parse errors
            }
          }
        }

        // Check for filename delimiter
        if (!filenameComplete && accumulatedText.includes('FILENAME_RESULT:')) {
          const delimiterIndex = accumulatedText.indexOf('FILENAME_RESULT:');
          const afterDelimiter = delimiterIndex + 'FILENAME_RESULT:'.length;
          const afterNewlines = accumulatedText.indexOf('\n\n', afterDelimiter);

          if (afterNewlines !== -1) {
            // Extract filename (everything between FILENAME_RESULT: and \n\n)
            const filename = accumulatedText.substring(afterDelimiter, afterNewlines).trim();
            setFilenameResult(filename);
            filenameComplete = true;
          }
        }

        // Once both classification and filename are complete, start extraction phase
        if (classificationComplete && filenameComplete && currentPhase === 'classifying') {
          setCurrentPhase('extracting');
        }

        // Handle extraction text (everything after FILENAME_RESULT:\n\n)
        if (classificationComplete && filenameComplete) {
          const filenameDelimiterIndex = accumulatedText.indexOf('FILENAME_RESULT:');
          if (filenameDelimiterIndex !== -1) {
            const afterFilenameDelimiter = filenameDelimiterIndex + 'FILENAME_RESULT:'.length;
            const afterFilenameNewlines = accumulatedText.indexOf('\n\n', afterFilenameDelimiter);
            if (afterFilenameNewlines !== -1) {
              const extraction = accumulatedText.substring(afterFilenameNewlines + 2);
              setExtractionResult(extraction);
            }
          }
        } else if (!classificationComplete && !accumulatedText.includes('CLASSIFICATION_RESULT:')) {
          // Still waiting for classification
          setClassificationResult(accumulatedText);
        }
      }
    } catch (error) {
      console.error('Error processing document:', error);
      setClassificationResult(`Error: ${error instanceof Error ? error.message : 'Unknown error'}`);
    } finally {
      setIsProcessing(false);
      setCurrentPhase('idle');
    }
  };

  const parseExtractionResult = (): ExtractedData | null => {
    if (!extractionResult) return null;
    try {
      return JSON.parse(extractionResult);
    } catch (e) {
      return null;
    }
  };

  return (
    <div className="example4-container">
      <h1>Example #4: Image/Document Understanding</h1>
      <p className="description">
        Upload an image or document (invoice, receipt, flight ticket, order confirmation, or other image)
        to see how LLMs can understand and extract structured data from visual content.
      </p>

      <div className="upload-section">
        <div className="file-input-container">
          <input
            ref={fileInputRef}
            type="file"
            accept="image/*,.pdf"
            onChange={handleFileSelect}
            style={{ display: 'none' }}
          />
          <button
            onClick={handleUploadClick}
            className="upload-button"
            disabled={isProcessing}
          >
            Choose File
          </button>
          {selectedFile && (
            <div className="file-info">
              <span>{selectedFile.name}</span>
              <span className="file-size">({(selectedFile.size / 1024).toFixed(2)} KB)</span>
            </div>
          )}
        </div>

        {filePreview && (
          <div className="preview-section">
            <h3>Preview</h3>
            <div className="preview-container">
              {selectedFile?.type === 'application/pdf' || selectedFile?.name.toLowerCase().endsWith('.pdf') ? (
                <iframe
                  src={filePreview}
                  className="preview-pdf"
                  title="PDF Preview"
                />
              ) : (
                <img src={filePreview} alt="Preview" className="preview-image" />
              )}
            </div>
          </div>
        )}

        {selectedFile && (
          <button
            onClick={handleProcess}
            disabled={isProcessing}
            className="process-button"
          >
            {isProcessing ? 'Processing...' : 'Process Document'}
          </button>
        )}
      </div>

      <div className="results-section">
        <div className="left-column">
          <div className="result-box result-box-small">
            <h3>Classification Result</h3>
            <div className="result-content">
              {isProcessing && currentPhase === 'classifying' && !classificationResult ? (
                <ShimmerLoader />
              ) : classificationResult ? (
                <pre className="json-output">{classificationResult}</pre>
              ) : (
                <div className="result-placeholder">Classification result will appear here...</div>
              )}
            </div>
          </div>

          <div className="result-box result-box-small">
            <h3>Alternative Filename</h3>
            <div className="result-content">
              {isProcessing && currentPhase === 'classifying' && !filenameResult ? (
                <ShimmerLoader />
              ) : filenameResult ? (
                <pre className="json-output">{filenameResult}</pre>
              ) : (
                <div className="result-placeholder">Alternative filename will appear here...</div>
              )}
            </div>
          </div>
        </div>

        <div className="result-box">
          <h3>Extracted Data</h3>
          <div className="result-content">
            {isProcessing && (currentPhase === 'extracting' || (currentPhase === 'classifying' && classificationResult && filenameResult)) && !extractionResult ? (
              <ShimmerLoader />
            ) : extractionResult ? (
              <pre className="json-output">{extractionResult}</pre>
            ) : (
              <div className="result-placeholder">Extracted data will appear here...</div>
            )}
          </div>
        </div>
      </div>

      {extractionResult && !isProcessing && (
        <div className="comparison-button-container">
          <button
            onClick={() => setShowComparisonModal(true)}
            className="comparison-button"
          >
            Compare Side-by-Side
          </button>
        </div>
      )}

      {showComparisonModal && (
        <ComparisonModal
          filePreview={filePreview}
          extractedData={parseExtractionResult()}
          documentType={documentType}
          filenameResult={filenameResult}
          onClose={() => setShowComparisonModal(false)}
        />
      )}
    </div>
  );
}

function ComparisonModal({ filePreview, extractedData, documentType, filenameResult, onClose }: {
  filePreview: string | null;
  extractedData: ExtractedData | null;
  documentType: string;
  filenameResult: string;
  onClose: () => void;
}) {
  const [zoomLevel, setZoomLevel] = useState(1);
  const [imageNaturalSize, setImageNaturalSize] = useState<{ width: number; height: number } | null>(null);
  const [initialFitSize, setInitialFitSize] = useState<{ width: number; height: number } | null>(null);
  const imageRef = useRef<HTMLImageElement>(null);
  const containerRef = useRef<HTMLDivElement>(null);

  if (!filePreview || !extractedData) return null;

  // Parse alternative filename from filenameResult
  let alternativeFilename = '';
  try {
    if (filenameResult) {
      const filenameJson = JSON.parse(filenameResult);
      alternativeFilename = filenameJson.alternativeFilename || '';
    }
  } catch (e) {
    // Ignore parse errors
  }

  const handleZoomIn = () => {
    setZoomLevel(prev => Math.min(prev + 0.25, 3)); // Max 3x zoom
  };

  const handleZoomOut = () => {
    setZoomLevel(prev => Math.max(prev - 0.25, 0.5)); // Min 0.5x zoom
  };

  const handleImageLoad = () => {
    if (imageRef.current && containerRef.current) {
      const naturalWidth = imageRef.current.naturalWidth;
      const naturalHeight = imageRef.current.naturalHeight;
      const containerWidth = containerRef.current.clientWidth - 32; // Account for padding
      const containerHeight = containerRef.current.clientHeight - 32;
      
      // Calculate the size that fits the container
      const widthRatio = containerWidth / naturalWidth;
      const heightRatio = containerHeight / naturalHeight;
      const ratio = Math.min(widthRatio, heightRatio, 1); // Don't scale up beyond natural size
      
      setImageNaturalSize({
        width: naturalWidth,
        height: naturalHeight
      });
      
      setInitialFitSize({
        width: naturalWidth * ratio,
        height: naturalHeight * ratio
      });
    }
  };

  useEffect(() => {
    // Reset zoom when image changes
    setZoomLevel(1);
    setImageNaturalSize(null);
    setInitialFitSize(null);
  }, [filePreview]);

  const isImage = !filePreview.includes('data:application/pdf');

  const renderFormattedData = (data: ExtractedData, type: string) => {
    switch (type) {
      case 'invoice':
        return <InvoiceView data={data} />;
      case 'receipt':
        return <ReceiptView data={data} />;
      case 'flight_ticket':
        return <FlightTicketView data={data} />;
      case 'order_confirmation':
        return <OrderConfirmationView data={data} />;
      default:
        return <OtherImageView data={data} />;
    }
  };

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal-content" onClick={(e) => e.stopPropagation()}>
        <button className="modal-close" onClick={onClose}>×</button>
        <div className="modal-grid">
          <div className="modal-left">
            <h2>Document</h2>
            {alternativeFilename && (
              <div className="modal-filename">
                <label>Suggested Filename:</label>
                <span>{alternativeFilename}</span>
              </div>
            )}
            {isImage && (
              <div className="zoom-controls">
                <button onClick={handleZoomOut} className="zoom-button" disabled={zoomLevel <= 0.5}>
                  −
                </button>
                <span className="zoom-level">{Math.round(zoomLevel * 100)}%</span>
                <button onClick={handleZoomIn} className="zoom-button" disabled={zoomLevel >= 3}>
                  +
                </button>
              </div>
            )}
            <div className="modal-preview-container" ref={containerRef}>
              {filePreview.includes('data:application/pdf') ? (
                <iframe src={filePreview} className="modal-preview-pdf" title="PDF Preview" />
              ) : (
                <div 
                  className="modal-image-wrapper"
                  style={
                    initialFitSize
                      ? {
                          width: `${initialFitSize.width * zoomLevel}px`,
                          height: `${initialFitSize.height * zoomLevel}px`,
                          minWidth: `${initialFitSize.width * zoomLevel}px`,
                          minHeight: `${initialFitSize.height * zoomLevel}px`,
                        }
                      : undefined
                  }
                >
                  <img 
                    ref={imageRef}
                    src={filePreview} 
                    alt="Document" 
                    className="modal-preview-image"
                    onLoad={handleImageLoad}
                    style={
                      initialFitSize
                        ? {
                            width: `${initialFitSize.width * zoomLevel}px`,
                            height: `${initialFitSize.height * zoomLevel}px`,
                          }
                        : { maxWidth: '100%', maxHeight: '100%' }
                    }
                  />
                </div>
              )}
            </div>
          </div>
          <div className="modal-right">
            <h2>Extracted Data</h2>
            <div className="modal-formatted-content">
              {renderFormattedData(extractedData, documentType)}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

function InvoiceView({ data }: { data: ExtractedData }) {
  return (
    <div className="formatted-view invoice-view">
      <div className="formatted-header">
        <h3>Invoice</h3>
      </div>
      <div className="formatted-section">
        <div className="formatted-field">
          <label>Invoice Number</label>
          <span>{data.invoiceNumber || 'N/A'}</span>
        </div>
        <div className="formatted-field">
          <label>Invoice Date</label>
          <span>{data.invoiceDate || 'N/A'}</span>
        </div>
        {data.dueDate && (
          <div className="formatted-field">
            <label>Due Date</label>
            <span>{data.dueDate}</span>
          </div>
        )}
        <div className="formatted-field">
          <label>Vendor</label>
          <span>{data.vendorName || 'N/A'}</span>
        </div>
        {data.customerName && (
          <div className="formatted-field">
            <label>Customer</label>
            <span>{data.customerName}</span>
          </div>
        )}
        {data.bankAccountNumber && (
          <div className="formatted-field">
            <label>Bank Account</label>
            <span>{data.bankAccountNumber}</span>
          </div>
        )}
        {data.cid && (
          <div className="formatted-field">
            <label>CID / Organization Number</label>
            <span>{data.cid}</span>
          </div>
        )}
        {data.currency && (
          <div className="formatted-field">
            <label>Currency</label>
            <span className="currency-value">{data.currency}</span>
          </div>
        )}
      </div>

      {data.lineItems && Array.isArray(data.lineItems) && data.lineItems.length > 0 && (
        <div className="formatted-section">
          <h4 className="formatted-section-title">Line Items</h4>
          <div className="items-table">
            <div className={`items-header ${data.lineItems.some((item: any) => item.quantity) ? 'has-quantity' : 'no-quantity'}`}>
              <span>Description</span>
              {data.lineItems.some((item: any) => item.quantity) && <span>Qty</span>}
              <span>Price</span>
              <span>Total</span>
            </div>
            {data.lineItems.map((item: any, index: number) => (
              <div key={index} className={`items-row ${data.lineItems.some((item: any) => item.quantity) ? 'has-quantity' : 'no-quantity'}`}>
                <span>{item.description || 'N/A'}</span>
                {data.lineItems.some((i: any) => i.quantity) && (
                  <span>{item.quantity || '-'}</span>
                )}
                <span>{item.unitPrice ? formatCurrency(item.unitPrice, data.currency) : '-'}</span>
                <span>{item.total ? formatCurrency(item.total, data.currency) : '-'}</span>
              </div>
            ))}
          </div>
        </div>
      )}

      <div className="formatted-totals">
        {data.subtotal && (
          <div className="total-row">
            <label>Subtotal</label>
            <span>{formatCurrency(data.subtotal, data.currency)}</span>
          </div>
        )}
        {data.tax && (
          <div className="total-row">
            <label>Tax</label>
            <span>{formatCurrency(data.tax, data.currency)}</span>
          </div>
        )}
        {data.total && (
          <div className="total-row total-row-final">
            <label>Total</label>
            <span>{formatCurrency(data.total, data.currency)}</span>
          </div>
        )}
      </div>
    </div>
  );
}

function ReceiptView({ data }: { data: ExtractedData }) {
  return (
    <div className="formatted-view receipt-view">
      <div className="formatted-header">
        <h3>{data.storeName || 'Receipt'}</h3>
      </div>
      <div className="formatted-section">
        <div className="formatted-field">
          <label>Date</label>
          <span>{data.transactionDate || 'N/A'}</span>
        </div>
        {data.transactionTime && (
          <div className="formatted-field">
            <label>Time</label>
            <span>{data.transactionTime}</span>
          </div>
        )}
        {data.paymentMethod && (
          <div className="formatted-field">
            <label>Payment Method</label>
            <span>{data.paymentMethod}</span>
          </div>
        )}
        {data.currency && (
          <div className="formatted-field">
            <label>Currency</label>
            <span className="currency-value">{data.currency}</span>
          </div>
        )}
      </div>
      
      {data.items && Array.isArray(data.items) && data.items.length > 0 && (
        <div className="formatted-section">
          <h4 className="formatted-section-title">Items</h4>
          <div className="items-table">
            <div className={`items-header receipt-header ${data.items.some((item: any) => item.quantity) ? 'has-quantity' : 'no-quantity'}`}>
              <span>Item</span>
              {data.items.some((item: any) => item.quantity) && <span>Qty</span>}
              <span>Price</span>
            </div>
            {data.items.map((item: any, index: number) => (
              <div key={index} className={`items-row receipt-row ${data.items.some((item: any) => item.quantity) ? 'has-quantity' : 'no-quantity'}`}>
                <span>{item.name || 'N/A'}</span>
                {data.items.some((i: any) => i.quantity) && (
                  <span>{item.quantity || '-'}</span>
                )}
                <span>{item.price ? formatCurrency(item.price, data.currency) : '-'}</span>
              </div>
            ))}
          </div>
        </div>
      )}
      
      <div className="formatted-totals">
        {data.subtotal && (
          <div className="total-row">
            <label>Subtotal</label>
            <span>{formatCurrency(data.subtotal, data.currency)}</span>
          </div>
        )}
        {data.tax && (
          <div className="total-row">
            <label>Tax</label>
            <span>{formatCurrency(data.tax, data.currency)}</span>
          </div>
        )}
        {data.total && (
          <div className="total-row total-row-final">
            <label>Total</label>
            <span>{formatCurrency(data.total, data.currency)}</span>
          </div>
        )}
      </div>
    </div>
  );
}

function FlightTicketView({ data }: { data: ExtractedData }) {
  const flights = Array.isArray(data) ? data : [data];

  return (
    <div className="formatted-view flight-view">
      <div className="formatted-header">
        <h3>Flight Ticket</h3>
      </div>
      {flights.map((flight: ExtractedData, index: number) => (
        <div key={index} className="formatted-section">
          {flights.length > 1 && (
            <h4 className="formatted-section-title">
              {index === 0 ? 'Outbound' : 'Return'}
            </h4>
          )}
          <div className="formatted-field">
            <label>From</label>
            <span className="flight-value">{flight.travelingFrom || 'N/A'}</span>
          </div>
          <div className="formatted-field">
            <label>To</label>
            <span className="flight-value">{flight.travelingTo || 'N/A'}</span>
          </div>
          <div className="formatted-field-group">
            <div className="formatted-field">
              <label>Departure Date</label>
              <span>{flight.departureDate || 'N/A'}</span>
            </div>
            <div className="formatted-field">
              <label>Departure Time</label>
              <span>{flight.departureTime || 'N/A'}</span>
            </div>
          </div>
          {(flight.arrivalDate || flight.arrivalTime) && (
            <div className="formatted-field-group">
              <div className="formatted-field">
                <label>Arrival Date</label>
                <span>{flight.arrivalDate || 'N/A'}</span>
              </div>
              <div className="formatted-field">
                <label>Arrival Time</label>
                <span>{flight.arrivalTime || 'N/A'}</span>
              </div>
            </div>
          )}
          {flight.flightNumber && (
            <div className="formatted-field">
              <label>Flight Number</label>
              <span>{flight.flightNumber}</span>
            </div>
          )}
          {flight.passengerName && (
            <div className="formatted-field">
              <label>Passenger</label>
              <span>{flight.passengerName}</span>
            </div>
          )}
          {flight.bookingReference && (
            <div className="formatted-field">
              <label>Booking Reference</label>
              <span>{flight.bookingReference}</span>
            </div>
          )}
        </div>
      ))}
    </div>
  );
}

function OrderConfirmationView({ data }: { data: ExtractedData }) {
  return (
    <div className="formatted-view order-view">
      <div className="formatted-header">
        <h3>Order Confirmation</h3>
      </div>
      <div className="formatted-section">
        <div className="formatted-field">
          <label>Order Number</label>
          <span>{data.orderNumber || 'N/A'}</span>
        </div>
        <div className="formatted-field">
          <label>Order Date</label>
          <span>{data.orderDate || 'N/A'}</span>
        </div>
        {data.currency && (
          <div className="formatted-field">
            <label>Currency</label>
            <span className="currency-value">{data.currency}</span>
          </div>
        )}
      </div>

      {data.items && Array.isArray(data.items) && data.items.length > 0 && (
        <div className="formatted-section">
          <h4 className="formatted-section-title">Items</h4>
          <div className="items-table">
            <div className="items-header">
              <span>Item</span>
              <span>Qty</span>
              <span>Price</span>
            </div>
            {data.items.map((item: any, index: number) => (
              <div key={index} className="items-row">
                <span>{item.name || 'N/A'}</span>
                <span>{item.quantity || '-'}</span>
                <span>{item.price ? formatCurrency(item.price, data.currency) : '-'}</span>
              </div>
            ))}
          </div>
        </div>
      )}

      <div className="formatted-totals">
        {data.subtotal && (
          <div className="total-row">
            <label>Subtotal</label>
            <span>{formatCurrency(data.subtotal, data.currency)}</span>
          </div>
        )}
        {data.tax && (
          <div className="total-row">
            <label>Tax</label>
            <span>{formatCurrency(data.tax, data.currency)}</span>
          </div>
        )}
        {data.shipping && (
          <div className="total-row">
            <label>Shipping</label>
            <span>{formatCurrency(data.shipping, data.currency)}</span>
          </div>
        )}
        {data.total && (
          <div className="total-row total-row-final">
            <label>Total</label>
            <span>{formatCurrency(data.total, data.currency)}</span>
          </div>
        )}
      </div>
    </div>
  );
}

function OtherImageView({ data }: { data: ExtractedData }) {
  return (
    <div className="formatted-view other-view">
      <div className="formatted-header">
        <h3>Image Description</h3>
      </div>
      <div className="formatted-section">
        <div className="formatted-field-full">
          <p>{data.description || 'No description available'}</p>
        </div>
      </div>
    </div>
  );
}
