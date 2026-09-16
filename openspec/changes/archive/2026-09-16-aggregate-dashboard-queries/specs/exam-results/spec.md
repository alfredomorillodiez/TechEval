## MODIFIED Requirements

### Requirement: Dashboard de estadísticas para administradores
El sistema SHALL exponer a usuarios con rol `Admin` un endpoint de dashboard (`GET /api/results/dashboard`) que devuelva el número de exámenes activos, el número de preguntas activas, la cantidad de resultados registrados en el mes en curso, el promedio de `ScorePercentage` del mes en curso, la tasa de aprobación del mes en curso, el número total de resultados pendientes de corrección, y los últimos 10 resultados registrados (ordenados por fecha de finalización descendente). El promedio de puntuación y la tasa de aprobación SHALL calcularse únicamente sobre resultados con `Status = Reviewed`, de forma que las puntuaciones parciales de los resultados pendientes no distorsionen las métricas.

Cada cifra SHALL obtenerse con una consulta agregada en la base de datos. El sistema SHALL NOT traer a memoria el conjunto completo de resultados ni el árbol completo de exámenes para contar, filtrar o promediar: el coste de la pantalla debe crecer con lo que muestra, no con el histórico acumulado.

#### Scenario: Consulta del dashboard con resultados en el mes actual
- **WHEN** un administrador solicita `GET /api/results/dashboard` y existen resultados completados dentro del mes calendario en curso
- **THEN** el sistema responde con el promedio de puntuación y la tasa de aprobación calculados únicamente sobre los resultados `Reviewed` de ese mes, junto con los conteos de exámenes/preguntas activos, el número de pendientes de corrección y los 10 resultados más recientes

#### Scenario: Consulta del dashboard sin resultados en el mes actual
- **WHEN** un administrador solicita `GET /api/results/dashboard` y no existe ningún resultado completado dentro del mes en curso
- **THEN** el sistema responde con promedio de puntuación y tasa de aprobación en `0` para el mes, sin fallar la consulta

#### Scenario: Los resultados pendientes no distorsionan las métricas
- **GIVEN** un mes con dos resultados `Reviewed` al 80 % y un resultado `PendingReview` con una puntuación parcial del 20 %
- **WHEN** un administrador solicita el dashboard
- **THEN** el promedio del mes es 80 % y la tasa de aprobación se calcula sobre los dos resultados corregidos, y el resultado pendiente se refleja únicamente en el contador de pendientes de corrección

#### Scenario: El coste no crece con el histórico
- **GIVEN** un histórico con muchos más resultados de los que el mes en curso contiene
- **WHEN** un administrador solicita el dashboard
- **THEN** el sistema SHALL pedir a la base de datos únicamente las cifras agregadas y las diez filas recientes
- **AND** el sistema SHALL NOT materializar los resultados de meses anteriores

#### Scenario: Contar exámenes activos no trae su contenido
- **WHEN** el sistema calcula el número de exámenes activos
- **THEN** SHALL contarlos en la base de datos
- **AND** SHALL NOT cargar sus preguntas, invitaciones, sesiones ni resultados, que son inclusiones cuyas uniones multiplican filas
