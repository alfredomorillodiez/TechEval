## Context

Ver `proposal.md` — Why para la motivación.

Lo que condiciona el enfoque:

- `ResultService.GetDashboardStatsAsync` compone las cifras a partir de dos listas completas que le devuelven los repositorios.
- `IRepository<T>` ya tiene `CountAsync(predicado)`, que sí se traduce a SQL. El dashboard lo usa para las preguntas activas, y es el patrón a extender.
- `GetAllWithDetailsAsync` y `GetWithStatsAsync` los usan también otras pantallas, así que no se pueden cambiar sin mirar a quién afectan.
- Las pruebas de `ResultServiceDashboardTests` afirman sobre las cifras, con dobles de repositorio.

## Goals / Non-Goals

**Goals:**

- Que el coste del dashboard crezca con lo que muestra, no con el histórico.
- Que las cifras salgan exactamente iguales.

**Non-Goals:**

- No se cambia el cuerpo de la respuesta.
- No se toca la paginación de los listados. Eso es `R1`.
- No se tocan `GetAllWithDetailsAsync` ni `GetWithStatsAsync`, que otras pantallas siguen necesitando tal cual.

## Decisions

### Métodos nuevos en el repositorio, en vez de cambiar los que hay

`GetAllWithDetailsAsync` sirve al listado de resultados, que sí necesita las filas. `GetWithStatsAsync` sirve al listado de exámenes, que sí necesita los conteos por examen. Cambiarlos para servir al dashboard estropearía a sus usuarios actuales.

Se añaden métodos con el propósito escrito en el nombre: contar activos, agregar el periodo, traer los recientes.

**Alternativa descartada**: dejar que el servicio componga `IQueryable` y ejecute la agregación él. Sería menos código, pero mete EF en la capa de aplicación, que es justo lo que la separación en capas evita.

### Una sola consulta para las tres métricas del mes

Cantidad, promedio y tasa de aprobación salen del mismo conjunto de filas. Pedirlas por separado serían tres recorridos sobre el mismo filtro.

Se agrupan en una proyección que devuelve los cuatro números que hacen falta: cuántos resultados hay en el mes, cuántos están corregidos, la suma de sus puntuaciones y cuántos aprobaron. El servicio hace la división, que es aritmética y no pertenece a la base de datos.

**Por qué la suma y no el promedio**: `AVG` sobre un conjunto vacío devuelve `NULL`, y el redondeo del promedio debe hacerse una sola vez, al final. Devolver suma y cuenta deja el caso vacío sin ambigüedad.

### El filtro del mes se calcula una vez y viaja como rango

El código actual compara año y mes contra `DateTime.UtcNow` dentro del filtro. Eso no se traduce a un índice.

El servicio calcula el primer día del mes y el primero del siguiente, y el repositorio filtra con un rango `>= desde` y `< hasta`. Un rango sí puede usar el índice de `CompletedAt`, que ya existe.

**Efecto secundario deseable**: la frontera del mes deja de depender de cuántas veces se lea el reloj durante la consulta.

## Risks / Trade-offs

**Las cifras podrían salir distintas por un descuido** → Es el riesgo principal de este cambio. Mitigación: las pruebas existentes del dashboard no se tocan y son el contrato; se comprueba además contra la API real que el cuerpo es el mismo antes y después.

**Tres consultas donde antes había dos** → Son tres consultas baratas frente a dos caras. El número de idas y vueltas sube; el trabajo baja mucho.

**Los dobles de las pruebas hay que ampliarlos** → Los métodos nuevos necesitan su configuración en el fixture. Es trabajo mecánico, y no cambia lo que las pruebas afirman.

## Migration Plan

Sin cambios de esquema. Sin cambios de API. Despliegue normal.

**Vuelta atrás**: revertir el commit.
