# aggregate-dashboard-queries

Que el dashboard pida a la base de datos seis números y diez filas, en vez de traérselo todo para contarlo en memoria

## Why

Hallazgo `R2`. El dashboard necesita seis cifras y una lista de diez, y para conseguirlas trae de la base de datos todo lo que hay.

`GetDashboardStatsAsync` hace dos consultas caras:

- **Todos los resultados**, cada uno con su examen, para después filtrar el mes en curso con LINQ en memoria. El coste crece con el histórico completo, no con lo que la pantalla muestra.
- **Todos los exámenes** con sus preguntas, sus invitaciones, las sesiones de esas invitaciones y los resultados de esas sesiones. Todo ese árbol, para acabar contando cuántos exámenes tienen `IsActive = true`. EF resuelve esas inclusiones con uniones que multiplican filas: un examen con veinte preguntas y treinta invitaciones vuelve como cientos de filas.

Hoy no duele porque hay pocos datos. Con un año de uso, la pantalla más consultada de la aplicación es la más cara.

## What Changes

- El recuento de exámenes activos pasa a ser un `COUNT` en la base de datos.
- Las métricas del mes —cantidad, promedio y tasa de aprobación— pasan a una sola consulta agregada, filtrada por fecha en SQL.
- El recuento de pendientes de corrección pasa a ser un `COUNT`.
- Los diez resultados recientes pasan a un `ORDER BY` con `TOP`.

## Capabilities

- `exam-results` — el cálculo de las métricas del dashboard

## Impact

**Sin cambios de API.** El endpoint devuelve exactamente el mismo cuerpo. La diferencia está en lo que se pide a la base de datos.

**Sin cambios de esquema.**

**Las cifras deben salir idénticas.** Es un cambio de rendimiento, no de significado: el mismo promedio, la misma tasa, los mismos diez resultados. Las pruebas existentes del dashboard son el contrato, y no se tocan.
