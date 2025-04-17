//using Backend_Development_Lab.Interfaces;
//using System.Text;
//using Backend_Development_Lab.Middleware;
//using Backend_Development_Lab.Services;
//using Microsoft.AspNetCore.Authentication.JwtBearer;
//using Microsoft.IdentityModel.Tokens;

//var builder = WebApplication.CreateBuilder(args);

//// Add services to the container.

//builder.Services.AddControllers();
//// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
//builder.Services.AddEndpointsApiExplorer();
//builder.Services.AddSwaggerGen();

//var app = builder.Build();

//app.UseMiddleware<ApiKeyMiddleware>();

//// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
//    app.UseSwagger();
//    app.UseSwaggerUI();
//}

//app.UseHttpsRedirection();

//app.UseAuthorization();

//app.MapControllers();

//app.Run();



//==================================
using Backend_Development_Lab.Interfaces;
using Backend_Development_Lab.Middleware;
using Backend_Development_Lab.Services; // Dodaj using dla Services
using Microsoft.AspNetCore.Authentication.JwtBearer; // Dodaj using
using Microsoft.IdentityModel.Tokens; // Dodaj using
using System.Text; // Dodaj using
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// --- Konfiguracja JWT ---
var jwtSettings = builder.Configuration.GetSection("Jwt");
var key = Encoding.ASCII.GetBytes(jwtSettings["Key"] ?? throw new InvalidOperationException("JWT Key not configured"));

// --- Rejestracja Us³ug ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Konfiguracja Swaggera do obs³ugi autoryzacji Bearer (JWT)
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Please enter JWT with Bearer into field",
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement {
    {
        new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Reference = new Microsoft.OpenApi.Models.OpenApiReference
            {
                Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                Id = "Bearer"
            }
        },
        new string[] { }
    }});
});

// Rejestracja serwisu u¿ytkowników jako Singleton (bo u¿ywa statycznej listy)
builder.Services.AddSingleton<IUserService, UserService>();

// --- Konfiguracja Uwierzytelniania JWT ---
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.Cookie.Name = "ExternalLoginCookie";
    options.ExpireTimeSpan = TimeSpan.FromMinutes(5); // Krótki czas ¿ycia
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // Wymagaj HTTPS
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = builder.Environment.IsProduction(); // W produkcji ustaw na true
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true, // Sprawdza, czy issuer w tokenie zgadza siê z oczekiwanym
        ValidIssuer = jwtSettings["Issuer"],
        ValidateAudience = true, // Sprawdza, czy audience w tokenie zgadza siê z oczekiwanym
        ValidAudience = jwtSettings["Audience"],
        ValidateLifetime = true, // Sprawdza, czy token nie wygas³
        ClockSkew = TimeSpan.Zero // Brak tolerancji czasowej przy sprawdzaniu wygaœniêcia
    };
})
.AddGoogle(options =>
{
    options.ClientId = builder.Configuration["Authentication:Google:ClientId"] ?? throw new InvalidOperationException("Google ClientId not configured");
    options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? throw new InvalidOperationException("Google ClientSecret not configured");

    // Opcjonalnie: Poproœ o dodatkowe zakresy (scopes)
    options.Scope.Add("profile"); // Domyœlnie zawiera openid, email, profile
    options.Scope.Add("openid");
    options.Scope.Add("email");

    // Opcjonalnie: Zapisz tokeny otrzymane od Google (access_token, refresh_token)
    // Przydatne, jeœli chcesz póŸniej wywo³ywaæ API Google w imieniu u¿ytkownika
    options.SaveTokens = true;

    // Okreœlamy, ¿e po udanym logowaniu Google, u¿ytkownik ma byæ zalogowany
    // do naszego systemu za pomoc¹ schematu ciasteczkowego (tymczasowo)
    options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    //options.CallbackPath = "/api/Auth/external-callback";

    options.Events = new Microsoft.AspNetCore.Authentication.OAuth.OAuthEvents
    {
        OnRemoteFailure = context =>
        {
            if (context.Failure?.Message != null && context.Failure.Message.Contains("access_denied", StringComparison.OrdinalIgnoreCase))
            {
                // U¿ytkownik anulowa³ autoryzacjê
                context.Response.Redirect("/api/Auth/external-callback?error=ACCES_DENIED2");
                context.HandleResponse(); // Zatrzymaj dalsze przetwarzanie
            }
            else
            {
                // Inny b³¹d, np. niepoprawne dane logowania
                context.Response.Redirect("/api/Auth/external-callback?error=ACCES_DENIED1");
                context.HandleResponse(); // Zatrzymaj dalsze przetwarzanie
            }
            return Task.CompletedTask;
        }
    };
});

// Rejestracja Autoryzacji (ju¿ pewnie masz, ale upewnij siê)
builder.Services.AddAuthorization();


var app = builder.Build();

// --- Konfiguracja Pipeline HTTP ---

// Komentujemy lub usuwamy ApiKeyMiddleware, chyba ¿e chcesz u¿ywaæ obu mechanizmów
// app.UseMiddleware<ApiKeyMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        // Umo¿liwia wpisanie tokenu w interfejsie Swaggera
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
    });
}

app.UseHttpsRedirection();

app.UseRouting();// Routing musi byæ przed Authentication/Authorization

// WA¯NE: UseAuthentication musi byæ przed UseAuthorization
app.UseAuthentication(); // Odpowiada za odczytanie tokenu i ustawienie u¿ytkownika
app.UseAuthorization(); // Sprawdza atrybuty [Authorize]

app.MapControllers();

app.Run();