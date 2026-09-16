## 1. El arranque con Compose (E11)

- [x] 1.1 Corregir la ruta de `sqlcmd` en el `healthcheck` y añadir el `-C` que exige la versión 18. Verificar que la ruta existe dentro de la imagen.
- [x] 1.2 Levantar el stack y comprobar que `sqlserver` llega a `healthy`. Verificar con `docker inspect`.
- [x] 1.3 Comprobar que `api` arranca después, que es lo que el `healthcheck` roto impedía. Verificar que responde.

## 2. Bootstrap (E9)

- [x] 2.1 Corregir el `integrity` de `bootstrap.bundle.min.js` con el valor del contenido servido. Verificar en la consola del navegador que ya no se bloquea.
- [x] 2.2 Comprobar que `window.bootstrap` existe en la página cargada. Verificar ejecutándolo en el navegador.
- [x] 2.3 Dejar escrito en el comentario por qué no era un cambio del CDN. Verificar leyendo el fichero.

## 3. Rotación de secretos (S7)

- [x] 3.1 Escribir el guion de rotación. Verificar que no imprime ningún valor y que guarda copia del `.env` anterior.
- [x] 3.2 Ejecutarlo en un directorio de pruebas. Verificar que escribe el fichero, que los valores son distintos en dos ejecuciones y que no aparecen en la salida.
- [x] 3.3 Mover la lista de comprobación a `docs/rotacion-de-secretos.md`. Verificar que incluye el aviso de que rotar la clave JWT cierra los exámenes en curso.
- [x] 3.4 Apuntar desde `README.md` a la ruta nueva. Verificar que el enlace anterior ya no aparece en ningún fichero.

## 4. Documentación sin IA (E10)

- [x] 4.1 Retirar de `documentacion.md` la sección 8 y el resto de apartados de generación con IA. Verificar que no queda ninguna mención de Ollama.
- [x] 4.2 Retirar las entradas de configuración y las tablas de trabajos de generación. Verificar con una búsqueda.
- [x] 4.3 Revisar que ninguna referencia cruzada apunte a lo retirado. Verificar con una búsqueda de "sección 8".

## 5. Cierre

- [x] 5.1 Ejecutar `dotnet test` completo. Verificar que sigue en verde.
- [x] 5.2 Comprobar que no queda ningún secreto en ficheros seguidos por git. Verificar con una búsqueda.
