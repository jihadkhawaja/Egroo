function scrollToEnd(id) {
    let scroll_to_bottom = document.getElementById(id);
    scroll_to_bottom.scrollTop = scroll_to_bottom.scrollHeight + 280;
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