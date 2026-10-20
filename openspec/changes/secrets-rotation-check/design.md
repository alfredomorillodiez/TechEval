## Context

Ver `proposal.md` — Why. Los requisitos están en `specs/`.

Lo que condiciona el enfoque:

- Rotar un secreto es sustituirlo. De los cuatro pasos de `docs/rotacion-de-secretos.md`, tres exigen acceso a un sistema de destino (el servidor de SQL Server, Entra ID, la cuenta del administrador ya creada) y los hace una persona. Lo único que se puede comprobar desde el repositorio es **qué valores hay en uso**, y si alguno de ellos **está en el historial**.
- Los guiones de rotación generan el `.env` desde cero con una plantilla fija. Con `docker-outside-localhost`, `docker-compose.yml` exige `MSSQL_PID` y define `FRONTEND_BASE_URL` y `API_BASE_URL`. Una plantilla que no los lleva deja un `.env` con el que Compose no arranca.
- El guion copia ya el `.env` anterior a `.env.<fecha>.bak`, pero `.gitignore` solo cubre `.env`: los respaldos, con los secretos que hay que dar por comprometidos, no estaban ignorados.
- `git log --all -S<cadena>` devuelve los commits en los que cambia el número de veces que aparece la cadena. Es una búsqueda literal, independiente de en qué fichero ni con qué formato se escribió el secreto (YAML, JSON, `.env`, una línea de documentación).
- No existe un `.env` real en este equipo (comprobado el 09·10·2026): no hay un despliegue en curso que rotar.

## Goals / Non-Goals

**Goals:**

- Que ejecutar el guion de rotación no estropee un despliegue que funcionaba.
- Una prueba objetiva, ejecutable por cualquiera, de que ningún secreto en uso es uno de los que estuvieron en git.

**Non-Goals:**

- Rotar nada en un sistema de destino, ni generar el secreto de Entra ID.
- Reescribir el historial de git.
- Comprobar la base de datos (que la contraseña de `sa` o la del administrador sean las nuevas). Exigiría conectar al servidor con las credenciales.

## Decisions

### D1. La comprobación busca el valor en el historial, no una lista de valores antiguos

Una lista de «los valores antiguos» tendría que escribirse en algún sitio, y ese sitio sería otro secreto comprometido. `git log -S` resuelve la misma pregunta («¿este valor estuvo en git?») sin guardar nada: el historial **es** la lista.

El valor llega a `git` como argumento de la orden, así que mientras dura la búsqueda es visible para los procesos de la máquina. Se acepta y se documenta: son valores que se están rotando, la máquina es la de quien despliega, y la alternativa (volcar el historial a un fichero para filtrarlo) es peor.

### D2. Qué hace fallar a la comprobación

Un valor en el historial, un secreto ausente o vacío, un valor de menos de 8 caracteres, `.env` o `appsettings.Local.json` rastreados, o una copia superficial.

- **Menos de 8 caracteres**: `-S` con una cadena corta casa con medio repositorio, o con nada si es única; en los dos casos no dice nada. Un secreto tan corto es, además, un problema en sí.
- **Copia superficial**: es el falso negativo más peligroso. Un `git clone --depth 1` no trae los commits antiguos, no encuentra los valores viejos y la comprobación diría «todo bien» sin haber mirado.
- Los cuatro secretos incluyen `O365_CLIENT_SECRET`, aunque no estuviera en git: si algún día se pegara en un fichero versionado, la comprobación lo vería.

### D3. Salida: nombres y commits, nunca valores

Cada línea es `[ok]` o `[FALLO]` seguida del nombre de la variable y, si aparece en el historial, el hash corto del commit. Nunca el valor. Las pruebas comprueban que ni los valores nuevos ni los viejos salen en la salida, en ninguno de los dos guiones.

### D4. El guion de rotación copia de una lista cerrada de variables

`MSSQL_PID`, `FRONTEND_BASE_URL`, `API_BASE_URL`, `O365_TENANT_ID`, `O365_CLIENT_ID` y `EMAIL_FROM`. Una lista cerrada y no «todo lo que no sea un secreto»: la segunda forma obliga a decidir qué es secreto, y un error copiaría uno al `.env` nuevo, que es lo contrario de rotar. Las pruebas comprueban que ningún valor anterior de los tres secretos generados ni del secreto de Entra pasa al `.env` nuevo.

Qué se escribe cuando no estaba en el anterior: las dos URL de ejemplo (valen para este equipo, y `.env.example` dice lo mismo), `MSSQL_PID` vacío (para que Compose obligue a elegir: un valor por defecto reabriría el problema de `Developer` en producción), y vacío el resto.

### D5. `.env.*` en `.gitignore`

Con `!.env.example` para que el de ejemplo siga versionado.

### D6. Dos implementaciones, no una

Un guion en bash y otro en PowerShell, como ya tenían los de rotación. En este equipo se usa PowerShell; en un servidor Linux, bash. Se prueban los dos con los mismos casos, y esa duplicación encontró un error real en la versión PowerShell que la de bash no tenía (`"$nombre: ..."` se interpreta como una variable con ámbito).

## Risks / Trade-offs

- **Un valor que aparezca en el historial por casualidad** (una contraseña corta y común que coincida con otra cadena) daría un falso positivo. Con 8 caracteres de mínimo y valores generados al azar, es despreciable; si pasa, se rota otra vez.
- **No ve ramas que no estén en el repositorio local**, ni `git stash`, ni objetos sin referencia. `--all` cubre ramas, etiquetas y remotos que se hayan traído. Para un repositorio con ramas que solo viven en el remoto, hay que hacer `git fetch --all` antes.
- **Un .env falso positivo de «rotado»**: la comprobación dice que los valores en uso no estuvieron en git. No dice que sean los que el servidor tiene. Si alguien rota el `.env` pero no cambia la contraseña de `sa` en el servidor, la API no conecta; si no reasigna al administrador, entra con la antigua. Son los pasos 2 y 4 del procedimiento, y la comprobación lo repite en su salida.
