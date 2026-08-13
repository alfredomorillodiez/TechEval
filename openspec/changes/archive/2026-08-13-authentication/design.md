## Context
TechEval expone endpoints de gestión (categorías, preguntas, exámenes, resultados) que solo el equipo de RR.HH./administración debe poder usar, mientras que el flujo de examen del candidato debe permanecer público (sin login, protegido por token de examen de un solo uso — fuera del alcance de esta capacidad). No existe un IdP externo ni requisito de SSO corporativo; se necesita un mecanismo simple, autocontenido y stateless que funcione bien con un front-end Blazor WebAssembly que no mantiene sesión de servidor.

## Goals / Non-Goals
**Goals**
- Autenticar a un único rol relevante (`Admin`) mediante email + contraseña.
- Emitir un token stateless (JWT) que `TechEval.Web` pueda guardar en el cliente y reenviar como `Authorization: Bearer`.
- Proteger todos los endpoints de gestión con `[Authorize(Roles = "Admin")]` sin lógica de autorización dispersa por controlador.
- Permitir que el sistema arranque con un administrador funcional desde el primer despliegue (seed), sin pasos manuales de creación de usuario.

**Non-Goals**
- Registro de usuarios (sign-up), recuperación de contraseña o gestión de perfil de administrador — no existen endpoints para ello.
- Múltiples roles o permisos granulares más allá de `Admin`/`User` — el claim `Role` solo distingue esos dos valores y hoy únicamente `Admin` se usa para autorizar.
- Refresh tokens o revocación de JWT antes de su expiración — el token vive las 8 horas configuradas y no hay lista de revocación.
- Multi-tenant o autenticación de candidatos — los candidatos usan el token de examen de un solo uso, un mecanismo completamente distinto (ver capacidad de entrega de examen).

## Decisions

### Decisión: SHA-256 en lugar de BCrypt/Argon2 para el hash de contraseña
`AuthController.VerifyPassword` calcula `SHA256.HashData` sobre la contraseña recibida (UTF-8) y la compara en hexadecimal contra `User.PasswordHash`. `DbSeeder` genera el hash del administrador inicial de la misma forma (ver `Program.cs`, línea donde se calcula `adminHash` antes de llamar a `DbSeeder.SeedAsync`).

- **Por qué es aceptable ahora**: TechEval es un MVP con un solo rol autenticable (administrador) y un número reducido de cuentas. SHA-256 evita añadir una dependencia externa (`BCrypt.Net`, `Konscious.Security.Cryptography` para Argon2) para un caso de uso de bajo volumen y baja exposición (no hay registro público de usuarios).
- **Por qué NO es la elección correcta a largo plazo**: SHA-256 es un hash rápido de propósito general, sin *salt* ni factor de costo, por lo que es vulnerable a ataques de diccionario/rainbow table si la base de datos se filtra. BCrypt o Argon2 añaden salting automático y un costo computacional ajustable que hace inviable el fuerza bruta a escala.
- **Migración planeada**: cuando se prioricen más cuentas de administrador o cumplimiento de seguridad, sustituir `VerifyPassword` por `BCrypt.Verify` (o equivalente Argon2) y regenerar el hash almacenado; `DbSeeder` solo necesita cambiar la función usada para calcular `adminPasswordHash`, no su firma.
- **Comparación de hashes y timing attacks**: en el código actual, `VerifyPassword` compara los hashes con el operador `==` de `string` (`computed == hash.ToLower()`), es decir, una comparación de igualdad estándar de .NET — no usa `CryptographicOperations.FixedTimeEquals`. Para strings de longitud fija como un hash hexadecimal, la superficie de un timing attack práctico es pequeña, pero la comparación no es *verificadamente* de tiempo constante. Se documenta aquí como riesgo conocido y candidato a corrección junto con la migración a BCrypt/Argon2 (que internamente sí usa comparación de tiempo constante).

### Decisión: JWT stateless con HS256 en vez de sesiones de servidor
`TokenService` firma los tokens con `SymmetricSecurityKey` + `HmacSha256` usando un secreto compartido (`Jwt:SecretKey`), en lugar de cookies de sesión o un almacén de sesiones en servidor.

- **Por qué**: Blazor WebAssembly no mantiene estado en el servidor entre peticiones; un JWT autocontenido permite validar la identidad en cada request sin persistir sesiones, y encaja con la decisión más amplia de servir el front-end como SPA estática.
- **Trade-off aceptado**: sin refresh tokens ni revocación, un JWT robado sigue siendo válido hasta su expiración (8 horas). Se mitiga manteniendo la ventana de expiración corta y recomendando HTTPS + rotación de `Jwt:SecretKey` en producción (ver `documentacion.md`, sección 13).

### Decisión: Autorización declarativa por rol en el controlador, no por acción
Se aplica `[Authorize(Roles = "Admin")]` a nivel de clase en `CategoriesController`, `QuestionsController`, `ExamsController` y `ResultsController`, en vez de decorar cada acción individualmente.

- **Por qué**: todas las acciones de esos controladores son operaciones de gestión; declarar la política una sola vez por controlador reduce el riesgo de que una acción nueva quede sin proteger por olvido.
- **Trade-off**: si en el futuro se necesita un endpoint público dentro de uno de esos controladores, requerirá `[AllowAnonymous]` explícito en esa acción, lo cual es una excepción visible y auditable.

## Risks / Trade-offs
- **Hash de contraseña sin salt/costo (SHA-256)**: riesgo de seguridad conocido y aceptado para el MVP; mitigación planeada mediante migración a BCrypt/Argon2 (ver decisión anterior).
- **Secreto JWT único y de larga vida**: si `Jwt:SecretKey` se filtra, todos los tokens pasados y futuros firmados con esa clave quedan comprometidos hasta que se rote la clave (lo que invalida también los tokens vigentes). Se recomienda un secreto de 32+ caracteres aleatorios y su gestión mediante un vault en producción.
- **Sin revocación de tokens**: cerrar sesión en el cliente no invalida el JWT en el servidor; un token robado sigue siendo válido hasta las 8 horas de expiración.
- **Un único rol de administrador**: el modelo de datos ya soporta `IsAdmin = false` (rol `User`), pero hoy ningún flujo de la aplicación crea o usa usuarios no administradores; es una extensión futura, no una limitación estructural.
