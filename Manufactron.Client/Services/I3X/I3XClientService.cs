using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Manufactron.I3X.Shared.Interfaces;
using Manufactron.I3X.Shared.Models;
using Manufactron.I3X.Shared.Models.Manufacturing;

namespace Manufactron.Services.I3X
{
    public class I3XClientService : II3XClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _aggregatorUrl;
        private readonly JsonSerializerOptions _jsonOptions;

        public I3XClientService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;

            var i3xConfig = configuration.GetSection("I3XServices");
            _aggregatorUrl = i3xConfig["AggregatorEndpoint"] ?? "http://localhost:7000";

            // Configure JSON serialization to handle camelCase from API
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };
        }

        public async Task<List<Namespace>> GetNamespacesAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_aggregatorUrl}/api/i3x/namespaces");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<List<Namespace>>(json, _jsonOptions) ?? new List<Namespace>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching namespaces from aggregator: {ex.Message}");
            }

            return new List<Namespace>();
        }

        public async Task<Instance> GetObjectAsync(string elementId, bool includeMetadata = false)
        {
            try
            {
                var url = $"{_aggregatorUrl}/api/i3x/objects/{elementId}?includeMetadata={includeMetadata}";
                var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<Instance>(json, _jsonOptions);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching object from aggregator: {ex.Message}");
            }

            return null;
        }

        public async Task<ManufacturingContext> GetManufacturingContextAsync(string elementId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_aggregatorUrl}/api/i3x/context/{elementId}");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<ManufacturingContext>(json, _jsonOptions) ?? new ManufacturingContext();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching manufacturing context from aggregator: {ex.Message}");
            }

            return new ManufacturingContext();
        }


        // Implement remaining interface methods...

        public Task<List<ObjectType>> GetObjectTypesAsync(string namespaceUri = null)
        {
            throw new NotImplementedException();
        }

        public Task<ObjectType> GetObjectTypeAsync(string elementId)
        {
            throw new NotImplementedException();
        }

        public Task<List<Instance>> GetObjectsAsync(string typeId = null, bool includeMetadata = false)
        {
            throw new NotImplementedException();
        }

        public Task<List<Instance>> GetRelationshipsAsync(string elementId, string relationshipType)
        {
            throw new NotImplementedException();
        }

        public Task<List<Instance>> GetChildrenAsync(string elementId, bool includeMetadata = false)
        {
            throw new NotImplementedException();
        }

        public Task<Instance> GetParentAsync(string elementId, bool includeMetadata = false)
        {
            throw new NotImplementedException();
        }

        public Task<Dictionary<string, object>> GetValueAsync(string elementId)
        {
            throw new NotImplementedException();
        }

        public Task<List<HistoricalValue>> GetHistoryAsync(string elementId, DateTime? startTime = null, DateTime? endTime = null, int? maxPoints = null)
        {
            throw new NotImplementedException();
        }

        public Task<bool> UpdateValueAsync(string elementId, Dictionary<string, object> values)
        {
            throw new NotImplementedException();
        }

        public Task<List<ValueUpdate>> UpdateValuesAsync(Dictionary<string, Dictionary<string, object>> updates)
        {
            throw new NotImplementedException();
        }

        public Task<string> CreateSubscriptionAsync(List<string> elementIds, bool includeMetadata = false)
        {
            throw new NotImplementedException();
        }

        public Task<bool> RegisterSubscriptionAsync(string subscriptionId, List<string> elementIds)
        {
            throw new NotImplementedException();
        }

        public Task<bool> UnregisterSubscriptionAsync(string subscriptionId, List<string> elementIds)
        {
            throw new NotImplementedException();
        }

        public Task<bool> DeleteSubscriptionAsync(string subscriptionId)
        {
            throw new NotImplementedException();
        }

        public IAsyncEnumerable<I3XUpdate> StreamUpdatesAsync(string subscriptionId)
        {
            throw new NotImplementedException();
        }

        // Helper method for simplified subscription
        public async IAsyncEnumerable<I3XUpdate> SubscribeToUpdatesAsync(
            string elementId, 
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // Create a subscription for the single element
            var subscriptionId = await CreateSubscriptionAsync(new List<string> { elementId }, true);
            
            try
            {
                // Stream updates
                await foreach (var update in StreamUpdatesAsync(subscriptionId).WithCancellation(cancellationToken))
                {
                    yield return update;
                }
            }
            finally
            {
                // Clean up subscription
                await DeleteSubscriptionAsync(subscriptionId);
            }
        }

        public Task<List<Instance>> TraverseAsync(I3XQueryPath path)
        {
            throw new NotImplementedException();
        }
    }
}