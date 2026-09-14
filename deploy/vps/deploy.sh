#!/usr/bin/env bash
set -euo pipefail

APP_DIR="${APP_DIR:-/opt/clinicapsi}"
COMPOSE_FILE="${COMPOSE_FILE:-docker-compose.vps.yml}"

cd "$APP_DIR"

if [[ ! -f .env ]]; then
  echo "ERRO: falta $APP_DIR/.env (copie de .env.vps.example)"
  exit 1
fi

# Garante que Astra continua intacto
if ! docker ps --format '{{.Names}}' | grep -qx 'astra-nginx'; then
  echo "AVISO: astra-nginx nao esta rodando. Continuando deploy do ClinicaPsi..."
fi

echo "==> Build + up ClinicaPsi (porta host 8080 por padrao)"
docker compose -f "$COMPOSE_FILE" --env-file .env up -d --build

echo "==> Status"
docker compose -f "$COMPOSE_FILE" ps
docker ps --format 'table {{.Names}}\t{{.Status}}\t{{.Ports}}' | sed -n '1p;/astra-\|clinicapsi-/p'

echo "==> Health"
sleep 5
curl -fsS "http://127.0.0.1:${CLINICAPSI_HTTP_PORT:-8080}/health" && echo " OK" || echo "Aguardando app subir..."

echo "Deploy concluido. ClinicaPsi: http://$(hostname -I | awk '{print $1}'):${CLINICAPSI_HTTP_PORT:-8080}"
echo "Astra permanece em :80/:443 (astrasedution.com)"
