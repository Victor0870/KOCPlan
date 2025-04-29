// Setup PlayFab
PlayFab.settings.titleId = "188E8A";

let loadingProgress = 0;
let progressInterval;
let loginTimeout;
let isLoginDone = false;
let isResourceDone = false;

function startLoadingProgress() {
    loadingProgress = 0;
    progressInterval = setInterval(() => {
        if (loadingProgress < 90) {
            loadingProgress++;
            document.getElementById('loading').innerText = "Loading game... " + loadingProgress + "%";
        } else if (isLoginDone && isResourceDone) {
            clearInterval(progressInterval);
            loadingProgress = 100;
            document.getElementById('loading').innerText = "Loading game... 100%";
            setTimeout(() => {
                document.getElementById('loading').style.display = 'none';
                document.getElementById('homepage').style.display = 'block';
            }, 300);
        }
    }, 50);
}

function login() {
    PlayFabClientSDK.LoginWithCustomID({
        TitleId: PlayFab.settings.titleId,
        CustomId: Telegram.WebApp.initDataUnsafe.user?.id?.toString(),
        CreateAccount: true
    }, function(result) {
        console.log("Đăng nhập thành công:", result);
        clearTimeout(loginTimeout);
        isLoginDone = true;
        loadResources();
    }, function(error) {
        console.error("Đăng nhập lỗi:", error);
        clearTimeout(loginTimeout);
        alert("Đăng nhập thất bại! Vui lòng thử lại.");
    });
}

function loadResources() {
    PlayFabClientSDK.GetUserInventory({}, function(result) {
        if (!result || !result.data) {
            alert("Không lấy được dữ liệu tài nguyên!");
            return;
        }

        const vc = result.data.VirtualCurrency;
        if (!vc) {
            alert("Không có dữ liệu tài nguyên!");
            return;
        }

        const missing = [];
        if (vc.FD === undefined) missing.push("FD");
        if (vc.WD === undefined) missing.push("WD");
        if (vc.IR === undefined) missing.push("IR");
        if (vc.ST === undefined) missing.push("ST");

        if (missing.length > 0) {
            alert("Thiếu tài nguyên: " + missing.join(", "));
            return;
        }

        // Hiển thị tài nguyên
        document.getElementById('fd').innerText = vc.FD;
        document.getElementById('wd').innerText = vc.WD;
        document.getElementById('ir').innerText = vc.IR;
        document.getElementById('st').innerText = vc.ST;

        isResourceDone = true;
    }, function(error) {
        console.error("Lỗi khi lấy tài nguyên:", error);
        alert("Kết nối server thất bại!");
    });
}

Telegram.WebApp.ready();

setTimeout(() => {
    startLoadingProgress();
    login();

    loginTimeout = setTimeout(() => {
        clearInterval(progressInterval);
        alert("Không kết nối được server. Vui lòng kiểm tra mạng!");
    }, 10000);
}, 100);
