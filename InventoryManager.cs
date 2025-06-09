// File: InventoryManager.cs
using UnityEngine;
using System.Collections;
using Firebase.Auth;
using Firebase.Firestore;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine.UI; // Dành cho Dropdown
using System;
using System.Linq; // Để sử dụng LINQ cho lọc dữ liệu

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

    // Thêm các Dropdown lọc mới
    [Header("Filter Dropdowns")]
    public TMP_Dropdown categoryFilterDropdown;
    public TMP_Dropdown manufacturerFilterDropdown;

    private FirebaseUser currentUser;
    private FirebaseFirestore db;
    private CollectionReference userProductsCollection; // Collection sản phẩm của người dùng
    private CollectionReference sampleInventoriesCollection; // Collection kho mẫu

    [Header("Inventory Display UI")] 
    public Transform productListContentParent;
    public GameObject productItemPrefab;

    // Biến để lưu trữ TẤT CẢ sản phẩm đã tải về từ Firestore
    private List<ProductData> allUserProducts = new List<ProductData>(); 

    // Biến để lưu trữ giá trị lọc hiện tại
    private string currentCategoryFilter = "Tất cả"; 
    private string currentManufacturerFilter = "Tất cả"; 

    // Danh sách các ngành hàng mẫu và ID Firestore tương ứng
    private Dictionary<string, string> sampleIndustries = new Dictionary<string, string>
    {
        { "Dược phẩm", "pharma" },
        { "Dầu bôi trơn", "dau_boi_tron" },
        { "Tạp hóa", "retail" }
    };

    void Awake()
    {
        if (AuthManager.Instance == null || AuthManager.Instance.GetCurrentUser() == null)
        {
            Debug.LogError("InventoryManager: Không tìm thấy AuthManager hoặc người dùng chưa đăng nhập.");
            // Có thể thêm chuyển cảnh về LoginScene ở đây
            return;
        }

        currentUser = AuthManager.Instance.GetCurrentUser();
        db = FirebaseFirestore.DefaultInstance;

        userProductsCollection = db.Collection("shops").Document(currentUser.UserId).Collection("products");
        sampleInventoriesCollection = db.Collection("sample_inventories");

        if (createFromSampleButton != null) createFromSampleButton.onClick.AddListener(OnCreateFromSampleButtonClicked);
        if (skipSampleButton != null) skipSampleButton.onClick.AddListener(OnSkipSampleButtonClicked);

        if (industryDropdown != null) PopulateIndustryDropdown();

        // Gắn sự kiện cho các Dropdown lọc mới
        if (categoryFilterDropdown != null)
        {
            categoryFilterDropdown.onValueChanged.AddListener(OnCategoryFilterChanged);
        }
        if (manufacturerFilterDropdown != null)
        {
            manufacturerFilterDropdown.onValueChanged.AddListener(OnManufacturerFilterChanged);
        }

        if (sampleInventorySelectionPanel != null) sampleInventorySelectionPanel.SetActive(false);
        if (sampleLoadingPanel != null) sampleLoadingPanel.SetActive(false);
    }

    void Start()
    {
        // Khi bắt đầu, kiểm tra kho và nếu có, tải TẤT CẢ sản phẩm để làm việc offline
        CheckUserInventory(); 
    }

    void OnDestroy()
    {
        if (createFromSampleButton != null) createFromSampleButton.onClick.RemoveListener(OnCreateFromSampleButtonClicked);
        if (skipSampleButton != null) skipSampleButton.onClick.RemoveListener(OnSkipSampleButtonClicked);
        if (categoryFilterDropdown != null) categoryFilterDropdown.onValueChanged.RemoveListener(OnCategoryFilterChanged);
        if (manufacturerFilterDropdown != null) manufacturerFilterDropdown.onValueChanged.RemoveListener(OnManufacturerFilterChanged);
    }

    private void PopulateIndustryDropdown()
    {
        industryDropdown.ClearOptions();
        List<string> options = new List<string>();
        foreach (var entry in sampleIndustries)
        {
            options.Add(entry.Key);
        }
        industryDropdown.AddOptions(options);
    }

    public async void CheckUserInventory()
    {
        if (inventoryStatusText != null) inventoryStatusText.text = "Đang kiểm tra kho hàng của bạn...";
        if (sampleLoadingPanel != null) sampleLoadingPanel.SetActive(true);

        try
        {
            QuerySnapshot snapshot = await userProductsCollection.Limit(1).GetSnapshotAsync();

            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                if (sampleLoadingPanel != null) sampleLoadingPanel.SetActive(false);

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
                    
                    // Tải TẤT CẢ sản phẩm để làm việc offline
                    LoadAllUserProductsFromFirestore(); 
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

    // Hàm MỚI VÀ QUAN TRỌNG: Tải tất cả sản phẩm từ Firestore chỉ MỘT LẦN
    private async void LoadAllUserProductsFromFirestore()
    {
        if (inventoryStatusText != null) inventoryStatusText.text = "Đang tải tất cả sản phẩm...";
        if (sampleLoadingPanel != null) sampleLoadingPanel.SetActive(true);

        try
        {
            QuerySnapshot snapshot = await userProductsCollection.GetSnapshotAsync(); // Lấy tất cả sản phẩm
            allUserProducts.Clear(); // Xóa dữ liệu cũ (nếu có)
            HashSet<string> uniqueCategories = new HashSet<string>();
            HashSet<string> uniqueManufacturers = new HashSet<string>();

            uniqueCategories.Add("Tất cả"); // Tùy chọn mặc định cho filter
            uniqueManufacturers.Add("Tất cả"); // Tùy chọn mặc định cho filter

            foreach (DocumentSnapshot document in snapshot.Documents)
            {
                ProductData product = document.ConvertTo<ProductData>();
                product.productId = document.Id; // Gán Firestore Document ID vào đối tượng ProductData
                allUserProducts.Add(product); // Lưu vào danh sách cục bộ

                // Thu thập các giá trị duy nhất cho dropdown
                if (!string.IsNullOrEmpty(product.category)) uniqueCategories.Add(product.category);
                if (!string.IsNullOrEmpty(product.manufacturer)) uniqueManufacturers.Add(product.manufacturer);
            }

            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                PopulateCategoryFilterDropdown(new List<string>(uniqueCategories));
                PopulateManufacturerFilterDropdown(new List<string>(uniqueManufacturers));

                // Sau khi tải và điền filter, hiển thị toàn bộ danh sách (chưa lọc)
                ApplyFiltersAndDisplayProducts(); 
                
                if (sampleLoadingPanel != null) sampleLoadingPanel.SetActive(false);
                if (inventoryStatusText != null) inventoryStatusText.text = $"Đã tải {allUserProducts.Count} sản phẩm.";
            });
        }
        catch (Exception e)
        {
            Debug.LogError("Lỗi khi tải tất cả sản phẩm từ Firestore: " + e.Message);
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                if (sampleLoadingPanel != null) sampleLoadingPanel.SetActive(false);
                if (inventoryStatusText != null) inventoryStatusText.text = "Lỗi khi tải tất cả sản phẩm. Vui lòng thử lại.";
            });
        }
    }

    // Cập nhật hàm điền Dropdown để nhận danh sách options
    private void PopulateCategoryFilterDropdown(List<string> categories)
    {
        if (categoryFilterDropdown == null) return;
        categoryFilterDropdown.ClearOptions();
        categories.Sort();
        categoryFilterDropdown.AddOptions(categories);
        categoryFilterDropdown.value = categories.IndexOf(currentCategoryFilter);
    }

    private void PopulateManufacturerFilterDropdown(List<string> manufacturers)
    {
        if (manufacturerFilterDropdown == null) return;
        manufacturerFilterDropdown.ClearOptions();
        manufacturers.Sort();
        manufacturerFilterDropdown.AddOptions(manufacturers);
        manufacturerFilterDropdown.value = manufacturers.IndexOf(currentManufacturerFilter);
    }

    // Hàm xử lý khi giá trị Dropdown Nhóm hàng thay đổi
    private void OnCategoryFilterChanged(int index)
    {
        currentCategoryFilter = categoryFilterDropdown.options[index].text;
        Debug.Log($"Lọc theo Nhóm hàng: {currentCategoryFilter}");
        ApplyFiltersAndDisplayProducts(); // Chỉ lọc trên dữ liệu cục bộ
    }

    // Hàm xử lý khi giá trị Dropdown Nhà sản xuất thay đổi
    private void OnManufacturerFilterChanged(int index)
    {
        currentManufacturerFilter = manufacturerFilterDropdown.options[index].text;
        Debug.Log($"Lọc theo Nhà sản xuất: {currentManufacturerFilter}");
        ApplyFiltersAndDisplayProducts(); // Chỉ lọc trên dữ liệu cục bộ
    }

    // Hàm MỚI: Áp dụng bộ lọc trên dữ liệu cục bộ và hiển thị
    private void ApplyFiltersAndDisplayProducts()
    {
        // Sử dụng LINQ để lọc dữ liệu
        IEnumerable<ProductData> filteredQuery = allUserProducts.AsEnumerable();

        if (currentCategoryFilter != "Tất cả")
        {
            filteredQuery = filteredQuery.Where(p => p.category == currentCategoryFilter);
        }

        if (currentManufacturerFilter != "Tất cả")
        {
            filteredQuery = filteredQuery.Where(p => p.manufacturer == currentManufacturerFilter);
        }
        
        List<ProductData> filteredProducts = filteredQuery.ToList();

        UpdateInventoryUI(filteredProducts); // Cập nhật UI với danh sách đã lọc
        if (inventoryStatusText != null) inventoryStatusText.text = $"Đã hiển thị {filteredProducts.Count} / {allUserProducts.Count} sản phẩm.";
    }

    // Hàm khi tạo kho mẫu: Sau khi sao chép, cần tải lại TẤT CẢ sản phẩm
    private async void OnCreateFromSampleButtonClicked()
    {
        // ... (phần code xác định selectedIndustryId, hiển thị loading, vô hiệu hóa panel)

        string selectedIndustryName = industryDropdown.options[industryDropdown.value].text;
        string sampleIndustryId = "";
        if (!sampleIndustries.TryGetValue(selectedIndustryName, out sampleIndustryId))
        {
            Debug.LogError($"Không tìm thấy ID ngành hàng mẫu cho: {selectedIndustryName}");
            if (sampleStatusText != null) sampleStatusText.text = "Lỗi: Ngành hàng được chọn không hợp lệ.";
            return;
        }

        if (sampleLoadingPanel != null) sampleLoadingPanel.SetActive(true);
        if (sampleStatusText != null) sampleStatusText.text = $"Đang sao chép dữ liệu cho ngành {selectedIndustryName}...";
        SetPanelInteractable(sampleInventorySelectionPanel, false);

        try
        {
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

            int productsCopied = 0;
            WriteBatch batch = db.StartBatch();

            foreach (DocumentSnapshot document in sampleProductsSnapshot.Documents)
            {
                ProductData product = document.ConvertTo<ProductData>();
                // Cần đảm bảo productId của sản phẩm mẫu không bị trùng lặp nếu nó có sẵn trong mẫu
                // Nếu muốn đảm bảo ID là duy nhất của người dùng, nên tạo ID mới
                DocumentReference newProductRef = userProductsCollection.Document(); // Tạo ID mới cho document của user
                batch.Set(newProductRef, product); 
                productsCopied++;
            }

            await batch.CommitAsync();

            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                if (sampleStatusText != null) sampleStatusText.text = $"Đã sao chép thành công {productsCopied} sản phẩm từ kho mẫu '{selectedIndustryName}'.";
                if (sampleLoadingPanel != null) sampleLoadingPanel.SetActive(false);
                SetPanelInteractable(sampleInventorySelectionPanel, true);
                
                sampleInventorySelectionPanel.SetActive(false);
                LoadAllUserProductsFromFirestore(); // Tải lại TOÀN BỘ kho hàng sau khi sao chép
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
    }

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

    // Hàm này sẽ cập nhật UI hiển thị danh sách sản phẩm (chỉ nhận danh sách đã được lọc từ ApplyFiltersAndDisplayProducts)
    public void UpdateInventoryUI(List<ProductData> productsToDisplay) // Đổi tên biến để rõ ràng hơn
    {
        foreach (Transform child in productListContentParent)
        {
            Destroy(child.gameObject);
        }

        if (productsToDisplay != null && productsToDisplay.Count > 0)
        {
            foreach (ProductData product in productsToDisplay)
            {
                GameObject productItemGO = Instantiate(productItemPrefab, productListContentParent);
                ProductUIItem uiItem = productItemGO.GetComponent<ProductUIItem>();
                if (uiItem != null)
                {
                    uiItem.SetProductData(product);
                }
                else
                {
                    Debug.LogWarning("Prefab productItemPrefab không có script ProductUIItem.");
                }
            }
        }
        else
        {
            Debug.Log("Không có sản phẩm nào để hiển thị sau khi lọc.");
            if (inventoryStatusText != null) inventoryStatusText.text = "Không tìm thấy sản phẩm phù hợp với bộ lọc.";
            // Không hiển thị panel mẫu khi lọc, chỉ khi kho trống hoàn toàn
            // if (sampleInventorySelectionPanel != null && productListContentParent.childCount == 0)
            // {
            //     sampleInventorySelectionPanel.SetActive(true);
            // }
        }
    }
}
