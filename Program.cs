using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Security.Claims;
using System.Text;
using WebApplication1.Data;
using WebApplication1.Hubs;
using WebApplication1.Repositories;
using WebApplication1.Seeding;
using WebApplication1.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Kết nối database
var connectionString =
    @"Server=(localdb)\MSSQLLocalDB;Database=Web2;Integrated Security=True;TrustServerCertificate=True";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

// 2. Controllers
builder.Services.AddControllers();

// 3. Dependency Injection
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IModulePermissionService, ModulePermissionService>();
builder.Services.AddScoped<IWarrantyRequestService, WarrantyRequestService>();
builder.Services.AddScoped<IWarrantyChatService, WarrantyChatService>();

// SignalR
builder.Services.AddSignalR();

// 4. Cấu hình JWT
var jwtSettings = builder.Configuration.GetSection("JwtSettings");

var secretKey = jwtSettings["SecretKey"];
var jwtIssuer = jwtSettings["Issuer"];
var jwtAudience = jwtSettings["Audience"];

if (string.IsNullOrWhiteSpace(secretKey))
{
    throw new InvalidOperationException(
        "Thiếu JwtSettings:SecretKey. Hãy cấu hình JwtSettings__SecretKey bằng biến môi trường.");
}

if (string.IsNullOrWhiteSpace(jwtIssuer) ||
    string.IsNullOrWhiteSpace(jwtAudience))
{
    throw new InvalidOperationException(
        "JwtSettings:Issuer và JwtSettings:Audience không được để trống.");
}

if (Encoding.UTF8.GetByteCount(secretKey) < 32)
{
    throw new InvalidOperationException(
        "JwtSettings:SecretKey phải có ít nhất 32 byte.");
}

// 5. Authentication
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme =
            JwtBearerDefaults.AuthenticationScheme;

        options.DefaultChallengeScheme =
            JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,

            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(secretKey)),

            RoleClaimType = ClaimTypes.Role,
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        options.Events = new JwtBearerEvents
        {
            // SignalR gửi JWT qua query string khi kết nối WebSocket
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var requestPath = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) &&
                    requestPath.StartsWithSegments("/hubs/warranty-chat"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            },

            OnTokenValidated = async context =>
            {
                var idValue = context.Principal?
                    .FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (!int.TryParse(idValue, out var userId))
                {
                    context.Fail("Invalid account claim.");
                    return;
                }

                var db = context.HttpContext.RequestServices
                    .GetRequiredService<AppDbContext>();

                var account = await db.Users
                    .AsNoTracking()
                    .Where(user => user.Id == userId)
                    .Select(user => new { user.Role, user.IsActive })
                    .FirstOrDefaultAsync(context.HttpContext.RequestAborted);

                var tokenRole = context.Principal?
                    .FindFirst(ClaimTypes.Role)?.Value;

                if (account is null ||
                    !account.IsActive ||
                    !string.Equals(
                        account.Role,
                        tokenRole,
                        StringComparison.OrdinalIgnoreCase))
                {
                    context.Fail("The account is disabled or its role has changed.");
                }
            }
        };
    });

// 6. Authorization
builder.Services.AddAuthorization();

// 7. Swagger
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "WebApplication1",
        Version = "v1"
    });

    options.AddSecurityDefinition("Bearer",
        new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Nhập JWT Token sau khi Login."
        });

    options.AddSecurityRequirement(
        new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
});

var app = builder.Build();

// 8. Migration và seed dữ liệu
if (app.Environment.IsDevelopment())
{
    await using (var scope = app.Services.CreateAsyncScope())
    {
        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        await db.Database.MigrateAsync();
        await AdminCatalogSeeder.SeedAsync(db);
        await SystemPermissionCatalog.SeedAsync(db);
    }

    await DevelopmentAdminSeeder.SeedAsync(
        app.Services,
        app.Configuration,
        app.Logger);
}

// 9. Swagger UI
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint(
            "/swagger/v1/swagger.json",
            "WebApplication1 v1");

        options.RoutePrefix = "swagger";
    });
}

// 10. Middleware
app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

// 11. SignalR Hub và Controllers
app.MapHub<WarrantyChatHub>("/hubs/warranty-chat");
app.MapControllers();

app.MapGet("/login", () => Results.Redirect("/login.html"));
app.MapGet("/register", () => Results.Redirect("/register.html"));
app.MapGet("/admin/dashboard", () => Results.Redirect("/admin/dashboard.html"));

foreach (var adminModule in new[]
{
    "accounts", "account-roles", "brands", "categories", "products",
    "warehouses", "receipts", "shipments", "inventory",
    "warranty-types", "fault-groups", "service-prices", "travel-prices",
    "warranty-requests", "components", "stations", "station-parts",
    "component-receipts", "allocations", "component-proposals", "dealers"
})
{
    app.MapGet(
        $"/admin/{adminModule}",
        () => Results.Redirect($"/admin/dashboard.html?module={adminModule}"));
}

app.MapGet("/user", () => Results.Redirect("/user/dashboard"));
app.MapGet("/staff/dashboard", () => Results.Redirect("/staff/dashboard.html"));
app.MapGet("/manager/dashboard", () => Results.Redirect("/manager/dashboard.html"));
app.MapGet("/technician/dashboard", () => Results.Redirect("/technician/dashboard.html"));

var userDashboardFile = Path.Combine(
    app.Environment.WebRootPath,
    "user",
    "index.html");

foreach (var userPage in new[]
{
    "dashboard", "products", "warranty-register", "warranty-request",
    "requests", "history", "profile"
})
{
    app.MapGet($"/user/{userPage}", async (HttpContext context) =>
    {
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.Headers.CacheControl = "no-store";
        await context.Response.SendFileAsync(userDashboardFile);
    });
}

// 12. Run
app.Run();