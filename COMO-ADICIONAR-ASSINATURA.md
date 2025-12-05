# Como Adicionar Assinatura Digital nos PDFs

## 📝 Passo a Passo

### 1. Preparar a Imagem da Assinatura

#### Opção A - Edição Manual (Recomendado)
1. Abra a foto da assinatura em um editor de imagens (Paint.NET, GIMP, Photoshop, etc.)
2. Recorte apenas a área da assinatura (o "A" estilizado)
3. Remova o fundo branco deixando transparente (formato PNG)
4. Ajuste o tamanho para aproximadamente 200x100 pixels
5. Salve como: `assinatura-psicologo.png`

#### Opção B - Ferramenta Online
1. Acesse: https://www.remove.bg/ ou https://pixlr.com/br/
2. Upload da foto da assinatura
3. Remova o fundo automaticamente
4. Baixe como PNG transparente
5. Salve como: `assinatura-psicologo.png`

### 2. Salvar no Projeto

Copie a imagem para:
```
src/ClinicaPsi.Web/wwwroot/images/assinaturas/assinatura-psicologo.png
```

### 3. Código já está preparado!

O código foi atualizado para incluir a assinatura automaticamente. Basta adicionar o arquivo PNG no local correto.

## 🎨 Especificações da Imagem

- **Formato**: PNG com fundo transparente
- **Tamanho recomendado**: 200x100 pixels (largura x altura)
- **Qualidade**: Mínimo 150 DPI
- **Cor**: Preferencialmente em azul escuro ou preto
- **Posição**: Centralizada acima do nome do psicólogo

## 📋 Checklist

- [ ] Imagem recortada (apenas assinatura)
- [ ] Fundo removido (transparente)
- [ ] Tamanho ajustado (200x100px)
- [ ] Salva como PNG
- [ ] Copiada para: `wwwroot/images/assinaturas/assinatura-psicologo.png`
- [ ] Testar geração de PDF

## 🔧 Personalização por Psicólogo (Futuro)

Para ter assinaturas diferentes por psicólogo:
1. Renomeie como: `assinatura-{crp}.png` (ex: `assinatura-08-45168.png`)
2. O sistema buscará primeiro a assinatura específica do psicólogo
3. Se não encontrar, usa a assinatura padrão

## ⚠️ Importante

- A assinatura digital é apenas visual
- O documento já tem validade legal sem ela
- A assinatura melhora a aparência profissional
- Certifique-se de ter direitos sobre a imagem da assinatura

## 🧪 Teste

Após adicionar a imagem:
1. Execute: `dotnet run --project src/ClinicaPsi.Web`
2. Acesse a página de documentos
3. Gere uma declaração ou atestado
4. Verifique se a assinatura aparece no PDF
