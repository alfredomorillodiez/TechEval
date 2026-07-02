-- ============================================================
-- TechEval — Preguntas adicionales + categorías Azure y Azure DevOps
-- ~10 preguntas por categoría existente + 25 Azure + 25 Azure DevOps
-- ============================================================

USE TechEvalDb;
GO

-- ── Nuevas categorías ─────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM dbo.Categories WHERE Name = 'Azure')
    INSERT INTO dbo.Categories (Name, Description)
    VALUES (N'Azure', N'Microsoft Azure: cómputo, almacenamiento, redes, datos e identidad');

IF NOT EXISTS (SELECT 1 FROM dbo.Categories WHERE Name = 'Azure DevOps')
    INSERT INTO dbo.Categories (Name, Description)
    VALUES (N'Azure DevOps', N'Boards, Repos, Pipelines CI/CD, Test Plans y Artifacts');
GO

-- ── IDs de categoría ──────────────────────────────────────
DECLARE @cs   INT = (SELECT Id FROM dbo.Categories WHERE Name = 'C#');
DECLARE @api  INT = (SELECT Id FROM dbo.Categories WHERE Name = 'APIs REST');
DECLARE @arch INT = (SELECT Id FROM dbo.Categories WHERE Name = 'Arquitectura');
DECLARE @sql  INT = (SELECT Id FROM dbo.Categories WHERE Name = 'SQL');
DECLARE @dops INT = (SELECT Id FROM dbo.Categories WHERE Name = 'DevOps');
DECLARE @fe   INT = (SELECT Id FROM dbo.Categories WHERE Name = 'Frontend');
DECLARE @az   INT = (SELECT Id FROM dbo.Categories WHERE Name = 'Azure');
DECLARE @azdo INT = (SELECT Id FROM dbo.Categories WHERE Name = 'Azure DevOps');

DECLARE @q INT;

-- ============================================================
-- SQL — 10 preguntas adicionales
-- ============================================================

-- SQL-E01
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué función de ventana asigna un número secuencial único a cada fila dentro de una partición sin dejar huecos aunque haya empates?', 1, 2, @sql, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'RANK()', 0, 1),
(@q, N'DENSE_RANK()', 1, 2),
(@q, N'ROW_NUMBER()', 0, 3),
(@q, N'NTILE()', 0, 4);

-- SQL-E02
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué cláusula se añade a una función de ventana para dividir el conjunto de filas en grupos independientes?', 1, 2, @sql, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'GROUP BY', 0, 1),
(@q, N'PARTITION BY', 1, 2),
(@q, N'ORDER BY', 0, 3),
(@q, N'HAVING', 0, 4);

-- SQL-E03
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué es una CTE (Common Table Expression)?', 1, 2, @sql, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Un índice especial para optimizar consultas recursivas', 0, 1),
(@q, N'Una consulta nombrada temporal definida con la cláusula WITH que existe solo durante la consulta', 1, 2),
(@q, N'Un tipo de tabla temporal que persiste durante la sesión', 0, 3),
(@q, N'Un sinónimo de vista materializada', 0, 4);

-- SQL-E04
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Cuántos índices CLUSTERED puede tener una tabla en SQL Server?', 1, 1, @sql, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Tantos como columnas tenga', 0, 1),
(@q, N'Hasta 5', 0, 2),
(@q, N'Solo uno', 1, 3),
(@q, N'Hasta 249', 0, 4);

-- SQL-E05
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué es un índice de cobertura (covering index)?', 1, 3, @sql, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Un índice que cubre toda la tabla con un escaneo completo', 0, 1),
(@q, N'Un índice clustered que incluye todas las columnas de la tabla', 0, 2),
(@q, N'Un índice non-clustered que incluye todas las columnas requeridas por la consulta, evitando Key Lookup', 1, 3),
(@q, N'Un índice que se crea automáticamente sobre la clave primaria', 0, 4);

-- SQL-E06
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué instrucción T-SQL combina INSERT, UPDATE y DELETE en una sola operación basada en una condición de coincidencia?', 1, 3, @sql, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'UPSERT', 0, 1),
(@q, N'MERGE', 1, 2),
(@q, N'SYNC', 0, 3),
(@q, N'REPLACE INTO', 0, 4);

-- SQL-E07
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué diferencia existe entre ISNULL() y COALESCE() en T-SQL?', 1, 2, @sql, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'No hay diferencia, son sinónimos exactos', 0, 1),
(@q, N'ISNULL acepta solo 2 argumentos y es específico de SQL Server; COALESCE acepta N argumentos y es estándar ANSI SQL', 1, 2),
(@q, N'COALESCE es más rápido que ISNULL en todos los casos', 0, 3),
(@q, N'ISNULL puede usarse en columnas de cualquier tipo; COALESCE solo en tipos numéricos', 0, 4);

-- SQL-E08
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué significan las siglas ACID en el contexto de transacciones de bases de datos?', 1, 1, @sql, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Availability, Consistency, Integrity, Durability', 0, 1),
(@q, N'Atomicity, Consistency, Isolation, Durability', 1, 2),
(@q, N'Atomicity, Concurrency, Integrity, Distribution', 0, 3),
(@q, N'Availability, Concurrency, Isolation, Data-integrity', 0, 4);

-- SQL-E09
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Cuál es la diferencia entre WHERE y HAVING en SQL?', 1, 1, @sql, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'WHERE filtra columnas; HAVING filtra filas', 0, 1),
(@q, N'No hay diferencia, son intercambiables', 0, 2),
(@q, N'WHERE filtra filas antes de agrupar; HAVING filtra grupos después del GROUP BY', 1, 3),
(@q, N'HAVING es más rápido que WHERE porque actúa sobre el índice', 0, 4);

-- SQL-E10
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué hace la función LAG() en una consulta con funciones de ventana?', 1, 2, @sql, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Devuelve el valor máximo de las filas anteriores dentro de la partición', 0, 1),
(@q, N'Accede al valor de una columna en una fila siguiente dentro de la partición', 0, 2),
(@q, N'Accede al valor de una columna en una fila anterior dentro de la partición', 1, 3),
(@q, N'Calcula la diferencia entre la fila actual y la primera fila de la partición', 0, 4);

-- ============================================================
-- C# — 10 preguntas adicionales
-- ============================================================

-- CS-E01
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué tipo devuelve un método marcado como async que no retorna ningún valor?', 1, 1, @cs, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'void', 0, 1),
(@q, N'Task', 1, 2),
(@q, N'Task<void>', 0, 3),
(@q, N'ValueTask', 0, 4);

-- CS-E02
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué es la ejecución diferida (deferred execution) en LINQ?', 1, 2, @cs, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'LINQ compila las consultas en tiempo de compilación para mayor rendimiento', 0, 1),
(@q, N'Las consultas LINQ no se ejecutan hasta que se itera sobre ellas, por ejemplo con ToList() o foreach', 1, 2),
(@q, N'LINQ aplaza la ejecución hasta que el GC libera memoria', 0, 3),
(@q, N'Las consultas LINQ se cachean automáticamente tras la primera ejecución', 0, 4);

-- CS-E03
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Cuál es la diferencia entre los tiempos de vida Singleton, Scoped y Transient en inyección de dependencias de .NET?', 1, 2, @cs, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Singleton: nueva instancia por hilo; Scoped: una por clase; Transient: una por aplicación', 0, 1),
(@q, N'Singleton: una instancia por aplicación; Scoped: una por petición HTTP; Transient: nueva instancia cada vez que se solicita', 1, 2),
(@q, N'Singleton: una instancia; Scoped: nueva en cada método; Transient: nueva en cada petición', 0, 3),
(@q, N'No hay diferencias funcionales, solo afectan al rendimiento', 0, 4);

-- CS-E04
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué garantiza el bloque using con un objeto que implementa IDisposable?', 1, 1, @cs, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Que el objeto se elimina del heap inmediatamente al salir del bloque', 0, 1),
(@q, N'Que Dispose() se llama automáticamente al salir del bloque, incluso si ocurre una excepción', 1, 2),
(@q, N'Que el GC recoge el objeto en el siguiente ciclo garantizado', 0, 3),
(@q, N'Que el objeto no puede ser nulo dentro del bloque', 0, 4);

-- CS-E05
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué es el boxing en C#?', 1, 2, @cs, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Encapsular una clase dentro de otra clase para proteger sus miembros privados', 0, 1),
(@q, N'Convertir un tipo valor (struct, int, etc.) en un tipo referencia (object) asignando en el heap', 1, 2),
(@q, N'Serializar un objeto a un formato binario comprimido', 0, 3),
(@q, N'Crear una copia profunda de un objeto en el stack', 0, 4);

-- CS-E06
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Cómo se declara un método de extensión en C#?', 1, 2, @cs, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Como un método de instancia en cualquier clase con el modificador extend', 0, 1),
(@q, N'Como un método estático en una clase estática cuyo primer parámetro lleva el modificador this', 1, 2),
(@q, N'Como un método virtual en una clase abstracta', 0, 3),
(@q, N'Como un método en una interfaz con implementación por defecto', 0, 4);

-- CS-E07
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué característica principal tiene un record en C# (introducido en C# 9)?', 1, 2, @cs, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Es un alias de struct que permite herencia múltiple', 0, 1),
(@q, N'Es un tipo de referencia con igualdad basada en valores e inmutabilidad por defecto', 1, 2),
(@q, N'Es equivalente a una clase sellada (sealed) sin propiedades virtuales', 0, 3),
(@q, N'Es un tipo valor que vive siempre en el stack', 0, 4);

-- CS-E08
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué restricción genérica impone where T : new() en C#?', 1, 2, @cs, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Que T debe ser un tipo valor (struct)', 0, 1),
(@q, N'Que T debe tener un constructor público sin parámetros', 1, 2),
(@q, N'Que T debe implementar IComparable', 0, 3),
(@q, N'Que T no puede ser null', 0, 4);

-- CS-E09
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Cuándo es preferible usar ValueTask<T> en lugar de Task<T>?', 1, 3, @cs, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Cuando la operación siempre es asíncrona y de larga duración', 0, 1),
(@q, N'Cuando el resultado suele estar disponible de forma síncrona, evitando la asignación de un objeto Task en el heap', 1, 2),
(@q, N'ValueTask siempre debe preferirse sobre Task por ser más moderno', 0, 3),
(@q, N'Cuando se necesita cancelación con CancellationToken', 0, 4);

-- CS-E10
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué diferencia hay entre IEnumerable<T> e IQueryable<T>?', 1, 3, @cs, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'IEnumerable es genérico; IQueryable no lo es', 0, 1),
(@q, N'No hay diferencia funcional, solo de nomenclatura', 0, 2),
(@q, N'IEnumerable ejecuta la consulta en memoria (LINQ to Objects); IQueryable traduce la expresión a SQL y la ejecuta en la BD', 1, 3),
(@q, N'IQueryable solo funciona con Entity Framework, IEnumerable con cualquier colección', 0, 4);

-- ============================================================
-- APIs REST — 10 preguntas adicionales
-- ============================================================

-- API-E01
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Cuántas partes tiene un token JWT y cómo se separan?', 1, 1, @api, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Dos partes separadas por comas: header y payload', 0, 1),
(@q, N'Tres partes separadas por puntos: header, payload y signature', 1, 2),
(@q, N'Cuatro partes separadas por guiones: version, header, payload y signature', 0, 3),
(@q, N'Tres partes separadas por comas: claim, subject y expiration', 0, 4);

-- API-E02
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Cuál es la diferencia entre los métodos HTTP PUT y PATCH?', 1, 2, @api, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'PUT crea recursos; PATCH los elimina', 0, 1),
(@q, N'PUT reemplaza el recurso completo; PATCH aplica modificaciones parciales', 1, 2),
(@q, N'PATCH es idempotente; PUT no lo es', 0, 3),
(@q, N'No hay diferencia, son sinónimos', 0, 4);

-- API-E03
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Cuál de estos métodos HTTP NO es idempotente?', 1, 2, @api, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'GET', 0, 1),
(@q, N'PUT', 0, 2),
(@q, N'POST', 1, 3),
(@q, N'DELETE', 0, 4);

-- API-E04
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué código HTTP debe devolver una API al crear un recurso exitosamente?', 1, 1, @api, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'200 OK', 0, 1),
(@q, N'201 Created', 1, 2),
(@q, N'202 Accepted', 0, 3),
(@q, N'204 No Content', 0, 4);

-- API-E05
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Cuál es la diferencia entre un código 401 Unauthorized y 403 Forbidden?', 1, 2, @api, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'401 es para APIs; 403 es para aplicaciones web', 0, 1),
(@q, N'401 significa que el cliente no está autenticado; 403 que está autenticado pero no tiene permiso', 1, 2),
(@q, N'401 es un error del servidor; 403 es un error del cliente', 0, 3),
(@q, N'Son equivalentes, cualquiera puede usarse indistintamente', 0, 4);

-- API-E06
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué es CORS (Cross-Origin Resource Sharing)?', 1, 2, @api, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Un mecanismo para cifrar comunicaciones entre dominios distintos', 0, 1),
(@q, N'Un mecanismo del navegador que controla qué peticiones HTTP desde un origen pueden acceder a recursos de otro origen', 1, 2),
(@q, N'Un protocolo de autenticación para APIs entre servicios', 0, 3),
(@q, N'Un estándar para compartir cookies entre subdominios', 0, 4);

-- API-E07
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Para qué sirve el ETag en las respuestas HTTP?', 1, 3, @api, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Para comprimir el cuerpo de la respuesta', 0, 1),
(@q, N'Para autenticar al cliente mediante un token de sesión', 0, 2),
(@q, N'Como identificador de versión del recurso que permite caché condicional y control de concurrencia optimista', 1, 3),
(@q, N'Para enrutar la petición al servidor correcto en un clúster', 0, 4);

-- API-E08
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué es el flujo Authorization Code con PKCE en OAuth 2.0?', 1, 3, @api, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Un flujo para aplicaciones servidor que usan certificados de cliente', 0, 1),
(@q, N'El flujo Client Credentials mejorado con cifrado de contraseña', 0, 2),
(@q, N'El flujo Authorization Code protegido con un verificador de código, diseñado para clientes públicos como SPAs y apps móviles', 1, 3),
(@q, N'Un flujo simplificado que devuelve el access token directamente en la URL', 0, 4);

-- API-E09
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué es OpenAPI (Swagger)?', 1, 1, @api, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Una librería JavaScript para consumir APIs REST desde el navegador', 0, 1),
(@q, N'Un framework de autenticación para APIs', 0, 2),
(@q, N'Una especificación estándar (YAML/JSON) para describir APIs RESTful de forma legible por máquinas y humanos', 1, 3),
(@q, N'Un protocolo alternativo a REST para comunicación binaria', 0, 4);

-- API-E10
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué es el patrón Circuit Breaker en arquitecturas de microservicios?', 1, 3, @api, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Un patrón de seguridad que bloquea peticiones maliciosas en el API Gateway', 0, 1),
(@q, N'Un patrón que detecta fallos repetidos en llamadas a servicios externos y los cortocircuita para evitar cascadas de errores', 1, 2),
(@q, N'Un balanceador de carga que redirige tráfico ante fallos de red', 0, 3),
(@q, N'Un mecanismo de reintento automático con backoff exponencial', 0, 4);

-- ============================================================
-- ARQUITECTURA — 10 preguntas adicionales
-- ============================================================

-- ARCH-E01
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué establece el principio de Responsabilidad Única (SRP)?', 1, 1, @arch, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Una clase debe tener una única instancia en todo el sistema', 0, 1),
(@q, N'Una clase debe tener una sola razón para cambiar, es decir, una única responsabilidad', 1, 2),
(@q, N'Un método debe realizar una sola operación atómica', 0, 3),
(@q, N'Cada módulo debe ser responsable de su propio manejo de errores', 0, 4);

-- ARCH-E02
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué establece el principio Open/Closed (OCP)?', 1, 2, @arch, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Las clases deben ser abiertas para herencia y cerradas para instanciación directa', 0, 1),
(@q, N'Los métodos públicos pueden modificarse; los privados no', 0, 2),
(@q, N'Las entidades software deben estar abiertas para extensión pero cerradas para modificación', 1, 3),
(@q, N'Los módulos abiertos se despliegan en producción; los cerrados quedan en desarrollo', 0, 4);

-- ARCH-E03
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué es CQRS (Command Query Responsibility Segregation)?', 1, 2, @arch, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Un patrón que prohíbe que los repositorios devuelvan datos y los comandos modifiquen datos al mismo tiempo', 0, 1),
(@q, N'Un patrón que separa el modelo de escritura (Commands) del modelo de lectura (Queries) para optimizarlos independientemente', 1, 2),
(@q, N'Una arquitectura que reemplaza a REST usando colas de comandos', 0, 3),
(@q, N'El principio de que las consultas SQL no deben contener subconsultas', 0, 4);

-- ARCH-E04
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué es el Event Sourcing?', 1, 3, @arch, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Una técnica para cachear eventos de dominio en Redis', 0, 1),
(@q, N'Un patrón en el que el estado de la aplicación se deriva de una secuencia inmutable de eventos almacenados', 1, 2),
(@q, N'El uso de eventos JavaScript para comunicar componentes frontend', 0, 3),
(@q, N'Un patrón de integración que suscribe microservicios a un bus de eventos', 0, 4);

-- ARCH-E05
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué garantiza la regla de dependencia (Dependency Rule) en Clean Architecture?', 1, 2, @arch, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Que todas las capas se comunican de forma bidireccional para mayor flexibilidad', 0, 1),
(@q, N'Que las dependencias solo apuntan hacia adentro: las capas externas dependen de las internas, nunca al revés', 1, 2),
(@q, N'Que la capa de infraestructura define las interfaces que implementan las capas de dominio', 0, 3),
(@q, N'Que cada capa tiene exactamente el mismo número de clases', 0, 4);

-- ARCH-E06
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué es un Aggregate Root en DDD (Domain-Driven Design)?', 1, 3, @arch, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'La base de datos principal de un Bounded Context', 0, 1),
(@q, N'El controlador de API que expone el Aggregate al exterior', 0, 2),
(@q, N'La entidad principal de un Aggregate que controla el acceso a todas las entidades internas y garantiza las invariantes', 1, 3),
(@q, N'Una interfaz que todas las entidades del dominio deben implementar', 0, 4);

-- ARCH-E07
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué establece el teorema CAP para sistemas distribuidos?', 1, 3, @arch, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Un sistema distribuido puede tener Concurrencia, Atomicidad y Persistencia simultáneamente', 0, 1),
(@q, N'Un sistema distribuido solo puede garantizar dos de las tres propiedades: Consistencia, Disponibilidad y Tolerancia a particiones', 1, 2),
(@q, N'Todos los sistemas distribuidos deben sacrificar la disponibilidad para mantener consistencia', 0, 3),
(@q, N'CAP define los tres tipos de bases de datos NoSQL: Column, Array y Property', 0, 4);

-- ARCH-E08
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué es el patrón Strangler Fig en migración de sistemas?', 1, 3, @arch, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Un patrón para eliminar código duplicado en sistemas legacy', 0, 1),
(@q, N'Una técnica que migra gradualmente un monolito a microservicios reemplazando funcionalidades una a una sin reescribir el sistema completo', 1, 2),
(@q, N'Un patrón para deprecar APIs antiguas de forma controlada', 0, 3),
(@q, N'Una estrategia de despliegue que elimina el entorno antiguo inmediatamente tras el despliegue nuevo', 0, 4);

-- ARCH-E09
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué problema resuelve el patrón Saga en microservicios?', 1, 3, @arch, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'La falta de un esquema de base de datos compartido entre servicios', 0, 1),
(@q, N'La gestión de transacciones distribuidas que abarcan múltiples servicios sin usar transacciones ACID distribuidas (2PC)', 1, 2),
(@q, N'El exceso de latencia en las llamadas síncronas entre microservicios', 0, 3),
(@q, N'La autenticación centralizada en arquitecturas de microservicios', 0, 4);

-- ARCH-E10
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué es el patrón Mediator y para qué se usa habitualmente en ASP.NET Core?', 1, 2, @arch, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Un patrón que expone los servicios internos de la aplicación mediante una API REST', 0, 1),
(@q, N'Un patrón que centraliza la comunicación entre objetos, reduciendo el acoplamiento; en ASP.NET Core se usa con MediatR para implementar CQRS', 1, 2),
(@q, N'Un patrón que actúa como middleware HTTP entre el cliente y la base de datos', 0, 3),
(@q, N'Un patrón equivalente al Facade pero orientado a microservicios', 0, 4);

-- ============================================================
-- DEVOPS — 10 preguntas adicionales
-- ============================================================

-- DOPS-E01
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué ventaja principal aporta el multi-stage build en Docker?', 1, 2, @dops, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Permite ejecutar múltiples contenedores en paralelo en la misma imagen', 0, 1),
(@q, N'Reduce el tamaño de la imagen final copiando solo los artefactos necesarios desde etapas de compilación previas', 1, 2),
(@q, N'Aumenta la velocidad de descarga de la imagen desde Docker Hub', 0, 3),
(@q, N'Permite usar varios sistemas operativos base en la misma imagen', 0, 4);

-- DOPS-E02
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué es un Pod en Kubernetes?', 1, 1, @dops, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'El nodo maestro que gestiona el estado del clúster', 0, 1),
(@q, N'Un servicio de balanceo de carga interno del clúster', 0, 2),
(@q, N'La unidad desplegable mínima en Kubernetes que contiene uno o más contenedores que comparten red y almacenamiento', 1, 3),
(@q, N'Una colección de nodos agrupados por zona de disponibilidad', 0, 4);

-- DOPS-E03
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Cuál es la diferencia entre el despliegue blue-green y el despliegue canary?', 1, 2, @dops, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Blue-green usa contenedores; canary usa máquinas virtuales', 0, 1),
(@q, N'Blue-green cambia el 100% del tráfico de golpe a la nueva versión; canary dirige solo un porcentaje reducido al inicio para validación gradual', 1, 2),
(@q, N'Canary requiere dos entornos separados; blue-green solo uno', 0, 3),
(@q, N'No hay diferencia, son sinónimos del mismo patrón', 0, 4);

-- DOPS-E04
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué hace el comando terraform plan?', 1, 1, @dops, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Aplica los cambios de infraestructura inmediatamente', 0, 1),
(@q, N'Inicializa el directorio de trabajo descargando providers y módulos', 0, 2),
(@q, N'Muestra los cambios que se aplicarían sin modificar la infraestructura real', 1, 3),
(@q, N'Destruye todos los recursos gestionados por Terraform', 0, 4);

-- DOPS-E05
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué es un SLO (Service Level Objective)?', 1, 2, @dops, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Un acuerdo contractual con el cliente que define penalizaciones por incumplimiento', 0, 1),
(@q, N'La métrica técnica que mide el estado del servicio (latencia, tasa de error)', 0, 2),
(@q, N'El objetivo de nivel de servicio interno que define el umbral aceptable para un SLI durante un período de tiempo', 1, 3),
(@q, N'El presupuesto asignado para operaciones de Site Reliability', 0, 4);

-- DOPS-E06
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué es el HPA (Horizontal Pod Autoscaler) en Kubernetes?', 1, 2, @dops, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Un controlador que aumenta los recursos de CPU y memoria de cada Pod', 0, 1),
(@q, N'Un controlador que escala automáticamente el número de réplicas de un Deployment basándose en métricas como CPU o memoria', 1, 2),
(@q, N'Un balanceador de carga que distribuye tráfico entre Pods', 0, 3),
(@q, N'Un objeto que gestiona actualizaciones de Pods sin tiempo de inactividad', 0, 4);

-- DOPS-E07
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Por qué se recomienda almacenar el estado de Terraform en un backend remoto en lugar de localmente?', 1, 2, @dops, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Porque el archivo de estado local no soporta variables de entorno', 0, 1),
(@q, N'Para permitir trabajo en equipo con bloqueo del estado, mayor durabilidad y acceso compartido', 1, 2),
(@q, N'Porque Terraform no puede leer el estado desde disco duro local', 0, 3),
(@q, N'Para que terraform plan sea más rápido al no tener que leer el disco', 0, 4);

-- DOPS-E08
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Cuáles son las cuatro "golden signals" de monitorización definidas por SRE?', 1, 3, @dops, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'CPU, Memoria, Disco y Red', 0, 1),
(@q, N'Disponibilidad, Rendimiento, Seguridad y Coste', 0, 2),
(@q, N'Latencia, Tráfico, Errores y Saturación', 1, 3),
(@q, N'Uptime, Throughput, MTTR y MTBF', 0, 4);

-- DOPS-E09
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué diferencia hay entre un liveness probe y un readiness probe en Kubernetes?', 1, 2, @dops, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Liveness comprueba la red; readiness comprueba el disco', 0, 1),
(@q, N'Liveness determina si el contenedor debe reiniciarse; readiness determina si puede recibir tráfico', 1, 2),
(@q, N'Son equivalentes, cualquiera puede sustituir al otro', 0, 3),
(@q, N'Readiness es para contenedores init; liveness para contenedores principales', 0, 4);

-- DOPS-E10
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué es trunk-based development?', 1, 2, @dops, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Una estrategia de ramificación con ramas main, develop, feature, release y hotfix', 0, 1),
(@q, N'Una estrategia donde todos los desarrolladores integran cambios frecuentemente (varias veces al día) en una única rama principal', 1, 2),
(@q, N'Un modelo de ramificación exclusivo para proyectos de código abierto', 0, 3),
(@q, N'La práctica de mantener el historial de Git completamente lineal mediante rebase', 0, 4);

-- ============================================================
-- FRONTEND — 10 preguntas adicionales
-- ============================================================

-- FE-E01
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué es un closure en JavaScript?', 1, 2, @fe, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Una función que no tiene acceso al scope global', 0, 1),
(@q, N'Una función que recuerda las variables del scope en el que fue creada, incluso después de que ese scope haya terminado', 1, 2),
(@q, N'Un bloque try-catch que captura errores de forma silenciosa', 0, 3),
(@q, N'Un módulo ES6 que encapsula su estado interno', 0, 4);

-- FE-E02
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué diferencia hay entre var, let y const en JavaScript?', 1, 1, @fe, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'var y let son iguales; const es inmutable a nivel de tipo', 0, 1),
(@q, N'var tiene scope de función y es hoisted; let y const tienen scope de bloque; const no permite reasignación', 1, 2),
(@q, N'const siempre almacena en memoria de solo lectura del sistema operativo', 0, 3),
(@q, N'No hay diferencias en navegadores modernos, son compatibles hacia atrás', 0, 4);

-- FE-E03
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué es el event loop en JavaScript?', 1, 2, @fe, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Un ciclo que gestiona los eventos del DOM en tiempo real sin callbacks', 0, 1),
(@q, N'El mecanismo que permite a JavaScript ejecutar código asíncrono procesando la call stack y las colas de tareas (macrotask/microtask) de forma no bloqueante', 1, 2),
(@q, N'Un bucle infinito que escucha eventos del sistema operativo', 0, 3),
(@q, N'La API del navegador que ejecuta animaciones a 60fps', 0, 4);

-- FE-E04
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué diferencia hay entre interface y type alias en TypeScript?', 1, 2, @fe, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'interface solo sirve para clases; type solo para funciones', 0, 1),
(@q, N'interface permite declaration merging y es extensible con extends; type puede representar uniones, intersecciones y primitivos', 1, 2),
(@q, N'Son completamente equivalentes en todos los escenarios', 0, 3),
(@q, N'type es más moderno y reemplaza completamente a interface en TypeScript 5', 0, 4);

-- FE-E05
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué hace la propiedad justify-content en un contenedor Flexbox?', 1, 1, @fe, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Alinea los elementos en el eje transversal (perpendicular al eje principal)', 0, 1),
(@q, N'Alinea los elementos a lo largo del eje principal del contenedor flex', 1, 2),
(@q, N'Define el tamaño de los elementos hijos cuando el contenedor tiene espacio libre', 0, 3),
(@q, N'Controla el orden de los elementos dentro del contenedor flex', 0, 4);

-- FE-E06
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Cómo se previene principalmente un ataque XSS (Cross-Site Scripting)?', 1, 2, @fe, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Usando HTTPS en todas las comunicaciones', 0, 1),
(@q, N'Escapando la salida HTML, aplicando una Content Security Policy (CSP) y sanitizando el contenido generado por el usuario', 1, 2),
(@q, N'Validando el token CSRF en cada petición POST', 0, 3),
(@q, N'Cifrando las cookies de sesión con AES-256', 0, 4);

-- FE-E07
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué es Blazor WebAssembly?', 1, 1, @fe, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Un framework de JavaScript desarrollado por Microsoft como alternativa a React', 0, 1),
(@q, N'Un framework que permite ejecutar C# directamente en el navegador mediante WebAssembly, sin necesidad de JavaScript', 1, 2),
(@q, N'Un motor de renderizado de servidor para aplicaciones ASP.NET Core', 0, 3),
(@q, N'Una librería de componentes UI basada en Bootstrap para aplicaciones .NET', 0, 4);

-- FE-E08
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué es el lazy loading en el contexto de aplicaciones web?', 1, 1, @fe, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Una técnica que pre-carga todos los recursos de la página antes de mostrársela al usuario', 0, 1),
(@q, N'Una técnica que retrasa la carga de recursos (imágenes, módulos JS) hasta que son necesarios, reduciendo el tiempo de carga inicial', 1, 2),
(@q, N'El uso de animaciones de skeleton para indicar que el contenido está cargando', 0, 3),
(@q, N'Un patrón de virtualización de listas para renderizar miles de elementos sin degradación de rendimiento', 0, 4);

-- FE-E09
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Para qué sirve un Service Worker en una Progressive Web App (PWA)?', 1, 2, @fe, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Para acelerar la ejecución de JavaScript en el hilo principal', 0, 1),
(@q, N'Es un script que se ejecuta en background independientemente del DOM, habilitando caché offline, push notifications y sincronización en background', 1, 2),
(@q, N'Para compartir estado entre múltiples pestañas del navegador', 0, 3),
(@q, N'Para compilar TypeScript a JavaScript en tiempo de ejecución', 0, 4);

-- FE-E10
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué son los Core Web Vitals de Google?', 1, 2, @fe, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Las tres métricas del rendimiento del servidor: TTFB, latencia y throughput', 0, 1),
(@q, N'Métricas de experiencia de usuario que Google usa para el ranking SEO: LCP (carga), INP (interactividad) y CLS (estabilidad visual)', 1, 2),
(@q, N'Los cuatro pilares de accesibilidad web: perceptible, operable, comprensible y robusto', 0, 3),
(@q, N'Estándares de seguridad para aplicaciones web definidos por el W3C', 0, 4);

-- ============================================================
-- AZURE — 25 preguntas nuevas
-- ============================================================

-- AZ-01
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Cuál es la diferencia entre IaaS, PaaS y SaaS en Azure?', 1, 1, @az, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'IaaS gestiona aplicaciones; PaaS gestiona datos; SaaS gestiona red', 0, 1),
(@q, N'IaaS proporciona infraestructura virtualizada (VMs, redes); PaaS proporciona plataforma gestionada (runtime, base de datos); SaaS entrega software completo al usuario final', 1, 2),
(@q, N'Son tres niveles de precio del mismo servicio de Azure', 0, 3),
(@q, N'IaaS es para on-premises; PaaS para la nube pública; SaaS para la nube privada', 0, 4);

-- AZ-02
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué es Azure App Service?', 1, 1, @az, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Un servicio para ejecutar contenedores Docker en Azure sin orquestación', 0, 1),
(@q, N'Una plataforma PaaS para hospedar aplicaciones web, APIs REST y backends móviles sin gestionar la infraestructura del servidor', 1, 2),
(@q, N'Un servicio de almacenamiento de archivos estáticos para sitios web', 0, 3),
(@q, N'Una herramienta de CI/CD integrada en Azure para despliegues automáticos', 0, 4);

-- AZ-03
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué es Azure Functions y qué modelo de ejecución usa?', 1, 1, @az, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Un servicio de máquinas virtuales que escala automáticamente según el tráfico', 0, 1),
(@q, N'Una plataforma de cómputo serverless donde el código se ejecuta en respuesta a triggers y solo se paga por el tiempo de ejecución', 1, 2),
(@q, N'Un servicio equivalente a Azure App Service pero exclusivo para APIs', 0, 3),
(@q, N'Un contenedor Docker gestionado que se reinicia automáticamente', 0, 4);

-- AZ-04
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué es AKS (Azure Kubernetes Service)?', 1, 1, @az, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Un orquestador de contenedores propietario de Microsoft alternativo a Kubernetes', 0, 1),
(@q, N'Un servicio Kubernetes gestionado donde Microsoft administra el plano de control y el cliente solo gestiona los nodos de trabajo', 1, 2),
(@q, N'Una herramienta de monitorización para clústeres Kubernetes on-premises', 0, 3),
(@q, N'Un registro privado de imágenes Docker integrado en Azure', 0, 4);

-- AZ-05
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Cuáles son los tres niveles de acceso de Azure Blob Storage?', 1, 2, @az, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Public, Private y Shared', 0, 1),
(@q, N'Hot, Cool y Archive: a mayor frío, menor coste de almacenamiento pero mayor coste y latencia de acceso', 1, 2),
(@q, N'Standard, Premium y Ultra', 0, 3),
(@q, N'LRS, ZRS y GRS', 0, 4);

-- AZ-06
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Cuál es la principal diferencia entre Azure SQL Database y Azure Cosmos DB?', 1, 2, @az, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Azure SQL es de pago; Cosmos DB es gratuito en el nivel básico', 0, 1),
(@q, N'Azure SQL es una base de datos relacional gestionada; Cosmos DB es una base de datos NoSQL distribuida globalmente con múltiples modelos de datos', 1, 2),
(@q, N'Cosmos DB solo almacena documentos JSON; Azure SQL solo almacena imágenes', 0, 3),
(@q, N'Azure SQL está disponible en Azure; Cosmos DB solo en AWS', 0, 4);

-- AZ-07
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué es un Network Security Group (NSG) en Azure?', 1, 2, @az, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Un servicio de VPN que cifra el tráfico entre VNets', 0, 1),
(@q, N'Un firewall de aplicaciones web (WAF) que filtra tráfico HTTP/HTTPS', 0, 2),
(@q, N'Un conjunto de reglas de seguridad de red de entrada y salida que se aplican a subredes o interfaces de red en Azure', 1, 3),
(@q, N'Un grupo de recursos de Azure que comparte la misma configuración de red', 0, 4);

-- AZ-08
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué es una Managed Identity en Azure y cuál es su principal ventaja?', 1, 2, @az, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Una cuenta de servicio de Active Directory que requiere renovación manual de contraseñas cada 90 días', 0, 1),
(@q, N'Una identidad gestionada automáticamente por Azure que permite a los recursos autenticarse con otros servicios Azure sin almacenar credenciales en el código o configuración', 1, 2),
(@q, N'Un usuario administrador con permisos globales sobre toda la suscripción', 0, 3),
(@q, N'Un certificado SSL gestionado automáticamente por Azure App Service', 0, 4);

-- AZ-09
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué es Azure Key Vault?', 1, 1, @az, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Un servicio de base de datos cifrada para almacenar datos sensibles de aplicaciones', 0, 1),
(@q, N'Un servicio para almacenar y gestionar de forma segura secretos, certificados y claves criptográficas con control de acceso granular', 1, 2),
(@q, N'Una bóveda de copias de seguridad para máquinas virtuales y bases de datos', 0, 3),
(@q, N'Un gestor de contraseñas para usuarios de la organización en Azure AD', 0, 4);

-- AZ-10
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Cuál es la diferencia entre Azure Service Bus y Azure Event Hub?', 1, 3, @az, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Service Bus es de pago; Event Hub es gratuito para menos de 1 millón de eventos', 0, 1),
(@q, N'Service Bus es un broker de mensajes empresarial (queues/topics) para integración entre aplicaciones; Event Hub es una plataforma de streaming masivo para telemetría e IoT', 1, 2),
(@q, N'Event Hub reemplaza completamente a Service Bus en arquitecturas modernas', 0, 3),
(@q, N'Service Bus solo funciona con .NET; Event Hub es independiente del lenguaje', 0, 4);

-- AZ-11
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué es Application Insights en Azure?', 1, 1, @az, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Una herramienta de gestión de código fuente integrada en Azure DevOps', 0, 1),
(@q, N'Un servicio de APM (Application Performance Monitoring) que recopila telemetría de aplicaciones: trazas, dependencias, excepciones, métricas y logs', 1, 2),
(@q, N'Un analizador estático de código que detecta vulnerabilidades de seguridad', 0, 3),
(@q, N'Un servicio de test de carga para APIs y aplicaciones web', 0, 4);

-- AZ-12
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué SLA de disponibilidad ofrecen las VMs de Azure desplegadas en múltiples Availability Zones?', 1, 2, @az, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'99.9%', 0, 1),
(@q, N'99.95%', 0, 2),
(@q, N'99.99%', 1, 3),
(@q, N'100%', 0, 4);

-- AZ-13
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué es Azure RBAC (Role-Based Access Control)?', 1, 1, @az, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Un sistema de autenticación multifactor para usuarios de Azure AD', 0, 1),
(@q, N'Sistema de control de acceso que gestiona quién puede realizar qué acciones sobre qué recursos Azure, asignando roles a usuarios, grupos o identidades', 1, 2),
(@q, N'Una herramienta para auditar los cambios realizados en recursos de Azure', 0, 3),
(@q, N'Un servicio de encriptación de datos en reposo para recursos Azure', 0, 4);

-- AZ-14
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué es Azure ExpressRoute y en qué se diferencia de una VPN Gateway?', 1, 2, @az, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'ExpressRoute es más barato que VPN Gateway para conexiones de alta velocidad', 0, 1),
(@q, N'ExpressRoute es una conexión privada y dedicada que no pasa por internet público, con mayor ancho de banda y menor latencia; VPN Gateway usa internet cifrado', 1, 2),
(@q, N'VPN Gateway es exclusivo para conexiones site-to-site; ExpressRoute para conexiones punto a punto', 0, 3),
(@q, N'Son equivalentes, la única diferencia es el proveedor de red subyacente', 0, 4);

-- AZ-15
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué es Azure Policy?', 1, 2, @az, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Una herramienta para gestionar las políticas de contraseñas de los usuarios de Azure AD', 0, 1),
(@q, N'Un servicio que permite crear, asignar y gestionar reglas (policies) para garantizar que los recursos Azure cumplan los estándares corporativos y regulatorios', 1, 2),
(@q, N'El sistema de backups automatizado para bases de datos en Azure', 0, 3),
(@q, N'Un firewall de aplicaciones web (WAF) configurable a nivel de suscripción', 0, 4);

-- AZ-16
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué es Azure Bicep?', 1, 2, @az, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Una extensión de Azure CLI para desplegar recursos desde la línea de comandos', 0, 1),
(@q, N'Un lenguaje DSL declarativo y conciso para desplegar recursos Azure como alternativa a las plantillas ARM en JSON', 1, 2),
(@q, N'Un framework de testing para pipelines de Azure DevOps', 0, 3),
(@q, N'Una herramienta de monitorización de costes de Azure en tiempo real', 0, 4);

-- AZ-17
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué son las Reserved Instances en Azure?', 1, 2, @az, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Instancias que Azure reserva automáticamente ante picos de demanda sin coste adicional', 0, 1),
(@q, N'Compromiso de uso de 1 o 3 años de recursos Azure a cambio de descuentos de hasta el 72% frente a precios pay-as-you-go', 1, 2),
(@q, N'VMs de alta disponibilidad garantizada con SLA del 99.999%', 0, 3),
(@q, N'Instancias dedicadas que ningún otro cliente de Azure comparte', 0, 4);

-- AZ-18
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué es un Private Endpoint en Azure?', 1, 3, @az, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Un punto de acceso privado exclusivo para administradores de la suscripción', 0, 1),
(@q, N'Una interfaz de red con IP privada dentro de una VNet que conecta servicios Azure sin exponer tráfico a internet público', 1, 2),
(@q, N'Un contenedor Docker con red aislada del exterior', 0, 3),
(@q, N'Una regla de NSG que bloquea todo el tráfico entrante externo', 0, 4);

-- AZ-19
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué es Azure Cosmos DB y cuántos modelos de datos soporta de forma nativa?', 1, 2, @az, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Una base de datos SQL distribuida que solo soporta el modelo relacional', 0, 1),
(@q, N'Una base de datos NoSQL distribuida globalmente que soporta múltiples APIs: SQL (documentos), MongoDB, Cassandra, Gremlin (grafos) y Table', 1, 2),
(@q, N'Un data warehouse analítico compatible únicamente con Apache Spark', 0, 3),
(@q, N'Una base de datos de series temporales para telemetría IoT', 0, 4);

-- AZ-20
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué es Azure API Management (APIM)?', 1, 2, @az, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Un framework para generar SDKs de cliente a partir de especificaciones OpenAPI', 0, 1),
(@q, N'Un servicio centralizado para publicar, proteger, transformar, documentar y monitorizar APIs, actuando como gateway entre clientes y backends', 1, 2),
(@q, N'Una herramienta de testing automático de APIs REST basada en colecciones Postman', 0, 3),
(@q, N'Un servicio de almacenamiento de respuestas de API en caché a nivel global', 0, 4);

-- AZ-21
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué es Azure Monitor y cuáles son sus principales componentes?', 1, 2, @az, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Una herramienta exclusiva para monitorizar el coste y la facturación de Azure', 0, 1),
(@q, N'Plataforma unificada de observabilidad que incluye métricas, logs (Log Analytics), Application Insights, alertas y dashboards para recursos Azure y on-premises', 1, 2),
(@q, N'Un servicio de auditoría de cambios en la configuración de recursos Azure', 0, 3),
(@q, N'Un sistema de gestión de incidencias integrado con Azure DevOps Boards', 0, 4);

-- AZ-22
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué son las Availability Zones (Zonas de disponibilidad) en Azure?', 1, 2, @az, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Regiones de Azure separadas geográficamente para recuperación ante desastres', 0, 1),
(@q, N'Ubicaciones físicamente separadas dentro de una misma región Azure con alimentación, refrigeración y red independientes, para alta disponibilidad', 1, 2),
(@q, N'Grupos de recursos de Azure organizados por zona horaria', 0, 3),
(@q, N'Particiones lógicas de una suscripción para separar entornos de dev y producción', 0, 4);

-- AZ-23
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué es Azure Static Web Apps?', 1, 2, @az, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Un servicio de almacenamiento de archivos estáticos equivalente a Azure Blob Storage', 0, 1),
(@q, N'Un servicio que combina hosting global de SPAs y Blazor WASM con una API serverless integrada (Azure Functions), CI/CD automático desde GitHub/Azure Repos', 1, 2),
(@q, N'Una CDN de pago por uso exclusiva para archivos HTML, CSS y JavaScript', 0, 3),
(@q, N'Un servicio de generación de sitios estáticos tipo Jekyll integrado en Azure', 0, 4);

-- AZ-24
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué es Azure Cache for Redis y para qué se usa principalmente?', 1, 1, @az, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Un servicio de base de datos NoSQL de clave-valor para almacenamiento persistente', 0, 1),
(@q, N'Un servicio de caché en memoria basado en Redis para reducir la latencia de las aplicaciones y la carga sobre bases de datos almacenando datos de acceso frecuente', 1, 2),
(@q, N'Una cola de mensajes de alto rendimiento para streaming de datos', 0, 3),
(@q, N'Un servicio de gestión de sesiones de usuario para aplicaciones web en Azure', 0, 4);

-- AZ-25
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué es el Azure Well-Architected Framework?', 1, 2, @az, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Un conjunto de plantillas ARM prediseñadas para arquitecturas de referencia comunes', 0, 1),
(@q, N'Marco de buenas prácticas con cinco pilares para diseñar soluciones Azure: Fiabilidad, Seguridad, Optimización de costes, Excelencia operativa y Eficiencia de rendimiento', 1, 2),
(@q, N'La documentación oficial de Microsoft sobre todos los servicios de Azure', 0, 3),
(@q, N'Una herramienta de diseño visual de arquitecturas integrada en Azure Portal', 0, 4);

-- ============================================================
-- AZURE DEVOPS — 25 preguntas nuevas
-- ============================================================

-- AZDO-01
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Cuál es la jerarquía de elementos de trabajo en Azure Boards?', 1, 1, @azdo, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Sprint > Backlog > User Story > Task', 0, 1),
(@q, N'Epic > Feature > User Story > Task / Bug', 1, 2),
(@q, N'Theme > Epic > Feature > Story', 0, 3),
(@q, N'Milestone > Issue > Sub-task > Comment', 0, 4);

-- AZDO-02
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Cuál es la diferencia principal entre pipelines YAML y pipelines clásicos (Classic) en Azure DevOps?', 1, 2, @azdo, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Los pipelines YAML son más rápidos; los clásicos tienen más tareas disponibles', 0, 1),
(@q, N'Los YAML se definen como código en el repositorio (pipeline as code, versionables con el código); los clásicos se configuran en la UI sin versionar junto al código fuente', 1, 2),
(@q, N'Los clásicos soportan despliegues en Azure; los YAML solo soportan builds', 0, 3),
(@q, N'Los YAML requieren agentes self-hosted; los clásicos usan agentes Microsoft-hosted', 0, 4);

-- AZDO-03
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Cuál es la jerarquía de un pipeline YAML en Azure DevOps?', 1, 1, @azdo, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Pipeline > Tasks > Steps > Scripts', 0, 1),
(@q, N'Pipeline > Stages > Jobs > Steps', 1, 2),
(@q, N'Workflow > Jobs > Steps > Actions', 0, 3),
(@q, N'Pipeline > Phases > Tasks > Commands', 0, 4);

-- AZDO-04
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué diferencia hay entre agentes Microsoft-hosted y self-hosted en Azure Pipelines?', 1, 2, @azdo, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Los Microsoft-hosted son gratuitos ilimitados; los self-hosted tienen coste por minuto', 0, 1),
(@q, N'Microsoft-hosted: Azure gestiona y provisiona el agente en cada ejecución; Self-hosted: el equipo instala y mantiene el agente en su propia infraestructura', 1, 2),
(@q, N'Self-hosted solo soportan Linux; Microsoft-hosted soportan todos los SO', 0, 3),
(@q, N'No hay diferencias funcionales, solo de latencia de inicio', 0, 4);

-- AZDO-05
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué son los Variable Groups en Azure Pipelines y para qué sirven?', 1, 2, @azdo, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Grupos de tareas que se ejecutan con las mismas variables de entorno', 0, 1),
(@q, N'Colecciones de variables reutilizables entre pipelines, que pueden vincularse a Azure Key Vault para gestionar secretos de forma segura', 1, 2),
(@q, N'Variables definidas a nivel de Stage que se heredan en todos sus Jobs', 0, 3),
(@q, N'Un tipo especial de variable para almacenar matrices (arrays) de valores', 0, 4);

-- AZDO-06
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué son los Environments en Azure Pipelines y qué funcionalidad clave aportan?', 1, 2, @azdo, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Contenedores Docker específicos para cada entorno de despliegue', 0, 1),
(@q, N'Representaciones de entornos de despliegue (dev, staging, prod) con historial de despliegues, approvals manuales y checks de calidad antes de desplegar', 1, 2),
(@q, N'Grupos de variables separados por entorno que se inyectan automáticamente', 0, 3),
(@q, N'Configuraciones de red que aíslan pipelines entre entornos de Azure', 0, 4);

-- AZDO-07
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué es un pipeline template en Azure DevOps y cuál es su ventaja principal?', 1, 2, @azdo, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Una imagen de agente preconstruida con todas las herramientas de build instaladas', 0, 1),
(@q, N'Un archivo YAML reutilizable que define steps, jobs o stages y puede incluirse en múltiples pipelines, evitando duplicación de código', 1, 2),
(@q, N'Un pipeline que sirve como referencia de documentación del proceso de CI/CD', 0, 3),
(@q, N'Una plantilla de Azure Resource Manager para desplegar la infraestructura del pipeline', 0, 4);

-- AZDO-08
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Cuáles son las estrategias de despliegue disponibles en un deployment job de Azure Pipelines YAML?', 1, 3, @azdo, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'deploy, rollback y validate', 0, 1),
(@q, N'runOnce, rolling y canary', 1, 2),
(@q, N'blue-green, red-black y shadow', 0, 3),
(@q, N'sequential, parallel y matrix', 0, 4);

-- AZDO-09
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué es Azure Artifacts y qué tipos de paquetes soporta?', 1, 1, @azdo, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Un repositorio de imágenes Docker exclusivo para Azure Container Registry', 0, 1),
(@q, N'Un servicio de gestión de paquetes que soporta NuGet, npm, Maven, Python (PyPI) y Universal Packages', 1, 2),
(@q, N'Un almacén de artefactos de build solo compatible con proyectos .NET', 0, 3),
(@q, N'Una extensión de Azure Blob Storage para almacenar binarios de aplicaciones', 0, 4);

-- AZDO-10
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué es un Personal Access Token (PAT) en Azure DevOps?', 1, 1, @azdo, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Una contraseña de un solo uso para acceder al Azure Portal', 0, 1),
(@q, N'Un token de autenticación con ámbito y expiración configurables que sustituye a la contraseña para acceder a la API o herramientas de Azure DevOps', 1, 2),
(@q, N'El token JWT generado automáticamente al iniciar sesión en Azure DevOps', 0, 3),
(@q, N'Una clave de cifrado para proteger secretos en Variable Groups', 0, 4);

-- AZDO-11
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué es una Service Connection en Azure DevOps?', 1, 2, @azdo, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Una conexión VPN entre el agente de build y los servidores de producción', 0, 1),
(@q, N'Una configuración que almacena credenciales para que Azure Pipelines se conecte de forma segura a servicios externos como Azure, Docker Hub o GitHub', 1, 2),
(@q, N'Un endpoint HTTP que expone el estado del pipeline a sistemas externos', 0, 3),
(@q, N'Un canal de comunicación entre Azure Boards y Azure Repos', 0, 4);

-- AZDO-12
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué hace la clave condition: en un step de Azure Pipelines YAML?', 1, 2, @azdo, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Establece las variables de entorno disponibles en el step', 0, 1),
(@q, N'Define una expresión que debe evaluarse como verdadera para que el step se ejecute, permitiendo ejecución condicional', 1, 2),
(@q, N'Indica el número máximo de reintentos del step si falla', 0, 3),
(@q, N'Especifica el tiempo de espera máximo del step en minutos', 0, 4);

-- AZDO-13
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Cómo se ejecuta un step en Azure Pipelines aunque el step anterior haya fallado?', 1, 2, @azdo, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Usando continueOnError: true en el step anterior', 0, 1),
(@q, N'Añadiendo condition: always() o condition: failed() en el step que debe ejecutarse siempre', 1, 2),
(@q, N'Poniendo el step en un Job separado con dependsOn vacío', 0, 3),
(@q, N'No es posible, un pipeline se detiene completamente si falla algún step', 0, 4);

-- AZDO-14
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué es la estrategia matrix en un Job de Azure Pipelines?', 1, 3, @azdo, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Una tabla de variables que se muestra en el resumen del pipeline tras la ejecución', 0, 1),
(@q, N'Una configuración que ejecuta el mismo Job en paralelo con diferentes combinaciones de variables, útil para tests multi-plataforma o multi-versión', 1, 2),
(@q, N'Un tipo de artefacto estructurado en formato de tabla para publicar resultados de tests', 0, 3),
(@q, N'La estrategia de rollback automático cuando falla el despliegue en producción', 0, 4);

-- AZDO-15
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué son las branch policies en Azure Repos?', 1, 2, @azdo, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Reglas de nomenclatura obligatoria para las ramas del repositorio', 0, 1),
(@q, N'Reglas que protegen ramas críticas (main, develop) requiriendo: aprobaciones de PR, build exitoso, resolución de comentarios, revisores mínimos, etc.', 1, 2),
(@q, N'Permisos de escritura asignados a grupos de usuarios sobre ramas específicas', 0, 3),
(@q, N'Políticas de retención que eliminan ramas después de cierto tiempo inactivas', 0, 4);

-- AZDO-16
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué diferencia hay entre Git y TFVC como sistemas de control de versiones en Azure Repos?', 1, 1, @azdo, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Git es de Microsoft; TFVC es un estándar open source de la industria', 0, 1),
(@q, N'Git es un sistema distribuido (cada desarrollador tiene el historial completo); TFVC es centralizado (el historial reside en el servidor)', 1, 2),
(@q, N'TFVC soporta Pull Requests; Git no tiene esa funcionalidad en Azure Repos', 0, 3),
(@q, N'Git solo soporta proyectos pequeños; TFVC está diseñado para repositorios empresariales grandes', 0, 4);

-- AZDO-17
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué es Azure Test Plans dentro de Azure DevOps?', 1, 1, @azdo, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Una herramienta de generación automática de tests unitarios a partir del código fuente', 0, 1),
(@q, N'Una herramienta para planificar, ejecutar y hacer seguimiento de pruebas manuales y automatizadas, con test suites, test cases y resultados de ejecución', 1, 2),
(@q, N'Un servicio de test de carga y rendimiento para APIs y aplicaciones web', 0, 3),
(@q, N'Un plugin de Visual Studio para ejecutar tests de integración en local', 0, 4);

-- AZDO-18
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Cómo se define un trigger de CI para la rama main en un pipeline YAML de Azure DevOps?', 1, 2, @azdo, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'on: push: branches: [main]', 0, 1),
(@q, N'trigger: branches: include: [main]  (o simplemente trigger: [main])', 1, 2),
(@q, N'pipeline_trigger: on_commit: branch: main', 0, 3),
(@q, N'watch: main: events: [push]', 0, 4);

-- AZDO-19
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué hace la tarea AzureCLI@2 en Azure Pipelines?', 1, 2, @azdo, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Instala la Azure CLI en el agente del pipeline', 0, 1),
(@q, N'Ejecuta scripts de Azure CLI en el agente con autenticación automática mediante la Service Connection configurada, sin necesidad de login manual', 1, 2),
(@q, N'Configura las variables de entorno de Azure en el agente para el resto del pipeline', 0, 3),
(@q, N'Despliega una aplicación web en Azure App Service usando la CLI', 0, 4);

-- AZDO-20
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué es el concepto de pipeline as code en Azure DevOps?', 1, 1, @azdo, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Generar código fuente automáticamente mediante pipelines de transformación', 0, 1),
(@q, N'Definir los pipelines de CI/CD en archivos YAML versionados junto al código fuente, permitiendo revisión, historial y branching del propio pipeline', 1, 2),
(@q, N'Usar PowerShell o Bash para crear recursos de Azure Pipelines mediante la API REST', 0, 3),
(@q, N'La práctica de documentar el pipeline en formato Markdown dentro del repositorio', 0, 4);

-- AZDO-21
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Para qué se usa dependsOn entre stages en un pipeline YAML de Azure DevOps?', 1, 2, @azdo, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Para compartir variables entre stages del pipeline', 0, 1),
(@q, N'Para establecer el orden de ejecución entre stages: un stage no comienza hasta que los stages de los que depende hayan finalizado con éxito', 1, 2),
(@q, N'Para indicar que dos stages pueden ejecutarse en paralelo', 0, 3),
(@q, N'Para pasar artefactos de un stage al siguiente automáticamente', 0, 4);

-- AZDO-22
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué es un scheduled trigger en Azure Pipelines?', 1, 1, @azdo, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Un trigger que inicia el pipeline cuando se programa un sprint en Azure Boards', 0, 1),
(@q, N'Un trigger que ejecuta el pipeline automáticamente según una expresión cron, independientemente de cambios en el código (útil para builds nocturnos o tests de regresión)', 1, 2),
(@q, N'Un trigger que espera a que se complete otro pipeline antes de ejecutarse', 0, 3),
(@q, N'Un trigger que inicia el pipeline cuando un elemento de trabajo cambia de estado en Boards', 0, 4);

-- AZDO-23
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué hace la tarea cache en Azure Pipelines y qué beneficio aporta?', 1, 2, @azdo, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Almacena los artefactos de build para usarlos en el stage de despliegue', 0, 1),
(@q, N'Almacena y restaura dependencias (node_modules, paquetes NuGet) entre ejecuciones del pipeline, reduciendo significativamente el tiempo de build', 1, 2),
(@q, N'Guarda en caché las respuestas de las llamadas HTTP del pipeline para evitar rate limiting', 0, 3),
(@q, N'Cachea la imagen del agente de build para acelerar el aprovisionamiento del agente', 0, 4);

-- AZDO-24
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué son los upstream sources en un feed de Azure Artifacts?', 1, 3, @azdo, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Los pipelines de upstream que publican paquetes en el feed', 0, 1),
(@q, N'Fuentes externas (nuget.org, npmjs.com) configuradas en el feed para que actúe como proxy y caché, centralizando el acceso a paquetes públicos y privados', 1, 2),
(@q, N'Las versiones anteriores de un paquete almacenadas para rollback', 0, 3),
(@q, N'Las organizaciones de Azure DevOps que pueden consumir el feed', 0, 4);

-- AZDO-25
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué diferencia hay entre un Epic y una Feature en Azure Boards?', 1, 1, @azdo, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Epic y Feature son sinónimos en Azure Boards, solo difieren en el proceso Scrum vs Kanban', 0, 1),
(@q, N'Un Epic representa una gran iniciativa estratégica de negocio de largo plazo; una Feature es una capacidad funcional entregable que forma parte de ese Epic', 1, 2),
(@q, N'Feature es el nivel superior; Epic es una agrupación de Features de la misma área', 0, 3),
(@q, N'Epic se usa en el backlog del producto; Feature en el backlog del sprint', 0, 4);

PRINT 'Script completado: preguntas adicionales y categorías Azure y Azure DevOps insertadas correctamente.';
GO
