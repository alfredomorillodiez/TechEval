## 1. Unidad de trabajo

- [x] 1.1 Declarar `IUnitOfWork` en `src/TechEval.Domain/Interfaces/Repositories`, con un único método que ejecute una operación dentro de una transacción. Verificar que compila y que no expone objetos de transacción al llamante.
- [x] 1.2 Implementarlo en `src/TechEval.Infrastructure` sobre `AppDbContext.Database.BeginTransactionAsync`, confirmando al terminar y revirtiendo ante cualquier excepción. Verificar que la excepción se relanza y no se traga.
- [x] 1.3 Registrarlo en `DependencyInjection.AddInfrastructure` con el mismo alcance que los repositorios. Verificar que `dotnet build` pasa sin avisos.

## 2. Corrección de abiertas

- [x] 2.1 Inyectar `IUnitOfWork` en `OpenQuestionReviewService` y envolver en la transacción la escritura de las `UserAnswer` y el cierre del `ExamResult`. Verificar que la validación sigue ocurriendo antes de abrir la transacción.
- [x] 2.2 Comprobar que el envío de correo queda fuera de la transacción, después de confirmar. Verificar que el orden es escribir, confirmar y luego notificar.

## 3. Envío de la prueba

- [x] 3.1 Inyectar `IUnitOfWork` en `ExamTokenService` y envolver en la transacción las `UserAnswer`, el `ExamResult` y el cierre de la `ExamSession`. Verificar que la salida anticipada por envío repetido queda fuera y antes de la transacción.
- [x] 3.2 Comprobar que el correo se envía tras confirmar, tanto en el camino con resultado como en el pendiente de corrección. Verificar leyendo el método.

## 4. Montajes de prueba

- [x] 4.1 Añadir el doble de `IUnitOfWork` a `ExamSubmissionTests`, `ExamResubmissionTests`, `ExamTokenServiceTests` y `OpenQuestionReviewServiceTests`, configurado para ejecutar la operación que recibe. Verificar que las 70 pruebas anteriores vuelven a pasar sin cambiar ninguna aserción.

## 5. Pruebas nuevas

- [x] 5.1 Añadir la prueba de que la corrección se ejecuta dentro de la transacción, y no fuera. Verificar que pasa.
- [x] 5.2 Añadir la prueba de que un fallo al cerrar el resultado revierte las puntuaciones ya escritas. Verificar que ninguna queda persistida.
- [x] 5.3 Añadir la prueba de que el envío de la prueba se ejecuta dentro de la transacción. Verificar que pasa.
- [x] 5.4 Añadir la prueba de que un fallo al escribir el resultado revierte las respuestas del envío. Verificar que pasa.
- [x] 5.5 Añadir la prueba de que el correo se envía fuera de la transacción, ya confirmada. Verificar que un fallo de correo no revierte nada.
- [x] 5.6 Ejecutar `dotnet test TechEval.sln` completo. Verificar que las 70 pruebas anteriores siguen en verde junto a las nuevas.

## 6. Cierre

- [x] 6.1 Reconstruir la solución completa antes de arrancar la API, por lo dicho en el hallazgo E8. Verificar que el binario es el nuevo.
- [x] 6.2 Enviar una prueba completa contra la API real y comprobar que el resultado, las respuestas y el cierre de sesión quedan escritos. Verificar con una consulta a la base de datos.
- [x] 6.3 Corregir un resultado con preguntas abiertas contra la API real. Verificar que las puntuaciones y el cierre quedan escritos juntos.
