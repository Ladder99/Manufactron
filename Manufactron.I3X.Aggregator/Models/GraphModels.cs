using System.Collections.Generic;

namespace Manufactron.I3X.Aggregator.Models
{
    public class ManufacturingGraph
    {
        public Dictionary<string, GraphNode> Nodes { get; set; } = new();
        public List<GraphEdge> Edges { get; set; } = new();
        public Dictionary<string, List<string>> AdjacencyList { get; set; } = new();
        public DateTime LastUpdated { get; set; }
    }

    public class GraphNode
    {
        public string ElementId { get; set; }
        public string Name { get; set; }
        public string TypeId { get; set; }
        public string NamespaceUri { get; set; }
        public string ServiceSource { get; set; } // ERP, MES, SCADA
        public ManufacturingContextType? ContextType { get; set; }
        public Dictionary<string, object> Attributes { get; set; } = new();
        public Dictionary<string, List<string>> Relationships { get; set; } = new();
    }

    public class GraphEdge
    {
        public string FromNodeId { get; set; }
        public string ToNodeId { get; set; }
        public string RelationshipType { get; set; }
        public EdgeDirection Direction { get; set; }
    }

    public enum EdgeDirection
    {
        Forward,
        Reverse,
        Bidirectional
    }

    public enum ManufacturingContextType
    {
        Order,
        Job,
        Line,
        Equipment,
        Operator,
        MaterialBatch,
        Customer,
        MaintenanceTeam,
        QualityInspector,
        Unknown
    }

    public class GraphPath
    {
        public List<string> NodeIds { get; set; } = new();
        public List<string> RelationshipTypes { get; set; } = new();
        public int Length => NodeIds.Count;
        public double Cost { get; set; } = 0;
    }

    public class TypePattern
    {
        public string Pattern { get; set; }
        public ManufacturingContextType ContextType { get; set; }
        public int Priority { get; set; }
    }
}