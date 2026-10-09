#!/usr/bin/env bash
#
# Comprueba que los secretos del .env no son los que estuvieron en el historial de git.
#
# Rotar un secreto es sustituirlo por otro. La única prueba objetiva de que se hizo es que el
# valor que está en uso NO aparece en ningún commit: un despliegue con los valores viejos
# funciona igual de bien que uno con los nuevos.
#
# Para SA_PASSWORD, JWT_SECRET_KEY, ADMIN_PASSWORD y O365_CLIENT_SECRET, busca el valor del .env
# en todo el historial (git log --all -S). Falla si:
#   - alguno aparece en cualquier commit;
#   - falta alguno, o es demasiado corto para comprobarlo con fiabilidad (menos de 8 caracteres);
#   - .env o appsettings.Local.json están rastreados por git;
#   - el repositorio es una copia superficial: un historial incompleto daría un falso «todo bien».
#
# NO imprime ningún valor, solo el nombre de la variable y el commit donde apareció. Pero git
# recibe el valor como argumento, así que durante la búsqueda es visible para los procesos de
# esta máquina. Ejecútalo en una máquina de confianza.
#
# No comprueba lo que no está en git: que la contraseña de 'sa' del servidor sea la del .env, que
# el secreto de cliente anterior esté revocado en Entra ID, o que la cuenta de administrador ya
# creada tenga la contraseña nueva. Eso son los pasos manuales de docs/rotacion-de-secretos.md.
#
# Código de salida: 0 si todo está bien; 1 si hay algo que corregir.
#
# Uso:  ./scripts/comprobar-rotacion.sh [directorio]

set -uo pipefail

RAIZ="${1:-$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)}"
LONGITUD_MINIMA=8
SECRETOS=(SA_PASSWORD JWT_SECRET_KEY ADMIN_PASSWORD O365_CLIENT_SECRET)
FICHEROS=(.env src/TechEval.API/appsettings.Local.json)

cd "$RAIZ" || exit 1

PROBLEMAS=()
BUENOS=()

if ! git rev-parse --git-dir >/dev/null 2>&1; then
    echo "No es un repositorio de git: $RAIZ" >&2
    exit 1
fi

# Un clon con --depth no trae el historial entero: un valor antiguo podría no aparecer y la
# comprobación diría que todo va bien.
if [ "$(git rev-parse --is-shallow-repository)" = "true" ]; then
    PROBLEMAS+=("el repositorio es una copia superficial: el historial esta incompleto y la comprobacion no es fiable. Ejecuta 'git fetch --unshallow'.")
fi

for f in "${FICHEROS[@]}"; do
    if git ls-files --error-unmatch "$f" >/dev/null 2>&1; then
        PROBLEMAS+=("$f esta rastreado por git: sacalo con 'git rm --cached $f'.")
    fi
done

ENV_PATH="$RAIZ/.env"
if [ ! -f "$ENV_PATH" ]; then
    echo "No existe $ENV_PATH. Genera uno con scripts/rotar-secretos.sh." >&2
    exit 1
fi

# Valor de una variable del .env, sin imprimir nada que no sea el valor. Quita las comillas que
# alguien haya puesto alrededor.
valor_de() {
    local linea v
    linea="$(grep -E "^[[:space:]]*$1[[:space:]]*=" "$ENV_PATH" | head -n 1 || true)"
    v="$(printf '%s' "$linea" | sed -E 's/^[^=]*=[[:space:]]*//; s/[[:space:]]+$//; s/\r$//')"
    case "$v" in
        \"*\") v="${v#\"}"; v="${v%\"}" ;;
        \'*\') v="${v#\'}"; v="${v%\'}" ;;
    esac
    printf '%s' "$v"
}

for nombre in "${SECRETOS[@]}"; do
    valor="$(valor_de "$nombre")"

    if [ -z "$valor" ]; then
        PROBLEMAS+=("$nombre falta o esta vacio en el .env.")
        continue
    fi
    if [ "${#valor}" -lt "$LONGITUD_MINIMA" ]; then
        PROBLEMAS+=("$nombre es demasiado corto para comprobarlo (menos de $LONGITUD_MINIMA caracteres): buscaria una cadena trivial.")
        continue
    fi

    # -S busca los commits en los que cambia el numero de veces que aparece la cadena, es decir,
    # los que la añadieron o la quitaron. Cadena literal, no expresion regular.
    if ! commit="$(git log --all --format=%h --max-count=1 "-S$valor" 2>/dev/null)"; then
        PROBLEMAS+=("$nombre: git no pudo buscar en el historial.")
        continue
    fi

    if [ -n "$commit" ]; then
        PROBLEMAS+=("$nombre: su valor aparece en el historial de git (commit $commit). No esta rotado.")
    else
        BUENOS+=("$nombre")
    fi
done

echo
for b in "${BUENOS[@]+"${BUENOS[@]}"}"; do echo "  [ok]    $b no aparece en el historial"; done
for p in "${PROBLEMAS[@]+"${PROBLEMAS[@]}"}"; do echo "  [FALLO] $p"; done
echo

if [ "${#PROBLEMAS[@]}" -gt 0 ]; then
    echo "La rotacion NO esta completa. Pasos: docs/rotacion-de-secretos.md"
    exit 1
fi

echo "Los ${#SECRETOS[@]} secretos del .env son distintos de todo lo que hay en el historial."
echo "Falta lo que esta comprobacion no ve: 'sa' en el servidor, Entra ID y el administrador ya creado."
exit 0
