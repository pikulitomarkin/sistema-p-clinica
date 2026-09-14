# Deploy ClinicaPsi na VPS (coexistência com Astra Seduction)

## Mapa de portas / dominios

| Projeto | Portas host | Dominio / URL |
|---------|-------------|---------------|
| Astra Seduction | 80, 443 | https://astrasedution.com |
| ClinicaPsi | 80/443 (via nginx Astra) + **8080** backup | https://psiianasantos.com.br / https://api.psiianasantos.com.br |

O ClinicaPsi usa rede Docker propria (`clinicapsi-net`) + entra na rede `astraseduction_astra-net` para o proxy. PostgreSQL interno **sem** publicar 5432.

## Deploy rapido

```bash
# Na VPS
cd /opt/clinicapsi
cp .env.vps.example .env   # edite a senha do Postgres
chmod +x deploy/vps/deploy.sh
./deploy/vps/deploy.sh
```

## Nginx / SSL

Snippet: `deploy/vps/nginx-clinicapsi.snippet.conf` (ja aplicado em `/opt/astraseduction/deploy/nginx/default.conf`).
Certificado Let's Encrypt: `psiianasantos.com.br` (+ www + api).
