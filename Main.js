PlayFab.settings.titleId = "188E8A";

let loadingProgress = 0;
let progressInterval;

function startLoadingProgress() {
    loadingProgress = 0;
    progressInterval = setInterval(() => {
        if (loadingProgress < 95) {
            loadingProgress++;
            updateProgressBar();
        }
    }, 50);
}

function updateProgressBar() {
    document.getElementById('progress-bar').style.width = loadingProgress + "%";
    document.getElementById('loading-text').innerText = "Loading game... " + loadingProgress + "%";
}

function finishLoading(success) {
    clearInterval(progressInterval);
    if (success) {
        loadingProgress = 100;
        updateProgressBar();
        setTimeout(() => {
            document.getElementById('loading').style.display = 'none';
            document.getElementById('homepage').style.display = 'block';
        }, 300);
    } else {
        document.getElementById('loading-text').innerText = "Lỗi tải dữ liệu!";
    }
}

function login() {
    PlayFabClientSDK.LoginWithCustomID({
        TitleId: PlayFab.settings.titleId,
        CustomId: Telegram.WebApp.initDataUnsafe.user.id.toString(),
        CreateAccount: true
    }, function(result) {
        console.log("Đăng nhập thành công:", result);
        loadResources();
    }, function(error) {
        console.error("Đăng nhập lỗi:", error);
        alert("Đăng nhập thất bại!");
        finishLoading(false);
    });
}

function loadResources() {
    PlayFabClientSDK.GetUserInventory({}, function(result) {
        if (!result || !result.data) {
            alert("Không lấy được dữ liệu tài nguyên!");
            finishLoading(false);
            return;
        }
        finishLoading(true);
    }, function(error) {
        console.error("Lỗi khi load:", error);
        finishLoading(false);
    });
}

Telegram.WebApp.ready();
startLoadingProgress();
login();
