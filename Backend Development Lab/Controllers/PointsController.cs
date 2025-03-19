using Backend_Development_Lab.Models;
using Microsoft.AspNetCore.Mvc;

namespace Backend_Development_Lab.Controllers
{
    [ApiController]
    [Route("api/points")]
    public class PointsController : ControllerBase
    {
        private static List<Point> points = new List<Point>();

        [HttpGet]
        public IActionResult GetAllPoints()
        {
            return Ok(points);
        }

        [HttpGet("{id}")]
        public IActionResult GetPointById(Guid id)
        {
            var point = points.FirstOrDefault(p => p.Id == id);
            if (point == null) return NotFound();
            return Ok(point);
        }

        [HttpPost]
        public IActionResult CreatePoint([FromBody] Point newPoint)
        {
            if (newPoint == null) return BadRequest();
            points.Add(newPoint);
            return CreatedAtAction(nameof(GetPointById), new { id = newPoint.Id }, newPoint);
        }

        [HttpPut("{id}")]
        public IActionResult UpdatePoint(Guid id, [FromBody] Point updatedPoint)
        {
            var point = points.FirstOrDefault(p => p.Id == id);
            if (point == null) return NotFound();

            point.Name = updatedPoint.Name;
            point.Description = updatedPoint.Description;
            point.Category = updatedPoint.Category;
            point.Position = updatedPoint.Position;
            point.Images = updatedPoint.Images;
            point.UserId = updatedPoint.UserId;
            point.Rating = updatedPoint.Rating;
            point.Reviews = updatedPoint.Reviews;

            return NoContent();
        }

        [HttpDelete("{id}")]
        public IActionResult DeletePoint(Guid id)
        {
            var point = points.FirstOrDefault(p => p.Id == id);
            if (point == null) return NotFound();

            points.Remove(point);
            return NoContent();
        }
    }
}
