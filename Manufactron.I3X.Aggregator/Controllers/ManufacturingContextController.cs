using Microsoft.AspNetCore.Mvc;
using Manufactron.I3X.Shared.Models;
using Manufactron.I3X.Aggregator.Services;

namespace Manufactron.I3X.Aggregator.Controllers
{
    [ApiController]
    [Route("api/i3x")]
    public class ManufacturingContextController : ControllerBase
    {
        private readonly I3XAggregatorService _aggregatorService;
        private readonly ILogger<ManufacturingContextController> _logger;

        public ManufacturingContextController(
            I3XAggregatorService aggregatorService,
            ILogger<ManufacturingContextController> logger)
        {
            _aggregatorService = aggregatorService;
            _logger = logger;
        }

        /// <summary>
        /// Get complete manufacturing context for any element
        /// </summary>
        [HttpGet("context/{elementId}")]
        public async Task<ActionResult<ManufacturingContext>> GetManufacturingContext(string elementId)
        {
            try
            {
                _logger.LogInformation("Getting manufacturing context for element: {ElementId}", elementId);

                var context = await _aggregatorService.GetManufacturingContextAsync(elementId);

                if (context == null)
                {
                    return NotFound($"Element {elementId} not found");
                }

                return Ok(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting manufacturing context for {ElementId}", elementId);
                return StatusCode(500, "An error occurred while fetching the manufacturing context");
            }
        }
    }
}