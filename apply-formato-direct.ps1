# Script para adicionar coluna Formato usando dotnet script temporário
Write-Host ""
Write-Host "Adicionando coluna Formato via SQL direto..." -ForegroundColor Cyan
Write-Host ""

$connectionString = "Host=switchyard.proxy.rlwy.net;Port=35011;Database=railway;Username=postgres;Password=xbvlxwTDGfYCgXFdleTStAUMqFvKhRVI;SSL Mode=Require;Trust Server Certificate=true"

# Criar projeto temporário
$tempDir = Join-Path $env:TEMP "AddFormatoColumn"
if (Test-Path $tempDir) { Remove-Item $tempDir -Recurse -Force }
New-Item -ItemType Directory -Path $tempDir | Out-Null

Write-Host "Criando projeto temporário..." -ForegroundColor Yellow

# Criar arquivo de projeto
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

$csproj | Out-File -FilePath "$tempDir\AddFormatoColumn.csproj" -Encoding UTF8

# Criar código C#
$program = @"
using System;
using Npgsql;

var connectionString = "$connectionString";
var sql = @"
DO `$`$ 
BEGIN
    IF NOT EXISTS (
        SELECT FROM information_schema.columns 
        WHERE table_name='Consultas' AND column_name='Formato'
    ) THEN
        ALTER TABLE ""Consultas"" ADD COLUMN ""Formato"" integer NOT NULL DEFAULT 1;
        RAISE NOTICE 'Coluna Formato adicionada com sucesso!';
    ELSE
        RAISE NOTICE 'Coluna Formato ja existe!';
    END IF;
END `$`$;
";

try
{
    using (var conn = new NpgsqlConnection(connectionString))
    {
        Console.WriteLine("Conectando ao banco Railway...");
        conn.Open();
        Console.WriteLine("Conectado!");
        Console.WriteLine();
        
        using (var cmd = new NpgsqlCommand(sql, conn))
        {
            cmd.ExecuteNonQuery();
        }
        
        Console.WriteLine();
        Console.WriteLine("SUCESSO! Coluna Formato adicionada.");
        Console.WriteLine();
    }
}
catch (Exception ex)
{
    Console.WriteLine("Erro: " + ex.Message);
    Environment.Exit(1);
}
"@

$program | Out-File -FilePath "$tempDir\Program.cs" -Encoding UTF8

Write-Host "Executando SQL..." -ForegroundColor Yellow
Write-Host ""

# Executar
Push-Location $tempDir
dotnet run --verbosity quiet
$exitCode = $LASTEXITCODE
Pop-Location

# Limpar
Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue

if ($exitCode -eq 0) {
    Write-Host ""
    Write-Host "Coluna adicionada com sucesso!" -ForegroundColor Green
    Write-Host "Aguarde 1-2 minutos para o Railway recarregar" -ForegroundColor Cyan
} else {
    Write-Host ""
    Write-Host "Erro ao adicionar coluna!" -ForegroundColor Red
}

Write-Host ""
Write-Host "Pressione Enter para sair..."
Read-Host
