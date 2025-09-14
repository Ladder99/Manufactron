using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Manufactron.I3X.Shared.Models;

namespace Manufactron.I3X.Shared.Interfaces
{
    public interface II3XDataSource
    {
        // Lifecycle management
        Task StartAsync(Func<I3XUpdate, Task> updateCallback = null);
        Task StopAsync();

        // Exploratory Methods (RFC 4.1.x)
        Task<List<Namespace>> GetNamespacesAsync();
        Task<List<ObjectType>> GetObjectTypesAsync(string namespaceUri = null);
        Task<ObjectType> GetObjectTypeByIdAsync(string elementId);
        Task<List<Instance>> GetInstancesAsync(string typeId = null, int? limit = null, int? offset = null);
        Task<Instance> GetInstanceByIdAsync(string elementId);
        Task<List<Instance>> GetRelatedInstancesAsync(string elementId, string relationshipType);
        Task<List<string>> GetHierarchicalRelationshipsAsync();
        Task<List<string>> GetNonHierarchicalRelationshipsAsync();
        Task<List<Instance>> GetChildrenAsync(string elementId);
        Task<Instance> GetParentAsync(string elementId);

        // Value Methods (RFC 4.2.1.x)
        Task<Dictionary<string, object>> GetValueAsync(string elementId);
        Task<List<HistoricalValue>> GetHistoryAsync(
            string elementId,
            DateTime startTime,
            DateTime endTime,
            int? maxPoints = null);

        // Update Methods (RFC 4.2.2.x)
        Task<List<ValueUpdate>> UpdateInstanceValuesAsync(
            List<string> elementIds,
            List<Dictionary<string, object>> values);

        // Relationship Methods
        Task<List<Relationship>> GetRelationshipsAsync(string elementId, string predicateType = null);
        Task<Relationship> CreateRelationshipAsync(string subjectId, string predicateType, string objectId);
        Task<bool> DeleteRelationshipAsync(string subjectId, string predicateType, string objectId);

        // Query Methods
        Task<ManufacturingContext> BuildManufacturingContextAsync(string elementId);
        Task<List<Instance>> TraverseGraphAsync(I3XQueryPath queryPath);

        // Subscription Support
        Task<string> CreateSubscriptionAsync(List<string> elementIds, bool includeMetadata = false);
        Task<bool> DeleteSubscriptionAsync(string subscriptionId);
        IAsyncEnumerable<I3XUpdate> SubscribeToUpdatesAsync(List<string> elementIds);
    }
}