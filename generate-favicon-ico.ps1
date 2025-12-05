# Script para gerar favicon.ico a partir de SVG
Write-Host "Gerando favicon.ico..." -ForegroundColor Cyan

$svgPath = "src/ClinicaPsi.Web/wwwroot/images/favicon.svg"
$icoPath = "src/ClinicaPsi.Web/wwwroot/favicon.ico"

# Criar SVG otimizado para ICO (tamanho 32x32)
$svgContent = @"
<svg xmlns="http://www.w3.org/2000/svg" width="32" height="32" viewBox="0 0 512 512">
  <defs>
    <linearGradient id="bg" x1="0%" y1="0%" x2="100%" y2="100%">
      <stop offset="0%" style="stop-color:#198754"/>
      <stop offset="100%" style="stop-color:#0d6efd"/>
    </linearGradient>
  </defs>
  <rect width="512" height="512" rx="100" fill="url(#bg)"/>
  <text x="256" y="340" font-family="Arial,sans-serif" font-size="280" font-weight="bold" fill="white" text-anchor="middle">PS</text>
</svg>
"@

# Salvar SVG temporário
$tempSvg = [System.IO.Path]::GetTempFileName() + ".svg"
$svgContent | Out-File -FilePath $tempSvg -Encoding UTF8

Write-Host "SVG criado: $tempSvg" -ForegroundColor Green
Write-Host ""
Write-Host "Para converter SVG para ICO, você pode:" -ForegroundColor Yellow
Write-Host "1. Usar uma ferramenta online como https://convertio.co/svg-ico/" -ForegroundColor White
Write-Host "2. Usar ImageMagick: magick convert favicon.svg -define icon:auto-resize=32,16 favicon.ico" -ForegroundColor White
Write-Host "3. Ou simplesmente usar o SVG (navegadores modernos suportam)" -ForegroundColor White
Write-Host ""
Write-Host "SVG temporário salvo em: $tempSvg" -ForegroundColor Cyan
