# Manufactron Architecture Overview

## Solution Structure

The Manufactron solution consists of several interconnected projects that implement a manufacturing intelligence system based on the I3X (Industrial Internet Information eXchange) standard.

### Core Projects

1. **Manufactron.I3X.Aggregator** (Port 7000)
   - Central hub that aggregates data from multiple I3X data sources
   - Provides unified API for querying manufacturing data
   - Implements context building service for relationship traversal
   - Services: ContextBuilderService, HistoricalDataService
   - Key endpoints: /api/i3x/objects, /api/i3x/context, /api/i3x/history

2. **Manufactron.I3X.ERP** (Port 7001)
   - Simulates Enterprise Resource Planning system
   - Provides customer orders, materials, and planning data
   - Implements II3XDataSource interface

3. **Manufactron.I3X.MES** (Port 7002)
   - Simulates Manufacturing Execution System
   - Manages production jobs, operators, and execution data
   - Implements II3XDataSource interface

4. **Manufactron.I3X.SCADA** (Port 7003)
   - Simulates SCADA (Supervisory Control and Data Acquisition)
   - Provides equipment status, sensor data, real-time metrics
   - Implements II3XDataSource interface

5. **Manufactron.I3X.Shared**
   - Shared models and interfaces for I3X implementation
   - Key interfaces: II3XClient, II3XDataSource
   - Models: Instance, ManufacturingContext, HistoricalValue

### Client Applications

1. **Manufactron.Client**
   - LLM-powered conversational assistant using Microsoft Semantic Kernel
   - Plugin-based architecture for manufacturing intelligence
   - Key Plugins:
     - EquipmentPlugin: Equipment monitoring and failure prediction
     - ContextPlugin: Context building and object search
     - AnalyticsPlugin: OEE calculation and analytics
     - I3XAwareProductionPlugin: Advanced production analysis with AI
   - Services: ConversationalAssistant, SemanticFunctions
   - Features: Natural language processing, automatic function calling

2. **Manufactron.ExploratoryClient**
   - Visual console application for manual I3X exploration
   - Rich UI using Spectre.Console
   - Features: Object browsing, context discovery, graph visualization
   - Used for debugging and understanding I3X relationships

## Key Technical Details

### I3X Implementation
- Objects have required attributes: ElementId, Name, TypeId, ParentId, HasChildren, NamespaceUri
- Relationships are bidirectional (Forward/Reverse)
- Context building traverses relationships to gather complete manufacturing context
- Historical data tracked with timestamps and quality indicators

### Semantic Kernel Integration
- Uses OpenAI GPT-4 for natural language understanding
- Automatic function calling through KernelFunction decorations
- Plugins provide domain-specific manufacturing intelligence
- Conversation history maintained for context-aware responses

### Data Flow
1. SCADA/MES/ERP services provide raw I3X data
2. Aggregator consolidates and provides unified access
3. Client applications query aggregator for intelligence
4. Semantic Kernel processes natural language and invokes appropriate plugins
5. Results formatted and presented to user

### Configuration
- All services use appsettings.json for configuration
- Aggregator endpoint: http://localhost:7000
- OpenAI API key required for LLM features
- PowerShell script (Start-AllServices.ps1) launches all services

## Recent Updates
- Fixed circular dependency in aggregator using lazy loading pattern
- Simplified I3X metadata display (ParentId, HasChildren, NamespaceUri)
- Implemented conversational assistant with plugin architecture
- Fixed type conversion issues in EquipmentPlugin for JsonElement handling
- Cleaned up unused classes and configuration sections