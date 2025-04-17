using System.ComponentModel.DataAnnotations;

namespace Backend_Development_Lab.Dtos
{
    public class RegisterDto
    {
        [Required]
        [MinLength(3)]
        public required string Username { get; set; }

        [Required]
        [EmailAddress]
        public required string Email { get; set; }

        [Required]
        [MinLength(6)] // Wymagaj minimalnej długości hasła
        public required string Password { get; set; }
    }

    public class LoginDto
    {
        [Required]
        public required string Email { get; set; } // Może być Username lub Email

        [Required]
        public required string Password { get; set; }
    }

    public class LoginResponseDto
    {
        public required string Token { get; set; }
    }
}
