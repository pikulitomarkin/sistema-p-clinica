using ClinicaPsi.Application.Services;
using ClinicaPsi.Infrastructure.Data;
using ClinicaPsi.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using System.Text.Json;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

// CRÍTICO: Configurar Npgsql ANTES de tudo para aceitar DateTime sem UTC
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
AppContext.SetSwitch("Npgsql.DisableDateTimeInfinityConversions", true);

var builder = WebApplication.CreateBuilder(args);

// Configurar Data Protection para usar EFS (armazenamento compartilhado)
// Isso garante que múltiplas instâncias possam compartilhar as chaves de criptografia
try
{
    var dataProtectionPath = Path.Combine("/mnt/efs", "DataProtection-Keys");
    if (Directory.Exists("/mnt/efs"))
    {
        Directory.CreateDirectory(dataProtectionPath);
        builder.Services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath))
            .SetApplicationName("ClinicaPsi");
    }
    else
    {
        // Fallback para diretório local se EFS não estiver montado
        builder.Services.AddDataProtection()
            .SetApplicationName("ClinicaPsi");
    }
}
catch
{
    // Em caso de erro, usar configuração padrão
    builder.Services.AddDataProtection()
        .SetApplicationName("ClinicaPsi");
}

// Configurar OpenTelemetry Tracing
var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"] ?? "http://localhost:4318";
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService("ClinicaPsi.Web"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation(options =>
        {
            options.RecordException = true;
            options.Filter = httpContext =>
            {
                // Não rastrear health checks
                return !httpContext.Request.Path.StartsWithSegments("/health");
            };
        })
        .AddHttpClientInstrumentation(options =>
        {
            options.RecordException = true;
        })
        .AddEntityFrameworkCoreInstrumentation(options =>
        {
            options.SetDbStatementForText = true;
            options.SetDbStatementForStoredProcedure = true;
        })
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri(otlpEndpoint);
            options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
        }));

// Adicionar serviços
builder.Services.AddRazorPages();

// Health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>();

// Configurar banco de dados
// Railway fornece DATABASE_URL no formato postgres://user:pass@host:port/db
var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL");

if (!string.IsNullOrEmpty(connectionString))
{
    Console.WriteLine("DATABASE_URL detectada, convertendo para formato Npgsql...");
    
    // Converter de postgres:// para formato Npgsql
    var uri = new Uri(connectionString);
    var npgsqlConnection = $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};Username={uri.UserInfo.Split(':')[0]};Password={uri.UserInfo.Split(':')[1]};SSL Mode=Require;Trust Server Certificate=true";
    connectionString = npgsqlConnection;
    
    Console.WriteLine($"Connection string convertida: {npgsqlConnection.Replace(uri.UserInfo.Split(':')[1], "***")}");
}
else
{
    connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
        ?? "Data Source=clinicapsi.db";
}

// Detectar tipo de banco baseado na connection string
var usePostgreSql = connectionString.Contains("Host=") || (connectionString.Contains("Server=") && connectionString.Contains("Database="));
Console.WriteLine($"Usando PostgreSQL: {usePostgreSql}");
    
builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (usePostgreSql)
    {
        options.UseNpgsql(connectionString)
            .EnableSensitiveDataLogging() // Para debug
            .LogTo(Console.WriteLine); // Log SQL commands
    }
    else
    {
        options.UseSqlite(connectionString);
    }
});

// Configurar Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Configurações de senha
    options.Password.RequiredLength = 6;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    
    // Configurações de usuário
    options.User.RequireUniqueEmail = true;
    
    // Configurações de login
    options.SignIn.RequireConfirmedEmail = false;
    options.SignIn.RequireConfirmedPhoneNumber = false;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// Configurar cookies de autenticação
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(24);
    options.SlidingExpiration = true;
});

// Configurar policies de autorização
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("PsicologoPolicy", policy =>
        policy.RequireRole("Psicologo", "Admin"));
    options.AddPolicy("AdminPolicy", policy =>
        policy.RequireRole("Admin"));
    options.AddPolicy("ClientePolicy", policy =>
        policy.RequireRole("Cliente"));
});

// Serviços de aplicação
builder.Services.AddScoped<PacienteService>();
builder.Services.AddScoped<ConsultaService>();
builder.Services.AddScoped<PsicologoService>();
builder.Services.AddScoped<ProntuarioService>();
builder.Services.AddScoped<AuditoriaService>();
builder.Services.AddScoped<NotificacaoService>();
builder.Services.AddScoped<PdfService>();
builder.Services.AddScoped<ConfiguracaoService>();
builder.Services.AddScoped<WhatsAppService>();
builder.Services.AddScoped<OpenAIService>();
builder.Services.AddScoped<WhatsAppBotService>();
builder.Services.AddScoped<WhatsAppNotificationService>();

// Configurar HttpClient para WhatsApp Web (Venom-Bot)
builder.Services.AddHttpClient<WhatsAppWebService>(client =>
{
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddScoped<WhatsAppWebService>();

// Registrar Background Service para notificações automáticas
builder.Services.AddHostedService<WhatsAppNotificationBackgroundService>();

// Configurar HttpClient para WhatsApp (legado)
builder.Services.AddHttpClient("WhatsApp", client =>
{
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Serviços em background para notificações
builder.Services.AddHostedService<ClinicaPsi.Web.Services.NotificacaoBackgroundService>();
builder.Services.AddHostedService<ClinicaPsi.Web.Services.WhatsAppNotificacaoBackgroundService>();

var app = builder.Build();

// Inicializar banco de dados e dados padrão
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    try
    {
        // Aplicar migrations pendentes automaticamente
        logger.LogInformation("Verificando migrations pendentes...");
        var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
        if (pendingMigrations.Any())
        {
            logger.LogInformation($"Aplicando {pendingMigrations.Count()} migration(s) pendente(s): {string.Join(", ", pendingMigrations)}");
            await context.Database.MigrateAsync();
            logger.LogInformation("✅ Migrations aplicadas com sucesso!");
        }
        else
        {
            logger.LogInformation("Nenhuma migration pendente.");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "❌ Erro ao aplicar migrations: {Message}", ex.Message);
        throw;
    }
    
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    
    await DbInitializer.SeedAsync(context, userManager, roleManager);
}

// Configurar pipeline HTTP
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// COMENTADO: WhatsApp webhook precisa aceitar HTTP  
// app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Health check endpoint (DEVE vir após UseRouting)
app.MapHealthChecks("/health");

// API Controllers (necessário para WhatsAppWebhookController)
app.MapControllers();

app.MapRazorPages();

// Webhook endpoint para WhatsApp
// GET -> validação inicial (hub.challenge)
app.MapGet("/api/whatsapp/webhook", (HttpRequest req) =>
{
    var query = req.Query;
    var mode = query["hub.mode"].ToString();
    var challenge = query["hub.challenge"].ToString();
    var token = query["hub.verify_token"].ToString();

    var expected = builder.Configuration["WhatsApp:VerifyToken"] ?? string.Empty;
    if (!string.IsNullOrEmpty(mode) && mode == "subscribe" && token == expected)
    {
        return Results.Text(challenge);
    }

    return Results.BadRequest();
});

// POST -> recebimento de mensagens; validação opcional por HMAC se AppSecret estiver configurado
app.MapPost("/api/whatsapp/webhook", async (HttpRequest req, IServiceProvider sp) =>
{
    var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("WhatsAppWebhook");

    // Ler body como bytes para permitir verificação de assinatura
    byte[] bodyBytes;
    using (var ms = new MemoryStream())
    {
        await req.Body.CopyToAsync(ms);
        bodyBytes = ms.ToArray();
    }

    // Verificar assinatura (X-Hub-Signature-256) se AppSecret estiver presente
    var appSecret = builder.Configuration["WhatsApp:AppSecret"];
    if (!string.IsNullOrEmpty(appSecret))
    {
        if (!req.Headers.TryGetValue("X-Hub-Signature-256", out var sigHeaders))
        {
            logger.LogWarning("Assinatura ausente no webhook WhatsApp");
            return Results.BadRequest();
        }

        var provided = sigHeaders.FirstOrDefault() ?? string.Empty; // formato: sha256=HEX
        try
        {
            using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(appSecret));
            var computed = hmac.ComputeHash(bodyBytes);
            var computedHex = BitConverter.ToString(computed).Replace("-", string.Empty).ToLowerInvariant();
            if (!provided.EndsWith(computedHex, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("Assinatura inválida no webhook WhatsApp");
                return Results.BadRequest();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro verificando assinatura do webhook");
            return Results.BadRequest();
        }
    }

    try
    {
        using var doc = JsonDocument.Parse(bodyBytes);
        var root = doc.RootElement;

        // Meta/WhatsApp envia objeto complexo; tentamos localizar texto e telefone
        var entry = root.GetProperty("entry")[0];
        var changes = entry.GetProperty("changes")[0];
        var value = changes.GetProperty("value");
        var messages = value.GetProperty("messages")[0];
        var from = messages.GetProperty("from").GetString();
        var text = "";
        if (messages.TryGetProperty("text", out var t))
            text = t.GetProperty("body").GetString() ?? "";

        var bot = sp.GetRequiredService<WhatsAppBotService>();
        _ = Task.Run(() => bot.ProcessIncomingMessageAsync(from ?? string.Empty, text));

        return Results.Ok();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Erro processando webhook WhatsApp");
        return Results.BadRequest();
    }
});

// Aplicar migrations automaticamente ao iniciar (útil para Railway/AWS)
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    try
    {
        logger.LogInformation("Aplicando migrations pendentes ao banco de dados...");
        context.Database.Migrate();
        logger.LogInformation("Migrations aplicadas com sucesso!");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Erro ao aplicar migrations. O app continuará, mas pode haver problemas.");
    }
}

app.Run();
