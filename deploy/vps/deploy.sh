#!/usr/bin/env bash
set -euo pipefail

APP_DIR="${APP_DIR:-/opt/clinicapsi}"
COMPOSE_FILE="${COMPOSE_FILE:-docker-compose.vps.yml}"
ASTRA_NET="${ASTRA_NET:-astraseduction_astra-net}"
ASTRA_NGINX_CONF_HOST="${ASTRA_NGINX_CONF_HOST:-/opt/astraseduction/deploy/nginx/default.conf}"
NGINX_SNIPPET="${NGINX_SNIPPET:-$APP_DIR/deploy/vps/nginx-clinicapsi.snippet.conf}"
HEALTH_URL_LOCAL="http://127.0.0.1:${CLINICAPSI_HTTP_PORT:-8080}/health"
HEALTH_URL_PUBLIC="https://psiianasantos.com.br/health"

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

echo "==> Build da imagem (sem derrubar o container ainda)"
docker compose -f "$COMPOSE_FILE" --env-file .env build

echo "==> Recreate clinicapsi-web com nova imagem"
docker compose -f "$COMPOSE_FILE" --env-file .env up -d --no-deps --force-recreate clinicapsi-web

# Garante attachment mesmo se compose antigo nao tinha astra-net
docker network connect "$ASTRA_NET" clinicapsi-app 2>/dev/null || true

wait_http() {
  local url="$1"
  local label="$2"
  local attempts="${3:-40}"
  local i
  for i in $(seq 1 "$attempts"); do
    if curl -fsSk "$url" >/dev/null 2>&1; then
      echo "OK $label ($url)"
      return 0
    fi
    sleep 3
  done
  echo "FALHA: $label nao respondeu a tempo ($url)"
  return 1
}

echo "==> Aguardando health local :8080"
wait_http "$HEALTH_URL_LOCAL" "health-local" 40

# Atualiza snippet ClinicaPsi no nginx do Astra (DNS dinamico) e recarrega
if [[ -f "$NGINX_SNIPPET" && -f "$ASTRA_NGINX_CONF_HOST" ]]; then
  echo "==> Atualizando bloco nginx ClinicaPsi + reload astra-nginx"
  python3 - "$ASTRA_NGINX_CONF_HOST" "$NGINX_SNIPPET" <<'PY'
import sys
from pathlib import Path
host_conf = Path(sys.argv[1])
snippet = Path(sys.argv[2]).read_text()
text = host_conf.read_text()
marker = "# Integracao ClinicaPsi no nginx do Astra"
start = text.find(marker)
if start < 0:
    start = text.find("upstream clinicapsi_app")
if start < 0:
    print("AVISO: nao encontrou bloco ClinicaPsi; append no final")
    host_conf.write_text(text.rstrip() + "\n\n" + snippet + "\n")
    raise SystemExit(0)
start = text.rfind("\n", 0, start) + 1 if start > 0 else start
new_text = text[:start].rstrip() + "\n\n" + snippet.rstrip() + "\n"
host_conf.write_text(new_text)
print(f"nginx conf atualizado: {host_conf}")
PY
  docker exec astra-nginx nginx -t
  docker exec astra-nginx nginx -s reload
else
  echo "AVISO: snippet/nginx host conf ausente; tentando so reload"
  docker exec astra-nginx nginx -s reload 2>/dev/null || true
fi

echo "==> Status"
docker compose -f "$COMPOSE_FILE" ps
docker ps --format 'table {{.Names}}\t{{.Status}}\t{{.Ports}}' | sed -n '1p;/astra-\|clinicapsi-/p'

echo "==> Health via dominio"
wait_http "$HEALTH_URL_PUBLIC" "health-psi" 20 || true
curl -fsSk "https://api.psiianasantos.com.br/health" && echo " OK api.psi" || echo "Dominio api.psi ainda nao respondeu"

echo "Deploy concluido."
echo "ClinicaPsi: https://psiianasantos.com.br  |  https://api.psiianasantos.com.br  |  :8080"
echo "Astra: https://astrasedution.com"
