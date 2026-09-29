## ADDED Requirements

### Requirement: Guion aditivo de roles de usuario
El sistema SHALL ofrecer `scripts/add_user_roles.sql` para actualizar una base `TechEvalDb` ya existente con el rol de usuario, el sello de seguridad y la tabla de enlaces para fijar la contraseña. El guion SHALL rellenar el rol de cada usuario existente a partir de `IsAdmin`: `Admin` si valía `1` y `Alumno` si valía `0`. Después de rellenar el rol, el guion SHALL quitar la columna `IsAdmin`, para que el rol tenga una sola fuente. El guion SHALL ser idempotente y SHALL conservar todas las filas existentes.

#### Scenario: Ejecución sobre una base con usuarios previos
- **GIVEN** una base con un administrador (`IsAdmin = 1`) y tres alumnos (`IsAdmin = 0`)
- **WHEN** se ejecuta `scripts/add_user_roles.sql`
- **THEN** el administrador queda con el rol `Admin`, los tres alumnos quedan con el rol `Alumno`, cada usuario tiene un sello de seguridad propio y la columna `IsAdmin` ya no existe
- **AND** ninguna fila de `Users` ni de otra tabla se pierde

#### Scenario: Reejecución idempotente
- **GIVEN** una base en la que el guion ya se ejecutó con éxito
- **WHEN** se vuelve a ejecutar `scripts/add_user_roles.sql`
- **THEN** el guion termina sin error, sin duplicar objetos y sin cambiar ningún rol

#### Scenario: Convergencia con la creación completa
- **WHEN** se compara una base actualizada con este guion y otra creada desde cero con `scripts/create_database.sql`
- **THEN** las dos tienen las mismas columnas en `Users` y la misma tabla de enlaces para fijar la contraseña

### Requirement: El despliegue del rol no deja pruebas a medias
La documentación de despliegue SHALL advertir que, tras desplegar este cambio, los tokens emitidos por la versión anterior dejan de valer. Un candidato con una prueba abierta perdería el guardado de sus respuestas hasta volver a abrir su enlace. La documentación SHALL indicar cómo comprobar, antes de desplegar, que no hay sesiones de examen en curso.

#### Scenario: Comprobación previa al despliegue
- **WHEN** un operador prepara el despliegue de este cambio
- **THEN** la documentación le da una consulta que cuenta las sesiones de examen en curso y le indica que despliegue cuando el recuento es cero
