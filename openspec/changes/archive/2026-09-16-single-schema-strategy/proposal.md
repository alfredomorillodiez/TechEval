# single-schema-strategy

Una sola forma de crear el esquema, y una prueba que avise cuando el modelo y el guion dejen de decir lo mismo

## Why

Hallazgo `A4`. Hoy conviven tres estrategias sobre la misma base de datos y ninguna puede ser la verdad a la vez que las otras.

- `DbSeeder` llama a `EnsureCreatedAsync()` en cada arranque. Eso crea el esquema desde el modelo de EF y deja `__EFMigrationsHistory` sin crear nunca.
- El `README.md` manda ejecutar `dotnet ef database update`. Sobre una base creada por `EnsureCreated`, la primera migración **falla**, porque EF no encuentra su historial y cree que no se ha aplicado ninguna.
- `scripts/` mantiene el esquema en SQL versionado, que es lo que se usa de verdad al desplegar.

El daño no es teórico. Quien siga el README en una base recién creada por la aplicación se encuentra un error que no explica nada.

**Y ya se ha materializado.** El cambio `faithful-answer-record`, de esta misma mañana, añadió cuatro columnas a `UserAnswers` con un guion aditivo, pero **no las añadió a `create_database.sql`**. Una base creada desde cero con el guion completo hoy no tiene esas columnas, y la aplicación falla al enviar un examen. Nadie lo detectó porque nada compara el modelo con el guion.

Hallazgo `A5` va en el mismo método y es la misma decisión: `DbSeeder` siembra cinco categorías y tres preguntas de ejemplo en cualquier entorno. En un despliegue nuevo de cliente, esas preguntas de muestra entran en el banco real.

## What Changes

- `create_database.sql` recupera las cuatro columnas que le faltan.
- `DbSeeder` deja de crear el esquema. Si no lo encuentra, para con un mensaje que nombra el guion que hay que ejecutar.
- El contenido de ejemplo se siembra solo en desarrollo. El administrador se siembra siempre, porque sin él no se puede entrar.
- El `README.md` deja de mandar migraciones de EF.
- Una prueba compara el modelo con `create_database.sql` y falla si alguna tabla o columna del modelo no aparece en el guion.

## Capabilities

- `deployment-ops` — la creación del esquema y el contenido inicial

## Impact

**Cambia cómo se arranca en local.** Hasta ahora bastaba con levantar la API contra una base vacía. Ahora hay que ejecutar antes `scripts/create_database.sql`. El `README.md` lo dice, y la aplicación lo dice si falta.

**Sin cambios de API.** Sin cambios de comportamiento para el candidato ni para el administrador.

**Un despliegue nuevo deja de recibir preguntas de ejemplo.** Es el objetivo. Un entorno de desarrollo las sigue recibiendo.

**La prueba de deriva es de texto, no de esquema real.** Compara nombres contra el guion. Detecta lo que de verdad se olvida —una columna nueva sin añadir al guion— pero no comprueba tipos, longitudes ni claves foráneas. Eso pide una base de datos real, y eso es `E2`.
