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
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false; // W produkcji ustaw na true
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

// WA¯NE: UseAuthentication musi byæ przed UseAuthorization
app.UseAuthentication(); // Dodaj to! Odpowiada za odczytanie tokenu i ustawienie u¿ytkownika
app.UseAuthorization(); // Dodaj to (lub upewnij siê, ¿e jest)! Sprawdza atrybuty [Authorize]

app.MapControllers();

app.Run();