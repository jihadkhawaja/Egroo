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
        const isArrowKey = event.key === 'ArrowDown'
            || event.key === 'Down'
            || event.key === 'ArrowUp'
            || event.key === 'Up';
        const isEnterKey = event.key === 'Enter' || event.key === 'NumpadEnter';

        if (mentionPopupActive && (isArrowKey || isEnterKey)) {
            event.preventDefault();
            event.stopPropagation();
            dotNetObject.invokeMethodAsync('HandleMentionNavigationKey', event.key, !!event.shiftKey);
            return;
        }

        if (!event.shiftKey && isEnterKey) {
            event.preventDefault();
            event.stopPropagation();
            dotNetObject.invokeMethodAsync('HandleComposerEnterKey', false);
        }
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

window.registerEnterToSend = function (selector, dotNetObject, callbackName) {
    const input = document.querySelector(selector);
    if (!input) {
        return;
    }

    if (input.__egrooEnterToSendHandler) {
        input.removeEventListener('keydown', input.__egrooEnterToSendHandler, true);
    }

    const handler = function (event) {
        const isEnterKey = event.key === 'Enter' || event.key === 'NumpadEnter';
        if (!isEnterKey || event.shiftKey) {
            return;
        }

        event.preventDefault();
        event.stopPropagation();
        dotNetObject.invokeMethodAsync(callbackName, false);
    };

    input.__egrooEnterToSendHandler = handler;
    input.addEventListener('keydown', handler, true);
};

window.unregisterEnterToSend = function (selector) {
    const input = document.querySelector(selector);
    if (!input || !input.__egrooEnterToSendHandler) {
        return;
    }

    input.removeEventListener('keydown', input.__egrooEnterToSendHandler, true);
    delete input.__egrooEnterToSendHandler;
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