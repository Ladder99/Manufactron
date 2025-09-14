namespace Manufactron.Memory;

public interface IManufacturingMemory
{
    Task StoreKnowledgeAsync(string collection, string id, string text, string description);
    Task<List<MemoryResult>> QueryKnowledgeAsync(string query, int limit = 5);
    Task StoreIncidentResolutionAsync(string incidentId, string issue, string resolution);
    Task<List<MemoryResult>> GetSimilarIncidentsAsync(string issue, int limit = 3);
}

public class MemoryResult
{
    public string Id { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double Relevance { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}