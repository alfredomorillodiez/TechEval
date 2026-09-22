// Avisa al componente del examen cuando la ventana vuelve a estar a la vista.
//
// El navegador frena o para los temporizadores de una pestaña oculta. Si el
// temporizador de .NET no vuelve por sí solo, el reloj del examen se queda
// congelado. Estos eventos fuerzan un recálculo en cuanto el candidato vuelve.
window.examTimer = {
    _handler: null,

    register: function (dotNetRef) {
        window.examTimer.unregister();

        const handler = function () {
            if (document.visibilityState !== 'visible') return;
            dotNetRef.invokeMethodAsync('RefreshTimer');
        };

        window.examTimer._handler = handler;
        document.addEventListener('visibilitychange', handler);
        window.addEventListener('focus', handler);
        window.addEventListener('pageshow', handler);
    },

    unregister: function () {
        const handler = window.examTimer._handler;
        if (!handler) return;

        document.removeEventListener('visibilitychange', handler);
        window.removeEventListener('focus', handler);
        window.removeEventListener('pageshow', handler);
        window.examTimer._handler = null;
    }
};
