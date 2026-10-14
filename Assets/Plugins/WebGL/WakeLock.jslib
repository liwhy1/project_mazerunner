mergeInto(LibraryManager.library, {
    RequestWakeLock: function () {
        if (!("wakeLock" in navigator))
            return;

        navigator.wakeLock.request("screen")
            .then(function (wakeLock) {
                window.unityWakeLock = wakeLock;

                wakeLock.addEventListener("release", function () {
                    window.unityWakeLock = null;
                });
            })
            .catch(function (error) {
                console.warn("Wake Lock failed:", error);
            });
    },

    ReleaseWakeLock: function () {
        if (window.unityWakeLock) {
            window.unityWakeLock.release();
            window.unityWakeLock = null;
        }
    }
});