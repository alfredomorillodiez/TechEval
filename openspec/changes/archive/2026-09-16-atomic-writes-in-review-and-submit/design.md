## Context

Ver `proposal.md` — Why para la motivación.

Lo que condiciona el enfoque:

- `BaseRepository.AddAsync`, `UpdateAsync` y `DeleteAsync` llaman a `SaveChangesAsync`. Cada operación confirma sola.
- `AppDbContext` se registra con `AddDbContext`, que es `Scoped`. Los repositorios también son `Scoped`. Dentro de una petición, **todos comparten la misma instancia de contexto**. Ese detalle es lo que hace viable la solución barata.
- `SubmitReviewAsync` hace N+1 confirmaciones; `SubmitExamAsync`, N+2.
- Los arreglos de D1 y D2 ya conviven con la ventana: ambos tratan «sesión con `ExamResult`» como terminada, en dos sitios distintos.

## Goals / Non-Goals

**Goals:**

- Que la corrección cumpla lo que su spec afirma, también ante un fallo a mitad.
- Que el envío no pueda dejar una sesión con resultado y sin cerrar.
- Que el alcance quepa en dos métodos, no en toda la aplicación.

**Non-Goals:**

- No se retira el `SaveChanges` de los repositorios. Es la causa de fondo y su propio hallazgo.
- No se convierten en transaccionales otras operaciones. Las demás escriben una sola entidad.
- No se retiran las comprobaciones que D1 y D2 introdujeron para tolerar la ventana.

## Decisions

### Transacción explícita, no retirada del `SaveChanges`

La solución de manual es quitar `SaveChangesAsync` de los repositorios y exponer una unidad de trabajo que confirme al final. Es lo correcto a largo plazo y **no es lo que hace este cambio**.

El motivo es el riesgo. Ese refactor obliga a revisar cada servicio y cada ruta de escritura de la aplicación. Un solo sitio donde nadie llame a `SaveChangesAsync` deja de guardar, en silencio y sin error. El coste del fallo es una escritura perdida en producción, y no hay pruebas de integración que lo atrapen.

La transacción explícita da la garantía que D4 pide con un alcance de dos métodos. Los `SaveChanges` intermedios siguen ahí, pero dentro de una transacción abierta sobre el mismo contexto participan de ella: EF los envía sin confirmar hasta el `Commit`.

El precio: sigue habiendo N viajes a la base de datos donde bastaría uno. Es rendimiento, no corrección, y estas dos operaciones son poco frecuentes.

### La unidad de trabajo recibe la operación, no devuelve la transacción

```
Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken ct);
```

**Alternativa descartada**: devolver un objeto de transacción con `Commit` y `Rollback`. Es más flexible y se presta a olvidarse del `Commit`, que es exactamente el fallo silencioso que este cambio quiere evitar. Con la operación como parámetro, no hay forma de escribir mal el caso normal: si la función termina, se confirma; si lanza, se revierte.

Los valores que el servicio necesita después —el resultado, el porcentaje— se capturan en variables locales desde el cierre. Funciona y evita una segunda interfaz.

### El correo va fuera de la transacción

En los dos servicios, el correo se envía tras confirmar. Una transacción abierta mientras se espera al SMTP mantiene filas bloqueadas durante segundos, y un servidor de correo lento se convierte en un problema de base de datos.

La consecuencia es que un correo puede fallar con la escritura ya confirmada. Es lo correcto y ya estaba decidido: el spec de la corrección dice que el fallo del correo no revierte nada.

### `IUnitOfWork` vive junto a los repositorios

Se declara en `Domain/Interfaces/Repositories` y se implementa en Infrastructure, igual que `IRepository<T>`. No introduce un patrón nuevo en el proyecto: ocupa el sitio que ya tenía reservado.

## Risks / Trade-offs

**El proveedor en memoria de EF no admite transacciones** → Las pruebas de repositorio que usan `UseInMemoryDatabase` lanzarían al abrir una. Mitigación: ninguna de ellas pasa por los dos servicios afectados, que se prueban con dobles. Si más adelante se quiere una prueba de integración real, hará falta SQL Server en contenedor, que es el hallazgo de pruebas de recorrido.

**Cuatro montajes de prueba reciben un doble más** → Es ruido mecánico, pero también la señal de que el constructor cambió. Sin ese ruido, un servicio podría quedarse sin transacción sin que nadie lo note.

**La transacción no cubre lo que ocurra fuera de la petición** → Si el proceso muere entre el `Commit` y el `return`, el cliente no recibe respuesta pero los datos están bien. Es el comportamiento deseado: el reenvío lo resuelve el cambio de idempotencia.

**Las comprobaciones de «sesión con resultado» quedan como redundancia** → Dejan de ser imprescindibles para los datos nuevos, pero siguen protegiendo de las sesiones a medio cerrar que ya existan en producción. Retirarlas ahora sería cambiar el comportamiento sobre datos que no se han limpiado.

## Migration Plan

Sin cambios de esquema. Sin script SQL.

Las sesiones a medio cerrar que ya existan en producción no se arreglan solas: siguen ahí, y las siguen cubriendo las comprobaciones de D1 y D2. La consulta para localizarlas está en las notas de despliegue de `resume-exam-in-progress`.
