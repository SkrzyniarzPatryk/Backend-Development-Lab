using Backend_Development_Lab.Dtos;
using Backend_Development_Lab.Interfaces;
using Backend_Development_Lab.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Backend_Development_Lab.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IConfiguration _configuration;

        public AuthController(IUserService userService, IConfiguration configuration)
        {
            _userService = userService;
            _configuration = configuration;
        }

        [HttpPost("register")]
        [AllowAnonymous] // Każdy może się zarejestrować
        public async Task<IActionResult> Register([FromBody] RegisterDto registerDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var existingUserByUsername = await _userService.GetUserByUsernameAsync(registerDto.Username);
            if (existingUserByUsername != null)
            {
                return Conflict("Username already exists."); // Konflikt - użytkownik istnieje
            }

            var existingUserByEmail = await _userService.GetUserByEmailAsync(registerDto.Email);
            if (existingUserByEmail != null)
            {
                return Conflict("Email already exists."); // Konflikt - email istnieje
            }

            var newUser = await _userService.RegisterUserAsync(registerDto.Username, registerDto.Email, registerDto.Password);

            if (newUser == null)
            {
                // Logika UserService powinna zapobiec temu, jeśli sprawdzenie Conflict zadziałało,
                // ale na wszelki wypadek
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to register user.");
            }

            // Można zwrócić dane użytkownika (bez hasha!) lub tylko status Created/Ok
            // return CreatedAtAction(nameof(GetUser), new { id = newUser.Id }, newUser); // Jeśli masz endpoint GetUser
            return Ok(new { message = "User registered successfully" });
        }

        [HttpPost("login")]
        [AllowAnonymous] // Każdy może próbować się zalogować
        public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Spróbuj znaleźć użytkownika po nazwie lub emailu
            User? user = await _userService.GetUserByUsernameAsync(loginDto.Login)
                         ?? await _userService.GetUserByEmailAsync(loginDto.Login);

            if (user == null)
            {
                return Unauthorized("Invalid credentials."); // Nie znaleziono użytkownika
            }

            // Weryfikuj hasło
            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(loginDto.Password, user.PasswordHash);

            if (!isPasswordValid)
            {
                return Unauthorized("Invalid credentials."); // Błędne hasło
            }

            // Generuj token JWT
            var token = GenerateJwtToken(user);

            return Ok(new LoginResponseDto { Token = token });
        }

        // --- Prywatna metoda do generowania tokenu ---
        private string GenerateJwtToken(User user)
        {
            var jwtSettings = _configuration.GetSection("Jwt");
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"] ?? throw new InvalidOperationException("JWT Key not configured")));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            // Definiowanie Claimów (informacji zawartych w tokenie)
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()), // Identyfikator użytkownika
                new Claim(JwtRegisteredClaimNames.Name, user.Username),     // Nazwa użytkownika
                new Claim(JwtRegisteredClaimNames.Email, user.Email),       // Email
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()) // Unikalny identyfikator tokenu
                // Możesz dodać więcej claimów, np. role
            };

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(1), // Czas ważności tokenu (np. 1 godzina)
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        // Przykład chronionego endpointu - Zwraca dane zalogowanego użytkownika
        [HttpGet("me")]
        [Authorize] // Wymaga ważnego tokenu JWT
        public async Task<IActionResult> GetMyInfo()
        {
            // Pobierz ID użytkownika z claimów w tokenie
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier); // lub JwtRegisteredClaimNames.Sub
            if (userIdClaim == null || !Guid.TryParse(userIdClaim, out Guid userId))
            {
                return Unauthorized("Invalid token."); // Token nie zawiera poprawnego ID
            }

            var user = await _userService.GetUserByIdAsync(userId);
            if (user == null)
            {
                return NotFound("User not found."); // Użytkownik usunięty po wydaniu tokenu?
            }

            // Zwróć bezpieczne dane (bez hasha!)
            return Ok(new { user.Id, user.Username, user.Email });
        }
    }
}
