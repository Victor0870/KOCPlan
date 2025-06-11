// File: ImportStockPanelManager.cs
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Firebase.Firestore;
using Firebase.Extensions; // Quan trọng cho ContinueWithOnMainThread
using System;
using System.Threading.Tasks; // Cần thiết nếu dùng async/await

public class ImportStockPanelManager : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject panelRoot; // Kéo GameObject gốc của panel nhập kho vào đây
    public TMP_Text productNameText;
    public TMP_Text currentStockText;
    public TMP_InputField importQuantityInputField;
    public Button confirmButton;
    public Button cancelButton;
    public TMP_Text statusMessageText; // Để hiển thị thông báo lỗi/thành công tạm thời

    private FirebaseFirestore db;
    private Firebase.Auth.FirebaseUser currentUser;
    private CollectionReference userProductsCollection;

    private ProductData productToUpdate; // Lưu trữ sản phẩm đang được thao tác
    private Action onStockUpdatedCallback; // Callback để thông báo cho InventoryManager

    void Awake()
    {
        // Đảm bảo panel ẩn khi bắt đầu
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }

        // Đăng ký sự kiện click cho các nút
        if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirmImportButtonClicked);
        if (cancelButton != null) cancelButton.onClick.AddListener(HidePanel);

        // Khởi tạo Firebase Firestore
        db = FirebaseFirestore.DefaultInstance;
        Firebase.Auth.FirebaseAuth auth = Firebase.Auth.FirebaseAuth.DefaultInstance;
        auth.StateChanged += AuthStateChanged; // Lắng nghe trạng thái đăng nhập
    }

    void OnDestroy()
    {
        Firebase.Auth.FirebaseAuth auth = Firebase.Auth.FirebaseAuth.DefaultInstance;
        if (auth != null)
        {
            auth.StateChanged -= AuthStateChanged;
        }
    }

    private void AuthStateChanged(object sender, EventArgs e)
    {
        Firebase.Auth.FirebaseUser newUser = Firebase.Auth.FirebaseAuth.DefaultInstance.CurrentUser;
        if (newUser != currentUser)
        {
            currentUser = newUser;
            if (currentUser != null)
            {
                // Chỉ thiết lập userProductsCollection khi có người dùng đăng nhập
                userProductsCollection = db.Collection("shops").Document(currentUser.UserId).Collection("products");
                Debug.Log($"ImportStockPanelManager: UserProductsCollection set for UID: {currentUser.UserId}");
            }
            else
            {
                // Xử lý khi người dùng đăng xuất (ví dụ: reset trạng thái)
                userProductsCollection = null;
                Debug.Log("ImportStockPanelManager: User logged out.");
            }
        }
    }

    // Hàm này được gọi từ InventoryManager để hiển thị panel
    public void ShowPanel(ProductData product, Action callback)
    {
        if (product == null)
        {
            Debug.LogError("ProductData is null when trying to show import panel.");
            return;
        }

        productToUpdate = product;
        onStockUpdatedCallback = callback;

        if (productNameText != null) productNameText.text = product.productName;
        if (currentStockText != null) currentStockText.text = $"Tồn kho hiện tại: {product.stock}";
        if (importQuantityInputField != null) importQuantityInputField.text = ""; // Xóa giá trị cũ
        if (statusMessageText != null) statusMessageText.text = ""; // Xóa thông báo cũ

        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }
    }

    // Ẩn panel nhập kho
    public void HidePanel()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
        statusMessageText.text = ""; // Xóa thông báo khi đóng panel
    }

    // Xử lý khi nút "Xác nhận" được bấm
    private async void OnConfirmImportButtonClicked()
    {
        if (productToUpdate == null || string.IsNullOrEmpty(productToUpdate.productId))
        {
            Debug.LogError("Không có sản phẩm nào được chọn hoặc thiếu ProductId.");
            if (statusMessageText != null) statusMessageText.text = "Lỗi: Không tìm thấy thông tin sản phẩm.";
            return;
        }

        if (userProductsCollection == null)
        {
            Debug.LogError("UserProductsCollection chưa được thiết lập. Người dùng chưa đăng nhập?");
            if (statusMessageText != null) statusMessageText.text = "Lỗi: Vui lòng đăng nhập.";
            return;
        }

        if (!long.TryParse(importQuantityInputField.text, out long quantityToAdd) || quantityToAdd <= 0)
        {
            if (statusMessageText != null) statusMessageText.text = "Vui lòng nhập số lượng hợp lệ (> 0).";
            return;
        }

        if (statusMessageText != null) statusMessageText.text = "Đang cập nhật tồn kho...";
        confirmButton.interactable = false; // Tạm thời tắt nút để tránh click nhiều lần
        cancelButton.interactable = false;

        DocumentReference productDocRef = userProductsCollection.Document(productToUpdate.productId);

        try
        {
            // Sử dụng FieldValue.Increment để cập nhật số lượng tồn kho một cách an toàn
            // Tránh race condition nếu có nhiều người dùng cùng lúc hoặc nhiều thao tác nhanh
            Dictionary<string, object> updates = new Dictionary<string, object>
            {
                { "stock", FieldValue.Increment(quantityToAdd) }
            };

            await productDocRef.UpdateAsync(updates);

            // Cập nhật stock cục bộ trong đối tượng ProductData (chỉ để hiển thị ngay lập tức)
            productToUpdate.stock += quantityToAdd;

            Debug.Log($"Đã cập nhật tồn kho cho sản phẩm '{productToUpdate.productName}'. Thêm: {quantityToAdd}, Tồn kho mới: {productToUpdate.stock}");
            if (statusMessageText != null) statusMessageText.text = "Cập nhật tồn kho thành công!";

            // Gọi callback để InventoryManager biết cần làm mới danh sách
            onStockUpdatedCallback?.Invoke();

            // Đóng panel sau một thời gian ngắn
            await Task.Delay(1000); // Đợi 1 giây để người dùng thấy thông báo thành công
            HidePanel();
        }
        catch (Exception e)
        {
            Debug.LogError($"Lỗi khi cập nhật tồn kho cho sản phẩm '{productToUpdate.productName}': {e.Message}");
            if (statusMessageText != null) statusMessageText.text = $"Lỗi cập nhật: {e.Message}";
        }
        finally
        {
            confirmButton.interactable = true;
            cancelButton.interactable = true;
        }
    }
}
