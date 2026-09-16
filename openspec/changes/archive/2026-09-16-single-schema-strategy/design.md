## Context

Ver `proposal.md` — Why para la motivación.

Lo que condiciona el enfoque:

- El proyecto no tiene migraciones de EF: la carpeta `Migrations/` no existe. Elegir EF ahora obligaría a generar una migración inicial contra bases ya creadas por `EnsureCreated`, que es justo lo que hoy no funciona.
- `scripts/` ya tiene el esquema completo y los guiones aditivos, y es lo que se usa al desplegar.
- `DbSeeder.SeedAsync` hace tres cosas: crear el esquema, sembrar el administrador y sembrar contenido de ejemplo.
- El modelo tiene diez `DbSet` y el guion completo tiene diez `CREATE TABLE`.

## Goals / Non-Goals

**Goals:**

- Que haya una sola forma de crear el esquema.
- Que olvidarse de llevar una columna al guion se note antes de desplegar.
- Que un despliegue de cliente no reciba preguntas de muestra.

**Non-Goals:**

- No se adoptan migraciones de EF. Es una decisión mayor y el despliegue por cliente encaja mejor con SQL versionado.
- No se comprueban tipos, longitudes ni claves foráneas. Eso pide una base de datos real.
- No se toca el contenido de los guiones de siembra de preguntas.

## Decisions

### Se eligen los guiones SQL

Los tres candidatos hacen lo mismo y solo uno puede mandar. Los guiones ganan porque ya funcionan, porque encajan con desplegar en el servidor de un cliente sin el CLI de .NET, y porque el equipo ya los mantiene.

**Alternativa descartada**: adoptar migraciones de EF y borrar los guiones. Es la opción estándar, pero obliga a generar una migración inicial que cuadre con las bases ya existentes, creadas por `EnsureCreated` sin historial. Eso es un trabajo delicado con riesgo de pérdida de datos, a cambio de una comodidad que este equipo no está usando.

**Alternativa descartada**: dejar `EnsureCreated` solo en desarrollo. Suena cómodo, pero entonces el esquema de desarrollo sale del modelo y el de producción del guion. Los dos divergirían en silencio, que es el problema de fondo.

### La aplicación para si no encuentra el esquema

Sin `EnsureCreated`, arrancar contra una base vacía daría un error de SQL en la primera consulta, en mitad de una petición y sin decir qué hacer.

Se comprueba al arrancar y se para con un mensaje que nombra el guion. Es el mismo criterio que `StartupSecrets`: un entorno mal preparado debe parar en seco y explicarse.

### La prueba de deriva compara nombres contra el texto del guion

Lee el modelo de EF por reflexión —tablas y columnas mapeadas— y busca cada nombre en `create_database.sql`.

**Alternativa descartada**: levantar SQL Server en un contenedor, ejecutar el guion y comparar el esquema real con el del modelo. Es la comprobación de verdad, y detectaría tipos y claves foráneas. Depende de Docker en cada ejecución de la batería, y el proyecto todavía no tiene infraestructura de pruebas de integración. Eso es `E2`.

**Lo que esta prueba no ve**: que una columna esté declarada con otro tipo, otra longitud o sin su clave foránea. Conviene decirlo en la propia prueba, para que nadie la lea como una garantía de que el esquema es correcto.

**Lo que sí ve, y es lo que pasa en la práctica**: que alguien añada una propiedad o una entidad y se olvide del guion. Ocurrió el 16·09 con las cuatro columnas de la copia.

### El administrador se siembra siempre; el contenido de ejemplo, no

Sin administrador no hay forma de entrar, así que su siembra no puede depender del entorno. Las categorías y preguntas de muestra sí: en un cliente son basura en su banco.

## Risks / Trade-offs

**Un desarrollador nuevo ya no arranca contra una base vacía** → Tiene que ejecutar el guion primero. Mitigación: el `README.md` lo dice y la aplicación lo dice con el nombre del fichero.

**Las bases ya creadas por `EnsureCreated` no cambian** → Siguen funcionando; simplemente dejan de recrearse. Si a alguna le falta una columna añadida después, lo dirá el error de la aplicación, igual que ahora.

**La prueba de deriva puede dar un falso positivo** → Una columna con nombre distinto en el guion, o una propiedad que EF mapea a otra columna, la haría fallar sin motivo. Mitigación: la comparación usa el nombre de columna que EF resuelve, no el de la propiedad.

**Quitar el contenido de ejemplo de producción no lo borra de donde ya está** → Un despliegue que ya lo tenga lo conserva. Borrarlo es decisión de quien lo opera, no de este cambio.

## Migration Plan

1. Sobre una base ya existente, ejecutar los guiones aditivos que falten. Nada más.
2. Sobre una base nueva, ejecutar `scripts/create_database.sql`, que ahora sí está completo.
3. Desplegar.

**Vuelta atrás**: revertir el commit devuelve `EnsureCreated`. No hay cambio de datos que deshacer.
