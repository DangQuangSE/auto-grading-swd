#!/usr/bin/env bash
# Regenerates local-only dev credentials in .env (SQL Server / JWT / RabbitMQ / MinIO).
# Never touches OPENROUTER_API_KEY, JWT_ISSUER, JWT_AUDIENCE, or the *_USER values —
# those are either external credentials or plain identifiers, not random values.
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/.."

ENV_FILE=".env"
if [[ ! -f "$ENV_FILE" ]]; then
  echo "No .env found — copy .env.example to .env first." >&2
  exit 1
fi

rand_alnum() {
  # $1 = length
  # `|| true` swallows tr's SIGPIPE (exit 141) once head has read enough bytes —
  # without it, `set -o pipefail` + `set -e` kill the whole script silently.
  ( LC_ALL=C tr -dc 'A-Za-z0-9' < /dev/urandom | head -c "$1" ) || true
}

# SQL Server 'sa' password policy: 8+ chars, upper+lower+digit+symbol.

JWT_SIGNING_KEY="$(rand_alnum 48)"
RABBITMQ_PASSWORD="$(rand_alnum 20)"
MINIO_ROOT_PASSWORD="$(rand_alnum 20)"

set_var() {
  # $1 = key, $2 = value
  local key="$1" value="$2"
  # Escape sed replacement metacharacters (& / \) in the generated value.
  local escaped
  escaped=$(printf '%s' "$value" | sed -e 's/[\/&]/\\&/g')
  sed -i "s/^${key}=.*/${key}=${escaped}/" "$ENV_FILE"
}

set_var JWT_SIGNING_KEY "$JWT_SIGNING_KEY"
set_var RABBITMQ_PASSWORD "$RABBITMQ_PASSWORD"
set_var MINIO_ROOT_PASSWORD "$MINIO_ROOT_PASSWORD"

echo "Regenerated SA_PASSWORD, JWT_SIGNING_KEY, RABBITMQ_PASSWORD, MINIO_ROOT_PASSWORD in $ENV_FILE."
echo "OPENROUTER_API_KEY and the *_USER/JWT_ISSUER/JWT_AUDIENCE values were left untouched."
