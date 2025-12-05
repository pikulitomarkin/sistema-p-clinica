# Script para desabilitar completamente o Efficiency Mode do Windows 11
# Execute como Administrador: clique direito -> "Executar como Administrador"

Write-Host "Desabilitando Efficiency Mode..." -ForegroundColor Cyan

# Desabilitar EcoQoS (Quality of Service de eficiência energética)
try {
    Set-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\Power" -Name "EnergyEstimationEnabled" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue
    Write-Host "✓ EnergyEstimationEnabled desabilitado" -ForegroundColor Green
}
catch {
    Write-Host "✗ Erro ao desabilitar EnergyEstimationEnabled: $($_.Exception.Message)" -ForegroundColor Red
}

try {
    Set-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\Power" -Name "EcoQoSEnabled" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue
    Write-Host "✓ EcoQoSEnabled desabilitado" -ForegroundColor Green
}
catch {
    Write-Host "✗ Erro ao desabilitar EcoQoSEnabled: $($_.Exception.Message)" -ForegroundColor Red
}

# Desabilitar o Throttling de processos em background
try {
    Set-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Power" -Name "HiberbootEnabled" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue
    Write-Host "✓ HiberbootEnabled desabilitado" -ForegroundColor Green
}
catch {
    Write-Host "✗ Erro ao desabilitar HiberbootEnabled: $($_.Exception.Message)" -ForegroundColor Red
}

# Desabilitar o Power Throttling
try {
    if (-not (Test-Path "HKLM:\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling")) {
        New-Item -Path "HKLM:\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling" -Force | Out-Null
    }
    Set-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling" -Name "PowerThrottlingOff" -Value 1 -Type DWord -Force -ErrorAction SilentlyContinue
    Write-Host "✓ PowerThrottling desabilitado" -ForegroundColor Green
}
catch {
    Write-Host "✗ Erro ao desabilitar PowerThrottling: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Yellow
Write-Host "IMPORTANTE: Reinicie o computador para aplicar as alteracoes!" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Yellow
Write-Host ""

# Perguntar se quer reiniciar agora
$resposta = Read-Host "Deseja reiniciar o computador agora? (S/N)"
if ($resposta -eq "S" -or $resposta -eq "s") {
    Write-Host "Reiniciando em 10 segundos..." -ForegroundColor Cyan
    Start-Sleep -Seconds 3
    Restart-Computer -Force
}
else {
    Write-Host "Lembre-se de reiniciar depois!" -ForegroundColor Yellow
}
