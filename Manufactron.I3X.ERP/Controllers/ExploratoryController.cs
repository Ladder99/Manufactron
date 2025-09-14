using Microsoft.AspNetCore.Mvc;
using Manufactron.I3X.Shared.Interfaces;
using Manufactron.I3X.Shared.Models;

namespace Manufactron.I3X.ERP.Controllers
{
    [ApiController]
    [Route("api/i3x")]
    public class ExploratoryController : ControllerBase
    {
        private readonly II3XDataSource _dataSource;

        public ExploratoryController(II3XDataSource dataSource)
        {
            _dataSource = dataSource;
        }

        [HttpGet("namespaces")]
        public async Task<IActionResult> GetNamespaces()
        {
            var namespaces = await _dataSource.GetNamespacesAsync();
            return Ok(namespaces);
        }

        [HttpGet("types")]
        public async Task<IActionResult> GetObjectTypes([FromQuery] string namespaceUri = null)
        {
            var types = await _dataSource.GetObjectTypesAsync(namespaceUri);
            return Ok(types);
        }

        [HttpGet("types/{elementId}")]
        public async Task<IActionResult> GetObjectType(string elementId)
        {
            var type = await _dataSource.GetObjectTypeByIdAsync(elementId);
            if (type == null)
                return NotFound();
            return Ok(type);
        }

        [HttpGet("objects")]
        public async Task<IActionResult> GetObjects(
            [FromQuery] string typeId = null,
            [FromQuery] int limit = 100,
            [FromQuery] int offset = 0,
            [FromQuery] bool includeMetadata = false)
        {
            var objects = await _dataSource.GetInstancesAsync(typeId, limit, offset);

            if (!includeMetadata)
            {
                // Remove metadata if not requested
                objects.ForEach(o => o.LastUpdated = default);
            }

            return Ok(objects);
        }

        [HttpGet("objects/{elementId}")]
        public async Task<IActionResult> GetObject(string elementId, [FromQuery] bool includeMetadata = false)
        {
            var obj = await _dataSource.GetInstanceByIdAsync(elementId);
            if (obj == null)
                return NotFound();

            if (!includeMetadata)
            {
                obj.LastUpdated = default;
            }

            return Ok(obj);
        }

        [HttpGet("relationships/{elementId}/{relationshipType}")]
        public async Task<IActionResult> GetRelationships(string elementId, string relationshipType)
        {
            var related = await _dataSource.GetRelatedInstancesAsync(elementId, relationshipType);
            return Ok(related);
        }

        [HttpGet("relationships")]
        public async Task<IActionResult> GetRelationshipTypes([FromQuery] string type = "all")
        {
            if (type == "hierarchical")
            {
                var hierarchical = await _dataSource.GetHierarchicalRelationshipsAsync();
                return Ok(hierarchical);
            }
            else if (type == "non-hierarchical")
            {
                var nonHierarchical = await _dataSource.GetNonHierarchicalRelationshipsAsync();
                return Ok(nonHierarchical);
            }
            else
            {
                var hierarchical = await _dataSource.GetHierarchicalRelationshipsAsync();
                var nonHierarchical = await _dataSource.GetNonHierarchicalRelationshipsAsync();
                var all = hierarchical.Concat(nonHierarchical).Distinct().ToList();
                return Ok(all);
            }
        }

        [HttpGet("objects/{elementId}/children")]
        public async Task<IActionResult> GetChildren(string elementId, [FromQuery] bool includeMetadata = false)
        {
            var children = await _dataSource.GetChildrenAsync(elementId);

            if (!includeMetadata)
            {
                children.ForEach(c => c.LastUpdated = default);
            }

            return Ok(children);
        }

        [HttpGet("objects/{elementId}/parent")]
        public async Task<IActionResult> GetParent(string elementId, [FromQuery] bool includeMetadata = false)
        {
            var parent = await _dataSource.GetParentAsync(elementId);
            if (parent == null)
                return NotFound();

            if (!includeMetadata)
            {
                parent.LastUpdated = default;
            }

            return Ok(parent);
        }
    }
}