import { useState } from 'react';
import './Example1.css';

interface Example {
  id: string;
  title: string;
  description: string;
  placeholder: string;
  endpoint: string;
}

const examples: Example[] = [
  {
    id: 'invoice',
    title: 'Extract Invoice Data',
    description: 'Extract structured data from unstructured invoice text',
    placeholder: 'Invoice #12345\nDate: 2024-01-15\nVendor: Acme Corp\nItems:\n- Widget A: 10 units @ $5.00 = $50.00\n- Widget B: 5 units @ $8.00 = $40.00\nTotal: $90.00',
    endpoint: '/api/Example1/extract-invoice-data'
  },
  {
    id: 'receipt',
    title: 'Parse Receipt',
    description: 'Parse receipt text into structured JSON',
    placeholder: 'STORE NAME\n123 Main St\n\nItems:\nMilk 2.99\nBread 1.99\nEggs 3.49\n\nSubtotal: 8.47\nTax: 0.68\nTotal: 9.15\n\nCard ending in 1234',
    endpoint: '/api/Example1/parse-receipt'
  },
  {
    id: 'products',
    title: 'Structure Product Descriptions',
    description: 'Convert unstructured product descriptions into structured data',
    placeholder: 'Widget Pro - A high-quality widget for professionals. Features: durable, lightweight, comes in multiple colors. Price: $29.99\n\nGadget Max - The ultimate gadget with advanced features. Features: wireless charging, waterproof, long battery life. Price: $49.99',
    endpoint: '/api/Example1/structure-product-descriptions'
  }
];

export default function Example1() {
  const [selectedExample, setSelectedExample] = useState<Example>(examples[0]);
  const [input, setInput] = useState(examples[0].placeholder);
  const [output, setOutput] = useState('');
  const [isLoading, setIsLoading] = useState(false);

  const handleGenerate = async () => {
    if (!input.trim() || isLoading) return;

    setIsLoading(true);
    setOutput('');

    try {
      const requestBody = selectedExample.id === 'invoice'
        ? { invoiceText: input }
        : selectedExample.id === 'receipt'
          ? { receiptText: input }
          : { productDescriptions: input };

      const response = await fetch(`http://localhost:5064${selectedExample.endpoint}`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(requestBody),
      });

      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }

      const data = await response.json();
      setOutput(data.result || '');
    } catch (error) {
      console.error('Error generating response:', error);
      setOutput(`Error: ${error instanceof Error ? error.message : 'Unknown error'}`);
    } finally {
      setIsLoading(false);
    }
  };

  const handleExampleSelect = (example: Example) => {
    setSelectedExample(example);
    setInput(example.placeholder);
    setOutput('');
  };

  return (
    <div className="example1-container">
      <h1>Example #1: Unstructured → Structured Data</h1>
      <p className="description">
        These examples demonstrate how LLMs excel at taking unstructured text and converting it into structured JSON.
        Perfect for extracting data from invoices, receipts, product descriptions, and more.
      </p>

      <div className="examples-selector">
        <h2>Select an Example:</h2>
        <div className="example-buttons">
          {examples.map((example) => (
            <button
              key={example.id}
              className={`example-button ${selectedExample.id === example.id ? 'active' : ''}`}
              onClick={() => handleExampleSelect(example)}
            >
              {example.title}
            </button>
          ))}
        </div>
      </div>

      <div className="selected-example">
        <h2>{selectedExample.title}</h2>
        <p className="example-description">{selectedExample.description}</p>
      </div>

      <div className="generate-button-container">
        <button
          onClick={handleGenerate}
          disabled={isLoading || !input.trim()}
          className="stream-button"
        >
          {isLoading ? 'Generating...' : 'Generate Response'}
        </button>
      </div>

      <div className="input-output-container">
        <div className="input-section">
          <label htmlFor="input-text">Input (Unstructured Text):</label>
          <textarea
            id="input-text"
            value={input}
            onChange={(e) => setInput(e.target.value)}
            placeholder={selectedExample.placeholder}
            rows={20}
            disabled={isLoading}
          />
        </div>

        <div className="output-section">
          <label>Output (Structured JSON):</label>
          <div className="output-container">
            {output ? (
              <pre className="json-output">{output}</pre>
            ) : (
              <div className="output-placeholder">
                Response will appear here...
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
