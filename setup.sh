#!/bin/bash

echo "🚀 Criando estrutura do projeto ClinicaPsi..."

cd "/Users/Apple/Desktop/pasta sem título"

# Criar pastas
mkdir -p src/ClinicaPsi.Shared/Models
mkdir -p src/ClinicaPsi.Infrastructure/Data
mkdir -p src/ClinicaPsi.Application/Services
mkdir -p src/ClinicaPsi.Web/Pages
mkdir -p src/ClinicaPsi.Web/wwwroot/css

echo "✅ Pastas criadas"

# Criar projetos .csproj
echo "📦 Criando arquivos de projeto..."

# ClinicaPsi.Shared.csproj
cat > src/ClinicaPsi.Shared/ClinicaPsi.Shared.csproj << 'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
EOF

# ClinicaPsi.Infrastructure.csproj
cat > src/ClinicaPsi.Infrastructure/ClinicaPsi.Infrastructure.csproj << 'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore" Version="8.0.0" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="8.0.0" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\ClinicaPsi.Shared\ClinicaPsi.Shared.csproj" />
  </ItemGroup>
</Project>
EOF

# ClinicaPsi.Application.csproj
cat > src/ClinicaPsi.Application/ClinicaPsi.Application.csproj << 'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\ClinicaPsi.Infrastructure\ClinicaPsi.Infrastructure.csproj" />
    <ProjectReference Include="..\ClinicaPsi.Shared\ClinicaPsi.Shared.csproj" />
  </ItemGroup>
</Project>
EOF

# ClinicaPsi.Web.csproj
cat > src/ClinicaPsi.Web/ClinicaPsi.Web.csproj << 'EOF'
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="8.0.0" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\ClinicaPsi.Application\ClinicaPsi.Application.csproj" />
    <ProjectReference Include="..\ClinicaPsi.Infrastructure\ClinicaPsi.Infrastructure.csproj" />
    <ProjectReference Include="..\ClinicaPsi.Shared\ClinicaPsi.Shared.csproj" />
  </ItemGroup>
</Project>
EOF

echo "✅ Arquivos .csproj criados"

# Restaurar e compilar
echo "📥 Restaurando pacotes..."
cd src/ClinicaPsi.Web
dotnet restore

echo "🔨 Compilando projeto..."
dotnet build

echo ""
echo "✨ Estrutura criada com sucesso!"
echo "Para executar: cd src/ClinicaPsi.Web && dotnet run"
