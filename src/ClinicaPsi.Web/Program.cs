using ClinicaPsi.Application.Services;
using ClinicaPsi.Infrastructure.Data;
using ClinicaPsi.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Localization;
using System.Globalization;
using System.Text.Json;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

// CRÍTICO: Configurar Npgsql ANTES de tudo para aceitar DateTime sem UTC
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
AppContext.SetSwitch("Npgsql.DisableDateTimeInfinityConversions", true);

// Cultura padrão pt-BR (moeda R$, datas e números brasileiros)
var culturaPtBr = new CultureInfo("pt-BR");
CultureInfo.DefaultThreadCurrentCulture = culturaPtBr;
CultureInfo.DefaultThreadCurrentUICulture = culturaPtBr;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture(culturaPtBr);
    options.SupportedCultures = new[] { culturaPtBr };
    options.SupportedUICultures = new[] { culturaPtBr };
    // App 100% BR: não deixar Accept-Language do browser sobrescrever (evita ¤ / $)
    options.RequestCultureProviders.Clear();
});

// Configurar Data Protection com armazenamento persistente quando disponivel
// Prioridade: EFS (AWS) -> /app/keys (Docker VPS) -> padrao em memoria/temp
try
{
    string? keysPath = null;
    if (Directory.Exists("/mnt/efs"))
    {
        keysPath = Path.Combine("/mnt/efs", "DataProtection-Keys");
    }
    else if (Directory.Exists("/app/keys"))
    {
        keysPath = "/app/keys";
    }

    if (keysPath is not null)
    {
        Directory.CreateDirectory(keysPath);
        builder.Services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
            .SetApplicationName("ClinicaPsi");
    }
    else
    {
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
builder.Services.AddRazorPages(options =>
{
    options.Conventions.ConfigureFilter(new ClinicaPsi.Web.Filters.PsicologoValidacaoPageFilter());
});
builder.Services.AddSignalR();

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

// Adicionar DbContextFactory para uso em background services e webhooks
builder.Services.AddDbContextFactory<AppDbContext>(options =>
{
    if (usePostgreSql)
    {
        options.UseNpgsql(connectionString)
            .EnableSensitiveDataLogging()
            .LogTo(Console.WriteLine);
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
builder.Services.AddScoped<UsuarioPsicologoSyncService>();
builder.Services.AddScoped<UsuarioPacienteOnboardingService>();
builder.Services.AddScoped<ProntuarioService>();
builder.Services.AddScoped<VideoConsultaService>();
builder.Services.AddScoped<AuditoriaService>();
builder.Services.AddScoped<NotificacaoService>();
builder.Services.AddScoped<PdfService>();
builder.Services.AddScoped<ConfiguracaoService>();
builder.Services.AddScoped<ClinicaPsi.Web.Services.FotoPerfilService>();
builder.Services.AddScoped<ClinicaPsi.Web.Services.DocumentoCadastroService>();
builder.Services.AddScoped<WhatsAppService>();
builder.Services.AddScoped<OpenAIService>();
builder.Services.AddScoped<WhatsAppBotService>();
builder.Services.AddScoped<WhatsAppNotificationService>();

// E-mail via Resend (API key só por env/secret — nunca no git)
builder.Services.Configure<ClinicaPsi.Application.Services.Email.EmailOptions>(options =>
{
    builder.Configuration.GetSection(ClinicaPsi.Application.Services.Email.EmailOptions.SectionName).Bind(options);
    options.ApiKey ??= builder.Configuration["RESEND_API_KEY"]
        ?? builder.Configuration["Email:ApiKey"];
    if (string.IsNullOrWhiteSpace(options.From))
        options.From = builder.Configuration["Email:From"] ?? "noreply@psiianasantos.com.br";
    if (string.IsNullOrWhiteSpace(options.FromName))
        options.FromName = builder.Configuration["Email:FromName"] ?? "PsyAll";
    options.PublicAppUrl ??= builder.Configuration["PUBLIC_APP_URL"]
        ?? builder.Configuration["WhatsApp:SiteUrl"]
        ?? "https://psyall.com.br";
});
builder.Services.AddHttpClient("Resend", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddScoped<ClinicaPsi.Application.Services.Email.IEmailService, ClinicaPsi.Application.Services.Email.ResendEmailService>();

// Mercado Pago (pagamento de consultas) — secrets só via env / .env da VPS
builder.Services.Configure<ClinicaPsi.Application.Services.MercadoPago.MercadoPagoOptions>(options =>
{
    builder.Configuration.GetSection(ClinicaPsi.Application.Services.MercadoPago.MercadoPagoOptions.SectionName).Bind(options);
    options.AccessToken ??= builder.Configuration["MercadoPago:AccessToken"]
        ?? builder.Configuration["MercadoPago__AccessToken"]
        ?? builder.Configuration["MERCADOPAGO_ACCESS_TOKEN"];
    options.PublicKey ??= builder.Configuration["MercadoPago:PublicKey"]
        ?? builder.Configuration["MercadoPago__PublicKey"]
        ?? builder.Configuration["MERCADOPAGO_PUBLIC_KEY"];
    options.WebhookSecret ??= builder.Configuration["MercadoPago:WebhookSecret"]
        ?? builder.Configuration["MercadoPago__WebhookSecret"]
        ?? builder.Configuration["MERCADOPAGO_WEBHOOK_SECRET"];
    options.ClientId ??= builder.Configuration["MercadoPago:ClientId"]
        ?? builder.Configuration["MercadoPago__ClientId"]
        ?? builder.Configuration["MERCADOPAGO_CLIENT_ID"];
    options.ClientSecret ??= builder.Configuration["MercadoPago:ClientSecret"]
        ?? builder.Configuration["MercadoPago__ClientSecret"]
        ?? builder.Configuration["MERCADOPAGO_CLIENT_SECRET"];
    var sandboxRaw = builder.Configuration["MercadoPago:UseSandbox"]
        ?? builder.Configuration["MercadoPago__UseSandbox"]
        ?? builder.Configuration["MERCADOPAGO_USE_SANDBOX"];
    if (bool.TryParse(sandboxRaw, out var sandbox))
        options.UseSandbox = sandbox;
    else if (sandboxRaw is null)
        options.UseSandbox = true; // padrão: teste
    var isProdRaw = builder.Configuration["MercadoPago:IsProduction"]
        ?? builder.Configuration["MercadoPago__IsProduction"]
        ?? builder.Configuration["MERCADOPAGO_IS_PRODUCTION"];
    if (bool.TryParse(isProdRaw, out var isProd))
    {
        options.IsProduction = isProd;
        if (isProd)
            options.UseSandbox = false;
    }
    options.PublicAppUrl ??= builder.Configuration["PUBLIC_APP_URL"]
        ?? builder.Configuration["WhatsApp:SiteUrl"]
        ?? "https://psyall.com.br";
});
builder.Services.AddHttpClient("MercadoPago", client =>
{
    client.BaseAddress = new Uri("https://api.mercadopago.com/");
    client.Timeout = TimeSpan.FromSeconds(45);
    client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
});
builder.Services.AddScoped<ClinicaPsi.Application.Services.CarteiraService>();
builder.Services.AddScoped<ClinicaPsi.Application.Services.MercadoPago.MercadoPagoService>();

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
        // Aplicar migrations pendentes automaticamente; se nao houver, EnsureCreated
        logger.LogInformation("Verificando schema do banco...");
        var pendingMigrations = (await context.Database.GetPendingMigrationsAsync()).ToList();
        var appliedMigrations = (await context.Database.GetAppliedMigrationsAsync()).ToList();

        if (pendingMigrations.Count > 0)
        {
            logger.LogInformation("Aplicando {Count} migration(s) pendente(s): {List}",
                pendingMigrations.Count, string.Join(", ", pendingMigrations));
            await context.Database.MigrateAsync();
            logger.LogInformation("Migrations aplicadas com sucesso!");
        }
        else if (appliedMigrations.Count == 0)
        {
            logger.LogWarning("Nenhuma migration no assembly. Criando schema com EnsureCreated...");
            await context.Database.EnsureCreatedAsync();
            logger.LogInformation("Schema criado com EnsureCreated.");
        }
        else
        {
            logger.LogInformation("Nenhuma migration pendente.");
        }

        await GarantirSchemaProntuarioEVideoAsync(context, logger);
        await GarantirSchemaEmailAsync(context, logger);
        await GarantirSchemaPsicologoExcluidoAsync(context, logger);
        await GarantirSchemaOnboardingAsync(context, logger);
        await GarantirSchemaFotoPerfilAsync(context, logger);
        await GarantirSchemaAvaliacoesAsync(context, logger);
        await GarantirSchemaValidacaoPsicologoAsync(context, logger);
        await GarantirSchemaLgpdAsync(context, logger);
        await GarantirSchemaMercadoPagoAsync(context, logger);
        await GarantirSchemaCarteiraAsync(context, logger);
        await GarantirPrecoSocialPsicologosAsync(context, logger);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Erro ao preparar schema: {Message}", ex.Message);
        throw;
    }
    
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    
    await DbInitializer.SeedAsync(context, userManager, roleManager);

    try
    {
        var configService = scope.ServiceProvider.GetRequiredService<ConfiguracaoService>();
        await configService.InicializarConfiguracoesAsync();
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Falha ao inicializar configurações padrão (não bloqueia o boot)");
    }

    try
    {
        var sync = scope.ServiceProvider.GetRequiredService<UsuarioPsicologoSyncService>();
        var syncResult = await sync.SincronizarTodosAsync();
        if (syncResult.TeveAlteracoes)
        {
            logger.LogInformation(
                "Backfill usuários↔psicólogos no boot: users={U}, psicólogos={P}, vínculos={V}",
                syncResult.UsuariosCriados, syncResult.PsicologosCriados, syncResult.VinculosAtualizados);
        }
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Falha no backfill usuários↔psicólogos (não bloqueia o boot)");
    }
}

// Configurar pipeline HTTP
// Confiar no proxy (nginx) para esquema/host corretos em HTTPS
{
    var forwarded = new ForwardedHeadersOptions
    {
        ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
                         | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
                         | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedHost
    };
    // Nginx em rede Docker nao e loopback — limpar listas padrao
    forwarded.KnownNetworks.Clear();
    forwarded.KnownProxies.Clear();
    app.UseForwardedHeaders(forwarded);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// COMENTADO: WhatsApp webhook precisa aceitar HTTP  
// app.UseHttpsRedirection();
app.UseStaticFiles();

// Fotos de perfil em volume persistente (/app/data/uploads) — URL pública /uploads/...
{
    var uploadsRoot = Path.Combine(app.Environment.ContentRootPath, "data", "uploads");
    Directory.CreateDirectory(Path.Combine(uploadsRoot, "perfil"));
    Directory.CreateDirectory(Path.Combine(uploadsRoot, "docs"));
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploadsRoot),
        RequestPath = "/uploads",
        OnPrepareResponse = ctx =>
        {
            ctx.Context.Response.Headers.CacheControl = "public,max-age=3600";
        }
    });
}

app.UseRequestLocalization();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Health check endpoint (DEVE vir após UseRouting)
app.MapHealthChecks("/health");

// Diagnóstico de cultura (moeda R$ / pt-BR)
app.MapGet("/health/culture", () =>
{
    var culture = System.Globalization.CultureInfo.CurrentCulture;
    return Results.Json(new
    {
        culture = culture.Name,
        currencySymbol = culture.NumberFormat.CurrencySymbol,
        sample = 0m.ToString("C")
    });
});

// API Controllers (necessário para WhatsAppWebhookController)
app.MapControllers();

app.MapHub<ClinicaPsi.Web.Hubs.VideoConsultaHub>("/hubs/video-consulta");

// Aliases amigáveis da sala de consulta
app.MapGet("/Psicologo/SalaConsulta/{id:int}", (int id) => Results.Redirect($"/consulta/{id}/video"));
app.MapGet("/Cliente/SalaConsulta/{id:int}", (int id) => Results.Redirect($"/consulta/{id}/video"));

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

static async Task GarantirSchemaMercadoPagoAsync(AppDbContext context, ILogger logger)
{
    try
    {
        await context.Database.ExecuteSqlRawAsync(
            @"ALTER TABLE ""Consultas"" ADD COLUMN IF NOT EXISTS ""StatusPagamento"" integer NOT NULL DEFAULT 0;
              ALTER TABLE ""Consultas"" ADD COLUMN IF NOT EXISTS ""MercadoPagoPaymentId"" character varying(100) NULL;
              ALTER TABLE ""Consultas"" ADD COLUMN IF NOT EXISTS ""MercadoPagoPreferenceId"" character varying(100) NULL;
              ALTER TABLE ""Consultas"" ADD COLUMN IF NOT EXISTS ""PaidAt"" timestamp without time zone NULL;
              CREATE INDEX IF NOT EXISTS ""IX_Consultas_MercadoPagoPaymentId"" ON ""Consultas"" (""MercadoPagoPaymentId"");
              CREATE INDEX IF NOT EXISTS ""IX_Consultas_MercadoPagoPreferenceId"" ON ""Consultas"" (""MercadoPagoPreferenceId"");");
        logger.LogInformation("Schema Mercado Pago (pagamento consulta) verificado.");
    }
    catch (Exception ex)
    {
        try
        {
            await context.Database.ExecuteSqlRawAsync(
                @"ALTER TABLE ""Consultas"" ADD COLUMN ""StatusPagamento"" INTEGER NOT NULL DEFAULT 0;");
            await context.Database.ExecuteSqlRawAsync(
                @"ALTER TABLE ""Consultas"" ADD COLUMN ""MercadoPagoPaymentId"" TEXT NULL;");
            await context.Database.ExecuteSqlRawAsync(
                @"ALTER TABLE ""Consultas"" ADD COLUMN ""MercadoPagoPreferenceId"" TEXT NULL;");
            await context.Database.ExecuteSqlRawAsync(
                @"ALTER TABLE ""Consultas"" ADD COLUMN ""PaidAt"" TEXT NULL;");
            logger.LogInformation("Colunas de pagamento Mercado Pago adicionadas (SQLite).");
        }
        catch (Exception ex2)
        {
            logger.LogDebug(ex2, "Schema Mercado Pago já existe ou não aplicável. PG err={Pg}", ex.Message);
        }
    }
}

static async Task GarantirSchemaCarteiraAsync(AppDbContext context, ILogger logger)
{
    try
    {
        await context.Database.ExecuteSqlRawAsync(
            @"CREATE TABLE IF NOT EXISTS ""CarteirasCliente"" (
                ""Id"" SERIAL PRIMARY KEY,
                ""PacienteId"" integer NOT NULL,
                ""Saldo"" numeric(12,2) NOT NULL DEFAULT 0,
                ""DataCriacao"" timestamp without time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                ""DataAtualizacao"" timestamp without time zone NULL,
                CONSTRAINT ""FK_CarteirasCliente_Pacientes_PacienteId""
                    FOREIGN KEY (""PacienteId"") REFERENCES ""Pacientes"" (""Id"") ON DELETE RESTRICT
              );
              CREATE UNIQUE INDEX IF NOT EXISTS ""IX_CarteirasCliente_PacienteId"" ON ""CarteirasCliente"" (""PacienteId"");

              CREATE TABLE IF NOT EXISTS ""MovimentacoesCarteira"" (
                ""Id"" SERIAL PRIMARY KEY,
                ""CarteiraClienteId"" integer NOT NULL,
                ""Tipo"" integer NOT NULL,
                ""Valor"" numeric(12,2) NOT NULL,
                ""SaldoApos"" numeric(12,2) NOT NULL DEFAULT 0,
                ""Descricao"" character varying(300) NOT NULL DEFAULT '',
                ""Status"" integer NOT NULL DEFAULT 0,
                ""ConsultaId"" integer NULL,
                ""MercadoPagoPaymentId"" character varying(100) NULL,
                ""ExternalReference"" character varying(120) NULL,
                ""PixCopiaECola"" character varying(8000) NULL,
                ""PixQrCodeBase64"" text NULL,
                ""PixExpiraEm"" timestamp without time zone NULL,
                ""DataCriacao"" timestamp without time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                ""DataConfirmacao"" timestamp without time zone NULL,
                CONSTRAINT ""FK_MovimentacoesCarteira_CarteirasCliente_CarteiraClienteId""
                    FOREIGN KEY (""CarteiraClienteId"") REFERENCES ""CarteirasCliente"" (""Id"") ON DELETE CASCADE,
                CONSTRAINT ""FK_MovimentacoesCarteira_Consultas_ConsultaId""
                    FOREIGN KEY (""ConsultaId"") REFERENCES ""Consultas"" (""Id"") ON DELETE SET NULL
              );
              CREATE INDEX IF NOT EXISTS ""IX_MovimentacoesCarteira_CarteiraClienteId"" ON ""MovimentacoesCarteira"" (""CarteiraClienteId"");
              CREATE INDEX IF NOT EXISTS ""IX_MovimentacoesCarteira_MercadoPagoPaymentId"" ON ""MovimentacoesCarteira"" (""MercadoPagoPaymentId"");
              CREATE INDEX IF NOT EXISTS ""IX_MovimentacoesCarteira_ExternalReference"" ON ""MovimentacoesCarteira"" (""ExternalReference"");
              CREATE INDEX IF NOT EXISTS ""IX_MovimentacoesCarteira_Status"" ON ""MovimentacoesCarteira"" (""Status"");");
        logger.LogInformation("Schema carteira do cliente verificado.");
    }
    catch (Exception ex)
    {
        try
        {
            await context.Database.ExecuteSqlRawAsync(
                @"CREATE TABLE IF NOT EXISTS ""CarteirasCliente"" (
                    ""Id"" INTEGER PRIMARY KEY AUTOINCREMENT,
                    ""PacienteId"" INTEGER NOT NULL UNIQUE,
                    ""Saldo"" TEXT NOT NULL DEFAULT '0',
                    ""DataCriacao"" TEXT NOT NULL,
                    ""DataAtualizacao"" TEXT NULL
                  );");
            await context.Database.ExecuteSqlRawAsync(
                @"CREATE TABLE IF NOT EXISTS ""MovimentacoesCarteira"" (
                    ""Id"" INTEGER PRIMARY KEY AUTOINCREMENT,
                    ""CarteiraClienteId"" INTEGER NOT NULL,
                    ""Tipo"" INTEGER NOT NULL,
                    ""Valor"" TEXT NOT NULL,
                    ""SaldoApos"" TEXT NOT NULL DEFAULT '0',
                    ""Descricao"" TEXT NOT NULL DEFAULT '',
                    ""Status"" INTEGER NOT NULL DEFAULT 0,
                    ""ConsultaId"" INTEGER NULL,
                    ""MercadoPagoPaymentId"" TEXT NULL,
                    ""ExternalReference"" TEXT NULL,
                    ""PixCopiaECola"" TEXT NULL,
                    ""PixQrCodeBase64"" TEXT NULL,
                    ""PixExpiraEm"" TEXT NULL,
                    ""DataCriacao"" TEXT NOT NULL,
                    ""DataConfirmacao"" TEXT NULL
                  );");
            logger.LogInformation("Schema carteira criado (SQLite).");
        }
        catch (Exception ex2)
        {
            logger.LogDebug(ex2, "Schema carteira já existe ou não aplicável. PG err={Pg}", ex.Message);
        }
    }
}

/// <summary>Alinha ValorConsulta / ValorContratoConsulta ao preço social R$ 50.</summary>
static async Task GarantirPrecoSocialPsicologosAsync(AppDbContext context, ILogger logger)
{
    const decimal precoSocial = 50m;
    try
    {
        var atualizados = await context.Psicologos
            .Where(p => p.ExcluidoEm == null &&
                        (p.ValorConsulta != precoSocial || p.ValorContratoConsulta != precoSocial))
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.ValorConsulta, precoSocial)
                .SetProperty(p => p.ValorContratoConsulta, precoSocial)
                .SetProperty(p => p.DataAtualizacao, DateTime.Now));

        if (atualizados > 0)
            logger.LogInformation("Preço social R$ 50 aplicado a {Count} psicólogo(s).", atualizados);

        // Config padrão da clínica
        var cfg = await context.ConfiguracoesSistema
            .FirstOrDefaultAsync(c => c.Chave == "Consultas.ValorPadrao");
        if (cfg is not null && cfg.Valor != "50.00" && cfg.Valor != "50")
        {
            cfg.Valor = "50.00";
            cfg.DataAtualizacao = DateTime.Now;
            await context.SaveChangesAsync();
            logger.LogInformation("Config Consultas.ValorPadrao atualizada para 50.00.");
        }
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Falha ao sincronizar preço social R$ 50 (não bloqueia o boot)");
    }
}

static async Task GarantirSchemaProntuarioEVideoAsync(AppDbContext context, ILogger logger)
{
    try
    {
        await context.Database.ExecuteSqlRawAsync(
            @"ALTER TABLE ""Consultas"" ADD COLUMN IF NOT EXISTS ""VideoRoomName"" character varying(100) NULL;
              ALTER TABLE ""Consultas"" ADD COLUMN IF NOT EXISTS ""VideoRoomUrl"" character varying(500) NULL;
              ALTER TABLE ""Consultas"" ADD COLUMN IF NOT EXISTS ""VideoChamadaAtivaEm"" timestamp without time zone NULL;");

        await context.Database.ExecuteSqlRawAsync(
            @"CREATE TABLE IF NOT EXISTS ""ProntuariosEletronicos"" (
                ""Id"" SERIAL PRIMARY KEY,
                ""PacienteId"" integer NOT NULL,
                ""ConsultaId"" integer NULL,
                ""PsicologoId"" integer NOT NULL,
                ""DataSessao"" timestamp without time zone NOT NULL,
                ""TipoAtendimento"" character varying(50) NOT NULL DEFAULT 'Individual',
                ""QueixaPrincipal"" text NOT NULL,
                ""Observacoes"" text NOT NULL,
                ""Evolucao"" text NULL,
                ""Intervencoes"" text NULL,
                ""PlanoTerapeutico"" text NULL,
                ""ProximaSessao"" text NULL,
                ""EstadoEmocional"" character varying(100) NULL,
                ""MedicamentosAtuais"" text NULL,
                ""Anexos"" text NULL,
                ""Finalizado"" boolean NOT NULL DEFAULT FALSE,
                ""DataCriacao"" timestamp without time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                ""DataAtualizacao"" timestamp without time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                ""Confidencial"" boolean NOT NULL DEFAULT TRUE
              );");

        await context.Database.ExecuteSqlRawAsync(
            @"CREATE INDEX IF NOT EXISTS ""IX_ProntuariosEletronicos_PacienteId"" ON ""ProntuariosEletronicos"" (""PacienteId"");
              CREATE INDEX IF NOT EXISTS ""IX_ProntuariosEletronicos_PsicologoId"" ON ""ProntuariosEletronicos"" (""PsicologoId"");
              CREATE INDEX IF NOT EXISTS ""IX_ProntuariosEletronicos_ConsultaId"" ON ""ProntuariosEletronicos"" (""ConsultaId"");
              CREATE INDEX IF NOT EXISTS ""IX_ProntuariosEletronicos_DataSessao"" ON ""ProntuariosEletronicos"" (""DataSessao"");");

        logger.LogInformation("Schema de prontuário/vídeo verificado (colunas e tabela).");
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Não foi possível garantir schema de prontuário/vídeo (pode ser SQLite local).");
    }
}

static async Task GarantirSchemaEmailAsync(AppDbContext context, ILogger logger)
{
    try
    {
        await context.Database.ExecuteSqlRawAsync(
            @"ALTER TABLE ""AspNetUsers"" ADD COLUMN IF NOT EXISTS ""MustChangePassword"" boolean NOT NULL DEFAULT FALSE;");
        logger.LogInformation("Schema de e-mail/senha provisória verificado (MustChangePassword).");
    }
    catch (Exception ex)
    {
        try
        {
            await context.Database.ExecuteSqlRawAsync(
                @"ALTER TABLE ""AspNetUsers"" ADD COLUMN ""MustChangePassword"" INTEGER NOT NULL DEFAULT 0;");
            logger.LogInformation("Coluna MustChangePassword adicionada (SQLite).");
        }
        catch (Exception ex2)
        {
            logger.LogDebug(ex2, "MustChangePassword já existe ou schema não aplicável. PG err={Pg}", ex.Message);
        }
    }
}

static async Task GarantirSchemaPsicologoExcluidoAsync(AppDbContext context, ILogger logger)
{
    try
    {
        await context.Database.ExecuteSqlRawAsync(
            @"ALTER TABLE ""Psicologos"" ADD COLUMN IF NOT EXISTS ""ExcluidoEm"" timestamp without time zone NULL;");
        logger.LogInformation("Schema Psicologos.ExcluidoEm verificado.");
    }
    catch (Exception ex)
    {
        try
        {
            await context.Database.ExecuteSqlRawAsync(
                @"ALTER TABLE ""Psicologos"" ADD COLUMN ""ExcluidoEm"" TEXT NULL;");
            logger.LogInformation("Coluna ExcluidoEm adicionada (SQLite).");
        }
        catch (Exception ex2)
        {
            logger.LogDebug(ex2, "ExcluidoEm já existe ou schema não aplicável. PG err={Pg}", ex.Message);
        }
    }
}

static async Task GarantirSchemaOnboardingAsync(AppDbContext context, ILogger logger)
{
    try
    {
        await context.Database.ExecuteSqlRawAsync(
            @"ALTER TABLE ""AspNetUsers"" ADD COLUMN IF NOT EXISTS ""OnboardingCompleted"" boolean NOT NULL DEFAULT FALSE;");
        logger.LogInformation("Schema de onboarding verificado (OnboardingCompleted).");
    }
    catch (Exception ex)
    {
        try
        {
            await context.Database.ExecuteSqlRawAsync(
                @"ALTER TABLE ""AspNetUsers"" ADD COLUMN ""OnboardingCompleted"" INTEGER NOT NULL DEFAULT 0;");
            logger.LogInformation("Coluna OnboardingCompleted adicionada (SQLite).");
        }
        catch (Exception ex2)
        {
            logger.LogDebug(ex2, "OnboardingCompleted já existe ou schema não aplicável. PG err={Pg}", ex.Message);
        }
    }
}

static async Task GarantirSchemaFotoPerfilAsync(AppDbContext context, ILogger logger)
{
    try
    {
        await context.Database.ExecuteSqlRawAsync(
            @"ALTER TABLE ""AspNetUsers"" ADD COLUMN IF NOT EXISTS ""FotoUrl"" character varying(300) NULL;
              ALTER TABLE ""Pacientes"" ADD COLUMN IF NOT EXISTS ""FotoUrl"" character varying(300) NULL;
              ALTER TABLE ""Psicologos"" ADD COLUMN IF NOT EXISTS ""FotoUrl"" character varying(300) NULL;");
        logger.LogInformation("Schema de foto de perfil verificado (FotoUrl).");
    }
    catch (Exception ex)
    {
        try
        {
            await context.Database.ExecuteSqlRawAsync(
                @"ALTER TABLE ""AspNetUsers"" ADD COLUMN ""FotoUrl"" TEXT NULL;");
            await context.Database.ExecuteSqlRawAsync(
                @"ALTER TABLE ""Pacientes"" ADD COLUMN ""FotoUrl"" TEXT NULL;");
            await context.Database.ExecuteSqlRawAsync(
                @"ALTER TABLE ""Psicologos"" ADD COLUMN ""FotoUrl"" TEXT NULL;");
            logger.LogInformation("Colunas FotoUrl adicionadas (SQLite).");
        }
        catch (Exception ex2)
        {
            logger.LogDebug(ex2, "FotoUrl já existe ou schema não aplicável. PG err={Pg}", ex.Message);
        }
    }
}

static async Task GarantirSchemaValidacaoPsicologoAsync(AppDbContext context, ILogger logger)
{
    try
    {
        await context.Database.ExecuteSqlRawAsync(
            @"ALTER TABLE ""Psicologos"" ADD COLUMN IF NOT EXISTS ""StatusValidacao"" integer NOT NULL DEFAULT 2;
              ALTER TABLE ""Psicologos"" ADD COLUMN IF NOT EXISTS ""AceiteTermosEm"" timestamp without time zone NULL;
              ALTER TABLE ""Psicologos"" ADD COLUMN IF NOT EXISTS ""AceiteContratoEm"" timestamp without time zone NULL;
              ALTER TABLE ""Psicologos"" ADD COLUMN IF NOT EXISTS ""ValorContratoConsulta"" numeric(10,2) NOT NULL DEFAULT 50;
              ALTER TABLE ""Psicologos"" ADD COLUMN IF NOT EXISTS ""DocumentoCnhUrl"" character varying(400) NULL;
              ALTER TABLE ""Psicologos"" ADD COLUMN IF NOT EXISTS ""DocumentoCrpUrl"" character varying(400) NULL;
              ALTER TABLE ""Psicologos"" ADD COLUMN IF NOT EXISTS ""ValidadoEm"" timestamp without time zone NULL;
              ALTER TABLE ""Psicologos"" ADD COLUMN IF NOT EXISTS ""ValidadoPorUserId"" character varying(450) NULL;
              ALTER TABLE ""Psicologos"" ADD COLUMN IF NOT EXISTS ""MotivoRecusa"" character varying(1000) NULL;");
        // DEFAULT 2 (Aprovado) preserva psicólogos já ativos; novos cadastros definem Pendente=1 no app.
        logger.LogInformation("Schema de validação de psicólogo verificado.");
    }
    catch (Exception ex)
    {
        try
        {
            await context.Database.ExecuteSqlRawAsync(
                @"ALTER TABLE ""Psicologos"" ADD COLUMN ""StatusValidacao"" INTEGER NOT NULL DEFAULT 2;");
            await context.Database.ExecuteSqlRawAsync(
                @"ALTER TABLE ""Psicologos"" ADD COLUMN ""AceiteTermosEm"" TEXT NULL;");
            await context.Database.ExecuteSqlRawAsync(
                @"ALTER TABLE ""Psicologos"" ADD COLUMN ""AceiteContratoEm"" TEXT NULL;");
            await context.Database.ExecuteSqlRawAsync(
                @"ALTER TABLE ""Psicologos"" ADD COLUMN ""ValorContratoConsulta"" TEXT NOT NULL DEFAULT '50';");
            await context.Database.ExecuteSqlRawAsync(
                @"ALTER TABLE ""Psicologos"" ADD COLUMN ""DocumentoCnhUrl"" TEXT NULL;");
            await context.Database.ExecuteSqlRawAsync(
                @"ALTER TABLE ""Psicologos"" ADD COLUMN ""DocumentoCrpUrl"" TEXT NULL;");
            await context.Database.ExecuteSqlRawAsync(
                @"ALTER TABLE ""Psicologos"" ADD COLUMN ""ValidadoEm"" TEXT NULL;");
            await context.Database.ExecuteSqlRawAsync(
                @"ALTER TABLE ""Psicologos"" ADD COLUMN ""ValidadoPorUserId"" TEXT NULL;");
            await context.Database.ExecuteSqlRawAsync(
                @"ALTER TABLE ""Psicologos"" ADD COLUMN ""MotivoRecusa"" TEXT NULL;");
            logger.LogInformation("Colunas de validação de psicólogo adicionadas (SQLite).");
        }
        catch (Exception ex2)
        {
            logger.LogDebug(ex2, "Validação psicólogo já existe ou schema não aplicável. PG err={Pg}", ex.Message);
        }
    }
}

static async Task GarantirSchemaAvaliacoesAsync(AppDbContext context, ILogger logger)
{
    try
    {
        await context.Database.ExecuteSqlRawAsync(
            @"CREATE TABLE IF NOT EXISTS ""Avaliacoes"" (
                ""Id"" SERIAL PRIMARY KEY,
                ""ConsultaId"" integer NOT NULL,
                ""PsicologoId"" integer NULL,
                ""PacienteId"" integer NULL,
                ""Alvo"" integer NOT NULL,
                ""AvaliadorUserId"" character varying(450) NOT NULL,
                ""Nota"" integer NOT NULL,
                ""Comentario"" character varying(1000) NULL,
                ""DataCriacao"" timestamp without time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                ""Publica"" boolean NOT NULL DEFAULT TRUE
              );
              CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Avaliacoes_ConsultaId_Alvo"" ON ""Avaliacoes"" (""ConsultaId"", ""Alvo"");
              CREATE INDEX IF NOT EXISTS ""IX_Avaliacoes_PsicologoId"" ON ""Avaliacoes"" (""PsicologoId"");
              CREATE INDEX IF NOT EXISTS ""IX_Avaliacoes_PacienteId"" ON ""Avaliacoes"" (""PacienteId"");");
        logger.LogInformation("Schema de Avaliacoes verificado.");
    }
    catch (Exception ex)
    {
        try
        {
            await context.Database.ExecuteSqlRawAsync(
                @"CREATE TABLE IF NOT EXISTS ""Avaliacoes"" (
                    ""Id"" INTEGER PRIMARY KEY AUTOINCREMENT,
                    ""ConsultaId"" INTEGER NOT NULL,
                    ""PsicologoId"" INTEGER NULL,
                    ""PacienteId"" INTEGER NULL,
                    ""Alvo"" INTEGER NOT NULL,
                    ""AvaliadorUserId"" TEXT NOT NULL,
                    ""Nota"" INTEGER NOT NULL,
                    ""Comentario"" TEXT NULL,
                    ""DataCriacao"" TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    ""Publica"" INTEGER NOT NULL DEFAULT 1
                  );");
            logger.LogInformation("Tabela Avaliacoes criada (SQLite).");
        }
        catch (Exception ex2)
        {
            logger.LogDebug(ex2, "Avaliacoes já existe ou schema não aplicável. PG err={Pg}", ex.Message);
        }
    }
}

static async Task GarantirSchemaLgpdAsync(AppDbContext context, ILogger logger)
{
    try
    {
        await context.Database.ExecuteSqlRawAsync(
            @"ALTER TABLE ""AspNetUsers"" ADD COLUMN IF NOT EXISTS ""AceiteTermosEm"" timestamp without time zone NULL;
              ALTER TABLE ""AspNetUsers"" ADD COLUMN IF NOT EXISTS ""AceitePrivacidadeEm"" timestamp without time zone NULL;
              ALTER TABLE ""AspNetUsers"" ADD COLUMN IF NOT EXISTS ""ConsentimentoDadosSaudeEm"" timestamp without time zone NULL;
              ALTER TABLE ""AspNetUsers"" ADD COLUMN IF NOT EXISTS ""CookieAnalyticsAceito"" boolean NULL;
              ALTER TABLE ""AspNetUsers"" ADD COLUMN IF NOT EXISTS ""CookieConsentimentoEm"" timestamp without time zone NULL;
              ALTER TABLE ""Pacientes"" ADD COLUMN IF NOT EXISTS ""AceiteTermosEm"" timestamp without time zone NULL;
              ALTER TABLE ""Pacientes"" ADD COLUMN IF NOT EXISTS ""AceitePrivacidadeEm"" timestamp without time zone NULL;
              ALTER TABLE ""Pacientes"" ADD COLUMN IF NOT EXISTS ""ConsentimentoDadosSaudeEm"" timestamp without time zone NULL;
              ALTER TABLE ""Psicologos"" ADD COLUMN IF NOT EXISTS ""AceitePrivacidadeEm"" timestamp without time zone NULL;
              CREATE TABLE IF NOT EXISTS ""SolicitacoesPrivacidade"" (
                ""Id"" SERIAL PRIMARY KEY,
                ""UserId"" character varying(450) NOT NULL,
                ""NomeTitular"" character varying(200) NOT NULL,
                ""EmailTitular"" character varying(200) NOT NULL,
                ""Tipo"" integer NOT NULL,
                ""Status"" integer NOT NULL DEFAULT 1,
                ""Detalhes"" character varying(2000) NULL,
                ""DataCriacao"" timestamp without time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                ""DataAtualizacao"" timestamp without time zone NULL,
                ""ObservacaoAdmin"" character varying(2000) NULL,
                ""RespondidoPorUserId"" character varying(450) NULL
              );
              CREATE INDEX IF NOT EXISTS ""IX_SolicitacoesPrivacidade_UserId"" ON ""SolicitacoesPrivacidade"" (""UserId"");
              CREATE INDEX IF NOT EXISTS ""IX_SolicitacoesPrivacidade_Status"" ON ""SolicitacoesPrivacidade"" (""Status"");
              CREATE INDEX IF NOT EXISTS ""IX_SolicitacoesPrivacidade_DataCriacao"" ON ""SolicitacoesPrivacidade"" (""DataCriacao"");");
        logger.LogInformation("Schema LGPD verificado.");
    }
    catch (Exception ex)
    {
        try
        {
            await context.Database.ExecuteSqlRawAsync(
                @"ALTER TABLE ""AspNetUsers"" ADD COLUMN ""AceiteTermosEm"" TEXT NULL;");
            await context.Database.ExecuteSqlRawAsync(
                @"ALTER TABLE ""AspNetUsers"" ADD COLUMN ""AceitePrivacidadeEm"" TEXT NULL;");
            await context.Database.ExecuteSqlRawAsync(
                @"ALTER TABLE ""AspNetUsers"" ADD COLUMN ""ConsentimentoDadosSaudeEm"" TEXT NULL;");
            await context.Database.ExecuteSqlRawAsync(
                @"ALTER TABLE ""AspNetUsers"" ADD COLUMN ""CookieAnalyticsAceito"" INTEGER NULL;");
            await context.Database.ExecuteSqlRawAsync(
                @"ALTER TABLE ""AspNetUsers"" ADD COLUMN ""CookieConsentimentoEm"" TEXT NULL;");
            await context.Database.ExecuteSqlRawAsync(
                @"ALTER TABLE ""Pacientes"" ADD COLUMN ""AceiteTermosEm"" TEXT NULL;");
            await context.Database.ExecuteSqlRawAsync(
                @"ALTER TABLE ""Pacientes"" ADD COLUMN ""AceitePrivacidadeEm"" TEXT NULL;");
            await context.Database.ExecuteSqlRawAsync(
                @"ALTER TABLE ""Pacientes"" ADD COLUMN ""ConsentimentoDadosSaudeEm"" TEXT NULL;");
            await context.Database.ExecuteSqlRawAsync(
                @"ALTER TABLE ""Psicologos"" ADD COLUMN ""AceitePrivacidadeEm"" TEXT NULL;");
            await context.Database.ExecuteSqlRawAsync(
                @"CREATE TABLE IF NOT EXISTS ""SolicitacoesPrivacidade"" (
                    ""Id"" INTEGER PRIMARY KEY AUTOINCREMENT,
                    ""UserId"" TEXT NOT NULL,
                    ""NomeTitular"" TEXT NOT NULL,
                    ""EmailTitular"" TEXT NOT NULL,
                    ""Tipo"" INTEGER NOT NULL,
                    ""Status"" INTEGER NOT NULL DEFAULT 1,
                    ""Detalhes"" TEXT NULL,
                    ""DataCriacao"" TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    ""DataAtualizacao"" TEXT NULL,
                    ""ObservacaoAdmin"" TEXT NULL,
                    ""RespondidoPorUserId"" TEXT NULL
                  );");
            logger.LogInformation("Schema LGPD adicionado (SQLite).");
        }
        catch (Exception ex2)
        {
            logger.LogDebug(ex2, "Schema LGPD já existe ou não aplicável. PG err={Pg}", ex.Message);
        }
    }
}

