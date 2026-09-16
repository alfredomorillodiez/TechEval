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

# Un .env en uso perdido deja el despliegue sin arrancar y sin forma de recuperar lo que
# tenía. Copia antes de escribir, siempre.
if [ -f "$ENV_PATH" ]; then
    COPIA="$ENV_PATH.$(date +%Y%m%d-%H%M%S).bak"
    cp "$ENV_PATH" "$COPIA"
    echo "Se ha guardado el .env anterior en $(basename "$COPIA")"
fi

umask 077
cat > "$ENV_PATH" <<EOF
# Generado por scripts/rotar-secretos.sh el $(date '+%Y-%m-%d %H:%M').
# NO subir al repositorio. .gitignore ya lo cubre.
#
# Rotar estos valores aquí NO basta. Ver docs/rotacion-de-secretos.md para los pasos
# que hay que dar en SQL Server, en el proveedor de correo y con la cuenta de
# administrador. Sin ellos, los valores antiguos siguen siendo válidos.

SA_PASSWORD=$(contrasena_sql)
JWT_SECRET_KEY=$(clave_aleatoria 48)
ADMIN_PASSWORD=$(contrasena_sql)

# La clave del proveedor de correo NO se puede generar aquí: la emite el proveedor.
# Revoca la anterior en su panel, emite una nueva y pégala.
SENDGRID_API_KEY=
EMAIL_FROM=
EOF

cat <<'FIN'

Escrito .env con valores nuevos. No se muestran por pantalla a proposito.

FALTA lo que este guion no puede hacer:
  1. Cambiar la contrasena de 'sa' en el SQL Server de destino.
  2. Revocar la clave de correo anterior en el panel del proveedor.
  3. Reasignar la contrasena del administrador ya creado.
  4. Rellenar SENDGRID_API_KEY y EMAIL_FROM.

AVISO: la clave JWT nueva cierra todas las sesiones abiertas, tambien las
invitaciones de examen en curso. Elige una hora sin examenes.

Pasos completos: docs/rotacion-de-secretos.md
FIN
