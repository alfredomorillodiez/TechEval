// Ayudas de la pantalla de la prueba: estado de la conexión y marcas «para revisar».
//
// Las marcas viven en el navegador, por sesión de prueba: son una ayuda del candidato, no
// un dato de la prueba, y no viajan al servidor. Todo acceso al almacenamiento va en try/catch
// porque un navegador puede rechazarlo (modo privado, datos bloqueados): sin él, la marca
// sigue funcionando mientras la página esté abierta.
window.examUi = {
    _connection: null,

    registerConnection: function (dotNetRef) {
        window.examUi.unregisterConnection();

        const notify = function () {
            dotNetRef.invokeMethodAsync('OnConnectionChanged', navigator.onLine !== false);
        };

        window.examUi._connection = notify;
        window.addEventListener('online', notify);
        window.addEventListener('offline', notify);
        notify();
    },

    unregisterConnection: function () {
        const notify = window.examUi._connection;
        if (!notify) return;

        window.removeEventListener('online', notify);
        window.removeEventListener('offline', notify);
        window.examUi._connection = null;
    },

    getFlags: function (sessionId) {
        try {
            const raw = window.localStorage.getItem('techeval.flags.' + sessionId);
            const parsed = raw ? JSON.parse(raw) : [];
            return Array.isArray(parsed) ? parsed.filter(Number.isInteger) : [];
        } catch (e) {
            return [];
        }
    },

    setFlags: function (sessionId, ids) {
        try {
            window.localStorage.setItem('techeval.flags.' + sessionId, JSON.stringify(ids));
        } catch (e) {
            // Sin almacenamiento: la marca solo dura mientras la página siga abierta.
        }
    }
};
