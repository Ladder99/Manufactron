using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Manufactron.I3X.Shared.Models;

namespace Manufactron.I3X.Shared.Interfaces
{
    public interface II3XClient
    {
        // Exploratory operations
        Task<List<Namespace>> GetNamespacesAsync();
        Task<List<ObjectType>> GetObjectTypesAsync(string namespaceUri = null);
        Task<ObjectType> GetObjectTypeAsync(string elementId);
        Task<List<Instance>> GetObjectsAsync(string typeId = null, bool includeMetadata = false);
        Task<Instance> GetObjectAsync(string elementId, bool includeMetadata = false);
        Task<List<Instance>> GetRelationshipsAsync(string elementId, string relationshipType);
        
        // Hierarchical operations
        Task<List<Instance>> GetChildrenAsync(string elementId, bool includeMetadata = false);
        Task<Instance> GetParentAsync(string elementId, bool includeMetadata = false);
        
        // Value operations
        Task<Dictionary<string, object>> GetValueAsync(string elementId);
        Task<List<HistoricalValue>> GetHistoryAsync(string elementId, DateTime? startTime = null, DateTime? endTime = null, int? maxPoints = null);
        Task<bool> UpdateValueAsync(string elementId, Dictionary<string, object> values);
        Task<List<ValueUpdate>> UpdateValuesAsync(Dictionary<string, Dictionary<string, object>> updates);
        
        // Subscription operations
        Task<string> CreateSubscriptionAsync(List<string> elementIds, bool includeMetadata = false);
        Task<bool> RegisterSubscriptionAsync(string subscriptionId, List<string> elementIds);
        Task<bool> UnregisterSubscriptionAsync(string subscriptionId, List<string> elementIds);
        Task<bool> DeleteSubscriptionAsync(string subscriptionId);
        IAsyncEnumerable<I3XUpdate> StreamUpdatesAsync(string subscriptionId);
        IAsyncEnumerable<I3XUpdate> SubscribeToUpdatesAsync(string elementId, CancellationToken cancellationToken = default);
        
        // Graph traversal
        Task<List<Instance>> TraverseAsync(I3XQueryPath path);
        
        // Manufacturing context operations
        Task<ManufacturingContext> GetManufacturingContextAsync(string elementId);
    }
}