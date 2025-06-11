using UnityEngine;
using TMPro; // Sử dụng cho TextMeshPro
using UnityEngine.UI; // Sử dụng cho Image và Button
using System.Collections; // Dành cho Coroutine nếu tải ảnh
using UnityEngine.Events; // Quan trọng: Thêm namespace này cho UnityEvent

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

    // Thêm Button Edit
    [Header("Interaction Buttons")]
    public Button editButton; // Kéo Button component của nút Edit vào đây trong Inspector

    // Định nghĩa một UnityEvent để gửi dữ liệu sản phẩm khi nút Edit được nhấn
    // InventoryManager sẽ đăng ký lắng nghe sự kiện này
    public UnityEvent<ProductData> OnEditActionRequested; 

    private ProductData currentProductData; // Lưu trữ dữ liệu sản phẩm của item này

    void Awake()
    {
        // Khởi tạo UnityEvent nếu nó chưa được khởi tạo
        if (OnEditActionRequested == null)
        {
            OnEditActionRequested = new UnityEvent<ProductData>();
        }

        // Gán hàm xử lý sự kiện cho nút Edit
        if (editButton != null)
        {
            editButton.onClick.AddListener(OnEditButtonClicked);
        }
        else
        {
            Debug.LogWarning("Nút 'Edit Button' chưa được gán trong ProductUIItem. Vui lòng kéo Button vào trường 'Edit Button' trong Inspector.");
        }
    }

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

    // Phương thức này sẽ được gọi khi nút Edit trên item UI được nhấn
    public void OnEditButtonClicked()
    {
        Debug.Log($"Nút chỉnh sửa đã được nhấn cho sản phẩm: {currentProductData.productName}");
        // Kích hoạt sự kiện và truyền dữ liệu sản phẩm hiện tại
        OnEditActionRequested?.Invoke(currentProductData);
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
}
