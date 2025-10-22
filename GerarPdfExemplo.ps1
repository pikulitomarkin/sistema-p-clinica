# Script PowerShell para gerar PDF de exemplo
# Execute: .\GerarPdfExemplo.ps1

$projectPath = "C:\Users\Admin\sistema-p-clinica-clean\src\ClinicaPsi.Web"
$url = "http://localhost:5000/Cliente/Historico?handler=ExportarPdf"

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "  GERADOR DE PDF DE EXEMPLO - ClinicaPsi" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Este script gerará um PDF de exemplo do histórico de consultas." -ForegroundColor Yellow
Write-Host ""
Write-Host "Passos:" -ForegroundColor Green
Write-Host "1. Inicie a aplicação: dotnet run" -ForegroundColor White
Write-Host "2. Faça login como Cliente (paciente)" -ForegroundColor White
Write-Host "3. Acesse: Meu Histórico > Exportar PDF" -ForegroundColor White
Write-Host ""
Write-Host "Credenciais de teste:" -ForegroundColor Green
Write-Host "Email: paciente@clinicapsi.com" -ForegroundColor White
Write-Host "Senha: Paciente@123" -ForegroundColor White
Write-Host ""
Write-Host "O PDF será gerado com:" -ForegroundColor Cyan
Write-Host "- Dados do paciente (Nome, CPF, Email, Telefone)" -ForegroundColor White
Write-Host "- Saldo de PsicoPontos" -ForegroundColor White
Write-Host "- Estatísticas (Total, Realizadas, Canceladas, Valor)" -ForegroundColor White
Write-Host "- Tabela completa de consultas" -ForegroundColor White
Write-Host "- Informações sobre o sistema PsicoPontos" -ForegroundColor White
Write-Host ""
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

$response = Read-Host "Deseja iniciar a aplicação agora? (S/N)"

if ($response -eq "S" -or $response -eq "s") {
    Write-Host ""
    Write-Host "Iniciando aplicação..." -ForegroundColor Green
    Write-Host ""
    Set-Location $projectPath
    dotnet run
} else {
    Write-Host ""
    Write-Host "Para iniciar manualmente, execute:" -ForegroundColor Yellow
    Write-Host "cd $projectPath" -ForegroundColor White
    Write-Host "dotnet run" -ForegroundColor White
    Write-Host ""
}
