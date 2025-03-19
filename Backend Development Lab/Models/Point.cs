namespace Backend_Development_Lab.Models
{
    public class Point
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; }
        public string? Description { get; set; }
        public string? Category { get; set; }
        public List<string>? Position { get; set; }
        public List<string>? Images { get; set; }
        public string? UserId { get; set; }
        public double Rating { get; set; } = 0.0;
        public int Reviews { get; set; } = 0;
    }
}
