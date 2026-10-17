// Browsers reject pointer lock requests (embedded frames, a click released too quickly, a
// player pressing Escape) with a rejected promise. Unity's loader reports every unhandled
// rejection as a fatal content error and alerts, so swallow these before it sees them; the
// game keeps running with the cursor unlocked and Unity retries on the next click.
var HotelPointerLock = {
    $HFPointerLock__postset: "HFPointerLock.install();",
    $HFPointerLock: {
        install: function () {
            // Capture-phase listeners run before the loader's bubbling listener on the same target.
            window.addEventListener("unhandledrejection", function (event) {
                var reason = event.reason;
                var text = String((reason && (reason.name + " " + reason.message)) || reason);
                if (/pointer lock/i.test(text)) {
                    console.warn("Pointer lock unavailable: " + text);
                    event.preventDefault();
                    event.stopImmediatePropagation();
                }
            }, true);
        }
    },
    HFPointerLock_Installed: function () { return 1; }
};

autoAddDeps(HotelPointerLock, "$HFPointerLock");
mergeInto(LibraryManager.library, HotelPointerLock);
