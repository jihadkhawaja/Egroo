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