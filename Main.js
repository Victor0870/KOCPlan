// script.js

// --- Khai bao bien --- let progress = 0; let isLoginDone = false; let progressInterval; let loginTimeout;

Telegram.WebApp.ready();

// Ham bat dau chay loading progress function startLoadingProgress() { const progressBar = document.getElementById("progressBar"); const progressText = document.getElementById("progressText");

progressInterval = setInterval(() => {
    if (progress < 90) {
        progress++;
        progressBar.style.width = progress + "%";
        progressText.innerText = progress + "%";
    }
    // Khi da login xong thi fill 100% va chuyen scene
    if (isLoginDone && progress >= 90) {
        clearInterval(progressInterval);
        progress = 100;
        progressBar.style.width = "100%";
        progressText.innerText = "100%";
        setTimeout(() => {
            window.location.href = "home.html";
        }, 500);
    }
}, 50); // cap nhat moi 50ms

}

// Ham dang nhap vao PlayFab function login() { PlayFab.settings.titleId = "188E8A"; // Replace thanh TitleId cua sep

PlayFabClientSDK.LoginWithCustomID({
    TitleId: PlayFab.settings.titleId,
    CustomId: Telegram.WebApp.initDataUnsafe.user?.id?.toString(),
    CreateAccount: true
}, function (result) {
    console.log("Dang nhap thanh cong:", result);
    clearTimeout(loginTimeout);
    isLoginDone = true;
}, function (error) {
    console.error("Dang nhap that bai:", error);
    clearTimeout(loginTimeout);
    alert("Dang nhap that bai. Vui long thu lai!");
});

}

// --- CHAY ---

// 1. Cho Telegram san sang -> loading + login setTimeout(() => { startLoadingProgress(); login();

// 2. Neu 10s khong xong thi bao loi
loginTimeout = setTimeout(() => {
    clearInterval(progressInterval);
    alert("Khong ket noi duoc den server. Kiem tra mang!");
}, 10000);

}, 100);

