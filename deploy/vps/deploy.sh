#!/usr/bin/env bash
set -euo pipefail

APP_DIR="${APP_DIR:-/opt/clinicapsi}"
COMPOSE_FILE="${COMPOSE_FILE:-docker-compose.vps.yml}"
ASTRA_NET="${ASTRA_NET:-astraseduction_astra-net}"

cd "$APP_DIR"

if [[ ! -f .env ]]; then
  echo "ERRO: falta $APP_DIR/.env (copie de .env.vps.example)"
  exit 1
fi

if ! docker ps --format '{{.Names}}' | grep -qx 'astra-nginx'; then
  echo "AVISO: astra-nginx nao esta rodando. Continuando deploy do ClinicaPsi..."
fi

if ! docker network inspect "$ASTRA_NET" >/dev/null 2>&1; then
  echo "ERRO: rede externa $ASTRA_NET nao existe (necessaria para proxy por dominio)"
  exit 1
fi

echo "==> Build + up ClinicaPsi (porta host 8080 + rede do nginx Astra)"
docker compose -f "$COMPOSE_FILE" --env-file .env up -d --build

# Garante attachment mesmo se compose antigo nao tinha astra-net
docker network connect "$ASTRA_NET" clinicapsi-app 2>/dev/null || true

echo "==> Status"
docker compose -f "$COMPOSE_FILE" ps
docker ps --format 'table {{.Names}}\t{{.Status}}\t{{.Ports}}' | sed -n '1p;/astra-\|clinicapsi-/p'

echo "==> Health local :8080"
sleep 5
curl -fsS "http://127.0.0.1:${CLINICAPSI_HTTP_PORT:-8080}/health" && echo " OK" || echo "Aguardando app subir..."

echo "==> Health via dominio (se DNS/nginx ok)"
curl -fsSk "https://psiianasantos.com.br/health" && echo " OK psi" || echo "Dominio psi ainda nao respondeu"
curl -fsSk "https://api.psiianasantos.com.br/health" && echo " OK api.psi" || echo "Dominio api.psi ainda nao respondeu"

echo "Deploy concluido."
echo "ClinicaPsi: https://psiianasantos.com.br  |  https://api.psiianasantos.com.br  |  :8080"
echo "Astra: https://astrasedution.com"
