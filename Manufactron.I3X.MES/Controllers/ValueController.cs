using Microsoft.AspNetCore.Mvc;
using Manufactron.I3X.Shared.Interfaces;
using Manufactron.I3X.Shared.Models;

namespace Manufactron.I3X.MES.Controllers
{
    [ApiController]
    [Route("api/i3x")]
    public class ValueController : ControllerBase
    {
        private readonly II3XDataSource _dataSource;

        public ValueController(II3XDataSource dataSource)
        {
            _dataSource = dataSource;
        }

        [HttpGet("value/{elementId}")]
        public async Task<IActionResult> GetValue(string elementId)
        {
            var values = await _dataSource.GetValueAsync(elementId);
            if (values == null || !values.Any())
                return NotFound();

            return Ok(values);
        }

        [HttpGet("history/{elementId}")]
        public async Task<IActionResult> GetHistory(
            string elementId,
            [FromQuery] DateTime? startTime = null,
            [FromQuery] DateTime? endTime = null,
            [FromQuery] int maxPoints = 1000)
        {
            var start = startTime ?? DateTime.UtcNow.AddDays(-7);
            var end = endTime ?? DateTime.UtcNow;

            var history = await _dataSource.GetHistoryAsync(elementId, start, end, maxPoints);
            return Ok(history);
        }

        [HttpPut("value/{elementId}")]
        public async Task<IActionResult> UpdateValue(string elementId, [FromBody] Dictionary<string, object> values)
        {
            var updates = await _dataSource.UpdateInstanceValuesAsync(
                new List<string> { elementId },
                new List<Dictionary<string, object>> { values });

            if (!updates.Any())
                return NotFound();

            return Ok(updates.First());
        }

        [HttpPut("values")]
        public async Task<IActionResult> UpdateValues([FromBody] List<ValueUpdate> updates)
        {
            var elementIds = updates.Select(u => u.ElementId).ToList();
            var values = updates.Select(u => u.Values).ToList();

            var results = await _dataSource.UpdateInstanceValuesAsync(elementIds, values);
            return Ok(results);
        }
    }
}