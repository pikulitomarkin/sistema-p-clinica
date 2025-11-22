# 🐳 Docker Deployment Guide

## Desenvolvimento Local

### 1. Build da imagem

```powershell
docker build -t clinicapsi:latest .
```

### 2. Executar container

```powershell
# Com SQLite (desenvolvimento)
docker run -d `
  -p 5000:80 `
  -v ${PWD}/data:/app/data `
  -e ASPNETCORE_ENVIRONMENT=Development `
  --name clinicapsi-app `
  clinicapsi:latest
```

### 3. Verificar logs

```powershell
docker logs -f clinicapsi-app
```

### 4. Acessar aplicação

```
http://localhost:5000
```

## Docker Compose (Recomendado)

### 1. Criar arquivo .env

Copie `.env.example` para `.env` e configure:

```bash
cp .env.example .env
```

Edite `.env` com suas credenciais:

```env
WHATSAPP_ACCESS_TOKEN=seu_token_aqui
WHATSAPP_PHONE_NUMBER_ID=seu_id_aqui
WHATSAPP_BUSINESS_ACCOUNT_ID=seu_business_id_aqui
```

### 2. Subir aplicação

```powershell
docker-compose up -d
```

### 3. Ver logs

```powershell
docker-compose logs -f clinicapsi-web
```

### 4. Parar aplicação

```powershell
docker-compose down
```

### 5. Rebuild após mudanças

```powershell
docker-compose up -d --build
```

## Produção com Nginx

### 1. Configurar SSL

Coloque seus certificados em `nginx/ssl/`:

```
nginx/ssl/
├── cert.pem
└── key.pem
```

### 2. Atualizar nginx.conf

Descomente a seção HTTPS no `nginx/nginx.conf`:

```nginx
server {
    listen 443 ssl http2;
    server_name seu-dominio.com;

    ssl_certificate /etc/nginx/ssl/cert.pem;
    ssl_certificate_key /etc/nginx/ssl/key.pem;
    # ... resto da configuração
}
```

### 3. Subir com Nginx

```powershell
docker-compose up -d
```

A aplicação estará disponível em:
- HTTP: http://localhost
- HTTPS: https://localhost (após configurar SSL)

## Health Checks

### Endpoint de health check

```
GET http://localhost:5000/health
```

Resposta esperada:

```json
{
  "status": "Healthy",
  "results": {
    "db": {
      "status": "Healthy"
    }
  }
}
```

### Verificar health no Docker

```powershell
docker inspect --format='{{.State.Health.Status}}' clinicapsi-app
```

## Volumes e Persistência

### Volume do banco de dados

O banco SQLite é persistido em volume Docker:

```yaml
volumes:
  clinicapsi-data:
    driver: local
```

### Backup do banco

```powershell
# Copiar banco para host
docker cp clinicapsi-app:/app/data/clinicapsi.db ./backup/

# Restaurar banco
docker cp ./backup/clinicapsi.db clinicapsi-app:/app/data/
```

## Troubleshooting

### Container não inicia

```powershell
# Ver logs detalhados
docker logs clinicapsi-app

# Ver eventos
docker events
```

### Problemas de permissão no volume

```powershell
# Windows: Garantir que Docker tem acesso ao drive
# Docker Desktop > Settings > Resources > File Sharing
```

### Resetar ambiente

```powershell
# Parar e remover containers
docker-compose down -v

# Remover imagem
docker rmi clinicapsi:latest

# Rebuild tudo
docker-compose up -d --build
```

### Acessar shell do container

```powershell
docker exec -it clinicapsi-app /bin/bash
```

## Multi-stage Build

O Dockerfile usa multi-stage build para otimizar:

1. **Build Stage**: SDK completo para compilar
2. **Runtime Stage**: Apenas runtime ASP.NET (menor)

Benefícios:
- Imagem final ~200MB (vs ~1GB com SDK)
- Build mais rápido com cache de layers
- Segurança (sem ferramentas de desenvolvimento)

## Docker Registry

### Push para Docker Hub

```powershell
# Tag
docker tag clinicapsi:latest seu-usuario/clinicapsi:latest

# Login
docker login

# Push
docker push seu-usuario/clinicapsi:latest
```

### Push para AWS ECR

```powershell
# Login
aws ecr get-login-password --region us-east-1 | docker login --username AWS --password-stdin 123456789.dkr.ecr.us-east-1.amazonaws.com

# Tag
docker tag clinicapsi:latest 123456789.dkr.ecr.us-east-1.amazonaws.com/clinicapsi:latest

# Push
docker push 123456789.dkr.ecr.us-east-1.amazonaws.com/clinicapsi:latest
```

## Comandos Úteis

```powershell
# Listar containers
docker ps -a

# Listar imagens
docker images

# Remover containers parados
docker container prune

# Remover imagens não usadas
docker image prune

# Ver uso de recursos
docker stats clinicapsi-app

# Inspecionar container
docker inspect clinicapsi-app

# Ver networks
docker network ls

# Ver volumes
docker volume ls
```

## Performance

### Otimizações

1. **Layer caching**: COPY ordenado para melhor cache
2. **Multi-stage**: Imagem final menor
3. **.dockerignore**: Evita copiar arquivos desnecessários
4. **Health checks**: Restart automático se unhealthy

### Monitoramento

```powershell
# CPU e memória em tempo real
docker stats

# Logs com timestamp
docker logs -f --timestamps clinicapsi-app
```

## Segurança

### Boas práticas implementadas

✅ Non-root user (aspnet user)
✅ Multi-stage build
✅ Health checks
✅ Minimal base image (aspnet vs sdk)
✅ No secrets no Dockerfile
✅ .dockerignore configurado

### Scan de vulnerabilidades

```powershell
# Docker Scout (se disponível)
docker scout cves clinicapsi:latest

# Trivy
trivy image clinicapsi:latest
```

## CI/CD

### GitHub Actions

Exemplo de workflow `.github/workflows/docker.yml`:

```yaml
name: Docker Build

on:
  push:
    branches: [ main ]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Build image
        run: docker build -t clinicapsi:latest .
      
      - name: Run tests
        run: docker run --rm clinicapsi:latest dotnet test
      
      - name: Push to registry
        run: |
          echo ${{ secrets.DOCKER_PASSWORD }} | docker login -u ${{ secrets.DOCKER_USERNAME }} --password-stdin
          docker push clinicapsi:latest
```

## Comparação: Docker vs AWS

| Aspecto | Docker Local | AWS ECS | AWS App Runner |
|---------|--------------|---------|----------------|
| Setup | Simples | Médio | Simples |
| Custo | $0 | ~$15-50/mês | ~$25-40/mês |
| Escalabilidade | Manual | Auto | Auto |
| Load Balancer | Nginx | ALB | Incluído |
| SSL | Manual | ACM | Incluído |
| Logs | Docker | CloudWatch | CloudWatch |
| Banco | SQLite | RDS | RDS |

## Próximos Passos

- [ ] Configurar CI/CD
- [ ] Adicionar monitoring (Prometheus/Grafana)
- [ ] Configurar backup automático
- [ ] Implementar log aggregation
- [ ] Configurar alertas
- [ ] Deploy em AWS
