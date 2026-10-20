## 1. Guiones de rotación

- [x] 1.1 `scripts/rotar-secretos.sh` y `.ps1` conservan `MSSQL_PID`, las dos URL, el tenant, el cliente y el remitente del `.env` anterior, escriben los valores de ejemplo cuando faltan, dejan `MSSQL_PID` vacío con aviso y nunca copian un secreto (design.md D4). Verificar en un directorio temporal, con los dos guiones: (a) un `.env` en uso conserva las seis variables; (b) los tres secretos generados difieren de los anteriores y `O365_CLIENT_SECRET` queda vacío; (c) ningún valor anterior de un secreto aparece en el `.env` nuevo; (d) la copia `.bak` conserva el anterior; (e) la salida no imprime ningún valor; (f) sin `.env` previo salen las URL de ejemplo y `MSSQL_PID` vacío con aviso; (g) un `.env` anterior a `docker-outside-localhost` conserva el correo.
- [x] 1.2 `.gitignore` cubre `.env.*` y no `.env.example` (design.md D5). Verificar con `git check-ignore` sobre `.env`, `.env.20261009-101500.bak`, `.env.local` y `.env.example`.

## 2. Comprobación de la rotación

- [x] 2.1 `scripts/comprobar-rotacion.sh` y `.ps1` (design.md D1–D3, D6). Verificar con los dos guiones en un repositorio temporal con un secreto inventado en dos commits (puesto y quitado): todos nuevos sale con 0; cada uno de los tres secretos viejos (uno con `+` y `/`) falla nombrando la variable; `O365_CLIENT_SECRET` vacío falla; un valor de 3 caracteres falla; un valor viejo entre comillas falla; `.env` rastreado falla; una copia con `--depth 1` falla; y en ningún caso sale un valor.
- [x] 2.2 El resultado de `rotar-secretos` pasa `comprobar-rotacion`, con los dos guiones, una vez pegado el secreto de Entra. Verificado en la prueba de 1.1 (extremo a extremo).

## 3. Documentación

- [x] 3.1 `docs/rotacion-de-secretos.md`: la comprobación como paso final, lo que conserva el guion, que la copia `.bak` hay que borrarla al terminar, y que el secreto de Graph nunca estuvo en git (verificado en el historial el 09·10·2026). `openspec validate secrets-rotation-check --strict` pasa.

## 4. Lo que sigue siendo de una persona

- [ ] 4.1 Los pasos 2 a 4 de `docs/rotacion-de-secretos.md`: cambiar la contraseña de `sa` en el servidor, emitir y revocar el secreto de Entra ID, cambiar la contraseña del buzón SMTP de las credenciales versionadas, y reasignar la contraseña del administrador ya creado. Después, ejecutar `comprobar-rotacion` sobre el `.env` real. No hay un `.env` real en este equipo, así que no hay nada rotado todavía.
