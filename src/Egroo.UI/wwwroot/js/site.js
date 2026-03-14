function scrollToEnd(id) {
    let el = document.getElementById(id);
    if (el) {
        el.scrollTop = el.scrollHeight;
    }
}

function isNearBottom(id, threshold) {
    let el = document.getElementById(id);
    if (!el) return true;
    return (el.scrollHeight - el.scrollTop - el.clientHeight) <= (threshold || 150);
}

function scrollToEndIfNearBottom(id, threshold) {
    if (isNearBottom(id, threshold)) {
        scrollToEnd(id);
    }
}

window.scrollElementIntoView = function (id) {
    const el = document.getElementById(id);
    if (!el) {
        return;
    }

    el.scrollIntoView({ behavior: 'smooth', block: 'center' });
};

window.registerMentionNavigation = function (selector, dotNetObject) {
    const input = document.querySelector(selector);
    if (!input) {
        return;
    }

    if (input.__egrooMentionNavigationHandler) {
        input.removeEventListener('keydown', input.__egrooMentionNavigationHandler, true);
    }

    const handler = function (event) {
        const mentionPopupActive = document.querySelector('.channel-mention-entry') !== null;
        const isHandledKey = event.key === 'ArrowDown'
            || event.key === 'Down'
            || event.key === 'ArrowUp'
            || event.key === 'Up'
            || event.key === 'Enter'
            || event.key === 'NumpadEnter';

        if (!mentionPopupActive || !isHandledKey) {
            return;
        }

        event.preventDefault();
        event.stopPropagation();
        dotNetObject.invokeMethodAsync('HandleMentionNavigationKey', event.key, !!event.shiftKey);
    };

    input.__egrooMentionNavigationHandler = handler;
    input.addEventListener('keydown', handler, true);
};

window.unregisterMentionNavigation = function (selector) {
    const input = document.querySelector(selector);
    if (!input || !input.__egrooMentionNavigationHandler) {
        return;
    }

    input.removeEventListener('keydown', input.__egrooMentionNavigationHandler, true);
    delete input.__egrooMentionNavigationHandler;
};

window.onbeforeunload = function () {
    try {
        DotNet.invokeMethodAsync('Egroo.UI', 'OnClosedWindow')
            .then(data => {
                console.log("Window unloaded!");
                console.log(data);
            });
    }
    catch (e) {
        console.log(e);
    }
}