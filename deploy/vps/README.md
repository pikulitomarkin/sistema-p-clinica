# Deploy ClinicaPsi na VPS (coexistência com Astra Seduction)

## Mapa de portas

| Projeto | Portas host | Dominio / URL |
|---------|-------------|---------------|
| Astra Seduction | 80, 443 | https://astrasedution.com |
| ClinicaPsi | **8080** | http://IP:8080 |

O ClinicaPsi usa rede Docker propria (`clinicapsi-net`) e PostgreSQL interno **sem** publicar a porta 5432 no host.

## Deploy rapido

```bash
# Na VPS
cd /opt/clinicapsi
cp .env.vps.example .env   # edite a senha do Postgres
chmod +x deploy/vps/deploy.sh
./deploy/vps/deploy.sh
```

## Dominio futuro (opcional)

Veja `deploy/vps/nginx-clinicapsi.snippet.conf` para integrar no nginx do Astra sem trocar as portas 80/443.
