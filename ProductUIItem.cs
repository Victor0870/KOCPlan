// File: ProductUIItem.cs
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.Networking;
using UnityEngine.Events; // Quan trọng: Thêm thư viện này cho UnityEvent

public class ProductUIItem : MonoBehaviour
{
    [Header("Product UI Elements")]
    public TMP_Text productNameText;
    public TMP_Text unitText;
    public TMP_Text priceText;
    public TMP_Text categoryText;
    public Image productImage; // Kéo component Image vào đây (tùy chọn)

    // Thêm các TMP_Text khác nếu cần:
    // public TMP_Text barcodeText;
    // public TMP_Text manufacturerText;
    // public TMP_Text importPriceText;

    public TMP_Text stockText; // Hiển thị số lượng tồn kho

    // Thêm Button nhập kho
    public Button importButton; // Kéo Button "Nhập Kho" vào đây trong Inspector

    // Event sẽ được Invoke khi nút nhập kho được click
    // Nó sẽ truyền dữ liệu của sản phẩm này cho InventoryManager
    public UnityEvent<ProductData> OnImportRequested = new UnityEvent<ProductData>(); 

    private ProductData currentProductData; // Lưu trữ dữ liệu sản phẩm của item này

    public void SetProductData(ProductData product)
    {
        currentProductData = product;

        if (productNameText != null) productNameText.text = product.productName;
        if (unitText != null) unitText.text = product.unit;
        if (priceText != null) priceText.text = $"{product.price:N0} VNĐ"; // Định dạng tiền tệ
        if (categoryText != null) categoryText.text = product.category;
        if (stockText != null) stockText.text = $"Tồn kho: {product.stock}"; // Cập nhật hiển thị tồn kho

        // Gán sự kiện click cho nút nhập kho
        if (importButton != null)
        {
            // Xóa tất cả các listener cũ để tránh việc lắng nghe nhiều lần
            // Điều này quan trọng nếu SetProductData có thể được gọi nhiều lần trên cùng một instance của ProductUIItem
            importButton.onClick.RemoveAllListeners();
            // Thêm listener mới, khi click sẽ Invoke event OnImportRequested và truyền currentProductData
            importButton.onClick.AddListener(() => OnImportRequested.Invoke(currentProductData));
        }
        else
        {
            Debug.LogWarning("Import Button chưa được gán vào ProductUIItem trong Inspector.");
        }

        // TODO: Tải ảnh từ URL (sẽ cần thư viện ngoài hoặc UnityWebRequest)
        // Đây là phần comment để bạn tự xử lý nếu muốn tải ảnh động
        // if (productImage != null && !string.IsNullOrEmpty(product.imageUrl))
        // {
        //     StartCoroutine(LoadImage(product.imageUrl));
        // }
        // else if (productImage != null)
        // {
        //     productImage.sprite = null; // Hoặc một sprite mặc định
        // }

        // Cập nhật các text khác nếu có
        // if (barcodeText != null) barcodeText.text = product.barcode;
        // if (manufacturerText != null) manufacturerText.text = product.manufacturer;
        // if (importPriceText != null) importPriceText.text = $"{product.importPrice:N0} VNĐ";
    }

    // Tùy chọn: Hàm tải ảnh từ URL (cần using UnityEngine.Networking;)
    // private IEnumerator LoadImage(string url)
    // {
    //     using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
    //     {
    //         yield return request.SendWebRequest();

    //         if (request.result == UnityWebRequest.Result.Success)
    //         {
    //             Texture2D texture = DownloadHandlerTexture.GetContent(request);
    //             productImage.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
    //         }
    //         else
    //         {
    //             Debug.LogError($"Failed to load image from {url}: {request.error}");
    //         }
    //     }
    // }

    // Bạn có thể thêm các hàm khác ở đây, ví dụ cho nút "Sửa" hoặc "Xóa"
    // public void OnEditButtonClicked() {
    //     // Logic để mở panel chỉnh sửa
    // }
    // public void OnDeleteButtonClicked() {
    //     // Logic để xác nhận và xóa sản phẩm
    // }
}
