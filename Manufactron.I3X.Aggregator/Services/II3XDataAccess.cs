using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Manufactron.I3X.Shared.Models;

namespace Manufactron.I3X.Aggregator.Services
{
    public interface II3XDataAccess
    {
        Task<List<Namespace>> GetNamespacesAsync();
        Task<List<ObjectType>> GetObjectTypesAsync(string namespaceUri = null);
        Task<ObjectType> GetObjectTypeAsync(string elementId);
        Task<List<Instance>> GetObjectsAsync(string typeId = null, bool includeMetadata = false);
        Task<Instance> GetObjectAsync(string elementId, bool includeMetadata = false);
        Task<List<Instance>> GetRelationshipsAsync(string elementId, string relationshipType);
        Task<List<Instance>> GetChildrenAsync(string elementId, bool includeMetadata = false);
        Task<Instance> GetParentAsync(string elementId, bool includeMetadata = false);
        Task<Dictionary<string, object>> GetValueAsync(string elementId);
        Task<List<HistoricalValue>> GetHistoryAsync(string elementId, DateTime? startTime = null, DateTime? endTime = null, int? maxPoints = null);
        Task<bool> UpdateValueAsync(string elementId, Dictionary<string, object> values);
    }
}