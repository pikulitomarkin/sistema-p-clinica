# Script de Otimizacao de Inicializacao e Performance
# Execute como Administrador

Write-Host "=== OTIMIZACAO DE PERFORMANCE ===" -ForegroundColor Cyan
Write-Host ""

# 1. Desabilitar programas pesados na inicializacao
Write-Host "[1/6] Verificando programas na inicializacao..." -ForegroundColor Yellow

$programasParaDesabilitar = @(
    "Steam",
    "Docker Desktop", 
    "Riot Client",
    "Teams"
)

foreach ($programa in $programasParaDesabilitar) {
    try {
        $startup = Get-CimInstance Win32_StartupCommand | Where-Object {$_.Name -like "*$programa*"}
        if ($startup) {
            Write-Host "  ! Encontrado na inicializacao: $programa" -ForegroundColor Yellow
            Write-Host "    Desabilite manualmente pelo Gerenciador de Tarefas" -ForegroundColor Gray
        }
    }
    catch {
        Write-Host "  - $programa nao encontrado" -ForegroundColor Gray
    }
}

Write-Host ""

# 2. Otimizar servicos do Windows
Write-Host "[2/6] Otimizando servicos do Windows..." -ForegroundColor Yellow

$servicosParaDesabilitar = @(
    "DiagTrack",
    "SysMain",
    "WSearch"
)

foreach ($servico in $servicosParaDesabilitar) {
    try {
        $service = Get-Service -Name $servico -ErrorAction SilentlyContinue
        if ($service) {
            if ($service.Status -eq "Running") {
                Stop-Service -Name $servico -Force -ErrorAction SilentlyContinue
                Write-Host "  - Parando: $servico" -ForegroundColor Yellow
            }
            Set-Service -Name $servico -StartupType Disabled -ErrorAction SilentlyContinue
            Write-Host "  + Desabilitado: $servico" -ForegroundColor Green
        }
    }
    catch {
        Write-Host "  x Erro: $servico" -ForegroundColor Red
    }
}

Write-Host ""

# 3. Limpar arquivos temporarios
Write-Host "[3/6] Limpando arquivos temporarios..." -ForegroundColor Yellow

try {
    $tempSize = (Get-ChildItem -Path $env:TEMP -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum / 1MB
    Remove-Item -Path "$env:TEMP\*" -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "  + Liberado: $([math]::Round($tempSize, 2)) MB do TEMP" -ForegroundColor Green
}
catch {
    Write-Host "  - Alguns arquivos em uso" -ForegroundColor Gray
}

Write-Host ""

# 4. Otimizar efeitos visuais
Write-Host "[4/6] Otimizando efeitos visuais..." -ForegroundColor Yellow

try {
    if (-not (Test-Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects")) {
        New-Item -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects" -Force | Out-Null
    }
    Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects" -Name "VisualFXSetting" -Value 2 -Force
    Write-Host "  + Efeitos visuais otimizados" -ForegroundColor Green
}
catch {
    Write-Host "  x Erro ao ajustar efeitos" -ForegroundColor Red
}

Write-Host ""

# 5. Otimizar memoria
Write-Host "[5/6] Otimizando gerenciamento de memoria..." -ForegroundColor Yellow

try {
    Disable-MMAgent -MemoryCompression -ErrorAction SilentlyContinue
    Write-Host "  + Compressao de memoria desabilitada" -ForegroundColor Green
}
catch {
    Write-Host "  - Ja estava desabilitado" -ForegroundColor Gray
}

Write-Host ""

# 6. Listar processos consumindo recursos
Write-Host "[6/6] Processos consumindo mais recursos:" -ForegroundColor Yellow
Write-Host ""

Get-Process | Where-Object {$_.CPU -gt 5} | Sort-Object CPU -Descending | Select-Object -First 10 ProcessName, @{Name="CPU(s)";Expression={[math]::Round($_.CPU,2)}}, @{Name="Memoria(MB)";Expression={[math]::Round($_.WorkingSet64/1MB,2)}}, Id | Format-Table -AutoSize

Write-Host ""
Write-Host "=== RECOMENDACOES ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "+ Inicie o Steam apenas quando for jogar" -ForegroundColor Green
Write-Host "+ Inicie o Docker apenas quando for desenvolver" -ForegroundColor Green  
Write-Host "+ Feche abas nao utilizadas do navegador" -ForegroundColor Green
Write-Host "+ Feche instancias extras do VS Code" -ForegroundColor Green
Write-Host ""
Write-Host "=== DESABILITAR PROGRAMAS NA INICIALIZACAO ===" -ForegroundColor Yellow
Write-Host ""
Write-Host "1. Pressione Ctrl+Shift+Esc" -ForegroundColor White
Write-Host "2. Va em 'Inicializar'" -ForegroundColor White
Write-Host "3. Desabilite: Steam, Docker, Riot Client, Teams" -ForegroundColor White
Write-Host ""
Write-Host "=== OTIMIZACAO CONCLUIDA ===" -ForegroundColor Green
Write-Host ""

$resposta = Read-Host "Deseja reiniciar o computador agora? (S/N)"
if ($resposta -eq "S" -or $resposta -eq "s") {
    Write-Host "Reiniciando em 10 segundos..." -ForegroundColor Cyan
    Start-Sleep -Seconds 10
    Restart-Computer -Force
}
else {
    Write-Host "Lembre-se de reiniciar depois!" -ForegroundColor Yellow
}
