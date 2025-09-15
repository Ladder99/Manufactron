# Manufactron - I3X Manufacturing Intelligence Platform

A comprehensive manufacturing intelligence platform implementing the **I3X (Industrial Internet Information eXchange)** standard for unified data access across enterprise systems.

## 🏗️ Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                   Exploratory Client                         │
│              (Interactive Console Interface)                 │
└───────────────────────┬─────────────────────────────────────┘
                        │ HTTP/REST
                        ▼
┌─────────────────────────────────────────────────────────────┐
│                  I3X Aggregator Service                      │
│                     (Port: 7000)                             │
│  • Dynamic Graph Discovery                                   │
│  • Context Building                                          │
│  • Unified API                                               │
└──────┬──────────────────┬──────────────────┬────────────────┘
       │                  │                  │
       ▼                  ▼                  ▼
┌──────────────┐  ┌──────────────┐  ┌──────────────┐
│ ERP Service  │  │ MES Service  │  │ SCADA Service│
│ (Port: 7001) │  │ (Port: 7002) │  │ (Port: 7003) │
└──────────────┘  └──────────────┘  └──────────────┘
```

## 🚀 Quick Start

### Prerequisites
- .NET 9.0 SDK
- Windows PowerShell (for automated startup)

### Start All Services

```powershell
# From the solution root directory
.\Start-AllServices.ps1
```

This will:
1. Start all four I3X services (Aggregator, ERP, MES, SCADA) in separate windows
2. Prompt to launch the Interactive Exploratory Client
3. Display all service URLs and endpoints

## 🔍 I3X Exploratory Client

The **Exploratory Client** is an interactive console application that provides visual exploration of the manufacturing intelligence graph through the aggregator service.

### Features

#### Interactive Menu System
- **📊 Discover Namespaces** - View all available namespaces across ERP, MES, and SCADA services
- **🏭 Browse Object Types** - Explore hierarchical view of all object types in the system
- **📦 List All Objects** - Display all instances grouped by their type
- **🔍 Search for Object** - Find objects by ID, name, or type with partial matching
- **🌐 Build Manufacturing Context** - Construct complete manufacturing context from any entity
- **📈 Visualize Object Graph** - Tree visualization showing relationships and hierarchy
- **🔗 Explore Relationships** - Navigate and understand object relationships
- **📜 View Object History** - Display historical data with configurable time ranges
- **🎯 Interactive Object Navigator** - Navigate through the graph interactively

### How to Use the Exploratory Client

1. **Launch the Client**
   - Run `Start-AllServices.ps1` and select 'Y' when prompted for the Exploratory Client
   - Or manually: `cd Manufactron.ExploratoryClient && dotnet run`

2. **Navigation**
   - Use arrow keys to navigate menu options
   - Press Enter to select
   - Follow on-screen prompts for input

3. **Example Workflows**

   **Exploring Manufacturing Context:**
   ```
   1. Select "🔍 Search for Object"
   2. Enter search term: "filler"
   3. Note the element ID (e.g., "scada-equipment-filler-1")
   4. Select "🌐 Build Manufacturing Context"
   5. Enter the element ID
   6. View complete context including Equipment, Line, Job, Order, Material, and Operator
   ```

   **Interactive Navigation:**
   ```
   1. Select "🎯 Interactive Object Navigator"
   2. Enter starting element: "scada-line-1"
   3. Navigate to children, parent, or follow relationships
   4. Build context or visualize graph at any point
   ```

### Configuration

The client reads its configuration from `appsettings.json`:

```json
{
  "AggregatorService": {
    "BaseUrl": "http://localhost:7000"
  }
}
```

## 🔄 I3X Aggregator Service

The Aggregator Service is the heart of the system, providing unified access to all manufacturing data sources.

### Key Features

#### Dynamic Graph Discovery
- Automatically discovers the manufacturing system structure
- Builds a comprehensive graph of all objects and relationships
- Caches the graph for 30 minutes for performance
- No hardcoded types or relationships

#### Intelligent Context Building
- Constructs complete manufacturing context from ANY starting entity
- Uses graph traversal algorithms (BFS/DFS) to find related entities
- Pattern matching for automatic type recognition
- Handles complex multi-hop relationships

#### Unified API Endpoints

| Endpoint | Description |
|----------|-------------|
| `GET /api/i3x/namespaces` | Get all namespaces from all services |
| `GET /api/i3x/types` | Get all object types |
| `GET /api/i3x/objects` | Get all objects (with optional metadata) |
| `GET /api/i3x/objects/{id}` | Get specific object |
| `GET /api/i3x/objects/{id}/parent` | Get parent object |
| `GET /api/i3x/objects/{id}/children` | Get child objects |
| `GET /api/i3x/objects/{id}/relationships/{type}` | Get related objects |
| `GET /api/i3x/context/{id}` | Build complete manufacturing context |
| `GET /api/i3x/history/{id}` | Get historical values |
| `GET /api/i3x/values/{id}` | Get current values |
| `POST /api/i3x/values/{id}` | Update values |

### How the Aggregator Works

1. **Service Discovery**
   - Queries all three services (ERP, MES, SCADA) for their capabilities
   - Merges namespaces and types into unified view

2. **Graph Construction**
   - Fetches all objects from all services
   - Builds nodes from objects with attributes
   - Creates edges from relationships
   - Maintains bidirectional adjacency lists

3. **Context Building Process**
   ```
   Starting Entity → Graph Discovery → Path Finding → Context Population
   ```
   - Identifies entity type through pattern matching
   - Traverses graph to find related entities (Order, Job, Line, Equipment, etc.)
   - Uses relationship semantics (ForOrder, ExecutedOn, HasChildren, etc.)
   - Aggregates all relationships across entities

## 📊 Data Services

### ERP Service (Port: 7001)
Manages enterprise-level data:
- **Orders** - Customer orders with products and quantities
- **Customers** - Customer information and relationships
- **Products** - Product definitions and specifications

### MES Service (Port: 7002)
Handles production execution:
- **Jobs** - Production jobs with planned quantities and schedules
- **Operators** - Worker assignments and shifts
- **Material Batches** - Raw material tracking

### SCADA Service (Port: 7003)
Controls equipment and lines:
- **Production Lines** - Line status and performance (OEE)
- **Equipment** - Mixers, Fillers, Cappers with real-time status
- **Sensors** - Temperature, pressure, flow rate monitoring

## 🔧 Development

### Project Structure
```
Manufactron/
├── Manufactron.I3X.Shared/          # Shared models and interfaces
├── Manufactron.I3X.ERP/             # ERP service implementation
├── Manufactron.I3X.MES/             # MES service implementation
├── Manufactron.I3X.SCADA/           # SCADA service implementation
├── Manufactron.I3X.Aggregator/      # Aggregator service with graph discovery
│   ├── Services/
│   │   ├── I3XAggregatorService.cs  # Main aggregation logic
│   │   ├── GraphDiscoveryService.cs # Dynamic graph discovery
│   │   └── ContextBuilderService.cs # Context building with graph traversal
│   └── Models/
│       └── GraphModels.cs           # Graph structures
├── Manufactron.ExploratoryClient/   # Interactive console client
└── Manufactron.Client/              # Basic client implementation
```

### Building from Source

```bash
# Build all projects
dotnet build

# Run individual services
cd Manufactron.I3X.Aggregator && dotnet run
cd Manufactron.I3X.ERP && dotnet run
cd Manufactron.I3X.MES && dotnet run
cd Manufactron.I3X.SCADA && dotnet run

# Run the exploratory client
cd Manufactron.ExploratoryClient && dotnet run
```

## 🎯 Key Concepts

### Manufacturing Context
A complete view of the manufacturing state including:
- **Equipment** - The primary equipment involved
- **Line** - The production line
- **Job** - Current production job
- **Order** - Customer order being fulfilled
- **Material Batch** - Raw materials being used
- **Operator** - Worker operating the equipment
- **All Relationships** - Complete relationship graph

### Graph-Based Discovery
The system uses advanced graph algorithms to:
- Discover system topology without configuration
- Find shortest paths between entities
- Build context from any starting point
- Handle complex multi-service relationships

### Pattern Recognition
Intelligent type detection through:
- Type ID patterns (e.g., "equipment-type", "order-type")
- Element ID patterns (e.g., "scada-equipment-mixer")
- Attribute analysis (e.g., presence of "customerId" indicates Order)
- Relationship semantics (e.g., "ForOrder", "ExecutedOn")

## 📝 Example Scenarios

### Scenario 1: Equipment Failure Investigation
1. Start from equipment showing error
2. Build context to see current job, order, and operator
3. Check material batch quality certificate
4. View historical sensor data
5. Navigate to upstream/downstream equipment

### Scenario 2: Order Status Tracking
1. Search for order by customer name
2. Build context to see associated jobs
3. Navigate to production lines executing jobs
4. Check equipment status and OEE
5. View operator assignments

### Scenario 3: Quality Issue Root Cause
1. Start from quality alert
2. Navigate to material batch
3. Check supplier and certificate
4. Find all jobs using the batch
5. Identify affected equipment and lines

## 🔒 Security Considerations

- Services run on localhost only by default
- No authentication implemented (development version)
- CORS enabled for development
- Production deployment requires:
  - Authentication/authorization
  - HTTPS endpoints
  - Network security
  - Access control

## 📄 License

This is a demonstration implementation of the I3X standard for educational purposes.

## 🤝 Contributing

This project demonstrates I3X implementation patterns. For production use:
1. Add authentication and authorization
2. Implement data persistence
3. Add error handling and retry logic
4. Configure for your specific equipment and systems
5. Add monitoring and logging

## 📧 Support

For questions about the I3X standard and implementation patterns, refer to the industrial standards documentation.

---

*Built with .NET 9.0 and the I3X Industrial Standard*