PlayFab.settings.titleId = "188E8A";

let loadingProgress = 0;
let progressInterval;
let isLoginDone = false;
let isResourceDone = false;

function startLoadingProgress() {
    loadingProgress = 0;
    progressInterval = setInterval(() => {
        if (loadingProgress < 90) {
            loadingProgress++;
            updateProgressBar();
        } else if (isLoginDone && isResourceDone) {
            loadingProgress = 100;
            updateProgressBar();
            clearInterval(progressInterval);
            setTimeout(() => {
                document.getElementById('loading').style.display = 'none';
                document.getElementById('homepage').style.display = 'block';
            }, 300);
        }
    }, 50);
}

function updateProgressBar() {
    document.getElementById('progress-bar').style.width = loadingProgress + "%";
    document.getElementById('loading-text').innerText = "Loading game... " + loadingProgress + "%";
}

function login() {
    PlayFabClientSDK.LoginWithCustomID({
        TitleId: PlayFab.settings.titleId,
        CustomId: Telegram.WebApp.initDataUnsafe.user.id.toString(),
        CreateAccount: true
    }, function(result) {
        console.log("Đăng nhập thành công:", result);
        isLoginDone = true;
        loadResources();
    }, function(error) {
        console.error("Đăng nhập lỗi:", error);
        alert("Đăng nhập thất bại!");
    });
}

function loadResources() {
    PlayFabClientSDK.GetUserInventory({}, function(result) {
        if (!result || !result.data) {
            alert("Không lấy được dữ liệu tài nguyên!");
            return;
        }
        isResourceDone = true;
    }, function(error) {
        console.error("Lỗi khi load:", error);
    });
}

Telegram.WebApp.ready();
startLoadingProgress();
login();
