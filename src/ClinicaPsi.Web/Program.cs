using ClinicaPsi.Application.Services;
using ClinicaPsi.Infrastructure.Data;
using ClinicaPsi.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using System;

// Configurar Npgsql para aceitar DateTime sem UTC
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
AppContext.SetSwitch("Npgsql.DisableDateTimeInfinityConversions", true);

var builder = WebApplication.CreateBuilder(args);

// Adicionar serviços
builder.Services.AddRazorPages();

// Health checks simples
builder.Services.AddHealthChecks();

// Função para converter DATABASE_URL (formato URI) para connection string Npgsql
string ConvertDatabaseUrl(string databaseUrl)
{
    try 
    {
        var uri = new Uri(databaseUrl);
        var userInfo = uri.UserInfo.Split(':');
        return $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};Username={userInfo[0]};Password={userInfo[1]};SSL Mode=Prefer;Trust Server Certificate=true";
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Erro ao converter DATABASE_URL: {ex.Message}");
        return databaseUrl; // Retorna original se falhar
    }
}

// Configurar banco de dados - Prioriza DATABASE_URL (Railway), depois appsettings
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
string connectionString;

if (!string.IsNullOrEmpty(databaseUrl) && databaseUrl.StartsWith("postgresql://"))
{
    Console.WriteLine("DATABASE_URL detectada, convertendo para formato Npgsql...");
    connectionString = ConvertDatabaseUrl(databaseUrl);
    Console.WriteLine($"Connection string convertida: {connectionString.Substring(0, Math.Min(50, connectionString.Length))}...");
}
else
{
    connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Data Source=clinicapsi.db";
}

// Detectar tipo de banco baseado na connection string
var usePostgreSql = connectionString.Contains("Host=") || connectionString.Contains("postgresql");
Console.WriteLine($"Usando PostgreSQL: {usePostgreSql}");

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (usePostgreSql)
    {
        options.UseNpgsql(connectionString);
    }
    else
    {
        options.UseSqlite(connectionString);
    }
});

// Configurar Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 6;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = false;
    options.SignIn.RequireConfirmedPhoneNumber = false;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// Configurar cookies de autenticacao
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(24);
    options.SlidingExpiration = true;
});

// Configurar policies de autorizacao
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("PsicologoPolicy", policy =>
        policy.RequireRole("Psicologo", "Admin"));
    options.AddPolicy("AdminPolicy", policy =>
        policy.RequireRole("Admin"));
    options.AddPolicy("ClientePolicy", policy =>
        policy.RequireRole("Cliente"));
});

// Servicos de aplicacao - comentar se causar problemas
try {
    builder.Services.AddScoped<PacienteService>();
    builder.Services.AddScoped<ConsultaService>();
    builder.Services.AddScoped<PsicologoService>();
    builder.Services.AddScoped<ProntuarioService>();
    builder.Services.AddScoped<AuditoriaService>();
    builder.Services.AddScoped<NotificacaoService>();
    builder.Services.AddScoped<PdfService>();
    builder.Services.AddScoped<ConfiguracaoService>();
} catch { }

var app = builder.Build();

// Inicializar banco de dados e dados padrao
try
{
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();     
        await DbInitializer.SeedAsync(context, userManager, roleManager);
    }
}
catch (Exception ex)
{
    Console.WriteLine($"ERRO CRITICO na inicializacao do banco: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");
    // Continua mesmo com erro para poder ver logs
}

// Configurar pipeline HTTP
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Health check endpoint
app.MapHealthChecks("/health");

// Endpoint raiz
app.MapGet("/", () => Results.Redirect("/Account/Login"));

app.MapRazorPages();

app.Run();
