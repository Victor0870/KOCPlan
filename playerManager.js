  const defaultStaticData = { FieldProductivity: 1, WoodProductivity: 1, IronProductivity: 1, StoneProductivity: 1 };

function playerLogin(callback) { PlayFab.settings.titleId = "188E8A";

PlayFabClientSDK.LoginWithCustomID({
    TitleId: PlayFab.settings.titleId,
    CustomId: Telegram.WebApp.initDataUnsafe.user.id.toString(),
    CreateAccount: true
}, function (result) {
    console.log("Đăng nhập thành công:", result);
    if (result.data.NewlyCreated) {
        console.log("Tạo static mặc định cho tài khoản mới.");
        createPlayerStatic(callback);
    } else {
        console.log("Tài khoản đã có, load static.");
        loadPlayerStatic(callback);
    }
}, function (error) {
    console.error("Lỗi đăng nhập:", error);
    alert("Đăng nhập thất bại!");
});

}

function createPlayerStatic(callback) { PlayFabClientSDK.UpdateUserData({ Data: defaultStaticData }, function (result) { console.log("Tạo dữ liệu mặc định thành công."); if (callback) callback(defaultStaticData); }, function (error) { console.error("Lỗi khi tạo dữ liệu mặc định:", error); alert("Không thể tạo dữ liệu player!"); }); }

function loadPlayerStatic(callback) { PlayFabClientSDK.GetUserData({}, function (result) { if (result.data && result.data.Data) { const staticData = {}; for (const key in defaultStaticData) { staticData[key] = parseInt(result.data.Data[key]?.Value || 0); } console.log("Static data đã load:", staticData); if (callback) callback(staticData); } else { alert("Không load được dữ liệu player!"); } }, function (error) { console.error("Lỗi khi load dữ liệu player:", error); alert("Không thể tải dữ liệu player!"); }); }

// main.js Telegram.WebApp.ready();

