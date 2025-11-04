# AI Use Examples - Application Overview

## Purpose

This application is a comprehensive demonstration platform showcasing practical use cases for Large Language Models (LLMs), specifically using Google's Gemini API. It serves as an educational tool and reference implementation for developers looking to understand how to integrate LLMs into real-world applications.

## Architecture

### Technology Stack

**Frontend:**
- **React 19** with TypeScript
- **Vite** for build tooling and development server
- **React Router DOM** for client-side routing
- Modern CSS for styling

**Backend:**
- **ASP.NET Core 8.0** Web API (C#)
- **Google Gemini API** (gemini-2.5-flash-lite model)
- Swagger/OpenAPI for API documentation

**Key Services:**
- Gemini API Service (text and multimodal completions)
- Weather Service (Norwegian Meteorological Institute API)
- Person Lookup Service (mock implementation)
- Meeting Service (mock implementation)

### Communication Flow

```
React Frontend (Port 5173/5174) 
    ↕ HTTP/REST API
ASP.NET Core API (Port 5064)
    ↕ HTTP API
Google Gemini API
```

The frontend communicates with the backend API, which handles all interactions with the Gemini API and external services.

## Application Examples

The application consists of four main examples, each demonstrating different LLM capabilities:

### Example 1: Unstructured → Structured Data

**Purpose:** Demonstrates how LLMs excel at extracting structured data from unstructured text.

**Features:**
- Extract invoice data (invoice number, date, vendor, line items, totals)
- Parse receipt information (store name, items, totals, payment method)
- Structure product descriptions (name, category, price, features)

**Use Cases:**
- Document processing and data extraction
- OCR post-processing
- Data migration from legacy systems
- Automated data entry

**API Endpoints:**
- `POST /api/Example1/extract-invoice-data`
- `POST /api/Example1/parse-receipt`
- `POST /api/Example1/structure-product-descriptions`

**Technical Details:**
- Uses text-only prompts with structured output requirements
- Returns JSON responses without markdown formatting
- Temperature set to 0.0 for consistent, deterministic results

---

### Example 2: Classification

**Purpose:** Demonstrates LLM capabilities in classifying both unstructured text and structured JSON data into predefined categories.

**Features:**

1. **Sentiment Classification (Unstructured)**
   - Classifies customer feedback as Positive, Neutral, or Negative
   - Provides confidence scores and reasoning

2. **Expense Type Classification (Unstructured)**
   - Categorizes expense descriptions (Travel, Meals, Office Supplies, Software, Utilities, Marketing, Professional Services, Other)

3. **Transaction Category Classification (Structured)**
   - Classifies JSON transaction data (Income, Expense, Transfer, Investment, Refund)

4. **Ticket Priority Classification (Structured)**
   - Assigns priority levels to support tickets (Critical, High, Medium, Low) based on ticket JSON data

**Use Cases:**
- Customer feedback analysis
- Expense report automation
- Transaction categorization for accounting systems
- Support ticket routing and prioritization

**API Endpoints:**
- `POST /api/Example2/classify-sentiment`
- `POST /api/Example2/classify-expense-type`
- `POST /api/Example2/classify-transaction-category`
- `POST /api/Example2/classify-ticket-priority`

**Technical Details:**
- Handles both text and JSON input
- Returns classification with confidence scores and reasoning
- Temperature set to 0.0 for consistent classifications

---

### Example 3: Function Calling Chat

**Purpose:** Demonstrates advanced LLM capabilities through function calling (tool use), enabling the AI to interact with external systems and services.

**Features:**
- Conversational chat interface with streaming responses
- Function calling integration with external services:
  - **Weather Service:** Fetches weather data from Norwegian Meteorological Institute API
  - **Person Lookup Service:** Mock service that looks up people by name to retrieve email addresses
  - **Meeting Service:** Mock service for scheduling meetings with attendees

**Capabilities:**
- Natural language understanding for weather queries (e.g., "Hvordan er været i Bodø nå?")
- Intelligent person name resolution before scheduling meetings
- Automatic meeting scheduling with attendee lookup
- Streaming response output for better user experience

**Function Definitions:**

1. **get_weather**
   - Parameters: `lat` (number), `lon` (number), `altitude` (number, optional)
   - Returns: XML weather data from Met.no API
   - The LLM intelligently determines coordinates from location names

2. **lookup_persons**
   - Parameters: `names` (array of strings)
   - Returns: Array of person objects with full name and email
   - Used to resolve partial names to email addresses

3. **schedule_meeting**
   - Parameters: `attendeeEmails` (array), `date` (string), `time` (string), `agenda` (string)
   - Returns: Meeting scheduling result with confirmation

**Use Cases:**
- AI assistants with tool integration
- Natural language interfaces to complex systems
- Automated workflow orchestration
- Intelligent data retrieval and action execution

**API Endpoint:**
- `POST /api/Example3/chat` (streaming response)

**Technical Details:**
- Implements iterative function calling loop (max 10 iterations)
- Converts conversation history to Gemini message format
- Handles function execution and response integration
- Streaming word-by-word output for real-time user feedback
- Uses thinking budget for complex reasoning tasks
- System message includes current timestamp for weather data context
- Responses provided in Norwegian

---

### Example 4: Image/Document Understanding

**Purpose:** Demonstrates multimodal LLM capabilities for understanding and extracting structured data from images and documents.

**Features:**
- Upload and process images (JPEG, PNG, GIF, WebP) and PDF documents
- Document type classification (invoice, receipt, flight ticket, order confirmation, other)
- Automatic alternative filename generation based on document content
- Structured data extraction tailored to document type
- Side-by-side comparison view of document and extracted data
- Formatted views for different document types:
  - Invoice view with line items and totals
  - Receipt view with items and payment information
  - Flight ticket view with travel details
  - Order confirmation view with order details

**Supported Document Types:**

1. **Invoice**
   - Extracts: invoice number, dates, vendor, customer, line items, totals, currency, bank account, organization number

2. **Receipt**
   - Extracts: store name, transaction date/time, items, totals, payment method, currency

3. **Flight Ticket**
   - Extracts: departure/arrival locations, dates, times, flight number, passenger name, booking reference

4. **Order Confirmation**
   - Extracts: order number, date, items, totals, shipping costs, currency

5. **Other Images**
   - Provides detailed description of image content

**Use Cases:**
- Automated document processing and archiving
- Expense report automation
- Invoice processing and accounts payable automation
- Travel booking management
- Document management systems with intelligent tagging

**API Endpoint:**
- `POST /api/Example4/process-document` (multipart/form-data, streaming response)

**Technical Details:**
- Uses Gemini multimodal API (vision capabilities)
- Two-phase processing:
  1. Classification and filename generation (parallel execution)
  2. Type-specific data extraction
- Streaming response with delimiters for progressive UI updates
- Supports multiple image formats and PDF
- Temperature set to 0.0 for consistent extraction
- Handles currency detection and formatting

---

## Backend Services

### GeminiApiService

Core service for interacting with Google's Gemini API. Provides three main methods:

1. **GenerateCompletionAsync**: Text-only completions
2. **GenerateCompletionWithToolsAsync**: Completions with function calling support
3. **GenerateMultimodalCompletionAsync**: Image + text multimodal completions

**Configuration:**
- Model: `gemini-2.5-flash-lite`
- API Key: Configured via `appsettings.json` (`Gemini:ApiKey`)
- Temperature: 0.0 (deterministic outputs)
- Max Output Tokens: 20,000
- Thinking Budget: Configurable per request type

### WeatherService

Fetches weather forecast data from the Norwegian Meteorological Institute (Met.no) API.

- **Endpoint:** `https://api.met.no/weatherapi/locationforecast/2.0/classic`
- **Returns:** XML weather data
- **Parameters:** Latitude, longitude, altitude (meters)

### PersonLookupService

Mock service for looking up people by name. Returns full name and email address.

- Maintains a mock database of 20 Norwegian names
- Fuzzy matching on partial names
- Returns deduplicated results

### MeetingService

Mock service for scheduling meetings.

- Accepts attendee emails, date, time, and agenda
- Returns meeting confirmation with meeting ID
- In production, would integrate with calendar systems (Google Calendar, Outlook, etc.)

## Frontend Architecture

### Component Structure

```
src/
├── App.tsx                 # Main app component with routing
├── components/
│   ├── Layout.tsx         # Main layout with navigation
│   └── ShimmerLoader.tsx  # Loading animation component
└── pages/
    ├── Example1.tsx       # Unstructured → Structured Data
    ├── Example2.tsx       # Classification
    ├── Example3.tsx       # Function Calling Chat
    └── Example4.tsx       # Image/Document Understanding
```

### Key Features

- **Responsive Design:** Modern, clean UI with side-by-side input/output views
- **Streaming Support:** Real-time updates for chat and document processing
- **Error Handling:** Comprehensive error handling and user feedback
- **Loading States:** Visual feedback during API calls
- **File Upload:** Drag-and-drop or click-to-upload for documents
- **Document Preview:** In-browser preview of uploaded images and PDFs
- **Comparison Modal:** Side-by-side view of documents and extracted data

## Configuration

### Backend Configuration

**appsettings.json:**
```json
{
  "Gemini": {
    "ApiKey": "your-gemini-api-key-here"
  }
}
```

**CORS Configuration:**
- Allows requests from `http://localhost:5173`, `http://localhost:5174`, and `http://localhost:3000`
- Configured for development environment

### Frontend Configuration

**API Endpoint:**
- Hardcoded to `http://localhost:5064` in components
- Should be configured via environment variables for production

## Development Setup

### Prerequisites

- .NET 8.0 SDK
- Node.js 18+ and npm
- Google Gemini API key

### Running the Application

1. **Backend:**
   ```bash
   cd server/AiUseExamples.Api
   dotnet restore
   dotnet run
   ```
   API will be available at `https://localhost:5064` (or `http://localhost:5064`)

2. **Frontend:**
   ```bash
   cd client
   npm install
   npm run dev
   ```
   Frontend will be available at `http://localhost:5173`

3. **Configure API Key:**
   - Add your Gemini API key to `server/AiUseExamples.Api/appsettings.json`
   - Or set via environment variable: `Gemini__ApiKey`

## Use Cases and Applications

This application demonstrates patterns that can be applied to:

1. **Business Process Automation**
   - Invoice processing
   - Expense report automation
   - Document classification and routing

2. **Customer Service**
   - Sentiment analysis
   - Ticket prioritization
   - Automated responses

3. **Data Management**
   - Data extraction from documents
   - Data normalization
   - Legacy system migration

4. **Intelligent Assistants**
   - Natural language interfaces
   - Function calling for tool integration
   - Multi-step workflows

5. **Content Understanding**
   - Image analysis
   - Document parsing
   - Content classification

## Limitations and Considerations

1. **API Costs:** Each request to Gemini API incurs costs. Consider rate limiting and caching strategies.

2. **Accuracy:** LLM outputs may contain errors. Implement validation and human review for critical operations.

3. **Latency:** API calls can be slow, especially for multimodal requests. Consider async processing for large-scale deployments.

4. **Security:** API keys should be stored securely. Current implementation uses configuration files (not suitable for production).

5. **Error Handling:** While basic error handling exists, production systems should have more robust error recovery.

6. **Mock Services:** Person lookup and meeting services are mock implementations. Replace with real integrations for production use.

## Future Enhancements

Potential improvements and extensions:

1. **Authentication & Authorization:** Add user authentication and role-based access control
2. **Database Integration:** Store extracted data and conversation history
3. **Batch Processing:** Support for processing multiple documents at once
4. **Export Functionality:** Export extracted data to various formats (CSV, Excel, JSON)
5. **Real Integrations:** Replace mock services with real calendar and directory services
6. **Multi-language Support:** Extend beyond Norwegian/English
7. **Custom Model Fine-tuning:** Fine-tune models for specific document types
8. **Analytics Dashboard:** Track usage, accuracy, and performance metrics

## Conclusion

This application serves as a comprehensive reference for implementing LLM-powered features in modern web applications. It demonstrates four fundamental patterns:

1. **Data Extraction:** Converting unstructured data to structured formats
2. **Classification:** Categorizing data using LLM reasoning
3. **Function Calling:** Enabling LLMs to interact with external systems
4. **Multimodal Understanding:** Processing images and documents with vision capabilities

Each example can be adapted and extended for specific business needs, providing a solid foundation for building production-ready AI-powered applications.

