## Context

Ver `proposal.md` — Why para la motivación. Los requisitos están en `specs/`.

Lo que condiciona el enfoque:

- `TakeExam.razor`, en `OnInitializedAsync`, valida el enlace, guarda el JWT y llama **siempre** a `StartSessionAsync`. Solo después decide si muestra la bienvenida, que lee de esa sesión el título, el nombre, el número de preguntas y el tiempo límite. En el servidor, `StartSessionAsync` crea la `ExamSession` con `StartedAt = UtcNow`, marca el token como usado y fija la semilla del orden. El servidor ya hace lo que pide la spec; el cliente lo llama demasiado pronto.
- `ExamTokenValidationDto` es un record posicional: `IsValid, Error, SessionId, ExamTitle, CandidateName, AuthToken`. No lleva el número de preguntas ni el tiempo límite. `GetWithExamAndSessionAsync` ya carga las preguntas de la prueba, así que la validación puede contarlas sin otra consulta.
- `UserAnswers.OpenAnswer` es `NVARCHAR(4000)` en el guion, y `UserAnswerConfiguration` declara `HasMaxLength(4000)` con un literal. Nada valida la longitud de `SubmitAnswerDto.OpenAnswer`. Con un valor más largo, EF manda el parámetro sin tope y SQL Server rechaza la escritura. Sale como `DbUpdateException`, que el middleware traduce a `500`. En el envío ocurre dentro de la transacción, así que se revierte todo.
- `GetOrCreateStudentAsync` crea el alumno con `Username = email.Split('@')[0]`. `IX_Users_Username` es único y filtrado a `WHERE Username IS NOT NULL`, en el guion y en EF. El login acepta email o `Username`, pero el alumno no tiene contraseña utilizable. Las cuentas de personal que crea la gestión de usuarios ya nacen sin `Username`.

## Goals / Non-Goals

**Goals:**

- Que el arreglo de D8 no cambie el contrato de `start` ni el de la reanudación.
- Un solo sitio que diga cuál es el máximo de la respuesta abierta.
- Que el rechazo por longitud llegue antes de escribir nada, y que nunca impida cerrar una prueba fuera de plazo.

**Non-Goals:**

- Cambiar el esquema de la base de datos.
- Vaciar el `Username` de las cuentas de alumno que ya lo tienen.
- Los demás defectos del análisis (D11, D12, S8, E12), aunque toquen los mismos ficheros.

## Decisions

### D1. La bienvenida se pinta con la validación, y la sesión la pide el botón

`ExamTokenValidationDto` gana `int? QuestionCount = null, int? TimeLimitMinutes = null` al final. Con valores por defecto, las respuestas de error no cambian y siguen sin ellos. `ValidateTokenAsync` los rellena solo en la respuesta válida.

En `TakeExam.razor`:

- `OnInitializedAsync` valida y guarda el JWT. Si la validación no trae `SessionId`, muestra la bienvenida y **no** pide la sesión. Si la trae, la prueba ya estaba empezada: pide la sesión, que el servidor devuelve tal cual, y sigue como hoy, con el envío inmediato si no queda tiempo.
- «Comenzar prueba» llama a un método asíncrono que pide la sesión y, si llega, arranca la prueba. Una marca y el botón desactivado impiden el doble clic. Un segundo `start` no haría daño, porque el servidor devuelve la sesión abierta, pero arrancaría dos veces los relojes.
- Pedir la sesión y preparar los relojes, los borradores y las respuestas guardadas pasa a un método común a los dos caminos. Los relojes locales arrancan al llegar la respuesta de `start`, como hoy.
- Si la validación no trae el número de preguntas o el tiempo, la bienvenida omite esa línea en lugar de mostrarla vacía.

Alternativas descartadas:

- **Crear la sesión al abrir y fijar `StartedAt` al pulsar el botón**, con un endpoint nuevo. Introduce un estado nuevo, «sesión creada sin empezar», que tendrían que entender la validación, el portal, la reanudación y el envío. El defecto está solo en el orden de llamadas del cliente.
- **Mostrar la bienvenida con el reloj ya en marcha.** Es el defecto.

La validación es pública y tiene límite de ritmo. El número de preguntas y el tiempo límite ya los mostraba la bienvenida, así que publicarlos ahí no revela nada nuevo.

### D2. Un único máximo para la respuesta abierta

- `UserAnswer.OpenAnswerMaxLength = 4000`, junto a la propiedad. Es el tamaño de la columna. `UserAnswerConfiguration` lo usa en lugar del literal.
- `SubmitAnswerDto.OpenAnswerMaxLength` lo republica para la Web, que no debe leer de las entidades del dominio.
- `ExamTokenService` comprueba la longitud con un método privado que lanza `ValidationException` (`400`) con el máximo en el mensaje:
  - En `SaveDraftAnswerAsync`, después de la propiedad (`403`) y del plazo (`409`), que conservan su prioridad, y antes de leer o escribir.
  - En `SubmitExamAsync`, después de la comprobación de idempotencia y solo si el envío llega en plazo. Se comprueban todas las respuestas del cuerpo antes de abrir la transacción. Fuera de plazo el cuerpo se descarta, así que su longitud no importa.
- La longitud es `string.Length`, en unidades UTF-16. Es lo mismo que cuentan `NVARCHAR` y el `maxlength` del navegador.
- En la Web, el área de texto lleva `maxlength`, un contador «N / 4000» y un aviso al llegar al máximo. El navegador recorta lo pegado para que quepa.

Alternativas descartadas:

- **Pasar la columna a `NVARCHAR(MAX)`.** Exige un guion aditivo en cada base desplegada y deja el campo sin tope. 4000 caracteres son una página de texto. Si hace falta más, se sube el máximo en el dominio y en un guion.
- **Recortar en silencio en el servidor.** Pierde texto del candidato sin avisar. La interfaz ya impide pasarse, así que el servidor solo rechaza peticiones hechas a mano.
- **`413` o `422`.** El proyecto traduce `ValidationException` a `400` en un solo sitio, y no hay motivo para una excepción nueva.

### D3. El alumno nuevo nace sin `Username`

`GetOrCreateStudentAsync` deja de derivarlo. `User.Username` ya es `string?`, la columna admite nulos y el índice está filtrado: no hace falta tocar el esquema ni la configuración. El login sigue aceptando el `Username` de las cuentas que lo tienen.

Alternativas descartadas:

- **Sufijo numérico en caso de choque** (`maria.lopez2`). Inventa un identificador que nadie usa y necesita un bucle de reintentos contra el índice.
- **El email completo como `Username`.** Duplica `Email`, que ya es único.
- **Un guion que vacíe el `Username` de los alumnos actuales.** No hace falta para arreglar el defecto y cambia datos en las bases desplegadas.

## Risks / Trade-offs

- [Candidatos que ya abrieron su enlace antes del despliegue tienen la sesión creada y el reloj corriendo] → El arreglo evita las sesiones nuevas, no reescribe las que existen. Si alguno lo reclama, la salida es una invitación nueva.
- [Una Web nueva contra una API anterior recibe la validación sin número de preguntas ni tiempo] → La bienvenida omite esas líneas. Lo normal es desplegar las dos juntas.
- [La interfaz recorta lo pegado al máximo sin pedir confirmación] → El contador y el aviso lo dejan a la vista, y el candidato conserva el texto original donde lo copió.
- [Otros campos de texto libre tampoco validan su longitud en el servidor, como `ReviewerComment`] → Fuera de alcance. Su pantalla ya limita con `maxlength`.

## Migration Plan

- Sin cambios de esquema ni de datos.
- Desplegar la API y la Web juntas.
- Para volver atrás, revertir las dos. La API anterior es compatible con la Web anterior, y la nueva también, porque los campos nuevos de la validación son opcionales.
