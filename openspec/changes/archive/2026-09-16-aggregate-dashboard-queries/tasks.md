## 1. Consultas agregadas

- [x] 1.1 Añadir a `IExamRepository` el recuento de exámenes activos. Verificar que no incluye ninguna navegación.
- [x] 1.2 Añadir a `IExamResultRepository` la agregación del periodo: cuántos, cuántos corregidos, suma de puntuaciones y cuántos aprobaron. Verificar que filtra por rango de fechas.
- [x] 1.3 Añadir a `IExamResultRepository` el recuento de pendientes de corrección. Verificar que es un `COUNT`.
- [x] 1.4 Añadir a `IExamResultRepository` los resultados recientes con un límite. Verificar que ordena en la base de datos.

## 2. El servicio compone

- [x] 2.1 Reescribir `GetDashboardStatsAsync` con los métodos nuevos. Verificar que ya no llama a `GetAllWithDetailsAsync` ni a `GetWithStatsAsync`.
- [x] 2.2 Calcular el rango del mes una sola vez en el servicio. Verificar que el repositorio recibe dos fechas, no un año y un mes.
- [x] 2.3 Comprobar que el caso sin resultados en el mes devuelve ceros y no falla. Verificar con su prueba.

## 3. Pruebas

- [x] 3.1 Ampliar el doble de `ResultServiceDashboardTests` con los métodos nuevos, **sin cambiar lo que las pruebas afirman**. Verificar que siguen pasando.
- [x] 3.2 Añadir una prueba de que el servicio no pide la lista completa de resultados. Verificar que falla si se vuelve al método antiguo.
- [x] 3.3 Ejecutar `dotnet test` completo. Verificar que sigue en verde.

## 4. Cierre

- [x] 4.1 Comprobar contra la API real que el cuerpo del dashboard es idéntico antes y después. Verificar comparando las dos respuestas.
