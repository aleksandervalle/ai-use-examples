import { useState } from 'react';
import './Example2.css';

interface Prompt {
  name: string;
  placeholder: string;
}

interface Example {
  id: string;
  title: string;
  description: string;
  endpoint: string;
  categories: string[];
  dataType: 'unstructured' | 'structured';
  prompts: Prompt[];
}

const examples: Example[] = [
  {
    id: 'sentiment',
    title: 'Classify Sentiment (Unstructured)',
    description: 'Classify customer feedback sentiment into Positive, Neutral, or Negative',
    endpoint: '/api/Example2/classify-sentiment',
    categories: ['Positive', 'Neutral', 'Negative'],
    dataType: 'unstructured',
    prompts: [
      {
        name: 'Prompt 1: Positive',
        placeholder: 'The product exceeded my expectations! The customer service was excellent and the delivery was fast. Highly recommend!'
      },
      {
        name: 'Prompt 2: Neutral',
        placeholder: 'I received the product on time. It works as described. Nothing special, but it does what it\'s supposed to do.'
      },
      {
        name: 'Prompt 3: Negative',
        placeholder: 'Very disappointed with the quality. The product broke after just one week of use. Customer service was unhelpful and refused to provide a refund.'
      }
    ]
  },
  {
    id: 'expense',
    title: 'Classify Expense Type (Unstructured)',
    description: 'Classify expense descriptions into categories',
    endpoint: '/api/Example2/classify-expense-type',
    categories: ['Travel', 'Meals', 'Office Supplies', 'Software', 'Utilities', 'Marketing', 'Professional Services', 'Other'],
    dataType: 'unstructured',
    prompts: [
      {
        name: 'Prompt 1: Travel',
        placeholder: 'Flight tickets from New York to San Francisco for business conference'
      },
      {
        name: 'Prompt 2: Meals',
        placeholder: 'Dinner meeting with client at restaurant downtown'
      },
      {
        name: 'Prompt 3: Office Supplies',
        placeholder: 'Printer paper, pens, and notebooks for the office'
      },
      {
        name: 'Prompt 4: Software',
        placeholder: 'Annual subscription for project management SaaS platform'
      }
    ]
  },
  {
    id: 'transaction',
    title: 'Classify Transaction Category (Structured)',
    description: 'Classify transaction JSON data into categories',
    endpoint: '/api/Example2/classify-transaction-category',
    categories: ['Income', 'Expense', 'Transfer', 'Investment', 'Refund'],
    dataType: 'structured',
    prompts: [
      {
        name: 'Prompt 1: Income',
        placeholder: '{\n  "amount": 5000.00,\n  "description": "Monthly salary payment",\n  "source": "Employer Corp",\n  "date": "2024-01-31"\n}'
      },
      {
        name: 'Prompt 2: Expense',
        placeholder: '{\n  "amount": 150.00,\n  "description": "Monthly subscription payment",\n  "merchant": "SaaS Platform Inc",\n  "date": "2024-01-15"\n}'
      },
      {
        name: 'Prompt 3: Transfer',
        placeholder: '{\n  "amount": 1000.00,\n  "description": "Transfer to savings account",\n  "fromAccount": "Checking",\n  "toAccount": "Savings",\n  "date": "2024-01-20"\n}'
      },
      {
        name: 'Prompt 4: Refund',
        placeholder: '{\n  "amount": 75.50,\n  "description": "Refund for returned product",\n  "merchant": "Online Store",\n  "originalTransactionDate": "2024-01-10",\n  "date": "2024-01-25"\n}'
      }
    ]
  },
  {
    id: 'ticket',
    title: 'Classify Ticket Priority (Structured)',
    description: 'Classify support ticket JSON data into priority levels',
    endpoint: '/api/Example2/classify-ticket-priority',
    categories: ['Critical', 'High', 'Medium', 'Low'],
    dataType: 'structured',
    prompts: [
      {
        name: 'Prompt 1: Critical',
        placeholder: '{\n  "title": "System down - cannot process orders",\n  "description": "Customers are unable to complete purchases. Error appears on checkout page.",\n  "reportedBy": "admin@company.com",\n  "affectedUsers": 150\n}'
      },
      {
        name: 'Prompt 2: High',
        placeholder: '{\n  "title": "Feature request - export functionality",\n  "description": "Users need ability to export reports to Excel format. This is blocking several key accounts.",\n  "reportedBy": "support@company.com",\n  "affectedUsers": 25\n}'
      },
      {
        name: 'Prompt 3: Medium',
        placeholder: '{\n  "title": "UI improvement suggestion",\n  "description": "Button placement could be improved for better user experience. Not urgent but would be nice to have.",\n  "reportedBy": "user@example.com",\n  "affectedUsers": 5\n}'
      },
      {
        name: 'Prompt 4: Low',
        placeholder: '{\n  "title": "Documentation update",\n  "description": "Minor typo found in user guide page 15. Should be fixed when convenient.",\n  "reportedBy": "editor@company.com",\n  "affectedUsers": 1\n}'
      }
    ]
  }
];

export default function Example2() {
  const [selectedExample, setSelectedExample] = useState<Example>(examples[0]);
  const [selectedPromptIndex, setSelectedPromptIndex] = useState(0);
  const [input, setInput] = useState(examples[0].prompts[0].placeholder);
  const [output, setOutput] = useState('');
  const [isLoading, setIsLoading] = useState(false);

  const handleClassify = async () => {
    if (!input.trim() || isLoading) return;

    setIsLoading(true);
    setOutput('');

    try {
      const requestBody = selectedExample.dataType === 'structured'
        ? { jsonData: input }
        : { text: input };

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
      console.error('Error classifying:', error);
      setOutput(`Error: ${error instanceof Error ? error.message : 'Unknown error'}`);
    } finally {
      setIsLoading(false);
    }
  };

  const handleExampleSelect = (example: Example) => {
    setSelectedExample(example);
    setSelectedPromptIndex(0);
    setInput(example.prompts[0].placeholder);
    setOutput('');
  };

  const handlePromptSelect = (index: number) => {
    setSelectedPromptIndex(index);
    setInput(selectedExample.prompts[index].placeholder);
    setOutput('');
  };

  return (
    <div className="example2-container">
      <h1>Example #2: Classification</h1>
      <p className="description">
        These examples demonstrate how LLMs excel at classification tasks. You can classify both unstructured text
        and structured JSON data into predefined categories. Perfect for sentiment analysis, expense categorization,
        transaction classification, and priority assignment.
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
        <div className="categories-info">
          <strong>Categories:</strong> {selectedExample.categories.join(', ')}
        </div>
        <div className="data-type-badge">
          {selectedExample.dataType === 'structured' ? '📊 Structured Data (JSON)' : '📝 Unstructured Data (Text)'}
        </div>
      </div>

      <div className="prompts-selector">
        <h3>Select a Prompt:</h3>
        <div className="prompt-buttons">
          {selectedExample.prompts.map((prompt, index) => (
            <button
              key={index}
              className={`prompt-button ${selectedPromptIndex === index ? 'active' : ''}`}
              onClick={() => handlePromptSelect(index)}
            >
              {prompt.name}
            </button>
          ))}
        </div>
      </div>

      <div className="generate-button-container">
        <button
          onClick={handleClassify}
          disabled={isLoading || !input.trim()}
          className="classify-button"
        >
          {isLoading ? 'Classifying...' : 'Classify'}
        </button>
      </div>

      <div className="input-output-container">
        <div className="input-section">
          <label htmlFor="input-text">
            Input ({selectedExample.dataType === 'structured' ? 'JSON Data' : 'Text'}):
          </label>
          <textarea
            id="input-text"
            value={input}
            onChange={(e) => setInput(e.target.value)}
            placeholder={selectedExample.prompts[selectedPromptIndex].placeholder}
            rows={20}
            disabled={isLoading}
          />
        </div>

        <div className="output-section">
          <label>Output (Classification Result):</label>
          <div className="output-container">
            {output ? (
              <pre className="json-output">{output}</pre>
            ) : (
              <div className="output-placeholder">
                Classification result will appear here...
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
