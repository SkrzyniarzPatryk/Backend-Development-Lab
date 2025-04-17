using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Backend_Development_Lab.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;

namespace Backend_Development_Lab.Controllers
{
    [ApiController]
    [Route("api/points")]
    public class PointsController : ControllerBase
    {
        private static List<Point> points = new List<Point>();
        static PointsController()
        {
            for (int i = 1; i <= 20; i++)
            {
                points.Add(new Point
                {
                    Id = Guid.NewGuid(),
                    Name = $"Sample Point {i}",
                    Description = $"This is sample point {i}.",
                    Category = $"Category{i % 3 + 1}",
                    Position = new List<string> { $"Position{i}" },
                    Images = new List<string> { $"image{i}.jpg" },
                    UserId = $"user{i}",
                    Rating = 4.0 + (i % 5) * 0.1,
                    Reviews = 5 + i
                });
            }
        }

        [HttpGet]
        [Authorize]
        public IActionResult GetAllPoints()
        {
            var userNameClaim = User.FindFirst(ClaimTypes.Name);
            var userEmaClaim = User.FindFirstValue(ClaimTypes.Email);
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);


            var tokken = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
            var decodedToken = new JwtSecurityTokenHandler().ReadJwtToken(tokken);


            Console.WriteLine($"Token: {tokken} \nPayload: {decodedToken}");

            return Ok(new { punkty = points, 
                user = new { 
                    username = userNameClaim,
                    id = userIdClaim,
                    email = userEmaClaim
            } });
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
