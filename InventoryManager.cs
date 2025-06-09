// File: InventoryManager.cs
using UnityEngine;
using System.Collections;
using Firebase.Auth;
using Firebase.Firestore;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine.UI; // Dành cho Dropdown
using System; // Để sử dụng Exception

public class InventoryManager : MonoBehaviour
{
    [Header("UI References")]
    public GameObject inventoryContentPanel; // Panel chứa danh sách tồn kho chính
    public GameObject sampleInventorySelectionPanel; // Panel để chọn kho mẫu
    public TMP_Text inventoryStatusText; // Text hiển thị trạng thái tồn kho
    public TMP_Dropdown industryDropdown; // Dropdown để chọn ngành hàng
    public Button createFromSampleButton;
    public Button skipSampleButton;
    public GameObject sampleLoadingPanel;
    public TMP_Text sampleStatusText;

    private FirebaseUser currentUser;
    private FirebaseFirestore db;
    private CollectionReference userProductsCollection; // Collection sản phẩm của người dùng
    private CollectionReference sampleInventoriesCollection; // Collection kho mẫu
    
    // Đã di chuyển [Header] vào đây cho dễ nhìn hơn, không bắt buộc
    [Header("Inventory Display UI")] 
    public Transform productListContentParent; // Kéo GameObject Content của ScrollView vào đây
    public GameObject productItemPrefab;       // Kéo Prefab UI của một sản phẩm vào đây


    // Danh sách các ngành hàng mẫu và ID Firestore tương ứng
    // ID Firestore sẽ khớp với key cấp cao nhất trong JSON của bạn (ví dụ: "pharma", "retail")
    private Dictionary<string, string> sampleIndustries = new Dictionary<string, string>
    {
        { "Dược phẩm", "pharma" }, // "Dược phẩm" là tên hiển thị, "pharma" là ID trong Firestore
        { "Dầu bôi trơn", "dau_boi_tron" },
        { "Tạp hóa", "retail" } // Thêm "retail" nếu bạn đã thêm vào JSON
        // Thêm các ngành hàng khác ở đây, đảm bảo key (tên hiển thị) và value (ID Firestore) khớp
    };

    void Awake()
    {
        // Kiểm tra AuthManager và lấy thông tin người dùng
        if (AuthManager.Instance == null || AuthManager.Instance.GetCurrentUser() == null)
        {
            Debug.LogError("InventoryManager: Không tìm thấy AuthManager hoặc người dùng chưa đăng nhập.");
            // Chuyển về màn hình đăng nhập nếu lỗi
            // SceneManager.LoadScene("LoginScene"); 
            return;
        }

        currentUser = AuthManager.Instance.GetCurrentUser();
        db = FirebaseFirestore.DefaultInstance;

        // Khởi tạo tham chiếu đến collection sản phẩm của người dùng
        // products là sub-collection dưới document shop của mỗi user
        // Cấu trúc của bạn là: shops/{userId}/products
        userProductsCollection = db.Collection("shops").Document(currentUser.UserId).Collection("products");
        sampleInventoriesCollection = db.Collection("sample_inventories");

        // Gắn sự kiện cho các nút
        if (createFromSampleButton != null) createFromSampleButton.onClick.AddListener(OnCreateFromSampleButtonClicked);
        if (skipSampleButton != null) skipSampleButton.onClick.AddListener(OnSkipSampleButtonClicked);

        // Khởi tạo Dropdown
        if (industryDropdown != null) PopulateIndustryDropdown();

        // Ban đầu ẩn panel chọn kho mẫu
        if (sampleInventorySelectionPanel != null) sampleInventorySelectionPanel.SetActive(false);
        if (sampleLoadingPanel != null) sampleLoadingPanel.SetActive(false);
    }

    void Start()
    {
        CheckUserInventory();
    }

    void OnDestroy()
    {
        if (createFromSampleButton != null) createFromSampleButton.onClick.RemoveListener(OnCreateFromSampleButtonClicked);
        if (skipSampleButton != null) skipSampleButton.onClick.RemoveListener(OnSkipSampleButtonClicked);
    }

    private void PopulateIndustryDropdown()
    {
        industryDropdown.ClearOptions();
        List<string> options = new List<string>();
        foreach (var entry in sampleIndustries)
        {
            options.Add(entry.Key); // Key là tên hiển thị (Dược phẩm)
        }
        industryDropdown.AddOptions(options);
    }

    public async void CheckUserInventory()
    {
        // Hiển thị loading khi kiểm tra
        if (inventoryStatusText != null) inventoryStatusText.text = "Đang kiểm tra kho hàng của bạn...";
        if (sampleLoadingPanel != null) sampleLoadingPanel.SetActive(true); // Sử dụng sampleLoadingPanel làm loading tổng

        try
        {
            // Lấy 1 sản phẩm giới hạn để kiểm tra xem có bất kỳ sản phẩm nào không
            QuerySnapshot snapshot = await userProductsCollection.Limit(1).GetSnapshotAsync();

            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                if (sampleLoadingPanel != null) sampleLoadingPanel.SetActive(false); // Tắt loading

                if (snapshot.Count == 0)
                {
                    // Kho trống, hiển thị lựa chọn kho mẫu
                    if (sampleInventorySelectionPanel != null)
                    {
                        sampleInventorySelectionPanel.SetActive(true);
                        if (inventoryStatusText != null) inventoryStatusText.text = "Kho hàng của bạn đang trống.";
                    }
                    else
                    {
                        if (inventoryStatusText != null) inventoryStatusText.text = "Kho trống. (Lỗi: Không tìm thấy panel lựa chọn mẫu)";
                        Debug.LogError("sampleInventorySelectionPanel không được gán trong Inspector.");
                    }
                }
                else
                {
                    // Kho có dữ liệu, ẩn lựa chọn kho mẫu
                    if (sampleInventorySelectionPanel != null) sampleInventorySelectionPanel.SetActive(false);
                    if (inventoryStatusText != null) inventoryStatusText.text = "Kho hàng của bạn có dữ liệu.";
                    LoadUserProducts(); // Hiển thị dữ liệu tồn kho hiện có
                }
            });
        }
        catch (Exception e)
        {
            Debug.LogError("Lỗi khi kiểm tra tồn kho người dùng: " + e.Message);
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                if (sampleLoadingPanel != null) sampleLoadingPanel.SetActive(false);
                if (inventoryStatusText != null) inventoryStatusText.text = "Lỗi khi tải kho hàng. Vui lòng thử lại.";
            });
        }
    }

    private async void OnCreateFromSampleButtonClicked()
    {
        if (industryDropdown == null)
        {
            Debug.LogError("Industry Dropdown không được gán.");
            if (sampleStatusText != null) sampleStatusText.text = "Lỗi: Vui lòng gán Dropdown Ngành hàng.";
            return;
        }

        string selectedIndustryName = industryDropdown.options[industryDropdown.value].text;
        string sampleIndustryId = "";
        if (sampleIndustries.TryGetValue(selectedIndustryName, out sampleIndustryId))
        {
            // ID đã được tìm thấy
        }
        else
        {
            Debug.LogError($"Không tìm thấy ID ngành hàng mẫu cho: {selectedIndustryName}");
            if (sampleStatusText != null) sampleStatusText.text = "Lỗi: Ngành hàng được chọn không hợp lệ.";
            return;
        }

        if (sampleLoadingPanel != null) sampleLoadingPanel.SetActive(true);
        if (sampleStatusText != null) sampleStatusText.text = $"Đang sao chép dữ liệu cho ngành {selectedIndustryName}...";
        SetPanelInteractable(sampleInventorySelectionPanel, false); // Vô hiệu hóa tương tác trong khi sao chép

        try
        {
            // 1. Tải dữ liệu từ kho mẫu
            CollectionReference sourceCollection = sampleInventoriesCollection.Document(sampleIndustryId).Collection("products");
            QuerySnapshot sampleProductsSnapshot = await sourceCollection.GetSnapshotAsync();

            if (sampleProductsSnapshot.Count == 0)
            {
                UnityMainThreadDispatcher.Instance().Enqueue(() => {
                    if (sampleStatusText != null) sampleStatusText.text = $"Kho mẫu '{selectedIndustryName}' trống. Không có gì để sao chép.";
                    if (sampleLoadingPanel != null) sampleLoadingPanel.SetActive(false);
                    SetPanelInteractable(sampleInventorySelectionPanel, true);
                });
                return;
            }

            // 2. Sao chép từng sản phẩm vào kho của người dùng
            int productsCopied = 0;
            WriteBatch batch = db.StartBatch(); // Sử dụng WriteBatch để ghi nhiều document hiệu quả

            foreach (DocumentSnapshot document in sampleProductsSnapshot.Documents)
            {
                // Lấy dữ liệu sản phẩm và chuyển đổi sang ProductData
                ProductData product = document.ConvertTo<ProductData>();
                
                // Firestore tự động tạo ID cho document nếu không chỉ định
                DocumentReference newProductRef = userProductsCollection.Document(); 
                
                // Set dữ liệu của ProductData vào document mới
                batch.Set(newProductRef, product); // Firestore sẽ tự động chuyển đổi ProductData về dictionary
                productsCopied++;
            }

            await batch.CommitAsync(); // Thực hiện ghi hàng loạt

            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                if (sampleStatusText != null) sampleStatusText.text = $"Đã sao chép thành công {productsCopied} sản phẩm từ kho mẫu '{selectedIndustryName}'.";
                if (sampleLoadingPanel != null) sampleLoadingPanel.SetActive(false);
                SetPanelInteractable(sampleInventorySelectionPanel, true);
                
                // Sau khi sao chép, ẩn panel chọn mẫu và tải lại kho hàng
                sampleInventorySelectionPanel.SetActive(false);
                LoadUserProducts(); // Tải lại kho hàng để hiển thị dữ liệu mới
            });
        }
        catch (Exception e)
        {
            Debug.LogError("Lỗi khi sao chép kho mẫu: " + e.Message);
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                if (sampleStatusText != null) sampleStatusText.text = $"Lỗi khi sao chép: {e.Message}";
                if (sampleLoadingPanel != null) sampleLoadingPanel.SetActive(false);
                SetPanelInteractable(sampleInventorySelectionPanel, true);
            });
        }
    }

    private void OnSkipSampleButtonClicked()
    {
        Debug.Log("Người dùng đã bỏ qua việc tạo kho mẫu.");
        if (sampleInventorySelectionPanel != null) sampleInventorySelectionPanel.SetActive(false);
        if (inventoryStatusText != null) inventoryStatusText.text = "Kho hàng của bạn đang trống. Bạn có thể thêm sản phẩm thủ công.";
        // TODO: Điều hướng người dùng đến màn hình thêm sản phẩm thủ công hoặc hiển thị UI trống
    }

    // Hàm SetPanelInteractable giống như trong AuthManager (có thể tạo một Utility class)
    private void SetPanelInteractable(GameObject panel, bool interactable)
    {
        if (panel != null)
        {
            CanvasGroup canvasGroup = panel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = panel.AddComponent<CanvasGroup>();
            }
            canvasGroup.interactable = interactable;
            canvasGroup.blocksRaycasts = interactable;
            canvasGroup.alpha = interactable ? 1f : 0.5f;
        }
    }

    // Hàm này sẽ tải sản phẩm từ kho của người dùng và gọi UpdateInventoryUI để hiển thị
    public async Task LoadUserProducts()
    {
        try
        {
            QuerySnapshot snapshot = await userProductsCollection.GetSnapshotAsync();
            List<ProductData> products = new List<ProductData>();
            foreach (DocumentSnapshot document in snapshot.Documents)
            {
                ProductData product = document.ConvertTo<ProductData>();
                products.Add(product);
            }

            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                UpdateInventoryUI(products); // Gọi hàm cập nhật UI trên Main Thread
                if (inventoryStatusText != null) inventoryStatusText.text = $"Đã tải {products.Count} sản phẩm.";
                if (sampleLoadingPanel != null) sampleLoadingPanel.SetActive(false); // Đảm bảo loading tắt
            });
        }
        catch (Exception e)
        {
            Debug.LogError("Lỗi khi tải sản phẩm của người dùng: " + e.Message);
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                if (inventoryStatusText != null) inventoryStatusText.text = "Lỗi khi tải sản phẩm.";
                if (sampleLoadingPanel != null) sampleLoadingPanel.SetActive(false);
            });
        }
    }

    // Hàm này sẽ cập nhật UI hiển thị danh sách sản phẩm
    public void UpdateInventoryUI(List<ProductData> products)
    {
        // 1. Xóa các sản phẩm cũ trên UI (nếu có) để tránh trùng lặp
        foreach (Transform child in productListContentParent)
        {
            Destroy(child.gameObject);
        }

        // 2. Tạo các GameObject UI mới cho từng sản phẩm và đổ dữ liệu
        if (products != null && products.Count > 0)
        {
            foreach (ProductData product in products)
            {
                GameObject productItemGO = Instantiate(productItemPrefab, productListContentParent);
                ProductUIItem uiItem = productItemGO.GetComponent<ProductUIItem>();
                if (uiItem != null)
                {
                    uiItem.SetProductData(product); // Gọi hàm để đổ dữ liệu vào Prefab
                }
                else
                {
                    Debug.LogWarning("Prefab productItemPrefab không có script ProductUIItem.");
                }
            }
        }
        else
        {
            // Hiển thị thông báo kho trống hoặc hướng dẫn người dùng
            Debug.Log("Không có sản phẩm nào để hiển thị.");
            if (inventoryStatusText != null) inventoryStatusText.text = "Kho hàng của bạn đang trống. Thêm sản phẩm hoặc tạo từ mẫu.";
            // Đảm bảo panel chọn mẫu được hiển thị nếu chưa có sản phẩm
            if (sampleInventorySelectionPanel != null && productListContentParent.childCount == 0)
            {
                sampleInventorySelectionPanel.SetActive(true);
            }
        }
    }

} // Đóng class InventoryManager
