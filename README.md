# TechEval

Plataforma de evaluación técnica para gestionar bancos de preguntas, generar exámenes y evaluar candidatos mediante un enlace de un solo uso enviado por email, con portal propio para el alumno y corrección manual de las preguntas abiertas.

![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet)
![Blazor](https://img.shields.io/badge/Blazor-WebAssembly-512BD4?logo=blazor)
![SQL Server](https://img.shields.io/badge/SQL%20Server-2022-CC2927?logo=microsoftsqlserver)
![Docker](https://img.shields.io/badge/Docker-Compose-2496ED?logo=docker)

---

## Características

### Banco de preguntas y exámenes

- **Banco de preguntas** — tipo test (4 opciones) y respuesta abierta, con categorías y niveles de dificultad
- **Generación de exámenes** — manual o automática con preguntas aleatorias por categoría y dificultad
- **Reparto por nivel** — la generación automática acepta un porcentaje de preguntas por nivel (por ejemplo 30 % básicas, 50 % intermedias, 20 % avanzadas). El número de cada nivel se redondea por el método del mayor resto, así que la suma es siempre el total. Si un nivel no tiene bastantes preguntas en las categorías elegidas, la generación se rechaza: nunca se completa con otro nivel. La pantalla muestra antes cuántas preguntas saldrán y cuántas hay de cada nivel
- **Envío por email** — enlace único por candidato con expiración configurable; envío masivo a múltiples candidatos desde CSV o lista manual
- **Prueba del candidato** — temporizador, auto-guardado de respuestas y envío automático al agotar el tiempo
- **Reanudación de la prueba** — una recarga, un cierre de pestaña o una pérdida de red no expulsan al candidato: vuelve a sus preguntas con las respuestas que ya tenía y con el tiempo restante que calcula el servidor. La invitación caducada no corta una prueba ya empezada
- **Corrección automática** — para preguntas tipo test; preguntas abiertas pendientes de revisión manual
- **Dashboard de resultados** — historial, estadísticas y detalle por candidato, con filtros combinables

### Integridad de la prueba

- **Orden propio de cada sesión** — cada candidato ve las preguntas, y las opciones de cada pregunta de test, en un orden distinto. El orden es estable: al recargar, el candidato ve el mismo. Los resultados y la corrección siguen mostrando el orden de la prueba
- **Marca de agua** — durante la prueba, el nombre y el correo del candidato aparecen en diagonal sobre las preguntas. Una captura de pantalla identifica a su autor
- **Registro de actividad** — el sistema registra cada salida de la página (cambio de pestaña o de ventana), con su duración, y cada pegado en una respuesta abierta, con su número de caracteres. El texto pegado no se guarda
- **Aviso previo** — la pantalla de bienvenida informa al candidato de todo lo anterior antes de empezar
- **Solo informa** — el corrector ve el registro en el detalle del resultado y en la pantalla de corrección. Nada en el sistema lo usa para puntuar ni para suspender, y el candidato no lo ve

> **Lo que estas medidas no cubren.** Una página web no puede impedir una captura de pantalla, una foto con el móvil ni la consulta a un buscador o a una IA desde otro dispositivo. La prueba completa llega al navegador en un solo JSON, visible en las herramientas de desarrollo. Un candidato con esas herramientas también puede bloquear el envío de las señales o quitar la marca de agua. **La ausencia de señales no prueba nada.**

### Corrección manual de preguntas abiertas

- **Cola de pendientes** — un resultado con preguntas abiertas queda en estado `PendingReview` hasta que un administrador o un evaluador asignado lo corrige; la cola se ordena del más antiguo al más reciente
- **Evaluadores por prueba** — el administrador asigna evaluadores en el detalle de la prueba. Cada evaluador ve solo los pendientes de sus pruebas, nunca un resultado suyo como candidato. Una prueba con preguntas abiertas y sin evaluador la corrige solo el administrador, y el dashboard la señala
- **Corrección a ciegas** — el evaluador ve las respuestas, la referencia y las señales de integridad, pero no el nombre ni el email del candidato: la API no los envía. El candidato aparece como `Candidato R-<id>`, la fecha va sin hora y las señales llevan el tiempo desde el inicio en lugar de la hora del reloj
- **Reserva de 30 minutos** — abrir una corrección la reserva para quien la abre; la pantalla la renueva mientras sigue abierta, y «Cancelar» o el envío la liberan. El administrador ve quién tiene cada reserva y puede liberarla
- **Historial del evaluador** — sus correcciones, a ciegas y en solo lectura, aunque ya no tenga asignada la prueba
- **Pantalla de corrección** — muestra cada respuesta del candidato junto a la respuesta de referencia, y admite puntuación y comentario por respuesta
- **Corrección completa** — el envío corrige todas las respuestas abiertas de una vez; no se admite la corrección parcial
- **Cierre del resultado** — al enviar la corrección, el sistema recalcula la nota, fija el veredicto, pasa el resultado a `Reviewed` y avisa al candidato
- **Trazabilidad** — cada resultado guarda quién lo corrigió (`ReviewedByUserId`), y el detalle se lo muestra al administrador
- **Sin doble corrección** — un resultado ya corregido devuelve `409 Conflict` ante un segundo intento

### Cuentas, roles y portal del alumno

- **Tres roles, uno por usuario** — `Admin` (consola de administración), `Evaluador` (corrige a ciegas las pruebas que tiene asignadas) y `Alumno` (portal propio)
- **Gestión de usuarios** (`/admin/usuarios`) — el administrador da de alta administradores y evaluadores, cambia el rol, desactiva, reactiva y restablece el acceso. Nadie puede cambiarse el rol, desactivarse ni restablecerse el acceso a sí mismo, y siempre queda al menos un administrador activo, también cuando dos actúan a la vez
- **La contraseña la fija su dueño** — el alta y el restablecimiento envían por correo un enlace de un solo uso que caduca a las 48 horas. El administrador nunca conoce la contraseña de otra persona. La base de datos guarda solo el SHA-256 del enlace
- **Revocación inmediata** — el JWT lleva un sello de seguridad que la API compara en cada petición. Desactivar una cuenta, cambiarle el rol o restablecerle el acceso invalida sus tokens en su siguiente petición, sin esperar a que caduquen, y la Web cierra la sesión
- **Invitaciones solo para alumnos** — enviar una prueba al correo de un administrador o de un evaluador se rechaza
- **Aprovisionamiento automático** — al abrir por primera vez el enlace de invitación se crea la cuenta del alumno a partir del email del candidato
- **Auto-login desde la invitación** — validar el token devuelve un JWT con rol `Alumno`; el candidato no necesita credenciales para hacer la prueba
- **Portal del alumno** (`/portal`) — sus pruebas pendientes (sin empezar, con fecha de expiración, o a medias con acceso directo a continuarlas) y su historial de pruebas realizadas con nota y aprobado/suspenso
- **Aislamiento por usuario** — cada alumno ve únicamente sus propias invitaciones y resultados

---

## Stack tecnológico

| Capa | Tecnología | Versión |
|------|-----------|---------|
| Runtime | .NET | 9.0 |
| Backend | ASP.NET Core API | 9.0.0 |
| Frontend | Blazor WebAssembly | 9.0.0 |
| ORM | Entity Framework Core | 9.0.0 |
| Base de datos | SQL Server | 2022 |
| Autenticación | JWT Bearer (roles `Admin` / `Evaluador` / `Alumno`, sello de seguridad) | 9.0.0 |
| Documentación API | Swagger (Swashbuckle) | 7.2.0 |
| Logging | Serilog (consola + fichero diario) | 9.0.0 |
| Contenedores | Docker / Docker Compose | — |

---

## Requisitos previos

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9)
- SQL Server 2019+ (o Docker)
- Cuenta SMTP (SendGrid, Gmail…) — o [MailHog](https://github.com/mailhog/MailHog) para desarrollo

---

## Inicio rápido con Docker

```bash
# Clonar el repositorio
git clone https://github.com/alfredomorillodiez/TechEval.git
cd TechEval

# Arrancar SQL Server + API + Web + MailHog (entorno de desarrollo)
docker-compose --profile dev up -d
```

| Servicio | URL |
|----------|-----|
| API | http://localhost:5000 |
| Swagger | http://localhost:5000/swagger |
| Web (Blazor) | http://localhost:5001 |
| MailHog (emails) | http://localhost:8025 |

**Administrador:** `admin@techeval.com`. Su contraseña sale de `AdminPassword`, que **no tiene valor por defecto**. En desarrollo la trae `appsettings.Development.json` con el valor público `Admin@123!`. Fuera de desarrollo, la API se niega a arrancar si falta, y también si conserva ese valor de desarrollo.

> Las cuentas de alumno se crean solas al abrir una invitación, y **nacen sin contraseña utilizable**: el candidato entra por el enlace de la invitación, que ya lo autentica. Hasta el 16·09·2026 la contraseña era la parte local de su email, así que cualquiera que conociese el email entraba en su portal.

> **Antes de levantar el stack**, copia `.env.example` a `.env` y rellena sus valores. Ni `docker-compose.yml` ni `appsettings.json` llevan secretos: si falta alguno, el arranque para y dice cuál.
>
> ```bash
> cp .env.example .env    # y edita los valores
> ```
>
> **Aviso de seguridad**: la contraseña de `sa`, la clave JWT, la contraseña del administrador y las credenciales SMTP **estuvieron versionadas** hasta el 16·09·2026. Siguen en el historial de git, así que hay que darlas por comprometidas: no basta con moverlas, hay que **rotarlas**. El procedimiento está en [docs/rotacion-de-secretos.md](docs/rotacion-de-secretos.md), con el guion que genera los valores nuevos.

---

## Instalación manual

### 1. Configurar la base de datos

Editar `src/TechEval.API/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=TechEvalDb;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

### 2. Crear el esquema

**Obligatorio antes del primer arranque.** La API ya no crea el esquema: si no lo encuentra, para y dice qué ejecutar.

```bash
sqlcmd -S localhost -i scripts/create_database.sql
```

> **El proyecto no usa migraciones de EF Core.** Hasta el 16·09·2026 la aplicación creaba el esquema desde el modelo con `EnsureCreated`, y eso dejaba las migraciones permanentemente inservibles: su tabla de historial nunca llegaba a existir, así que un `dotnet ef database update` fallaba siempre. Los guiones de `scripts/` son la única forma de crear y actualizar el esquema.

El guion crea **solo el esquema**. El administrador lo siembra la API en su primer arranque, a partir de `AdminPassword`.

Sobre una base de datos **ya existente** creada con una versión anterior, aplica los guiones incrementales por orden. Todos son idempotentes:

```bash
# Cuentas de alumno: Users.Username, ExamTokens.UserId, ExamResults.UserId
sqlcmd -S localhost -d TechEvalDb -i scripts/add_user_link_columns.sql

# Corrección manual: ExamResults.Status/ReviewedByUserId, UserAnswers.AwardedPoints/ReviewerComment
sqlcmd -S localhost -d TechEvalDb -i scripts/add_review_columns.sql

# Retirada del pipeline de IA (solo en bases de datos anteriores a su eliminación)
sqlcmd -S localhost -d TechEvalDb -i scripts/remove_ai_generation.sql

# Copia de lo preguntado en UserAnswers (D7)
sqlcmd -S localhost -d TechEvalDb -i scripts/add_answer_snapshot_columns.sql

# Integridad de la prueba: ExamSessions.ShuffleSeed/IntegrityLimitReached y la tabla ExamIntegrityEvents
sqlcmd -S localhost -d TechEvalDb -i scripts/add_integrity_columns.sql

# Roles: Users.Role y Users.SecurityStamp, tabla PasswordSetupTokens; quita Users.IsAdmin
sqlcmd -S localhost -d TechEvalDb -i scripts/add_user_roles.sql

# Evaluadores: tabla ExamEvaluators y la reserva en ExamResults (ReservedByUserId, ReservedUntil)
sqlcmd -S localhost -d TechEvalDb -i scripts/add_evaluator_columns.sql
```

> **Antes de aplicar `add_user_roles.sql`**, comprueba que no hay pruebas en curso. Desde este cambio el JWT lleva un sello de seguridad, y la API rechaza los tokens emitidos por la versión anterior. Un candidato a mitad de prueba perdería el guardado de sus respuestas hasta volver a abrir su enlace. Despliega cuando esta consulta devuelva `0`:
>
> ```sql
> SELECT COUNT(*) FROM dbo.ExamSessions WHERE Status = 1;   -- 1 = InProgress
> ```
>
> El guion y el binario nuevo van juntos: el guion quita `IsAdmin`, y la versión anterior ya no arranca contra la base actualizada. La cabecera del guion trae el SQL para volver atrás.

> **Atención: todos estos guiones llevan `USE TechEvalDb` dentro.** El `-d` de `sqlcmd` no cambia la base sobre la que actúan. `create_database.sql`, además, borra todas las tablas antes de crearlas.

> Las sesiones que ya existían quedan sin semilla. Conservan el orden de la prueba, y el corrector las ve como anteriores al registro de actividad, no como sesiones sin señales.

Una prueba de la batería compara el modelo con `create_database.sql` y falla si dejan de coincidir, para que una columna nueva no se quede fuera del guion.

### 3. Configurar email (desarrollo)

```bash
docker run -d -p 1025:1025 -p 8025:8025 mailhog/mailhog
```

### 4. Arrancar API y frontend

```bash
# Terminal 1
cd src/TechEval.API && dotnet run

# Terminal 2
cd src/TechEval.Web && dotnet run
```

---

## Configuración

Las claves viven en `appsettings.json` y se pueden sobrescribir por entorno (`appsettings.Development.json`, `appsettings.Local.json` — este último ignorado por git) o mediante variables de entorno usando doble guion bajo (`Jwt__SecretKey`).

> **`appsettings.Local.json` manda sobre todo lo demás**, también sobre las variables de entorno y la línea de órdenes, porque `Program.cs` lo carga el último. Si ese fichero trae un servidor SMTP real, `Email__Host=...` no lo sustituye y la API envía correo de verdad. Para probar el correo en local sin él, arranca la API con `--contentRoot` apuntando a una carpeta que no lo tenga.

| Clave | Descripción | Por defecto |
|-------|-------------|-------------|
| `ConnectionStrings:DefaultConnection` | Cadena de conexión a SQL Server | `Server=localhost;Database=TechEvalDb;…` |
| `Jwt:SecretKey` | Clave de firma HMAC-SHA256 — **obligatoria, sin valor por defecto** | vacío |
| `Jwt:ExpirationHours` | Vigencia del token de sesión | `8` |
| `Email:*` | Host, puerto, credenciales y remitente SMTP | vacío |
| `FrontendBaseUrl` | Base con la que se construyen los enlaces de invitación | `https://localhost:60805` |
| `AllowedOrigins` | Orígenes CORS permitidos en producción (separados por coma) | `http://localhost:5001` |
| `AdminPassword` | Contraseña del administrador creado en el primer arranque — **obligatoria** | vacío |
| `Jwt:Issuer` / `Jwt:Audience` | Emisor y destinatario del token | `TechEvalAPI` / `TechEvalClient` |

### Límite de ritmo

`POST /api/auth/login` admite 10 peticiones por minuto y dirección de origen. Las operaciones de `/api/auth/password-setup` admiten 10. `GET /api/exam/validate/{token}` admite 60. `POST /api/exam/integrity/{sessionId}` admite 120. Al superarlo, la API responde `429` con una cabecera `Retry-After`.

El motivo es el coste: verificar una contraseña cuesta cientos de milisegundos de CPU desde que el hash es PBKDF2, así que un volumen moderado de intentos deja al servidor sin hilos aunque ninguno acierte.

> **Si despliegas detrás de un proxy inverso**, declara sus direcciones en `ForwardedHeadersOptions`. Sin hacerlo, todas las peticiones llegan con la dirección del proxy: el cupo se comparte entre todos los candidatos y el primero que llegue agota el de los demás.
>
> No confíes en `X-Forwarded-For` de cualquier origen. Sin restringir qué proxies pueden fijarla, cualquiera se inventa su dirección y se salta el límite. Por eso la lista de proxies de confianza va vacía por defecto y la cabecera se ignora.

---

## Arquitectura

Clean Architecture en 4 capas con separación estricta de dependencias:

```
TechEval.Web (Blazor WASM)
        │  HTTP / REST
TechEval.API  (Controllers · JWT · Swagger · Middleware)
        │
TechEval.Application  (Services · DTOs · Validaciones)
        │
TechEval.Domain  (Entities · Interfaces · Enums)
        │
TechEval.Infrastructure  (EF Core · Repositorios · SMTP · JWT)
        │
    SQL Server
```

Domain no depende de ninguna otra capa. Application define los puertos —servicios de negocio, repositorios y envío de email— e Infrastructure los implementa. Así, un cambio de ORM o de proveedor de correo no toca ni Application ni Domain.

---

## Estructura del proyecto

```
TechEval/
├── src/
│   ├── TechEval.API/            # Controladores y middleware
│   ├── TechEval.Application/    # Lógica de negocio y DTOs
│   ├── TechEval.Domain/         # Entidades, enums e interfaces
│   ├── TechEval.Infrastructure/ # EF Core, email y JWT
│   └── TechEval.Web/            # Frontend Blazor WASM (admin + portal del alumno)
├── tests/
│   └── TechEval.Tests/          # Tests unitarios (xUnit)
├── scripts/
│   ├── create_database.sql        # Esquema completo, alternativa a migraciones
│   ├── add_user_link_columns.sql  # Incremental: cuentas de alumno
│   ├── add_review_columns.sql     # Incremental: corrección manual de abiertas
│   ├── remove_ai_generation.sql   # Incremental: retirada del pipeline de IA
│   ├── reset_exam_history.sql     # Limpieza del histórico de intentos
│   └── seed_questions_*.sql       # Banco de preguntas inicial
├── openspec/                    # Especificación viva del sistema e histórico de cambios
├── docker-compose.yml
├── Dockerfile.api · Dockerfile.web · nginx.conf
└── documentacion.md             # Documentación técnica completa
```

---

## Rutas del frontend

| Ruta | Rol | Descripción |
|------|-----|-------------|
| `/` · `/login` | — | Acceso con email (o usuario) y contraseña; lleva a la página de inicio del rol |
| `/admin` · `/admin/dashboard` | Admin | Dashboard con indicadores generales |
| `/admin/questions` | Admin | Banco de preguntas con filtros |
| `/admin/questions/new` · `/admin/questions/{id}` | Admin | Alta y edición de preguntas |
| `/admin/pruebas` · `/admin/pruebas/{id}` | Admin | Listado de pruebas y detalle |
| `/admin/pruebas/new` · `/admin/pruebas/generate` | Admin | Creación manual y generación automática |
| `/admin/results` · `/admin/results/{id}` | Admin | Resultados globales y detalle |
| `/admin/results/prueba/{examId}` | Admin | Resultados de una prueba concreta |
| `/admin/results/pending` | Admin | Cola de resultados pendientes de corrección |
| `/admin/results/{id}/review` | Admin | Corrección de las preguntas abiertas de un resultado |
| `/admin/usuarios` | Admin | Gestión de usuarios: alta, rol, estado y acceso |
| `/evaluacion` · `/evaluacion/{id}` | Evaluador | Cola de correcciones de sus pruebas y corrección a ciegas |
| `/evaluacion/historial` · `/evaluacion/historial/{id}` | Evaluador | Sus correcciones, en solo lectura |
| `/portal` | Alumno | Pruebas pendientes y realizadas del alumno |
| `/fijar-contrasena/{token}` | Público | Fijar la contraseña con el enlace del correo |
| `/sesion-no-valida` | Público | Aviso al alumno cuya sesión rechazó la API: debe volver a abrir su enlace |
| `/exam/{token}` · `/prueba/{token}` | Público | Apertura de la invitación y resolución de la prueba |

---

## API

Swagger publica la referencia completa en `/swagger` (solo en desarrollo). Resumen de endpoints:

| Método | Endpoint | Autorización |
|--------|----------|--------------|
| `POST` | `/api/auth/login` | Público |
| `GET` | `/api/auth/password-setup/{token}` | Público — nombre y email del dueño de un enlace vigente |
| `POST` | `/api/auth/password-setup` | Público — fija la contraseña con el enlace; no inicia sesión |
| `GET` `POST` | `/api/users` | Admin — listado con filtros, y alta |
| `PUT` | `/api/users/{id}/role` | Admin |
| `POST` | `/api/users/{id}/deactivate` · `/activate` · `/reset-access` | Admin |
| `GET` `POST` `PUT` `DELETE` | `/api/categories` | Admin |
| `GET` `POST` `PUT` `DELETE` | `/api/questions` | Admin |
| `GET` | `/api/questions/availability?categoryIds=` | Admin — preguntas activas de cada nivel, para la vista previa del reparto |
| `GET` `POST` `PUT` `DELETE` | `/api/exams` | Admin |
| `POST` | `/api/exams/generate` | Admin |
| `POST` | `/api/exams/send` · `/api/exams/send-bulk` | Admin |
| `GET` | `/api/results` · `/api/results/{id}` · `/api/results/exam/{examId}` · `/api/results/dashboard` | Admin |
| `GET` | `/api/results/{id}/integrity` | Admin — actividad del candidato durante la prueba |
| `GET` | `/api/review/pending` · `/api/review/{resultId}` | Admin |
| `POST` | `/api/review/{resultId}` | Admin |
| `POST` `DELETE` | `/api/review/{resultId}/reservation` | Admin — renueva la propia o libera cualquiera |
| `GET` `POST` `DELETE` | `/api/exams/{id}/evaluators[/{userId}]` | Admin — evaluadores de la prueba |
| `GET` | `/api/evaluation/queue` · `/api/evaluation/history[/{resultId}]` | Evaluador |
| `GET` `POST` | `/api/evaluation/{resultId}` | Evaluador — el `GET` reserva; `404` si la prueba no está asignada |
| `POST` `DELETE` | `/api/evaluation/{resultId}/reservation` | Evaluador — solo la propia |
| `GET` | `/api/evaluation/{resultId}/integrity` | Evaluador — señales sin la hora del reloj |
| `GET` | `/api/student/pending` · `/api/student/completed` | Alumno |
| `GET` | `/api/exam/validate/{token}` | Público — devuelve el JWT de alumno |
| `POST` | `/api/exam/start/{token}` · `/api/exam/answer/{sessionId}` · `/api/exam/submit` | Público — token del enlace |
| `POST` | `/api/exam/integrity/{sessionId}` | Alumno — solo sobre su propia sesión en curso |

---

## Usuarios y roles

| Capacidad | Admin | Evaluador | Alumno |
|---|:-:|:-:|:-:|
| Banco de preguntas, pruebas e invitaciones | ✓ | — | — |
| Resultados y dashboard | ✓ | — | — |
| Asignar evaluadores a las pruebas | ✓ | — | — |
| Corregir, con la identidad del candidato | ✓ (todas las pruebas) | — | — |
| Corregir a ciegas | — | ✓ (sus pruebas) | — |
| Historial de correcciones propias | — | ✓ | — |
| Gestión de usuarios | ✓ | — | — |
| Portal del alumno y resolución de la prueba | — | — | ✓ |

La matriz vive en un solo sitio, `src/TechEval.API/Authorization/Policies.cs`. Los controladores nombran una política (`Gestion`, `Evaluacion`, `Alumno`), nunca una lista de roles. Todo lo del evaluador vive en `api/evaluation`, con su propia política.

> **Lo que la corrección a ciegas no cubre.** La API no envía el nombre ni el email del candidato, pero lo que el candidato escribe llega tal cual: si pone su nombre en una respuesta, el evaluador lo lee. Y en una prueba que hizo una sola persona, quien sepa quién la hizo sabe de quién es. La ceguera quita la identidad de la pantalla; no impide deducirla.

**Alta de un evaluador o de otro administrador:**

1. En `/admin/usuarios`, pulsa «Nuevo usuario» y escribe nombre, email y rol.
2. La persona recibe un correo con un enlace que caduca a las 48 horas y vale una sola vez.
3. En `/fijar-contrasena/{token}` fija su contraseña, de 12 a 128 caracteres.
4. Después inicia sesión en `/login` con su email y esa contraseña.

Si el correo no sale, la cuenta queda creada y la página lo avisa. «Restablecer acceso» envía un enlace nuevo e invalida el anterior.

**Para retirar el acceso a alguien**, desactiva su cuenta. No hay paso a `Alumno`: le dejaría la contraseña y, con ella, la entrada al portal.

### Recuperar el acceso del único administrador

Si el único administrador activo pierde su contraseña, nadie puede restablecerle el acceso desde la aplicación, y `AdminPassword` solo actúa con la base vacía. Con acceso a la base de datos, se puede fijar una contraseña temporal:

1. Calcula el hash de una contraseña temporal con `PasswordHasher.Hash`, desde una prueba o una consola de .NET.
2. Escribe ese hash en la base y cambia el sello, para invalidar cualquier token anterior:

   ```sql
   UPDATE dbo.Users
   SET PasswordHash = '<hash>', SecurityStamp = NEWID(), IsActive = 1
   WHERE Email = 'admin@techeval.com';
   ```

3. Inicia sesión con la contraseña temporal. Crea un segundo administrador para que esto no vuelva a depender de una sola persona.

---

## Flujo de corrección manual

```
Candidato  →  POST /api/exam/submit
             │  corrección automática de las preguntas tipo test
             │
             ├─ sin preguntas abiertas  →  ExamResult = Reviewed · nota y veredicto firmes
             │
             └─ con preguntas abiertas  →  ExamResult = PendingReview · Passed = NULL
                          │
                          ▼
Admin      →  /admin/results/pending    cola completa, con la identidad y las reservas
Evaluador  →  /evaluacion               solo sus pruebas, a ciegas
                          │
                          ▼
Abrir la corrección reserva el resultado 30 minutos (GET /api/review/{id} · GET /api/evaluation/{id})
                          │  puntuación y comentario por respuesta
                          │  POST /api/review/{id} · POST /api/evaluation/{id}   (todas las abiertas a la vez)
                          ▼
   ExamResult = Reviewed · nota recalculada · veredicto fijado · aviso al candidato
```

Estado implicado: `ExamResultStatus` (`PendingReview` · `Reviewed`), y la reserva en `ExamResults.ReservedByUserId` y `ReservedUntil`. Cada respuesta guarda los puntos otorgados en `UserAnswers.AwardedPoints` y el comentario del corrector en `UserAnswers.ReviewerComment`.

---

## Especificaciones (OpenSpec)

El directorio [`openspec/`](openspec/) mantiene la especificación viva del sistema, dividida en 15 capacidades:

`admin-console` · `authentication` · `candidate-experience` · `deployment-ops` · `evaluator-review` · `exam-delivery` · `exam-integrity` · `exam-management` · `exam-results` · `exam-taking` · `open-question-review` · `project-architecture` · `question-bank` · `student-portal` · `user-management`

> `ai-question-generation` describe una capacidad ya retirada del código. Su spec sigue en `openspec/specs/` a la espera de archivarse.

Cada cambio funcional se propone en `openspec/changes/` (propuesta, diseño, deltas de spec y tareas) y, una vez implementado, se archiva en `openspec/changes/archive/` fusionando sus deltas en las specs principales.

---

## Tests

```bash
cd tests/TechEval.Tests
dotnet test
```

---

## Documentación

La documentación técnica completa (diseño de BD, API reference, flujos, decisiones técnicas) está en [`documentacion.md`](documentacion.md).
