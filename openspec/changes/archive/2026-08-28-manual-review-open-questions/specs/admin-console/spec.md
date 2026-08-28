## ADDED Requirements

### Requirement: Cola de correcciones pendientes en la interfaz de administración
El sistema SHALL ofrecer en `/admin/results/pending` un listado de los resultados pendientes de corrección manual, ordenados del más antiguo al más reciente, mostrando por cada uno el candidato, la prueba, la fecha de envío, los días transcurridos y el número de respuestas abiertas por corregir. El acceso a la cola SHALL estar restringido a usuarios con rol `Admin`, igual que el resto de páginas de administración.

#### Scenario: Carga de la cola con resultados pendientes
- **GIVEN** un administrador que navega a `/admin/results/pending`
- **WHEN** la página termina de cargar la cola de correcciones
- **THEN** el sistema SHALL mostrar los resultados pendientes ordenados por antigüedad descendente de espera, cada uno con su recuento de respuestas por corregir y un acceso a la pantalla de corrección

#### Scenario: Cola vacía
- **GIVEN** un administrador en `/admin/results/pending`
- **WHEN** no existe ningún resultado pendiente de corrección
- **THEN** el sistema SHALL mostrar un estado vacío indicando que no hay correcciones pendientes, sin error

#### Scenario: Acceso visible desde el dashboard
- **GIVEN** un administrador en el dashboard con resultados pendientes de corrección
- **WHEN** la página carga las estadísticas
- **THEN** el sistema SHALL mostrar el número de correcciones pendientes con un acceso directo a `/admin/results/pending`

### Requirement: Pantalla de corrección de respuestas abiertas
El sistema SHALL ofrecer una pantalla de corrección que presente, para cada respuesta abierta pendiente del resultado, el enunciado de la pregunta, el texto entregado por el candidato, la respuesta de referencia (`SampleAnswer`) cuando exista, y un campo para otorgar puntos entre `0` y los puntos máximos de la pregunta, junto a un campo opcional de comentario. La pantalla SHALL impedir el envío mientras alguna respuesta carezca de puntuación o tenga un valor fuera de rango, y SHALL enviar la corrección de todas las respuestas en una única operación.

#### Scenario: Corrección completa de un resultado
- **GIVEN** un administrador en la pantalla de corrección de un resultado con dos respuestas abiertas pendientes
- **WHEN** otorga puntos válidos a ambas respuestas y confirma la corrección
- **THEN** el sistema SHALL enviar ambas puntuaciones en una única operación y, al recibir la confirmación, SHALL navegar de vuelta a la cola mostrando el resultado ya corregido

#### Scenario: Envío bloqueado con puntuación incompleta
- **GIVEN** un administrador en la pantalla de corrección con dos respuestas abiertas pendientes
- **WHEN** solo ha puntuado una de las dos
- **THEN** el sistema SHALL mantener deshabilitada la acción de confirmar la corrección e indicar qué respuestas faltan por puntuar

#### Scenario: Puntuación fuera de rango señalada en la interfaz
- **GIVEN** una respuesta abierta correspondiente a una pregunta de 3 puntos
- **WHEN** el administrador introduce un valor negativo o superior a 3
- **THEN** el sistema SHALL señalar el valor como inválido e impedir el envío de la corrección

#### Scenario: Resultado corregido por otro administrador entretanto
- **GIVEN** dos administradores con la misma pantalla de corrección abierta
- **WHEN** el segundo confirma la corrección después de que el primero ya la haya completado
- **THEN** el sistema SHALL mostrar un aviso de que el resultado ya fue corregido y SHALL refrescar la cola, sin aplicar una segunda corrección

## MODIFIED Requirements

### Requirement: Consulta del listado global de resultados
El sistema SHALL ofrecer en `/admin/results` un listado de todas las evaluaciones completadas, con indicadores agregados y filtros combinables. Los indicadores agregados de aprobados, reprobados y nota media SHALL calcularse únicamente sobre los resultados ya corregidos; los resultados pendientes de corrección SHALL mostrarse identificados como tales, sin nota ni veredicto, y contabilizarse en un indicador propio.

#### Scenario: Carga inicial de resultados con KPIs
- **GIVEN** un administrador que navega a `/admin/results`
- **WHEN** la página termina de cargar los resultados vía `GET /api/results`
- **THEN** el sistema SHALL mostrar el total de evaluaciones, el número y porcentaje de aprobados, el número de reprobados y la nota media calculados sobre los resultados corregidos, más el número de resultados pendientes de corrección

#### Scenario: Fila de un resultado pendiente de corrección
- **GIVEN** un administrador en `/admin/results` con al menos un resultado pendiente
- **WHEN** la página muestra ese resultado
- **THEN** el sistema SHALL mostrarlo con la etiqueta «Pendiente de corrección» en lugar de «Aprobado» o «Reprobado», y sin barra de progreso de puntuación

#### Scenario: Filtrado combinado de resultados
- **GIVEN** un administrador en `/admin/results` con resultados cargados
- **WHEN** combina filtros de texto (candidato/email), prueba, resultado (aprobado/reprobado/pendiente), rango de nota y/o rango de fechas
- **THEN** el sistema SHALL aplicar todos los filtros activos simultáneamente sobre el listado en memoria y actualizar el contador de "Mostrando X de Y resultados"

#### Scenario: Los resultados pendientes quedan fuera del filtro por rango de nota
- **GIVEN** un administrador que filtra por un rango de nota
- **WHEN** existen resultados pendientes de corrección
- **THEN** el sistema SHALL excluirlos del resultado del filtro, ya que no tienen nota definitiva que comparar

#### Scenario: Acceso al detalle de un resultado
- **GIVEN** un administrador viendo el listado de resultados
- **WHEN** pulsa "Ver detalle" sobre una fila
- **THEN** el sistema SHALL navegar a la vista de detalle de ese resultado (`/admin/results/{id}`)
