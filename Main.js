// Setup PlayFab
 PlayFab.settings.titleId = "188E8A"; // TitleId của sếp
 
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
         document.getElementById('loading').innerText = 'Lỗi đăng nhập!';
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
             document.getElementById('loading').innerText = 'Lỗi grant tài nguyên!';
         });
     });
 }
 
 function loadResources() {
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
 
         const missing = [];
         if (vc.FD === undefined) missing.push("FD");
         if (vc.WD === undefined) missing.push("WD");
         if (vc.IR === undefined) missing.push("IR");
         if (vc.ST === undefined) missing.push("ST");
 
         if (missing.length > 0) {
             alert("Thiếu tài nguyên: " + missing.join(", "));
             document.getElementById('loading').innerText = 'Thiếu tài nguyên!';
             return;
         }
 
         document.getElementById('fd').innerText = vc.FD;
         document.getElementById('wd').innerText = vc.WD;
         document.getElementById('ir').innerText = vc.IR;
         document.getElementById('st').innerText = vc.ST;
 
         document.getElementById('loading').style.display = 'none';
         document.getElementById('homepage').style.display = 'block';
 
     }, function(error) {
         console.error("Lỗi khi lấy tài nguyên:", error);
         alert("Kết nối server thất bại!");
         document.getElementById('loading').innerText = 'Không thể kết nối!';
     });
 }
 
 Telegram.WebApp.ready();
 login();
