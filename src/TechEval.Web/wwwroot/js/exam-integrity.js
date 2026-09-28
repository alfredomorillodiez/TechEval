// Señales de integridad del examen: salidas de la página, vueltas y pegados.
//
// Solo avisa al componente cuando el estado cambia. Un 'blur' seguido de un
// 'visibilitychange' es una sola salida, no dos. La ausencia se mide aquí con
// performance.now(), que no depende del reloj del sistema: la hora del servidor no
// sirve, porque una señal reintentada llega con la hora del reintento.
//
// Del pegado solo pasa la longitud, nunca el texto. El portapapeles puede llevar
// cualquier dato del candidato que nada tiene que ver con la prueba.
window.examIntegrity = {
    _handlers: null,

    register: function (dotNetRef) {
        window.examIntegrity.unregister();

        let away = false;
        let leftAt = 0;

        const setAway = function (nowAway) {
            if (nowAway === away) return;
            away = nowAway;

            if (away) {
                leftAt = performance.now();
                dotNetRef.invokeMethodAsync('OnPageLeft');
            } else {
                const seconds = Math.round((performance.now() - leftAt) / 1000);
                dotNetRef.invokeMethodAsync('OnPageReturned', seconds);
            }
        };

        const onBlur = function () { setAway(true); };
        const onFocus = function () { setAway(false); };
        const onVisibility = function () {
            if (document.visibilityState !== 'visible') setAway(true);
            // Visible no basta: la ventana puede estar a la vista sin el foco.
            else if (document.hasFocus()) setAway(false);
        };

        const onPaste = function (e) {
            const target = e.target;
            if (!(target instanceof HTMLTextAreaElement)) return;

            const questionId = parseInt(target.dataset.questionId, 10);
            if (!questionId) return;

            const text = e.clipboardData ? e.clipboardData.getData('text') : '';
            dotNetRef.invokeMethodAsync('OnPaste', questionId, text.length);
        };

        window.addEventListener('blur', onBlur);
        window.addEventListener('focus', onFocus);
        document.addEventListener('visibilitychange', onVisibility);
        // En captura: el pegado se anota aunque algo por debajo detenga el evento.
        document.addEventListener('paste', onPaste, true);

        window.examIntegrity._handlers = { onBlur, onFocus, onVisibility, onPaste };

        // Una página que se abre ya sin el foco (en segundo plano, por ejemplo) empieza
        // fuera. El componente descarta la señal si la prueba no está en curso.
        if (document.visibilityState !== 'visible' || !document.hasFocus()) setAway(true);
    },

    unregister: function () {
        const h = window.examIntegrity._handlers;
        if (!h) return;

        window.removeEventListener('blur', h.onBlur);
        window.removeEventListener('focus', h.onFocus);
        document.removeEventListener('visibilitychange', h.onVisibility);
        document.removeEventListener('paste', h.onPaste, true);
        window.examIntegrity._handlers = null;
    }
};
