// Setup PlayFab
// TitleId của sếp

playerLogin(function(staticData) { console.log("Static Data đã sẵn sàng:", staticData);

PlayFabClientSDK.GetUserInventory({}, function(result) {
    if (!result || !result.data) {
        alert("Không lấy được dữ liệu tài nguyên!");
        document.getElementById('loading').innerText = 'Lỗi tải tài nguyên!';
        return;
    }

    const vc = result.data.VirtualCurrency;
    if (!vc) {
        alert("Không có dữ liệu tài nguyên!");
        document.getElementById('loading').innerText = 'Lỗi tải tài nguyên!';
        return;
    }

    document.getElementById('fd').innerText = vc.FD || 0;
    document.getElementById('wd').innerText = vc.WD || 0;
    document.getElementById('ir').innerText = vc.IR || 0;
    document.getElementById('st').innerText = vc.ST || 0;

    document.getElementById('loading').style.display = 'none';
    document.getElementById('homepage').style.display = 'block';
}, function(error) {
    console.error("Lỗi khi load tài nguyên:", error);
    alert("Không thể kết nối!");
    document.getElementById('loading').innerText = 'Không thể kết nối!';
});

});
