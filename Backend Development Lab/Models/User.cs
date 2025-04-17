namespace Backend_Development_Lab.Models
{
    public class User
    {
        public Guid Id { get; set; }
        public string? Username { get; set; }
        public required string Email { get; set; }
        public string? PasswordHash { get; set; }
        public string? ExternalProvider { get; set; }
        public string? ExternalId { get; set; }
    }
}
