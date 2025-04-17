using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Backend_Development_Lab.Dtos;
using Backend_Development_Lab.Interfaces;
using Backend_Development_Lab.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

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
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] RegisterDto registerDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            //var existingUserByUsername = await _userService.GetUserByUsernameAsync(registerDto.Username);
            //if (existingUserByUsername != null)
            //{
            //    return Conflict("Username already exists.");
            //}

            var existingUserByEmail = await _userService.GetUserByEmailAsync(registerDto.Email);
            if (existingUserByEmail != null)
            {
                return Conflict("Email already exists.");
            }

            var newUser = await _userService.RegisterUserAsync(registerDto.Username, registerDto.Email, registerDto.Password);

            if (newUser == null)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to register user.");
            }

            // return CreatedAtAction(nameof(GetUser), new { id = newUser.Id }, newUser);
            return Ok(new { message = "User registered successfully" });
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            User? user = await _userService.GetUserByEmailAsync(loginDto.Email);

            if (user == null)
            {
                return Unauthorized("Invalid credentials.");
            }

            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(loginDto.Password, user.PasswordHash);

            if (!isPasswordValid)
            {
                return Unauthorized("Invalid credentials.");
            }

            var token = GenerateJwtToken(user);
            return Ok(new LoginResponseDto { Token = token });
        }

        [HttpGet("list")]
        [AllowAnonymous]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _userService.GetAllUsersAsync();
            return Ok(users);
        }

        [HttpGet("external-login")]
        [AllowAnonymous]
        public IActionResult ExternalLogin([FromQuery] string provider, [FromQuery] string? returnUrl = null)
        {
            // Sprawdź, czy dostawca jest obsługiwany (na razie tylko Google)
            if (string.IsNullOrEmpty(provider) || !provider.Equals(GoogleDefaults.AuthenticationScheme, StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("Unsupported external provider.");
            }

            // Ścieżka, na którą użytkownik zostanie przekierowany w naszej aplikacji
            // PO udanym logowaniu u dostawcy zewnętrznego.
            // Tutaj middleware przechwyci żądanie i wymieni kod na token.
            // My następnie obsłużymy to w ExternalLoginCallback.
            var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Auth", new { ReturnUrl = returnUrl });

            // Właściwości przekazywane do dostawcy zewnętrznego
            var properties = new AuthenticationProperties
            {
                RedirectUri = redirectUrl, // Gdzie Google ma odesłać użytkownika PO AUTORYZACJI
                // Można tu dodać inne właściwości, np. do przekazania stanu
            };

            // Wywołaj wyzwanie dla schematu Google.
            // To spowoduje przekierowanie użytkownika na stronę logowania Google.
            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }

        // 2. Endpoint obsługujący callback od dostawcy zewnętrznego
        //    Ścieżka tego endpointu MUSI pasować do jednej z "Authorized redirect URIs"
        //    skonfigurowanych w Google Cloud Console ORAZ do ścieżki,
        //    na którą nasłuchuje middleware Google (domyślnie /signin-google).
        //    My użyjemy JAWNEGO callbacku zdefiniowanego w ExternalLogin.
        [HttpGet("external-callback")]
        [AllowAnonymous] // Middleware samo w sobie uwierzytelnia na podstawie ciasteczka
        public async Task<IActionResult> ExternalLoginCallback([FromQuery] string? error, string? returnUrl = null)
        {
            if (!string.IsNullOrEmpty(error))
            {
                return BadRequest(error);
            }
            // Pobierz informacje o użytkowniku z zewnętrznego ciasteczka uwierzytelniającego
            // To ciasteczko zostało utworzone przez middleware Google po udanym logowaniu
            // i ustawione jako DefaultSignInScheme (CookieAuthenticationDefaults.AuthenticationScheme)
            var authenticateResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            if (!authenticateResult.Succeeded || authenticateResult?.Principal == null)
            {
                // Coś poszło nie tak podczas logowania zewnętrznego
                return BadRequest("External authentication failed.");
                // Można dodać logowanie błędu: authenticateResult?.Failure?.Message
            }

            var accessToken = authenticateResult.Properties.GetTokenValue("access_token");
            var refreshToken = authenticateResult.Properties.GetTokenValue("refresh_token");

            if (!string.IsNullOrEmpty(accessToken))
            {
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                var userInfoResponse = await httpClient.GetAsync("https://www.googleapis.com/oauth2/v3/userinfo");

                if (userInfoResponse.IsSuccessStatusCode)
                {
                    var userInfoJson = await userInfoResponse.Content.ReadAsStringAsync();
                    // Tutaj zdeserializuj userInfoJson (np. używając System.Text.Json)
                    // i uzyskaj dostęp do pól jak 'picture', 'name' etc.
                    Console.WriteLine($"User Info from Google: {userInfoJson}");
                    // np. var userInfo = System.Text.Json.JsonSerializer.Deserialize<GoogleUserInfo>(userInfoJson);
                    // string pictureUrl = userInfo?.picture;
                }
                else
                {
                    Console.WriteLine($"Failed to get user info: {userInfoResponse.StatusCode}");
                }
            }

            // Pobierz oświadczenia (claims) od zewnętrznego dostawcy
            var externalPrincipal = authenticateResult.Principal;
            Console.WriteLine($"Decoded Token: {externalPrincipal}");
            var externalProvider = externalPrincipal.FindFirstValue(ClaimTypes.AuthenticationMethod) ?? externalPrincipal.Identity?.AuthenticationType; // Powinno być np. "Google"
            var externalUserId = externalPrincipal.FindFirstValue(ClaimTypes.NameIdentifier); // Unikalny ID użytkownika u dostawcy
            var email = externalPrincipal.FindFirstValue(ClaimTypes.Email);
            var username = externalPrincipal.FindFirstValue(ClaimTypes.Name) ?? email?.Split('@')[0]; // Użyj imienia lub części emaila jako username

            if (string.IsNullOrEmpty(externalUserId) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(externalProvider) || string.IsNullOrEmpty(username))
            {
                // Dostawca nie zwrócił wymaganych informacji
                return BadRequest("Could not retrieve required information from external provider.");
            }

            // Znajdź lub zarejestruj użytkownika w swoim systemie
            var user = await _userService.GetUserByExternalIdAsync(externalProvider, externalUserId);

            if (user == null)
            {
                // Użytkownik loguje się po raz pierwszy przez tego dostawcę
                // Sprawdź, czy email nie jest już zajęty przez konto lokalne (opcjonalne, zależy od logiki biznesowej)
                var existingLocalUser = await _userService.GetUserByEmailAsync(email);
                if (existingLocalUser != null)
                {
                    // Można zwrócić błąd, albo spróbować połączyć konta (bardziej skomplikowane)
                    return Conflict($"An account with email {email} already exists. Please log in using your password or link your accounts.");
                }


                // Zarejestruj nowego użytkownika bez hasła
                user = await _userService.RegisterUserAsync(username, email, null, externalProvider, externalUserId);
                if (user == null)
                {
                    // Problem z rejestracją
                    return StatusCode(StatusCodes.Status500InternalServerError, "Could not register the user.");
                }
            }

            // W tym momencie mamy użytkownika (istniejącego lub nowo zarejestrowanego)
            // Generujemy dla niego nasz własny token JWT
            var jwtToken = GenerateJwtToken(user); // Użyj istniejącej metody

            // WAŻNE: Wyloguj użytkownika z tymczasowego schematu ciasteczkowego
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // Zwróć token JWT do klienta (np. w ciele odpowiedzi)
            // Klient (np. SPA) powinien zapisać ten token i używać go do dalszych żądań API
            return Ok(new LoginResponseDto { Token = jwtToken });

            // Opcjonalnie: Jeśli był podany returnUrl, można by przekierować użytkownika
            // w aplikacji klienckiej (np. SPA) na ten URL, przekazując token
            // np. przez parametr query (?token=...) lub fragment (#token=...),
            // ale zwracanie go w ciele jest często bezpieczniejsze dla API.
            // if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            // {
            //     // Przekierowanie w stylu SPA - wymaga obsługi po stronie klienta
            //     // return Redirect($"{returnUrl}?token={jwtToken}");
            // }
        }
    
        private string GenerateJwtToken(User user)
        {
            var jwtSettings = _configuration.GetSection("Jwt");
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"] ?? throw new InvalidOperationException("JWT Key not configured")));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Name, user.Username),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetMyInfo()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim, out Guid userId))
            {
                return Unauthorized("Invalid token.");
            }

            var user = await _userService.GetUserByIdAsync(userId);
            if (user == null)
            {
                return NotFound("User not found.");
            }

            return Ok(new { user.Id, user.Username, user.Email });
        }
    }
}
