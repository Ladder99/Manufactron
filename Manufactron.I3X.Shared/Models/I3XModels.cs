using System;
using System.Collections.Generic;

namespace Manufactron.I3X.Shared.Models
{
    // Core I3X RFC-compliant models

    public class Namespace
    {
        public string Uri { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Version { get; set; }
    }

    public class ObjectType
    {
        public string ElementId { get; set; }
        public string Name { get; set; }
        public string NamespaceUri { get; set; }
        public string Description { get; set; }
        public List<AttributeDefinition> Attributes { get; set; } = new();
        public List<string> AllowedRelationships { get; set; } = new();
    }

    public class AttributeDefinition
    {
        public string Name { get; set; }
        public string DataType { get; set; }
        public string EngUnit { get; set; }
        public string Description { get; set; }
        public bool IsRequired { get; set; }
        public object DefaultValue { get; set; }
    }

    public class Instance
    {
        public string ElementId { get; set; }
        public string Name { get; set; }
        public string TypeId { get; set; }
        public string ParentId { get; set; }
        public bool HasChildren { get; set; }
        public string NamespaceUri { get; set; }
        public Dictionary<string, object> Attributes { get; set; } = new();
        public Dictionary<string, List<string>> Relationships { get; set; } = new();
        public DateTime LastUpdated { get; set; }
    }

    public class Relationship
    {
        public string SubjectId { get; set; }
        public string PredicateType { get; set; }
        public string ObjectId { get; set; }
        public DateTime EstablishedAt { get; set; }
        public Dictionary<string, object> Properties { get; set; } = new();
    }

    public class ValueUpdate
    {
        public string ElementId { get; set; }
        public Dictionary<string, object> Values { get; set; }
        public DateTime Timestamp { get; set; }
        public string Source { get; set; }
    }

    public class HistoricalValue
    {
        public string ElementId { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, object> Values { get; set; }
        public string Quality { get; set; }
    }

    public class Subscription
    {
        public string Id { get; set; }
        public List<string> ElementIds { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public string CallbackUrl { get; set; }
        public bool IncludeMetadata { get; set; }
    }

    public class I3XUpdate
    {
        public string ElementId { get; set; }
        public Dictionary<string, object> Attributes { get; set; }
        public DateTime Timestamp { get; set; }
        public string UpdateType { get; set; } // "value", "relationship", "metadata"
        public string Source { get; set; } // "ERP", "MES", "SCADA"
        public string EventType { get; set; } // The type of event
        public string UpdatedEntity { get; set; } // The entity that was updated
        public Dictionary<string, object> ChangedAttributes { get; set; } = new();
    }

    // Manufacturing-specific extensions

    public class ManufacturingContext
    {
        public Instance Equipment { get; set; }
        public Instance Line { get; set; }
        public Instance Job { get; set; }
        public Instance Order { get; set; }
        public Instance MaterialBatch { get; set; }
        public Instance Operator { get; set; }
        public List<Instance> UpstreamEquipment { get; set; } = new();
        public List<Instance> DownstreamEquipment { get; set; } = new();
        public Dictionary<string, List<Relationship>> AllRelationships { get; set; } = new();
    }

    public class I3XQueryPath
    {
        public string StartElementId { get; set; }
        public List<QueryStep> Steps { get; set; } = new();

        public class QueryStep
        {
            public string RelationshipType { get; set; }
            public string Direction { get; set; } // "forward" or "reverse"
            public string Filter { get; set; }
            public string TargetType { get; set; }
        }
    }

    // Response models

    public class I3XResponse<T>
    {
        public bool Success { get; set; }
        public T Data { get; set; }
        public string Error { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class PagedResponse<T>
    {
        public List<T> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageSize { get; set; }
        public int PageNumber { get; set; }
        public bool HasMore { get; set; }
    }
}