-- ============================================================
-- TechEval — Preguntas del examen de competencias IECSTRCEFNSTW25
-- Fuente: Examen competencias v3.pdf  (15/03/2026)
-- Secciones: Backend (39) · Frontend (6) · Bases de datos (38) · iECS (6)
-- ============================================================

USE TechEvalDb;
GO

-- ── Compatibilidad: añadir DEFAULT si el esquema fue creado por EF Core EnsureCreatedAsync
--    (create_database.sql ya los incluye; estos ALTER son no-op en ese caso)
IF NOT EXISTS (
    SELECT 1 FROM sys.default_constraints
    WHERE parent_object_id = OBJECT_ID('dbo.Categories') AND col_name(parent_object_id, parent_column_id) = 'CreatedAt')
    ALTER TABLE dbo.Categories ADD DEFAULT GETUTCDATE() FOR CreatedAt;

IF NOT EXISTS (
    SELECT 1 FROM sys.default_constraints
    WHERE parent_object_id = OBJECT_ID('dbo.Categories') AND col_name(parent_object_id, parent_column_id) = 'IsActive')
    ALTER TABLE dbo.Categories ADD DEFAULT 1 FOR IsActive;

IF NOT EXISTS (
    SELECT 1 FROM sys.default_constraints
    WHERE parent_object_id = OBJECT_ID('dbo.Questions') AND col_name(parent_object_id, parent_column_id) = 'CreatedAt')
    ALTER TABLE dbo.Questions ADD DEFAULT GETUTCDATE() FOR CreatedAt;

IF NOT EXISTS (
    SELECT 1 FROM sys.default_constraints
    WHERE parent_object_id = OBJECT_ID('dbo.Questions') AND col_name(parent_object_id, parent_column_id) = 'IsActive')
    ALTER TABLE dbo.Questions ADD DEFAULT 1 FOR IsActive;
GO

-- ── Nuevas categorías ─────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM dbo.Categories WHERE Name = 'Frontend')
    INSERT INTO dbo.Categories (Name, Description)
    VALUES ('Frontend', 'JavaScript, frameworks SPA, UX/UI, Blazor WASM');

IF NOT EXISTS (SELECT 1 FROM dbo.Categories WHERE Name = 'iECS')
    INSERT INTO dbo.Categories (Name, Description)
    VALUES ('iECS', 'Plataforma iECS de Grupo Pronet: listados, ventanas, tareas');
GO

-- ── IDs de categoría ──────────────────────────────────────
DECLARE @cs   INT = (SELECT Id FROM dbo.Categories WHERE Name = 'C#');
DECLARE @api  INT = (SELECT Id FROM dbo.Categories WHERE Name = 'APIs REST');
DECLARE @arch INT = (SELECT Id FROM dbo.Categories WHERE Name = 'Arquitectura');
DECLARE @sql  INT = (SELECT Id FROM dbo.Categories WHERE Name = 'SQL');
DECLARE @fe   INT = (SELECT Id FROM dbo.Categories WHERE Name = 'Frontend');
DECLARE @iecs INT = (SELECT Id FROM dbo.Categories WHERE Name = 'iECS');

DECLARE @q INT;  -- variable reutilizable para capturar el Id de cada pregunta

-- ============================================================
-- SECCIÓN BACKEND (39 preguntas)
-- ============================================================

-- B-01
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'En C#, ¿qué palabra clave se utiliza para implementar una interfaz?', 1, 1, @cs, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'base',          0, 1), (@q, N'override',      0, 2),
(@q, N'implements',    0, 3), (@q, N': (dos puntos)', 1, 4);

-- B-02
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Cuál de los siguientes patrones se usa para desacoplar la creación de objetos complejos?', 1, 2, @arch, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Singleton',  0, 1), (@q, N'Builder',    1, 2),
(@q, N'Repository', 0, 3), (@q, N'Decorator',  0, 4);

-- B-03
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'En ASP.NET Web API, el método HTTP idóneo para actualizar parcialmente un recurso es:', 1, 1, @api, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'POST', 0, 1), (@q, N'PUT',   0, 2),
(@q, N'GET',  0, 3), (@q, N'PATCH', 1, 4);

-- B-04
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué beneficio principal aporta el patrón Repository?', 1, 2, @arch, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Permite cachear entidades automáticamente',            0, 1),
(@q, N'Abstrae el acceso a datos y facilita pruebas unitarias', 1, 2),
(@q, N'Permite generar endpoints dinámicos',                  0, 3),
(@q, N'Mejora el rendimiento de las consultas SQL',           0, 4);

-- B-05
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Cuál de los siguientes frameworks se usa para pruebas unitarias en .NET?', 1, 1, @cs, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Mocha',  0, 1), (@q, N'NUnit',  1, 2),
(@q, N'Cypress',0, 3), (@q, N'Jest',   0, 4);

-- B-06
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué característica es propia de .NET 5+ frente a versiones anteriores?', 1, 1, @cs, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Es exclusivamente Windows',                 0, 1),
(@q, N'Su soporte para WebForms es nativo',        0, 2),
(@q, N'Plataforma unificada y multiplataforma',    1, 3),
(@q, N'Solo permite compilación JIT',              0, 4);

-- B-07
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué patrón se emplea para separar responsabilidades en aplicaciones MVC?', 1, 2, @arch, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Model-View-Presenter',    0, 1), (@q, N'Model-View-Controller', 1, 2),
(@q, N'Clean Architecture',      0, 3), (@q, N'CQRS',                  0, 4);

-- B-08
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué palabra clave evita que una clase pueda heredarse?', 1, 1, @cs, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'static',   0, 1), (@q, N'readonly', 0, 2),
(@q, N'sealed',   1, 3), (@q, N'final',    0, 4);

-- B-09
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Cuál es el tipo por defecto de una variable numérica entera en C#?', 1, 1, @cs, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'short', 0, 1), (@q, N'long', 0, 2),
(@q, N'int',   1, 3), (@q, N'byte', 0, 4);

-- B-10
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué instrucción permite manejar excepciones?', 1, 1, @cs, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'using',     0, 1), (@q, N'switch',    0, 2),
(@q, N'try-catch', 1, 3), (@q, N'handle',    0, 4);

-- B-11
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué interfaz se utiliza para implementar enumeración personalizada?', 1, 2, @cs, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'IList',                    0, 1), (@q, N'IDisposable',              0, 2),
(@q, N'IEnumerator / IEnumerable',1, 3), (@q, N'ICollection',              0, 4);

-- B-12
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué palabra clave se usa para liberar recursos no administrados?', 1, 1, @cs, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'finalize',         0, 1), (@q, N'close',            0, 2),
(@q, N'using (IDisposable)', 1, 3), (@q, N'clean',         0, 4);

-- B-13
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Cuál es el operador de null-coalescing?', 1, 1, @cs, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'??=', 0, 1), (@q, N'?:', 0, 2),
(@q, N'??',  1, 3), (@q, N'&&', 0, 4);

-- B-14
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué tipo permite valores nulos para tipos primitivos?', 1, 1, @cs, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Optional', 0, 1), (@q, N'variant',  0, 2),
(@q, N'Nullable', 1, 3), (@q, N'dynamic',  0, 4);

-- B-15
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué modificador hace que un método pueda redefinirse en clases hijas?', 1, 1, @cs, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'static', 0, 1), (@q, N'virtual', 1, 2),
(@q, N'sealed', 0, 3), (@q, N'base',    0, 4);

-- B-16
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Cuál es la palabra clave que impide sobreescribir un método heredado?', 1, 2, @cs, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'abstract',               0, 1), (@q, N'override',              0, 2),
(@q, N'sealed (sobre métodos)', 1, 3), (@q, N'final',                 0, 4);

-- B-17
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué estructura se usa para asegurar ejecución de código independientemente de excepciones?', 1, 1, @cs, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'catch',   0, 1), (@q, N'using',   0, 2),
(@q, N'finally', 1, 3), (@q, N'ensure',  0, 4);

-- B-18
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué colección garantiza pares clave-valor con claves únicas?', 1, 1, @cs, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'List',                   0, 1), (@q, N'Queue',                  0, 2),
(@q, N'Dictionary<TKey,TValue>',1, 3), (@q, N'ArrayList',              0, 4);

-- B-19
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué palabra clave indica que un método no tiene implementación?', 1, 2, @cs, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'override', 0, 1), (@q, N'virtual',  0, 2),
(@q, N'abstract', 1, 3), (@q, N'sealed',   0, 4);

-- B-20
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Cuál es el tipo base de todos los tipos en C#?', 1, 1, @cs, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'System.ValueType', 0, 1), (@q, N'System.Base',   0, 2),
(@q, N'System.Object',    1, 3), (@q, N'System.Type',   0, 4);

-- B-21
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué tipo de proyecto usa el archivo Program.cs con top-level statements?', 1, 2, @cs, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'.NET Framework',      0, 1), (@q, N'Mono',              0, 2),
(@q, N'.NET 6+ Console apps',1, 3), (@q, N'ASP.NET MVC 4',    0, 4);

-- B-22
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué palabra clave impide modificar el valor de un campo después de inicializado?', 1, 1, @cs, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'const',                          0, 1),
(@q, N'readonly (en tiempo de ejecución)',1, 2),
(@q, N'fixed',                          0, 3),
(@q, N'immutable',                      0, 4);

-- B-23
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué método se ejecuta al destruir un objeto gestionado por el GC?', 1, 2, @cs, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Dispose',               0, 1), (@q, N'Finalize (destructor)', 1, 2),
(@q, N'Close',                 0, 3), (@q, N'Kill',                  0, 4);

-- B-24
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué tecnología se usa para comunicación asíncrona basada en tareas?', 1, 1, @cs, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'IEnumerable',         0, 1), (@q, N'Parallel',            0, 2),
(@q, N'async / await con Task',1,3), (@q, N'Thread.Abort',        0, 4);

-- B-25
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Cuál es el tipo más adecuado para representar cantidades monetarias?', 1, 1, @cs, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'float',   0, 1), (@q, N'double',  0, 2),
(@q, N'decimal', 1, 3), (@q, N'int',     0, 4);

-- B-26
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué interfaz permite consultar colecciones de forma diferida con LINQ?', 1, 2, @cs, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'IList',              0, 1), (@q, N'IQueryable',          0, 2),
(@q, N'IEnumerable (LINQ)', 1, 3), (@q, N'ICollection',         0, 4);

-- B-27
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué atributo se usa para personalizar la serialización JSON con System.Text.Json?', 1, 2, @cs, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'[Serializable]',       0, 1), (@q, N'[JsonContract]',       0, 2),
(@q, N'[JsonPropertyName]',   1, 3), (@q, N'[DataValue]',           0, 4);

-- B-28
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué tipo representa una colección que no se puede modificar?', 1, 2, @cs, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'List',                          0, 1), (@q, N'Array',                         0, 2),
(@q, N'IReadOnlyList / ImmutableList', 1, 3), (@q, N'Queue',                         0, 4);

-- B-29
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué método LINQ ordena elementos ascendentemente?', 1, 1, @cs, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Sort()',     0, 1), (@q, N'Organize()', 0, 2),
(@q, N'OrderBy()',  1, 3), (@q, N'ByAsc()',    0, 4);

-- B-30
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué tipo se usa para expresiones lambda convertidas en árboles de expresión?', 1, 3, @cs, 2, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Func',       0, 1), (@q, N'Action',     0, 2),
(@q, N'Expression', 1, 3), (@q, N'Lambda',     0, 4);

-- B-31
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué palabra clave permite declarar un método que debe implementarse en clases derivadas?', 1, 2, @cs, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'sealed',                      0, 1), (@q, N'override',                    0, 2),
(@q, N'abstract (en clases abstractas)',1,3),(@q, N'base',                        0, 4);

-- B-32
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué mecanismo permite inyectar dependencias en ASP.NET Core?', 1, 2, @api, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'WebForms Injector',                      0, 1),
(@q, N'Autofill',                               0, 2),
(@q, N'IServiceCollection / DI nativo de .NET Core', 1, 3),
(@q, N'System.Inject',                          0, 4);

-- B-33
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué objeto representa una operación asíncrona cancelable?', 1, 2, @cs, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'CancelSwitch',                         0, 1),
(@q, N'CancellationToken / CancellationTokenSource', 1, 2),
(@q, N'ThreadToken',                          0, 3),
(@q, N'AbortHandle',                          0, 4);

-- B-34
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué palabra clave evita la sobrescritura de un método heredado pero permite la herencia de la clase?', 1, 2, @cs, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'abstract',                                          0, 1),
(@q, N'override',                                          0, 2),
(@q, N'sealed (sobre métodos) / no virtuales por defecto', 1, 3),
(@q, N'readonly',                                          0, 4);

-- B-35
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué método LINQ obtiene el primer elemento que cumpla una condición?', 1, 1, @cs, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'FirstOrIgnore',         0, 1), (@q, N'TakeOne',                0, 2),
(@q, N'First() / FirstOrDefault()', 1, 3), (@q, N'One()', 0, 4);

-- B-36
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué estructura de datos garantiza orden FIFO (primero en entrar, primero en salir)?', 1, 1, @cs, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Stack',      0, 1), (@q, N'Queue',      1, 2),
(@q, N'Dictionary', 0, 3), (@q, N'Heap',       0, 4);

-- B-37
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué palabra clave permite definir una función anónima inline en C#?', 1, 1, @cs, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'fn',                          0, 1), (@q, N'anonymous',                   0, 2),
(@q, N'lambda => (expresiones lambda)', 1, 3), (@q, N'inline',                   0, 4);

-- B-38 (abierta)
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive, SampleAnswer)
VALUES (
    N'¿Qué hace esta sentencia de código?'
    + CHAR(13)+CHAR(10)
    + N'var lotFractionsWeights = weightResiduesLot.Where(w => w.IdFraction != null).GroupBy(w => w.IdFraction).ToList();',
    2, 3, @cs, 3, 1,
    N'Filtra los elementos de weightResiduesLot cuyo IdFraction no sea null, los agrupa por el valor de IdFraction y materializa el resultado en una List<IGrouping<TKey,T>>. Cada grupo contiene todos los elementos que comparten el mismo IdFraction.'
);

-- B-39 (abierta)
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive, SampleAnswer)
VALUES (
    N'¿Qué hace esta sentencia de código?'
    + CHAR(13)+CHAR(10)
    + N'weightResiduesLot.Where(w => w.IdFraction == (int)RequestFraction.FR3).ToList().ForEach(w2 => w2.IdFraction = (int)RequestFraction.FR3LAMP);',
    2, 3, @cs, 3, 1,
    N'Filtra los elementos cuyo IdFraction sea igual al valor entero de RequestFraction.FR3, los materializa en una lista y, por cada elemento, actualiza in-place su IdFraction al valor de RequestFraction.FR3LAMP. Al ser referencias a objetos, el cambio se refleja en la colección original.'
);

-- ============================================================
-- SECCIÓN FRONTEND (6 preguntas)
-- ============================================================

-- F-01
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué concepto es fundamental en JavaScript respecto al manejo del tiempo y la asincronía?', 1, 1, @fe, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Compilación previa',   0, 1), (@q, N'Tipado estático',      0, 2),
(@q, N'Event loop',           1, 3), (@q, N'Preprocesamiento',      0, 4);

-- F-02
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'En AngularJS, el Two-Way Data Binding implica:', 1, 1, @fe, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Que los componentes no pueden comunicarse',                        0, 1),
(@q, N'Que los datos viajan solo del modelo a la vista',                  0, 2),
(@q, N'Que la vista y el modelo se mantienen sincronizados automáticamente', 1, 3),
(@q, N'Que se necesita compilar cada cambio manualmente',                 0, 4);

-- F-03
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Cuál de estos NO es un framework JavaScript?', 1, 1, @fe, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'React',   0, 1), (@q, N'Vue',     0, 2),
(@q, N'Blazor',  1, 3), (@q, N'Angular', 0, 4);

-- F-04
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'En diseño UX/UI, el principio de consistencia indica que:', 1, 1, @fe, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Cada pantalla debe ser completamente diferente',                   0, 1),
(@q, N'El usuario debe recibir respuestas imprevisibles',                 0, 2),
(@q, N'Los elementos deben comportarse igual en todas las vistas',        1, 3),
(@q, N'La estética es más importante que la usabilidad',                  0, 4);

-- F-05
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'En Blazor WebAssembly, ¿dónde se ejecuta el código C#?', 1, 2, @fe, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Directamente sobre el servidor',              0, 1),
(@q, N'Sobre WebAssembly en el navegador',           1, 2),
(@q, N'Mediante transpilación a JavaScript',         0, 3),
(@q, N'Solo en aplicaciones móviles',                0, 4);

-- F-06
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué patrón de frontend se asocia normalmente a la gestión de estado global?', 1, 2, @fe, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Observer', 0, 1), (@q, N'Singleton', 0, 2),
(@q, N'Redux',    1, 3), (@q, N'Bridge',    0, 4);

-- ============================================================
-- SECCIÓN BASES DE DATOS (38 preguntas)
-- ============================================================

-- D-01
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Cuál de las siguientes sentencias SQL se usa para obtener datos?', 1, 1, @sql, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'UPDATE', 0, 1), (@q, N'INSERT', 0, 2),
(@q, N'SELECT', 1, 3), (@q, N'COMMIT', 0, 4);

-- D-02
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué característica distingue a una base de datos NoSQL?', 1, 2, @sql, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Uso obligatorio de esquema fijo',                          0, 1),
(@q, N'Almacenamiento distribuido y escalabilidad horizontal',    1, 2),
(@q, N'Dependencia exclusiva de tablas',                          0, 3),
(@q, N'Uso de transacciones ACID sin excepción',                  0, 4);

-- D-03
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'En Cosmos DB, el concepto de partition key sirve principalmente para:', 1, 3, @sql, 2, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Comprimir datos',                         0, 1),
(@q, N'Garantizar claves primarias',             0, 2),
(@q, N'Distribuir la carga y mejorar rendimiento',1, 3),
(@q, N'Generar triggers internos',               0, 4);

-- D-04
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'Entity Framework es un ejemplo de:', 1, 1, @sql, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'ORM',                          1, 1), (@q, N'Sistema gestor de colas', 0, 2),
(@q, N'Motor de renderizado',         0, 3), (@q, N'Servidor web',            0, 4);

-- D-05
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Cuál es la sentencia usada para crear una tabla en SQL Server?', 1, 1, @sql, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'ADD TABLE',          0, 1), (@q, N'NEW TABLE',          0, 2),
(@q, N'CREATE TABLE',       1, 3), (@q, N'INSERT INTO TABLE',  0, 4);

-- D-06
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué tipo de base de datos NoSQL se caracteriza por almacenar documentos en formato JSON?', 1, 2, @sql, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'SQL relacional',   0, 1), (@q, N'Graph Query',     0, 2),
(@q, N'Document Query',   1, 3), (@q, N'Hadoop Query',    0, 4);

-- D-07
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué operador SQL se utiliza para comparar un valor con un conjunto de resultados?', 1, 1, @sql, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'LIKE',    0, 1), (@q, N'IN',      1, 2),
(@q, N'BETWEEN', 0, 3), (@q, N'EXISTS',  0, 4);

-- D-08
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Cuál es la diferencia principal entre INNER JOIN y LEFT JOIN?', 1, 2, @sql, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'INNER JOIN incluye todas las filas de ambas tablas',                         0, 1),
(@q, N'LEFT JOIN incluye solo las coincidencias exactas',                           0, 2),
(@q, N'LEFT JOIN devuelve todas las filas de la tabla izquierda aunque no haya coincidencia', 1, 3),
(@q, N'INNER JOIN devuelve filas nulas cuando no hay coincidencia',                 0, 4);

-- D-09
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué función de SQL Server devuelve el número de filas afectadas por la última sentencia DML?', 1, 2, @sql, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'ROWCOUNT()',   0, 1), (@q, N'COUNT(*)',     0, 2),
(@q, N'@@ROWCOUNT',   1, 3), (@q, N'AFFECTED()',   0, 4);

-- D-10
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Cuál de las siguientes cláusulas se evalúa primero en una consulta SQL estándar?', 1, 2, @sql, 2, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'ORDER BY', 0, 1), (@q, N'HAVING',   0, 2),
(@q, N'GROUP BY', 0, 3), (@q, N'FROM',     1, 4);

-- D-11
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué comando SQL se usa para otorgar permisos a un usuario o rol?', 1, 1, @sql, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'ALLOW',  0, 1), (@q, N'PERMIT', 0, 2),
(@q, N'GRANT',  1, 3), (@q, N'ENABLE', 0, 4);

-- D-12
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué hace la hint "WITH (NOLOCK)" en SQL Server?', 1, 3, @sql, 2, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Impide leer datos sucios',                                                        0, 1),
(@q, N'Permite leer datos sin bloquear pero con riesgo de datos inconsistentes (dirty reads)', 1, 2),
(@q, N'Obliga a usar transacciones explícitas',                                          0, 3),
(@q, N'Aumenta el nivel de aislamiento',                                                 0, 4);

-- D-13
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Cuál es la diferencia entre DELETE y TRUNCATE?', 1, 2, @sql, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'DELETE elimina datos y TRUNCATE solo limpia el log',           0, 1),
(@q, N'TRUNCATE elimina fila a fila y DELETE es más rápido',          0, 2),
(@q, N'TRUNCATE borra toda la tabla y no permite condiciones WHERE',  1, 3),
(@q, N'Son exactamente iguales',                                      0, 4);

-- D-14
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué tipo de índice suele mejorar búsquedas basadas en varias columnas?', 1, 2, @sql, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Índice clustered',                    0, 1),
(@q, N'Índice compuesto (composite index)',  1, 2),
(@q, N'Índice único',                        0, 3),
(@q, N'Índice hash',                         0, 4);

-- D-15
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué tipo de relación se suele gestionar con una tabla intermedia (tabla puente)?', 1, 1, @sql, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Uno a uno',        0, 1), (@q, N'Uno a muchos',   0, 2),
(@q, N'Muchos a muchos',  1, 3), (@q, N'Jerárquica',     0, 4);

-- D-16
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué instrucción activa una transacción explícita en SQL Server?', 1, 1, @sql, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'BEGIN TRAN',        1, 1), (@q, N'START TX',          0, 2),
(@q, N'OPEN TRANSACTION',  0, 3), (@q, N'ENABLE TRANS',      0, 4);

-- D-17 (abierta — análisis de consulta real)
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive, SampleAnswer)
VALUES (
    N'Explica qué hace la siguiente consulta SQL:'
    + CHAR(13)+CHAR(10)
    + N'SELECT qH.Id as IdProcess, qR.Id_ref as refRequest, qP.Ref as RefProvider, '
    + N'CASE WHEN (qP.Ref LIKE ''OL027'' OR qP.Ref LIKE ''PTR022'') AND qh.DateChange < ''01-05-2025'' '
    + N'THEN ''Gestora de Datos Confidenciales, S.L. (Regeneración de Recursos - R&R)'' ELSE qP.RazonSocial END as nameProvider, '
    + N'qP.CIF as CifProvider '
    + N'FROM U_descDocumentLinesBreakdownPriceHistoricChangesPrice qH '
    + N'INNER JOIN U_descLogisticDocumentLines qLDL on qLDL.Id=qH.Id_LogisticDocumentLine '
    + N'INNER JOIN U_descLogisticDocument qLD on qLD.Id=qLDL.Id_LogisticDocument '
    + N'INNER JOIN U_descRequest qR on qR.Id=qLD.Id_Request '
    + N'INNER JOIN U_descContainerLog qCL on qCL.id=qLD.Id_ContainerLog '
    + N'INNER JOIN U_Proveedores qP on qP.id=qLD.Id_Provider',
    2, 3, @sql, 3, 1,
    N'Obtiene por cada registro de cambio de precio histórico: el ID del proceso, la referencia de solicitud, '
    + N'la referencia y CIF del proveedor, y el nombre del proveedor con una lógica CASE: si el proveedor es OL027 o PTR022 '
    + N'y la fecha de cambio es anterior al 01-05-2025 muestra "Gestora de Datos Confidenciales, S.L. (Regeneración de Recursos - R&R)"; '
    + N'en caso contrario muestra la razón social real. '
    + N'Encadena 6 tablas mediante INNER JOIN: historial de cambios → líneas de documento logístico → documento logístico → solicitud → registro de contenedor → proveedor.'
);

-- D-18
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué tipo de JOIN devuelve todas las filas de ambas tablas, coincidan o no?', 1, 2, @sql, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'INNER JOIN',      0, 1), (@q, N'LEFT JOIN',       0, 2),
(@q, N'RIGHT JOIN',      0, 3), (@q, N'FULL OUTER JOIN', 1, 4);

-- D-19
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué cláusula SQL permite filtrar resultados después de un GROUP BY?', 1, 1, @sql, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'WHERE',    0, 1), (@q, N'ORDER BY', 0, 2),
(@q, N'HAVING',   1, 3), (@q, N'LIMIT',    0, 4);

-- D-20
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué comando SQL crea un índice?', 1, 1, @sql, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'NEW INDEX',    0, 1), (@q, N'CREATE INDEX', 1, 2),
(@q, N'ADD INDEX',    0, 3), (@q, N'INSERT INDEX', 0, 4);

-- D-21
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué hace la sentencia TRUNCATE TABLE?', 1, 2, @sql, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Borra la tabla',                                              0, 1),
(@q, N'Elimina todas las filas sin registrar cada eliminación individual', 1, 2),
(@q, N'Modifica columnas',                                           0, 3),
(@q, N'Elimina solo filas duplicadas',                               0, 4);

-- D-22
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué es un CTE (Common Table Expression)?', 1, 2, @sql, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Una función agregada',                    0, 1),
(@q, N'Una vista permanente',                    0, 2),
(@q, N'Una consulta temporal definida con WITH', 1, 3),
(@q, N'Una tabla física',                        0, 4);

-- D-23
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué operador SQL une resultados de dos consultas eliminando duplicados?', 1, 2, @sql, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'UNION ALL', 0, 1), (@q, N'INTERSECT', 0, 2),
(@q, N'JOIN',      0, 3), (@q, N'UNION',     1, 4);

-- D-24
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué significa el acrónimo ACID en bases de datos?', 1, 2, @sql, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Automatic Consistency in Data',              0, 1),
(@q, N'Atomicity, Consistency, Isolation, Durability', 1, 2),
(@q, N'Automated Cache In Databases',               0, 3),
(@q, N'Advanced Control Integration Data',          0, 4);

-- D-25
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué instrucción devuelve el número de filas afectadas por la última operación (MySQL)?', 1, 2, @sql, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'GET ROWS',      0, 1), (@q, N'ROW_COUNT()', 1, 2),
(@q, N'RETURN COUNT',  0, 3), (@q, N'ROWS()',       0, 4);

-- D-26
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Cuál es el resultado principal de un CROSS JOIN?', 1, 2, @sql, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Valores únicos',       0, 1), (@q, N'Filas filtradas',     0, 2),
(@q, N'Producto cartesiano',  1, 3), (@q, N'Coincidencias exactas',0, 4);

-- D-27
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué comando SQL se usa para modificar la estructura de una tabla existente?', 1, 1, @sql, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'UPDATE TABLE', 0, 1), (@q, N'CHANGE',       0, 2),
(@q, N'ALTER TABLE',  1, 3), (@q, N'MODIFY',        0, 4);

-- D-28
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué es una subconsulta correlacionada?', 1, 3, @sql, 2, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Una subconsulta que se ejecuta antes que la principal',              0, 1),
(@q, N'Una consulta temporal',                                              0, 2),
(@q, N'Una subconsulta que depende de cada fila de la consulta externa',    1, 3),
(@q, N'Una unión de tablas',                                                0, 4);

-- D-29
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué tipo de índice mejora búsquedas sobre múltiples columnas en un orden específico?', 1, 2, @sql, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Hash index',                        0, 1),
(@q, N'Índice compuesto (composite index)',1, 2),
(@q, N'Spatial index',                     0, 3),
(@q, N'Reverse index',                     0, 4);

-- D-30
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Cuál es la función de COALESCE()?', 1, 2, @sql, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Crea índices',                               0, 1),
(@q, N'Elimina nulos',                              0, 2),
(@q, N'Devuelve el primer valor no nulo de una lista', 1, 3),
(@q, N'Convierte tipos',                            0, 4);

-- D-31
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué instrucción SQL permite obtener una fila por grupo basada en un ranking o condición específica?', 1, 3, @sql, 2, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'DISTINCT',                             0, 1),
(@q, N'FIRST',                                0, 2),
(@q, N'WINDOW FUNCTIONS (como ROW_NUMBER)',   1, 3),
(@q, N'UNIQUE',                               0, 4);

-- D-32
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué constraint garantiza que una columna no acepte valores duplicados?', 1, 1, @sql, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'FOREIGN KEY',  0, 1), (@q, N'PRIMARY KEY', 0, 2),
(@q, N'CHECK',        0, 3), (@q, N'UNIQUE',      1, 4);

-- D-33
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué característica define una transacción de base de datos?', 1, 2, @sql, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Es siempre instantánea',                                             0, 1),
(@q, N'No puede fallar',                                                    0, 2),
(@q, N'Es un conjunto de operaciones que se ejecutan como una unidad atómica', 1, 3),
(@q, N'Requiere bloqueos exclusivos siempre',                               0, 4);

-- D-34
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Cuál es el propósito del comando EXPLAIN en SQL?', 1, 2, @sql, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Ejecutar la consulta',                         0, 1),
(@q, N'Ver errores de sintaxis',                      0, 2),
(@q, N'Mostrar el plan de ejecución de una consulta', 1, 3),
(@q, N'Optimizar índices automáticamente',            0, 4);

-- D-35
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué tipo de índice es más eficiente para búsquedas de igualdad en motores como MySQL InnoDB?', 1, 3, @sql, 2, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Bitmap',   0, 1), (@q, N'B-Tree',  1, 2),
(@q, N'Fulltext', 0, 3), (@q, N'Spatial', 0, 4);

-- D-36
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Qué ocurre cuando se aplica ON DELETE CASCADE a una clave foránea?', 1, 2, @sql, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Se bloquea el borrado',                               0, 1),
(@q, N'No se permite insertar nulos',                        0, 2),
(@q, N'Las filas relacionadas se eliminan automáticamente',  1, 3),
(@q, N'El borrado se convierte en UPDATE',                   0, 4);

-- D-37
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Cuál de estas opciones representa correctamente una ventana (window frame) en SQL?', 1, 3, @sql, 2, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'SELECT * FROM window',                          0, 1),
(@q, N'AS RANGE',                                      0, 2),
(@q, N'ROWS BETWEEN 1 PRECEDING AND CURRENT ROW',     1, 3),
(@q, N'FILTER FRAME',                                  0, 4);

-- D-38 (abierta — identificar errores SQL)
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive, SampleAnswer)
VALUES (
    N'En la siguiente consulta SQL existen varios errores. Identifícalos:'
    + CHAR(13)+CHAR(10)
    + N'SELECT qI.id as idInvoice, case qR.TypeCRT when ''Lote'' then (select RefExternal from USR15_descCATShipmentPlant where Id_Request = qR.Id) '
    + N'else qR.TypeCRT end as RefCont, '
    + N'case when qR.TypeCRT = ''Lote'' then case '
    + N'WHEN (proveedores3.Ref LIKE ''OL027'' OR qP.Ref LIKE ''PTR022'') AND qI.Date < ''01-05-2025'' THEN ''Gestora de Datos Confidenciales, S.L.'' '
    + N'WHEN (proveedores3.Ref LIKE ''OL095'' OR qP.Ref LIKE ''PTR065'') AND qI.Date < ''02-19-2026'' THEN ''Reciclados Store, S.L'' '
    + N'ELSE proveedores3.RazonSocial end as ProviderName, '
    + N'FROM dbo.USR15_descLogisticDocument AS qLD '
    + N'INNER JOIN medida_unid as qMU '
    + N'INNER JOIN Proveedores AS qP',
    2, 3, @sql, 3, 1,
    N'Errores identificados: '
    + N'1) Coma trailing antes de FROM ("end as ProviderName,"). '
    + N'2) El CASE anidado para ProviderName le falta el END exterior del CASE cuando qR.TypeCRT != ''Lote''. '
    + N'3) "INNER JOIN medida_unid as qMU" sin cláusula ON. '
    + N'4) "INNER JOIN Proveedores AS qP" sin cláusula ON. '
    + N'5) La subconsulta escalar en el primer CASE puede devolver más de una fila si existen múltiples RefExternal para el mismo Id_Request (error en ejecución). '
    + N'6) El formato de fecha ''02-19-2026'' no es válido en SQL Server (el mes 19 no existe); debe usarse formato ISO ''2026-02-19''.'
);

-- ============================================================
-- SECCIÓN IECS (6 preguntas)
-- ============================================================

-- I-01 (abierta)
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive, SampleAnswer)
VALUES (
    N'Detalla los pasos a seguir para registrar un nuevo listado en iECS (para que aparezca en el árbol y se pueda visualizar su contenido).',
    2, 3, @iecs, 3, 1,
    N'Pasos generales: '
    + N'1) Crear la vista o consulta SQL que actuará como fuente de datos del listado. '
    + N'2) Ejecutar los scripts de registro numerados en base de datos: definición del componente COM, registro del listado en la tabla de menús/árbol, permisos de acceso. '
    + N'3) Registrar el componente en el iecsConfigurator.exe o los scripts de BD correspondientes. '
    + N'4) Asignar el listado al nodo del árbol correspondiente mediante la tabla de navegación de iECS. '
    + N'5) Configurar las columnas, filtros y procesos disponibles en el listado. '
    + N'6) Verificar en iECS que el nodo aparece en el árbol y que el listado muestra datos correctamente.'
);

-- I-02
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'¿Dónde se establece el tipo de autenticación que utiliza iECS 4?', 1, 1, @iecs, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'Se define a nivel de base de datos',                               0, 1),
(@q, N'Se puede configurar durante la instalación del paquete',           0, 2),
(@q, N'Es posible cambiar el tipo de autenticación en el iecssettings',   0, 3),
(@q, N'Estas opciones se cambian a nivel de aplicación en el iecsConfigurator.exe', 1, 4);

-- I-03
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'Al registrar una nueva ventana en iECS mediante scripts numerados, ¿a qué grupo pertenece el script del componente COM?', 1, 2, @iecs, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'10', 0, 1), (@q, N'20', 0, 2),
(@q, N'30', 1, 3), (@q, N'40', 0, 4);

-- I-04
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive)
VALUES (N'En un listado de vista genérica de iECS, ¿cuál es el tipo de proceso que permite lanzar una ventana sin tener que marcar una línea o checkbox?', 1, 2, @iecs, 1, 1);
SET @q = SCOPE_IDENTITY();
INSERT INTO dbo.Answers (QuestionId, Text, IsCorrect, [Order]) VALUES
(@q, N'CheckSelf',    0, 1), (@q, N'NoCheckSelf',  0, 2),
(@q, N'CheckBlank',   0, 3), (@q, N'NoCheckBlank', 1, 4);

-- I-05 (abierta — tarea encolada que no avanza)
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive, SampleAnswer)
VALUES (
    N'Se ha encolado una tarea programada en iECS y lleva un tiempo prolongado en estado "Pendiente" sin ejecutarse. ¿Qué puede estar ocurriendo?',
    2, 3, @iecs, 3, 1,
    N'Posibles causas: '
    + N'1) El servicio de ejecución de tareas programadas (iECS Task Manager / Scheduler Service) está detenido o no se está ejecutando en el servidor. '
    + N'2) No hay instancias del worker disponibles (todas ocupadas con otras tareas o con el número máximo de ejecuciones simultáneas alcanzado). '
    + N'3) La tarea tiene una dependencia de otra tarea previa que no ha finalizado. '
    + N'4) El servidor donde corre el motor de tareas perdió conectividad con la base de datos. '
    + N'5) El proceso de iECS encargado de procesar la cola no está arrancado.'
);

-- I-06 (abierta — error InvalidUserOrPassword en tarea)
INSERT INTO dbo.Questions (Text, Type, Difficulty, CategoryId, Points, IsActive, SampleAnswer)
VALUES (
    N'Se ha lanzado una tarea programada en iECS y en el detalle de ejecución aparece el error: "No se ha podido ejecutar con éxito la tarea programada. No se ha podido iniciar sesión; es posible que no se haya configurado el usuario para la ejecución de tareas programadas. [InvalidUserOrPassword]". ¿Qué puede estar ocurriendo?',
    2, 3, @iecs, 3, 1,
    N'El usuario configurado para la ejecución de tareas programadas no tiene credenciales válidas o no está configurado. '
    + N'Solución: acceder a la configuración del planificador de tareas de iECS (iecsConfigurator.exe o la sección correspondiente en la administración) '
    + N'y establecer un usuario de sistema válido con la contraseña correcta que tenga permisos para ejecutar tareas programadas. '
    + N'Verificar también que la cuenta no esté bloqueada ni caducada en Active Directory/SQL Server.'
);

GO

PRINT N'✓ Preguntas del examen IECSTRCEFNSTW25 insertadas correctamente.';
PRINT N'  Backend: 39 | Frontend: 6 | Bases de datos: 38 | iECS: 6 | Total: 89';
GO
