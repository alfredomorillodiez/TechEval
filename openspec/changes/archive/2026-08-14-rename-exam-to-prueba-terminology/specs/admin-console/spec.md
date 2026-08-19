## MODIFIED Requirements

### Requirement: Acceso restringido a las páginas de administración
El sistema SHALL impedir el acceso a cualquier página bajo `/admin` cuando no exista una sesión autenticada válida, y SHALL ocultar la barra de navegación lateral en ausencia de sesión.

#### Scenario: Acceso sin sesión a una página protegida
- **GIVEN** un visitante sin token almacenado en `localStorage`
- **WHEN** navega directamente a `/admin`, `/admin/questions`, `/admin/pruebas` o `/admin/results`
- **THEN** el sistema MUST redirigir a `/login` antes de cargar los datos de la página

#### Scenario: Sidebar visible solo con sesión activa
- **GIVEN** el layout principal de la aplicación (`MainLayout`)
- **WHEN** `AuthStateService.IsAuthenticated` es verdadero
- **THEN** el sistema SHALL mostrar la barra lateral con enlaces a Dashboard, Preguntas, Pruebas y Resultados, el nombre del usuario conectado y el botón de cerrar sesión

#### Scenario: Cierre de sesión
- **GIVEN** un administrador autenticado en cualquier página de `/admin`
- **WHEN** pulsa "Cerrar sesión" en la barra lateral
- **THEN** el sistema SHALL limpiar el token en memoria y en `localStorage` (`AuthStateService.LogoutAsync`) y navegar a `/login`

### Requirement: Creación y generación de exámenes desde la interfaz
El sistema SHALL permitir al administrador crear una prueba manualmente o generarla automáticamente a partir de criterios de selección de preguntas, desde `/admin/pruebas`.

#### Scenario: Acceso a las dos vías de creación
- **GIVEN** un administrador en `/admin/pruebas`
- **WHEN** consulta la cabecera del listado
- **THEN** el sistema SHALL ofrecer un enlace a creación manual (`/admin/pruebas/new`) y otro a generación automática (`/admin/pruebas/generate`)

#### Scenario: Generación automática exitosa
- **GIVEN** un administrador en `/admin/pruebas/generate` con título, número de preguntas, tiempo límite y nota mínima de aprobación completados
- **WHEN** opcionalmente selecciona categorías y/o un nivel de dificultad, y confirma "Generar prueba"
- **THEN** el sistema llama a `POST /api/exams/generate` con esos criterios y, si la API devuelve la prueba creada, navega a `/admin/pruebas`

#### Scenario: Generación automática sin preguntas suficientes
- **GIVEN** un administrador en `/admin/pruebas/generate` con filtros de categoría y/o dificultad demasiado restrictivos
- **WHEN** confirma "Generar prueba" y la API no puede satisfacer el número de preguntas solicitado
- **THEN** el sistema MUST mostrar el mensaje "No hay suficientes preguntas con los filtros seleccionados. Prueba cambiando los filtros." sin navegar fuera de la página

#### Scenario: Validación de título obligatorio en generación automática
- **GIVEN** un administrador en `/admin/pruebas/generate`
- **WHEN** intenta generar la prueba sin haber introducido un título
- **THEN** el sistema MUST mostrar el mensaje "El título es obligatorio." sin llamar a la API

### Requirement: Envío de examen a candidatos desde el modal del listado
El sistema SHALL permitir enviar una prueba existente a uno o varios candidatos mediante un modal accesible desde el listado de pruebas, con modo individual y modo masivo.

#### Scenario: Envío individual exitoso
- **GIVEN** un administrador que abre el modal de envío de una prueba en modo "Individual"
- **WHEN** completa nombre, email y horas de expiración del candidato, y confirma "Enviar"
- **THEN** el sistema llama a `POST /api/exams/send` y, si la API devuelve un token, SHALL mostrar el mensaje de confirmación "Prueba enviada correctamente a {email}" dentro del propio modal

#### Scenario: Envío masivo mediante importación de CSV
- **GIVEN** un administrador en el modal de envío en modo "Masivo" con la pestaña "Pegar CSV" activa
- **WHEN** pega líneas con formato `Nombre,Email` y pulsa "Importar", y luego confirma el envío
- **THEN** el sistema SHALL poblar la lista de candidatos a partir de las líneas válidas, llamar a `POST /api/exams/send-bulk` con esa lista y la expiración indicada, y mostrar el recuento de envíos correctos y fallidos devuelto por la API

#### Scenario: Envío masivo sin candidatos añadidos
- **GIVEN** un administrador en el modal de envío en modo "Masivo" sin ningún candidato en la lista
- **WHEN** intenta confirmar el envío
- **THEN** el sistema MUST impedir la llamada a la API y mostrar el mensaje "Añade al menos un candidato antes de enviar."

#### Scenario: Cierre del modal tras un envío
- **GIVEN** un modal de envío que ya muestra un resultado (individual exitoso o resumen masivo)
- **WHEN** el administrador pulsa "Cerrar"
- **THEN** el sistema SHALL cerrar el modal y restablecer su estado para una próxima apertura

### Requirement: Consulta del listado global de resultados
El sistema SHALL ofrecer en `/admin/results` un listado de todas las evaluaciones completadas, con indicadores agregados y filtros combinables.

#### Scenario: Carga inicial de resultados con KPIs
- **GIVEN** un administrador que navega a `/admin/results`
- **WHEN** la página termina de cargar los resultados vía `GET /api/results`
- **THEN** el sistema SHALL mostrar el total de evaluaciones, el número y porcentaje de aprobados, el número de reprobados y la nota media calculados sobre el conjunto completo

#### Scenario: Filtrado combinado de resultados
- **GIVEN** un administrador en `/admin/results` con resultados cargados
- **WHEN** combina filtros de texto (candidato/email), prueba, resultado (aprobado/reprobado), rango de nota y/o rango de fechas
- **THEN** el sistema SHALL aplicar todos los filtros activos simultáneamente sobre el listado en memoria y actualizar el contador de "Mostrando X de Y resultados"

#### Scenario: Acceso al detalle de un resultado
- **GIVEN** un administrador viendo el listado de resultados
- **WHEN** pulsa "Ver detalle" sobre una fila
- **THEN** el sistema SHALL navegar a la vista de detalle de ese resultado (`/admin/results/{id}`)
