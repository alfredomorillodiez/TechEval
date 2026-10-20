#!/bin/sh
# Escribe appsettings.json de la web con la URL de la API de este despliegue.
#
# El `dotnet publish` copia a la imagen el appsettings.json de desarrollo, con la API en
# https://localhost:60807/. Sin este guion, una imagen solo sirve para quien tenga la API ahí, y
# apuntarla a otra obliga a reconstruirla. nginx ejecuta lo que haya en /docker-entrypoint.d/
# antes de arrancar, y un fallo aquí detiene el contenedor.
set -eu

API_BASE_URL="${API_BASE_URL:-http://localhost:5000/}"

# La web usa la URL como base de las rutas relativas: sin la barra final, `api/results` se pega
# al último segmento de la URL y no a ella.
case "$API_BASE_URL" in
    */) ;;
    *) API_BASE_URL="${API_BASE_URL}/" ;;
esac

# Se escribe a mano, sin una herramienta de JSON. Una comilla o una barra invertida romperían el
# fichero, y la web arrancaría sin saber dónde está la API. Mejor no arrancar y decir por qué.
case "$API_BASE_URL" in
    *\"* | *\\*)
        echo "API_BASE_URL no puede llevar comillas ni barras invertidas: $API_BASE_URL" >&2
        exit 1
        ;;
esac

# HTML_DIR solo cambia en las pruebas del guion. En el contenedor vale el de nginx.
HTML_DIR="${HTML_DIR:-/usr/share/nginx/html}"

printf '{\n  "ApiBaseUrl": "%s"\n}\n' "$API_BASE_URL" > "$HTML_DIR/appsettings.json"

# El publish deja copias comprimidas de cada fichero. Las de este siguen con la URL de desarrollo:
# si algún día se activa gzip_static o brotli, un cliente recibiría la URL vieja.
rm -f "$HTML_DIR/appsettings.json.br" "$HTML_DIR/appsettings.json.gz"

echo "web: ApiBaseUrl = $API_BASE_URL"
