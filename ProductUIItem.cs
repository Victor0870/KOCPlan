// File: ProductUIItem.cs
using UnityEngine;
using TMPro; // Sử dụng cho TextMeshPro
using UnityEngine.UI; // Sử dụng cho Image (nếu có)
using System.Collections; // Dành cho Coroutine nếu tải ảnh

public class ProductUIItem : MonoBehaviour
{
    [Header("Product UI Elements")]
    public TMP_Text productNameText;
    public TMP_Text unitText;
    public TMP_Text priceText;
    public TMP_Text categoryText;
    public Image productImage; // Kéo component Image vào đây (tùy chọn)

    // Các TMP_Text khác nếu cần:
    // public TMP_Text barcodeText;
    // public TMP_Text manufacturerText;
    // public TMP_Text importPriceText;

    private ProductData currentProductData; // Lưu trữ dữ liệu sản phẩm của item này

    public void SetProductData(ProductData product)
    {
        currentProductData = product;

        if (productNameText != null) productNameText.text = product.productName;
        if (unitText != null) unitText.text = product.unit;
        if (priceText != null) priceText.text = $"{product.price:N0} VNĐ"; // Định dạng tiền tệ
        if (categoryText != null) categoryText.text = product.category;

        // TODO: Tải ảnh từ URL (sẽ cần thư viện ngoài hoặc UnityWebRequest)
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
        // ...
    }

    // Tùy chọn: Hàm tải ảnh từ URL (cần using UnityEngine.Networking;)
    // private IEnumerator LoadImage(string url)
    // {
    //     using (UnityEngine.Networking.UnityWebRequest request = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(url))
    //     {
    //         yield return request.SendWebRequest();

    //         if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
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

    // Bạn có thể thêm các hàm khác ở đây, ví dụ:
    // public void OnEditButtonClicked() {
    //     // Mở panel chỉnh sửa với currentProductData
    // }
}
