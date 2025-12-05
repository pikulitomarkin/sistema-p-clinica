# ============================================
# Script para aplicar migration Formato no Railway
# ============================================

Write-Host "🚀 Aplicando migration da coluna Formato no Railway..." -ForegroundColor Cyan
Write-Host ""

# Obter a connection string do Railway
Write-Host "📋 Buscando connection string do Railway..." -ForegroundColor Yellow

# Tentar obter do arquivo .env se existir
$envFile = ".env"
$connectionString = $null

if (Test-Path $envFile) {
    $content = Get-Content $envFile
    $dbLine = $content | Where-Object { $_ -match "DATABASE_URL=" }
    if ($dbLine) {
        $connectionString = $dbLine -replace "DATABASE_URL=", ""
        Write-Host "✅ Connection string encontrada no .env" -ForegroundColor Green
    }
}

# Se não encontrou, pedir ao usuário
if (-not $connectionString) {
    Write-Host ""
    Write-Host "⚠️  Connection string não encontrada no .env" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Como obter a connection string do Railway:" -ForegroundColor Cyan
    Write-Host "1. Acesse: https://railway.app" -ForegroundColor White
    Write-Host "2. Abra seu projeto" -ForegroundColor White
    Write-Host "3. Clique no serviço PostgreSQL" -ForegroundColor White
    Write-Host "4. Vá em 'Connect' ou 'Variables'" -ForegroundColor White
    Write-Host "5. Copie a 'DATABASE_URL' ou 'Postgres Connection URL'" -ForegroundColor White
    Write-Host ""
    Write-Host "Cole a connection string abaixo:" -ForegroundColor Cyan
    $connectionString = Read-Host "Connection String"
}

if (-not $connectionString -or $connectionString.Trim() -eq "") {
    Write-Host ""
    Write-Host "❌ Connection string vazia. Abortando." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "🔧 Verificando se Npgsql está disponível..." -ForegroundColor Yellow

# Verificar se o pacote Npgsql está disponível
$npgsqlPath = "src/ClinicaPsi.Infrastructure/bin/Debug/net9.0/Npgsql.dll"

if (-not (Test-Path $npgsqlPath)) {
    Write-Host "⚠️  Compilando projeto para obter Npgsql..." -ForegroundColor Yellow
    dotnet build src/ClinicaPsi.Infrastructure/ClinicaPsi.Infrastructure.csproj -c Debug --no-restore
}

Write-Host ""
Write-Host "📝 SQL que será executado:" -ForegroundColor Cyan
Write-Host "   ALTER TABLE ""Consultas"" ADD COLUMN ""Formato"" integer NOT NULL DEFAULT 1;" -ForegroundColor White
Write-Host ""

$confirm = Read-Host "Deseja continuar? (S/N)"
if ($confirm -ne "S" -and $confirm -ne "s") {
    Write-Host "❌ Operação cancelada." -ForegroundColor Red
    exit 0
}

Write-Host ""
Write-Host "🔄 Executando migration..." -ForegroundColor Yellow

# Criar script C# inline para executar o SQL
$csharpScript = @"
using System;
using System.Data;
using Npgsql;

class Program {
    static void Main(string[] args) {
        var connectionString = args[0];
        
        try {
            using var conn = new NpgsqlConnection(connectionString);
            conn.Open();
            
            Console.WriteLine("✅ Conectado ao banco Railway");
            Console.WriteLine("");
            
            // Verificar se a coluna já existe
            using (var checkCmd = new NpgsqlCommand(
                "SELECT column_name FROM information_schema.columns WHERE table_name = 'Consultas' AND column_name = 'Formato'",
                conn))
            {
                var result = checkCmd.ExecuteScalar();
                if (result != null) {
                    Console.WriteLine("⚠️  Coluna 'Formato' já existe!");
                    Console.WriteLine("");
                    return;
                }
            }
            
            // Adicionar a coluna
            using (var cmd = new NpgsqlCommand(
                "ALTER TABLE \"Consultas\" ADD COLUMN \"Formato\" integer NOT NULL DEFAULT 1",
                conn))
            {
                cmd.ExecuteNonQuery();
                Console.WriteLine("✅ Coluna 'Formato' adicionada com sucesso!");
            }
            
            // Verificar
            using (var verifyCmd = new NpgsqlCommand(
                "SELECT COUNT(*) FROM \"Consultas\" WHERE \"Formato\" = 1",
                conn))
            {
                var count = verifyCmd.ExecuteScalar();
                Console.WriteLine($"✅ {count} consultas com Formato = 1");
            }
            
            Console.WriteLine("");
            Console.WriteLine("🎉 Migration aplicada com sucesso!");
        }
        catch (Exception ex) {
            Console.WriteLine($"❌ Erro: {ex.Message}");
            Environment.Exit(1);
        }
    }
}
"@

# Salvar o script C#
$tempCsFile = [System.IO.Path]::GetTempFileName() + ".cs"
$csharpScript | Out-File -FilePath $tempCsFile -Encoding UTF8

try {
    # Executar usando dotnet script ou compilar e executar
    Write-Host "🔧 Compilando script..." -ForegroundColor Yellow
    
    # Criar projeto temporário
    $tempDir = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), "MigrationScript")
    if (Test-Path $tempDir) {
        Remove-Item $tempDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $tempDir | Out-Null
    
    # Criar csproj
    $csproj = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Npgsql" Version="9.0.0" />
  </ItemGroup>
</Project>
"@
    
    $csproj | Out-File -FilePath "$tempDir/MigrationScript.csproj" -Encoding UTF8
    Copy-Item $tempCsFile "$tempDir/Program.cs"
    
    # Compilar e executar
    Push-Location $tempDir
    dotnet restore --verbosity quiet
    dotnet run -- "$connectionString"
    $exitCode = $LASTEXITCODE
    Pop-Location
    
    if ($exitCode -eq 0) {
        Write-Host ""
        Write-Host "✅ SUCESSO! A coluna Formato foi adicionada." -ForegroundColor Green
        Write-Host ""
        Write-Host "🔄 Aguarde 1-2 minutos para o Railway processar..." -ForegroundColor Yellow
        Write-Host "📱 Depois teste as páginas:" -ForegroundColor Cyan
        Write-Host "   - Cliente > Agendar Consulta" -ForegroundColor White
        Write-Host "   - Cliente > Minhas Consultas" -ForegroundColor White
        Write-Host "   - Psicólogo > Agenda" -ForegroundColor White
    } else {
        Write-Host ""
        Write-Host "❌ Erro ao executar migration." -ForegroundColor Red
        Write-Host "   Verifique a connection string e tente novamente." -ForegroundColor Yellow
    }
}
finally {
    # Limpar
    if (Test-Path $tempCsFile) {
        Remove-Item $tempCsFile -Force
    }
    if (Test-Path $tempDir) {
        Remove-Item $tempDir -Recurse -Force
    }
}

Write-Host ""
Write-Host "Pressione qualquer tecla para sair..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
