using ClinicaPsi.Application.Services;
using ClinicaPsi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Adicionar serviços
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// Configurar banco de dados
builder.Services.AddDbContext<AppDbContext>(options => 
    options.UseSqlite("Data Source=clinicapsi.db"));

// Serviços de aplicação
builder.Services.AddScoped<PacienteService>();
builder.Services.AddScoped<ConsultaService>();

var app = builder.Build();

// Criar banco de dados
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

// Configurar pipeline HTTP
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
