# 🚀 Otimização do VS Code - ClinicaPsi

## 🎯 Problema Resolvido

O VS Code estava travando porque indexava **304.8 MB** de arquivos em `bin/` e `obj/`, além de arquivos de banco de dados.

## ✅ O que foi feito

### 1. **`.vscode/settings.json`** (Criado)
Configurações otimizadas:
- ✅ Excluiu `bin/`, `obj/`, `.vs/` do explorer e busca
- ✅ Excluiu arquivos `.db` do file watcher
- ✅ Desabilitou formatação automática (economiza CPU)
- ✅ Limitou sugestões e validações
- ✅ Otimizou OmniSharp para C#
- ✅ Desabilitou auto-refresh do Git
- ✅ Desabilitou minimap do editor

### 2. **`.vscode/extensions.json`** (Criado)
Recomenda apenas extensões essenciais:
- C# Dev Kit
- GitHub Copilot
- REST Client

### 3. **`.editorconfig`** (Criado)
Configurações mínimas de formatação para não sobrecarregar o editor.

### 4. **`cleanup-workspace.ps1`** (Criado)
Script para limpar cache e arquivos temporários.

### 5. **`.gitignore`** (Atualizado)
Agora compartilha configurações do VS Code no Git (mas ignora cache).

---

## 🛠️ Como usar

### Primeira vez (OBRIGATÓRIO):

```powershell
# 1. Execute o script de limpeza
.\cleanup-workspace.ps1

# 2. Feche COMPLETAMENTE o VS Code (Ctrl+Q)

# 3. Reabra o workspace

# 4. Restaure e compile
dotnet restore
dotnet build
```

### Limpeza regular (quando o VS Code ficar lento):

```powershell
.\cleanup-workspace.ps1
```

---

## 📊 Melhorias de Performance

| Recurso | Antes | Depois |
|---------|-------|--------|
| Arquivos indexados | bin/ (304 MB) + obj/ (12 MB) | ❌ Excluídos |
| File watcher | Tudo | Apenas código fonte |
| Busca | Tudo | Apenas código relevante |
| Formatação | Auto | Manual |
| Git refresh | Automático | Manual |
| OmniSharp | Padrão | Otimizado |

---

## 🔧 Configurações Principais

### O que foi desabilitado (para ganhar performance):
- ✅ Indexação de `bin/`, `obj/`, `.vs/`
- ✅ File watcher em diretórios de build
- ✅ Formatação automática on-save
- ✅ Validação HTML/CSS/JS (não usados)
- ✅ Git auto-refresh
- ✅ Editor minimap
- ✅ Telemetria

### O que permanece ativo:
- ✅ IntelliSense C#
- ✅ GitHub Copilot
- ✅ Razor formatação
- ✅ Git decorations
- ✅ Auto-save (1s delay)

---

## 💡 Dicas Adicionais

### Se ainda estiver lento:

1. **Limpe o cache do OmniSharp manualmente:**
```powershell
Remove-Item -Path "$env:LOCALAPPDATA\OmniSharp" -Recurse -Force
```

2. **Reinicie o OmniSharp Server:**
   - `Ctrl+Shift+P` → `OmniSharp: Restart OmniSharp`

3. **Verifique extensões instaladas:**
   - Desabilite extensões não essenciais
   - `Ctrl+Shift+X` → Desabilitar extensões pesadas

4. **Feche outros programas:**
   - Navegadores com muitas abas
   - Docker Desktop (se não estiver usando)
   - Aplicativos pesados

### Se quiser mais performance:

Edite `.vscode/settings.json`:

```json
{
  "omnisharp.enableRoslynAnalyzers": false,  // Desabilita analyzers
  "editor.quickSuggestions": false,          // Desabilita sugestões
  "git.enabled": false                        // Desabilita Git completamente
}
```

---

## 📝 Manutenção

### Limpe regularmente:

```powershell
# Limpar tudo de uma vez
dotnet clean
.\cleanup-workspace.ps1

# Ou manual
Remove-Item .\src\*\bin -Recurse -Force
Remove-Item .\src\*\obj -Recurse -Force
```

### Monitore o tamanho:

```powershell
# Ver tamanho dos diretórios
Get-ChildItem -Path ".\src" -Include bin,obj -Recurse -Directory | 
  ForEach-Object { 
    $size = (Get-ChildItem $_.FullName -Recurse -File | Measure-Object -Property Length -Sum).Sum
    [PSCustomObject]@{
      Path = $_.FullName
      SizeMB = [math]::Round($size/1MB, 2)
    }
  }
```

---

## 🚨 Troubleshooting

### VS Code ainda travando?

1. **Verifique se as configurações foram aplicadas:**
   - Abra `.vscode/settings.json`
   - Verifique se `files.exclude` tem `bin` e `obj`

2. **Cache do Windows Search:**
```powershell
# Desabilitar Windows Search nesta pasta (temporariamente)
attrib +S "$PWD\src\*\bin" /S /D
attrib +S "$PWD\src\*\obj" /S /D
```

3. **Antivírus:**
   - Adicione exceção para:
     - `C:\Users\Admin\sistema-p-clinica-clean`
     - `%LOCALAPPDATA%\OmniSharp`

4. **Memória do notebook:**
```powershell
# Ver uso de memória
Get-Process code,dotnet,OmniSharp* | 
  Select-Object ProcessName,@{N='MemoryMB';E={[math]::Round($_.WS/1MB, 2)}}
```

---

## ✨ Resultado Esperado

Com essas otimizações, o VS Code deve:
- ✅ Iniciar em **menos de 10 segundos**
- ✅ IntelliSense responder **instantaneamente**
- ✅ Busca retornar resultados **em menos de 1 segundo**
- ✅ Não travar ao salvar arquivos
- ✅ Usar **menos de 1 GB de RAM**

---

## 📚 Referências

- [VS Code Performance](https://code.visualstudio.com/docs/setup/setup-overview#_performance-issues)
- [OmniSharp Configuration](https://github.com/OmniSharp/omnisharp-vscode/blob/master/README.md)
- [EditorConfig](https://editorconfig.org/)

---

**Última atualização:** 4 de dezembro de 2025
