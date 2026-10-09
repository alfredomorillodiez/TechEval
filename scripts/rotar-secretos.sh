#!/usr/bin/env bash
#
# Genera valores nuevos para los secretos del despliegue y los escribe en .env
#
# Este guion NO rota nada por sí solo. Genera los valores y deja el fichero listo.
# Cambiarlos en los sistemas de destino son pasos manuales: están en
# docs/rotacion-de-secretos.md y hay que darlos por obligatorios.
#
# Los valores se generan en esta máquina. El guion no los imprime ni los envía a ninguna
# parte. No hace falta verlos: van al .env, que es de donde los lee Docker Compose.
#
# Del .env anterior conserva lo que NO es un secreto y no cambia al rotar: la edición de SQL
# Server (MSSQL_PID), las dos URL (FRONTEND_BASE_URL y API_BASE_URL), y el tenant, el cliente y
# el remitente del correo. Sin eso, el .env nuevo no bastaba para levantar el stack. Los cuatro
# secretos NUNCA se copian del anterior.
#
# Después de rotar, scripts/comprobar-rotacion.sh dice si algún valor en uso sigue en el
# historial de git.
#
# Uso:  ./scripts/rotar-secretos.sh [directorio]

set -euo pipefail

RAIZ="${1:-$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)}"
ENV_PATH="$RAIZ/.env"

clave_aleatoria() {
    openssl rand -base64 "$1"
}

contrasena_sql() {
    # SQL Server exige mayúsculas, minúsculas y dígitos o símbolos. Base64 puede salir sin
    # alguno de los tres, así que se garantiza con un sufijo fijo y suficiente entropía antes.
    printf '%saA1!' "$(clave_aleatoria 24 | tr '/+' '_-')"
}

# Valor de una variable en el .env anterior, o el valor por defecto si no estaba o estaba vacía.
# Solo se llama con variables que no son secretos.
conservado() {
    local nombre="$1" defecto="$2" valor=""
    if [ -n "${COPIA:-}" ]; then
        valor="$(grep -E "^[[:space:]]*${nombre}[[:space:]]*=" "$COPIA" | head -n 1 \
            | sed -E 's/^[^=]*=[[:space:]]*//; s/[[:space:]]+$//' || true)"
    fi
    if [ -n "$valor" ]; then printf '%s' "$valor"; else printf '%s' "$defecto"; fi
}

# Un .env en uso perdido deja el despliegue sin arrancar y sin forma de recuperar lo que
# tenía. Copia antes de escribir, siempre.
COPIA=""
if [ -f "$ENV_PATH" ]; then
    COPIA="$ENV_PATH.$(date +%Y%m%d-%H%M%S).bak"
    cp "$ENV_PATH" "$COPIA"
    echo "Se ha guardado el .env anterior en $(basename "$COPIA")"
fi

MSSQL_PID_V="$(conservado MSSQL_PID '')"
FRONTEND_V="$(conservado FRONTEND_BASE_URL 'http://localhost:5001')"
API_V="$(conservado API_BASE_URL 'http://localhost:5000/')"
TENANT_V="$(conservado O365_TENANT_ID '')"
CLIENTE_V="$(conservado O365_CLIENT_ID '')"
REMITENTE_V="$(conservado EMAIL_FROM '')"

umask 077
cat > "$ENV_PATH" <<EOF
# Generado por scripts/rotar-secretos.sh el $(date '+%Y-%m-%d %H:%M').
# NO subir al repositorio. .gitignore ya lo cubre.
#
# Rotar estos valores aquí NO basta. Ver docs/rotacion-de-secretos.md para los pasos
# que hay que dar en SQL Server, en el proveedor de correo y con la cuenta de
# administrador. Sin ellos, los valores antiguos siguen siendo válidos.
#
# Los secretos de abajo son nuevos. Lo demás se ha copiado del .env anterior.

# Edición de SQL Server: Developer (solo desarrollo), Express, Standard o Enterprise.
MSSQL_PID=$MSSQL_PID_V

SA_PASSWORD=$(contrasena_sql)
JWT_SECRET_KEY=$(clave_aleatoria 48)
ADMIN_PASSWORD=$(contrasena_sql)

# La web y la API, vistas desde el navegador. Sin barra final en FRONTEND_BASE_URL.
FRONTEND_BASE_URL=$FRONTEND_V
API_BASE_URL=$API_V

# El secreto de la aplicación de correo NO se puede generar aquí: lo emite Entra ID.
# Emite uno nuevo en el registro de la aplicación, revoca el anterior y pégalo.
O365_TENANT_ID=$TENANT_V
O365_CLIENT_ID=$CLIENTE_V
O365_CLIENT_SECRET=
EMAIL_FROM=$REMITENTE_V
EOF

cat <<'FIN'

Escrito .env con valores nuevos. No se muestran por pantalla a proposito.

FALTA lo que este guion no puede hacer:
  1. Cambiar la contrasena de 'sa' en el SQL Server de destino.
  2. Revocar en Entra ID el secreto de cliente anterior de la aplicación de correo y pegar el nuevo en O365_CLIENT_SECRET.
  3. Reasignar la contrasena del administrador ya creado.
  4. Cambiar la contrasena del buzon cuyas credenciales SMTP estuvieron en git.
FIN

FALTAN=""
[ -z "$MSSQL_PID_V" ] && FALTAN="$FALTAN
  - MSSQL_PID (Developer solo para desarrollo; Express, Standard o Enterprise en produccion)"
[ -z "$TENANT_V" ] && FALTAN="$FALTAN
  - O365_TENANT_ID"
[ -z "$CLIENTE_V" ] && FALTAN="$FALTAN
  - O365_CLIENT_ID"
[ -z "$REMITENTE_V" ] && FALTAN="$FALTAN
  - EMAIL_FROM"
if [ -n "$FALTAN" ]; then
    printf '\nEl .env anterior no las tenia y hay que rellenarlas a mano:%s\n' "$FALTAN"
fi

cat <<'FIN'

AVISO: la clave JWT nueva cierra todas las sesiones abiertas, tambien las
invitaciones de examen en curso. Elige una hora sin examenes.

Cuando termines, comprueba que ningun valor en uso sigue en el historial:
  ./scripts/comprobar-rotacion.sh

Pasos completos: docs/rotacion-de-secretos.md
FIN
