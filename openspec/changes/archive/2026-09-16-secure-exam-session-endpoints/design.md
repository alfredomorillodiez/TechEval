## Context

Ver `proposal.md` — Why para la motivación.

Lo que condiciona el enfoque:

- `ValidateTokenAsync` ya emite un JWT con rol `Alumno` y `NameIdentifier` igual al `User.Id`, y ya resuelve y guarda `ExamToken.UserId`. Las dos mitades de la comprobación existen y nadie las junta.
- `TakeExam.razor` guarda esa sesión con `Auth.LoginAsync` **antes** de llamar a `start`, y `ApiService` pone la cabecera `Authorization` en su `HttpClient`. El cliente ya manda el token en todas las llamadas posteriores; simplemente nadie lo exige.
- `SaveDraftAnswerAsync` recibe solo el `sessionId`. No tiene por dónde llegar al dueño ni al plazo.
- `SubmitExamAsync` ya carga el `ExamToken` y el `Exam`, así que ahí las dos comprobaciones no cuestan ninguna consulta extra.
- El cambio que arregló la reanudación ya calcula el tiempo restante en el servidor, pero solo para **mostrarlo**.

## Goals / Non-Goals

**Goals:**

- Que un identificador de sesión adivinado no sirva para escribir en el examen de nadie.
- Que el plazo del examen lo decida el servidor y no el reloj del navegador.
- Que un candidato que vuelve tarde no pierda lo que ya tenía guardado.

**Non-Goals:**

- No se limita el número de intentos sobre los endpoints públicos.
- No se cierran las sesiones abandonadas, que siguen abiertas indefinidamente.
- No se cambia nada del cliente: ya manda el token.

## Decisions

### Fuera de plazo se rechaza el guardado, pero el envío se acepta vacío

Es la decisión de fondo de este cambio y merece explicación.

El ataque es sencillo: congelar el temporizador del navegador y seguir trabajando. Lo que lo hace posible no es el envío tardío, sino poder **escribir respuestas** después del plazo. Por eso `answer` se cierra a cal y canto.

Pero cerrar también el envío castigaría al candidato honrado. El caso es real y frecuente: se le cae la conexión en el minuto 59 y vuelve en el 61. Con la reanudación entra, ve cero segundos y el cliente envía de inmediato. Si el servidor rechazase ese envío, perdería el examen entero por un corte de red — un daño mucho peor que el fraude que se quiere evitar.

La salida es aceptar el envío e **ignorar lo que traiga**: el examen se cierra y se puntúa con lo que hubiera guardado en plazo. El candidato honrado conserva su trabajo; el que trabajó de más no gana nada, porque sus respuestas tardías nunca llegaron a escribirse.

**Alternativa descartada**: rechazar el envío tardío con `409`. Deja la sesión abierta para siempre y al candidato sin recurso, salvo que exista un proceso de cierre de abandonadas, que no existe.

**Alternativa descartada**: aceptar el envío tardío con su contenido y fiarlo todo al bloqueo de `answer`. No sirve: el envío escribe la respuesta de cada pregunta, así que un atacante se salta `answer` por completo y manda todo al final.

### El margen de gracia es de 60 segundos

El auto-envío del temporizador dispara en el cero exacto del cliente, y la petición tarda en llegar. Sin margen, un envío legítimo cae del lado malo por unos milisegundos de red.

Sesenta segundos son holgados para cualquier latencia real y despreciables como ventana de fraude: nadie responde una pregunta de más en un minuto que además tiene que compartir con la red.

### Una sesión sin dueño es una sesión ajena

`ExamToken.UserId` se rellena al validar la invitación. Una sesión cuyo token no lo tiene solo puede venir de un camino que se saltó la validación.

Podría tratarse como «de nadie» y dejarla pasar. Se hace lo contrario: dueño desconocido es dueño distinto. Un fallo de autorización debe cerrar, no abrir.

### La comprobación vive en el servicio, no en el controlador

El controlador aporta el identificador del usuario que saca del token; el servicio decide. Así la regla no depende de que cada endpoint futuro se acuerde de repetirla, y queda cubierta por pruebas de servicio en vez de necesitar una prueba de API que hoy no existe.

### Una consulta nueva para resolver dueño y plazo

`SaveDraftAnswerAsync` solo tiene el `sessionId`. Se añade `IExamTokenRepository.GetBySessionIdAsync`, que devuelve el token con su examen y su sesión: de ahí salen el `UserId` y el `StartedAt` con el `TimeLimitMinutes` en una sola lectura.

`SubmitExamAsync` no la usa: ya carga ambas cosas por su camino actual. Se prefiere no reescribir ese método, que acumula tres cambios recientes y sus pruebas.

## Risks / Trade-offs

**El guardado pasa de una consulta a dos** → El auto-guardado se dispara con cada opción que el candidato marca. La consulta nueva va por clave primaria sobre una relación uno a uno. Es aceptable; si algún día molesta, la sesión y su dueño caben en el propio JWT.

**Un candidato con dos pestañas sigue pisándose a sí mismo** → La propiedad se comprueba por usuario, no por pestaña. Es el comportamiento anterior y este cambio no lo empeora.

**El envío tardío pierde lo escrito sin salir del campo** → Las respuestas abiertas se guardan al perder el foco. Quien esté escribiendo cuando venza el plazo pierde ese último texto. Es el hallazgo del auto-guardado, que sigue abierto y gana importancia con este cambio.

**Las sesiones abandonadas siguen abiertas** → Nadie las cierra, así que su token sigue vivo. Con este cambio ya no sirve para escribir, solo para entrar y provocar el cierre con lo guardado. El riesgo baja pero el hallazgo sigue.

## Migration Plan

Sin cambios de esquema. Sin script SQL.

API y Web se despliegan juntas. El cliente ya manda la cabecera `Authorization`, así que no hay ventana en la que la Web vieja hable con la API nueva y falle — pero conviene no separarlos por si algún despliegue parcial dejase una Web anterior a la reanudación, que no guardaba la sesión antes de llamar a `start`.
