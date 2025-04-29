// Setup PlayFab
PlayFab.settings.titleId = "188E8A"; // Thay bằng TitleId thật của sếp

let loadingProgress = 0;
let progressInterval;

function startLoadingProgress() {
    loadingProgress = 0;
    progressInterval = setInterval(() => {
        if (loadingProgress < 95) {
            loadingProgress++;
            updateProgressBar();
        }
    }, 50); // 50ms tăng 1% => khá mượt
}

function updateProgressBar() {
    document.getElementById('progress-bar').style.width = loadingProgress + "%";
    document.getElementById('loading-text').innerText = "Loading game... " + loadingProgress + "%";
}

function finishLoading(success) {
    clearInterval(progressInterval); // Stop auto tăng
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

        if (result.data.NewlyCreated) {
            console.log("Tài khoản mới -> Grant 0 vào các tài nguyên.");
            grantZeroCurrency(["FD", "WD", "IR", "ST"], function() {
                loadResources();
            });
        } else {
            loadResources();
        }

    }, function(error) {
        console.error("Đăng nhập lỗi:", error);
        alert("Lỗi đăng nhập PlayFab!");
        finishLoading(false);
    });
}

function grantZeroCurrency(currencies, callback) {
    let count = 0;
    currencies.forEach(code => {
        PlayFabClientSDK.AddUserVirtualCurrency({
            VirtualCurrency: code,
            Amount: 0
        }, function(result) {
            console.log(`Grant 0 vào ${code} thành công.`);
            count++;
            if (count === currencies.length) {
                callback();
            }
        }, function(error) {
            console.error(`Lỗi grant 0 ${code}:`, error);
            alert(`Lỗi grant ${code}`);
            finishLoading(false);
        });
    });
}

function loadResources() {
    PlayFabClientSDK.GetUserInventory({}, function(result) {
        if (!result || !result.data) {
            alert("Không lấy được dữ liệu tài nguyên!");
            finishLoading(false);
            return;
        }

        const vc = result.data.VirtualCurrency;
        if (!vc) {
            alert("Không có dữ liệu tài nguyên!");
            finishLoading(false);
            return;
        }

        const missing = [];
        if (vc.FD === undefined) missing.push("FD");
        if (vc.WD === undefined) missing.push("WD");
        if (vc.IR === undefined) missing.push("IR");
        if (vc.ST === undefined) missing.push("ST");

        if (missing.length > 0) {
            alert("Thiếu tài nguyên: " + missing.join(", "));
            finishLoading(false);
            return;
        }

        document.getElementById('fd').innerText = vc.FD;
        document.getElementById('wd').innerText = vc.WD;
        document.getElementById('ir').innerText = vc.IR;
        document.getElementById('st').innerText = vc.ST;

        finishLoading(true); // Thành công!

    }, function(error) {
        console.error("Lỗi khi lấy tài nguyên:", error);
        alert("Kết nối server thất bại!");
        finishLoading(false);
    });
}

Telegram.WebApp.ready();
startLoadingProgress();
login();
