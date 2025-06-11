using UnityEngine;
using System.Collections;
using Firebase.Auth;
using Firebase.Firestore;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine.UI;
using System;
using System.Linq;

public class InventoryManager : MonoBehaviour
{
    [Header("UI References")]
    public GameObject inventoryContentPanel;
    public GameObject sampleInventorySelectionPanel;
    public TMP_Text inventoryStatusText;
    public TMP_Dropdown industryDropdown;
    public Button createFromSampleButton;
    public Button skipSampleButton;
    public GameObject sampleLoadingPanel;
    public TMP_Text sampleStatusText;
    public Transform productListContentParent;
    public GameObject productItemPrefab;

    [Header("Filter Dropdowns")]
    public TMP_Dropdown categoryFilterDropdown;
    public TMP_Dropdown manufacturerFilterDropdown;

    [Header("Inventory Summary UI")]
    public TMP_Text totalInventoryValueText;
    public TMP_Text totalInventoryQuantityText;

    [Header("Search & Sort UI")]
    public TMP_InputField searchInputField;
    public TMP_Dropdown sortDropdown;

    // THÊM CÁC TRƯỜNG NÀY CHO PANEL NHẬP KHO
    [Header("Import Stock Panel")]
    public ImportStockPanelManager importStockPanelManager; // Kéo script ImportStockPanelManager vào đây

    private FirebaseUser currentUser;
    private FirebaseFirestore db;
    private CollectionReference userProductsCollection;
    private CollectionReference sampleInventoriesCollection;

    private List<ProductData> allUserProducts = new List<ProductData>();
    private List<string> allCategories = new List<string>();
    private List<string> allManufacturers = new List<string>();

    void Awake()
    {
        db = FirebaseFirestore.DefaultInstance;
        auth = FirebaseAuth.DefaultInstance;
        auth.StateChanged += AuthStateChanged;

        sampleInventoriesCollection = db.Collection("sample_inventories");

        UnityMainThreadDispatcher.Instance();
    }

    void Start()
    {
        if (createFromSampleButton != null) createFromSampleButton.onClick.AddListener(OnCreateFromSampleButtonClicked);
        if (skipSampleButton != null) skipSampleButton.onClick.AddListener(OnSkipSampleButtonClicked);
        if (industryDropdown != null) industryDropdown.onValueChanged.AddListener(delegate { OnIndustryDropdownValueChanged(industryDropdown); });

        if (categoryFilterDropdown != null) categoryFilterDropdown.onValueChanged.AddListener(delegate { ApplyFiltersAndDisplayProducts(); });
        if (manufacturerFilterDropdown != null) manufacturerFilterDropdown.onValueChanged.AddListener(delegate { ApplyFiltersAndDisplayProducts(); });
        if (sortDropdown != null) sortDropdown.onValueChanged.AddListener(delegate { ApplyFiltersAndDisplayProducts(); });
        if (searchInputField != null) searchInputField.onValueChanged.AddListener(delegate { ApplyFiltersAndDisplayProducts(); });

        if (inventoryContentPanel != null) inventoryContentPanel.SetActive(false);
        if (sampleLoadingPanel != null) sampleLoadingPanel.SetActive(false);
        if (sampleInventorySelectionPanel != null) sampleInventorySelectionPanel.SetActive(false);
        
        // Đảm bảo panel nhập kho được ẩn khi bắt đầu
        if (importStockPanelManager != null) importStockPanelManager.HidePanel(); // Gọi hàm HidePanel của script kia

        LoadSampleIndustryList();
    }

    void OnDestroy()
    {
        if (auth != null)
        {
            auth.StateChanged -= AuthStateChanged;
        }
    }

    private void AuthStateChanged(object sender, EventArgs e)
    {
        FirebaseUser newUser = auth.CurrentUser;
        if (newUser != currentUser)
        {
            currentUser = newUser;
            if (currentUser != null)
            {
                Debug.Log($"InventoryManager: Người dùng đăng nhập: {currentUser.Email}");
                userProductsCollection = db.Collection("shops").Document(currentUser.UserId).Collection("products");
                CheckUserInventory();
            }
            else
            {
                Debug.Log("InventoryManager: Người dùng đã đăng xuất.");
                UnityEngine.SceneManagement.SceneManager.LoadScene("Login");
            }
        }
    }

    private async void LoadSampleIndustryList()
    {
        Debug.Log("Đang tải danh sách ngành hàng mẫu...");
        List<string> industries = new List<string>();
        industries.Add("Chọn ngành hàng");

        try
        {
            QuerySnapshot snapshot = await sampleInventoriesCollection.GetSnapshotAsync();
            foreach (DocumentSnapshot document in snapshot.Documents)
            {
                industries.Add(document.Id);
            }

            if (industryDropdown != null)
            {
                industryDropdown.ClearOptions();
                industryDropdown.AddOptions(industries);
            }
            Debug.Log($"Đã tải {industries.Count - 1} ngành hàng mẫu.");
        }
        catch (Exception e)
        {
            Debug.LogError($"Lỗi khi tải danh sách ngành hàng mẫu: {e.Message}");
        }
    }

    private void OnIndustryDropdownValueChanged(TMP_Dropdown dropdown)
    {
        SetButtonInteractable(createFromSampleButton, dropdown.value != 0);
    }

    private async void CheckUserInventory()
    {
        if (currentUser == null) return;

        Debug.Log("Đang kiểm tra kho người dùng...");
        if (inventoryStatusText != null) inventoryStatusText.text = "Đang kiểm tra kho...";

        try
        {
            QuerySnapshot querySnapshot = await userProductsCollection.GetSnapshotAsync();

            if (querySnapshot.Count == 0)
            {
                Debug.Log("Kho người dùng trống. Hiển thị panel chọn dữ liệu mẫu.");
                if (sampleInventorySelectionPanel != null) sampleInventorySelectionPanel.SetActive(true);
                if (inventoryContentPanel != null) inventoryContentPanel.SetActive(false);
                SetButtonInteractable(createFromSampleButton, false);
                if (inventoryStatusText != null) inventoryStatusText.text = "Kho trống. Vui lòng tạo từ mẫu hoặc bỏ qua.";
            }
            else
            {
                Debug.Log($"Kho người dùng có {querySnapshot.Count} sản phẩm. Hiển thị kho chính.");
                if (sampleInventorySelectionPanel != null) sampleInventorySelectionPanel.SetActive(false);
                if (inventoryContentPanel != null) inventoryContentPanel.SetActive(true);
                LoadAllUserProductsFromFirestore();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Lỗi khi kiểm tra kho người dùng: {e.Message}");
            if (inventoryStatusText != null) inventoryStatusText.text = $"Lỗi: {e.Message}";
            if (sampleInventorySelectionPanel != null) sampleInventorySelectionPanel.SetActive(true);
            if (inventoryContentPanel != null) inventoryContentPanel.SetActive(false);
        }
    }

    public void OnCreateFromSampleButtonClicked()
    {
        Debug.Log("Bắt đầu tạo kho từ dữ liệu mẫu...");
        if (industryDropdown.options.Count == 0 || industryDropdown.value >= industryDropdown.options.Count || industryDropdown.value == 0)
        {
            Debug.LogError("Chưa có ngành hàng nào được chọn hoặc dropdown trống/chọn mục mặc định.");
            sampleStatusText.text = "Lỗi: Vui lòng chọn một ngành hàng mẫu.";
            return;
        }

        string selectedIndustryId = industryDropdown.options[industryDropdown.value].text;
        Debug.Log($"Đang tạo kho từ ngành hàng mẫu: {selectedIndustryId}");

        if (sampleLoadingPanel != null) sampleLoadingPanel.SetActive(true);
        if (sampleStatusText != null) sampleStatusText.text = "Đang tải dữ liệu mẫu...";

        CollectionReference sampleProductsCollection = sampleInventoriesCollection.Document(selectedIndustryId).Collection("products");

        sampleProductsCollection.GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogError($"Lỗi khi lấy dữ liệu mẫu: {task.Exception}");
                if (sampleStatusText != null) sampleStatusText.text = $"Lỗi khi tải dữ liệu mẫu: {task.Exception.Message}";
                if (sampleLoadingPanel != null) sampleLoadingPanel.SetActive(false);
                return;
            }

            if (task.IsCompleted)
            {
                QuerySnapshot sampleSnapshot = task.Result;
                if (sampleSnapshot.Count == 0)
                {
                    Debug.LogWarning($"Không tìm thấy sản phẩm nào trong ngành hàng mẫu '{selectedIndustryId}'.");
                    if (sampleStatusText != null) sampleStatusText.text = $"Ngành hàng mẫu '{selectedIndustryId}' không có sản phẩm.";
                    if (sampleLoadingPanel != null) sampleLoadingPanel.SetActive(false);
                    return;
                }

                Debug.Log($"Tìm thấy {sampleSnapshot.Count} sản phẩm trong mẫu. Bắt đầu sao chép...");
                WriteBatch batch = db.StartBatch();
                int copiedCount = 0;

                foreach (DocumentSnapshot document in sampleSnapshot.Documents)
                {
                    ProductData productData = document.ConvertTo<ProductData>();
                    DocumentReference newProductRef = userProductsCollection.Document(); 
                    batch.Set(newProductRef, productData);
                    copiedCount++;
                }

                Debug.Log($"[Firestore Path Debug] Chuẩn bị ghi {copiedCount} sản phẩm vào đường dẫn: shops/{currentUser.UserId}/products");

                batch.CommitAsync().ContinueWithOnMainThread(commitTask =>
                {
                    if (sampleLoadingPanel != null) sampleLoadingPanel.SetActive(false);
                    if (commitTask.IsFaulted)
                    {
                        Debug.LogError($"Lỗi khi ghi dữ liệu mẫu vào kho người dùng: {commitTask.Exception}");
                        if (sampleStatusText != null) sampleStatusText.text = $"Lỗi khi tạo kho: {commitTask.Exception.Message}";
                    }
                    else if (commitTask.IsCompleted)
                    {
                        Debug.Log($"[Firestore Path Debug] Đã sao chép thành công {copiedCount} sản phẩm vào kho người dùng tại đường dẫn trên.");
                        if (sampleStatusText != null) sampleStatusText.text = "Tạo kho thành công!";
                        if (sampleInventorySelectionPanel != null) sampleInventorySelectionPanel.SetActive(false);
                        if (inventoryContentPanel != null) inventoryContentPanel.SetActive(true);

                        LoadAllUserProductsFromFirestore();
                    }
                });
            }
        });
    }

    public void OnSkipSampleButtonClicked()
    {
        Debug.Log("Người dùng chọn bỏ qua dữ liệu mẫu.");
        if (sampleInventorySelectionPanel != null) sampleInventorySelectionPanel.SetActive(false);
        if (inventoryContentPanel != null) inventoryContentPanel.SetActive(true);
        LoadAllUserProductsFromFirestore();
    }

    private void LoadAllUserProductsFromFirestore()
    {
        if (currentUser == null) return;

        Debug.Log("Đang tải tất cả sản phẩm của người dùng...");
        allUserProducts.Clear();
        if (inventoryStatusText != null) inventoryStatusText.text = "Đang tải sản phẩm...";

        userProductsCollection.GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogError($"Lỗi khi tải tất cả sản phẩm người dùng: {task.Exception}");
                if (inventoryStatusText != null) inventoryStatusText.text = "Lỗi khi tải sản phẩm.";
                return;
            }

            if (task.IsCompleted)
            {
                QuerySnapshot snapshot = task.Result;
                if (snapshot.Count == 0)
                {
                    Debug.Log("Kho người dùng trống sau khi tải lại.");
                    if (inventoryStatusText != null) inventoryStatusText.text = "Không có sản phẩm nào trong kho.";
                    UpdateInventorySummary(0, 0);
                    UpdateInventoryUI(new List<ProductData>());
                }
                else
                {
                    foreach (DocumentSnapshot document in snapshot.Documents)
                    {
                        ProductData product = document.ConvertTo<ProductData>();
                        product.productId = document.Id;
                        allUserProducts.Add(product);
                    }
                    Debug.Log($"Đã tải {allUserProducts.Count} sản phẩm.");
                    
                    PopulateFilterDropdowns();
                    ApplyFiltersAndDisplayProducts();
                    if (inventoryStatusText != null) inventoryStatusText.text = $"Tổng số sản phẩm: {allUserProducts.Count}";
                }
            }
        });
    }

    private void PopulateFilterDropdowns()
    {
        allCategories.Clear();
        allManufacturers.Clear();
        allCategories.Add("Tất cả danh mục");
        allManufacturers.Add("Tất cả nhà sản xuất");

        foreach (var product in allUserProducts)
        {
            if (!string.IsNullOrEmpty(product.category) && !allCategories.Contains(product.category))
            {
                allCategories.Add(product.category);
            }
            if (!string.IsNullOrEmpty(product.manufacturer) && !allManufacturers.Contains(product.manufacturer))
            {
                allManufacturers.Add(product.manufacturer);
            }
        }

        if (categoryFilterDropdown != null)
        {
            categoryFilterDropdown.ClearOptions();
            categoryFilterDropdown.AddOptions(allCategories);
        }
        if (manufacturerFilterDropdown != null)
        {
            manufacturerFilterDropdown.ClearOptions();
            manufacturerFilterDropdown.AddOptions(allManufacturers);
        }
    }


    private void ApplyFiltersAndDisplayProducts()
    {
        List<ProductData> filteredProducts = new List<ProductData>(allUserProducts);

        string searchText = searchInputField != null ? searchInputField.text.ToLower() : "";
        if (!string.IsNullOrEmpty(searchText))
        {
            filteredProducts = filteredProducts.Where(p =>
                p.productName.ToLower().Contains(searchText) ||
                p.barcode.ToLower().Contains(searchText) ||
                p.category.ToLower().Contains(searchText) ||
                p.manufacturer.ToLower().Contains(searchText)
            ).ToList();
        }

        if (categoryFilterDropdown != null && categoryFilterDropdown.value > 0)
        {
            string selectedCategory = categoryFilterDropdown.options[categoryFilterDropdown.value].text;
            filteredProducts = filteredProducts.Where(p => p.category == selectedCategory).ToList();
        }

        if (manufacturerFilterDropdown != null && manufacturerFilterDropdown.value > 0)
        {
            string selectedManufacturer = manufacturerFilterDropdown.options[manufacturerFilterDropdown.value].text;
            filteredProducts = filteredProducts.Where(p => p.manufacturer == selectedManufacturer).ToList();
        }

        if (sortDropdown != null)
        {
            string sortOption = sortDropdown.options[sortDropdown.value].text;
            switch (sortOption)
            {
                case "Tên A-Z":
                    filteredProducts = filteredProducts.OrderBy(p => p.productName).ToList();
                    break;
                case "Tên Z-A":
                    filteredProducts = filteredProducts.OrderByDescending(p => p.productName).ToList();
                    break;
                case "Giá tăng dần":
                    filteredProducts = filteredProducts.OrderBy(p => p.price).ToList();
                    break;
                case "Giá giảm dần":
                    filteredProducts = filteredProducts.OrderByDescending(p => p.price).ToList();
                    break;
                case "Tồn kho tăng dần":
                    filteredProducts = filteredProducts.OrderBy(p => p.stock).ToList();
                    break;
                case "Tồn kho giảm dần":
                    filteredProducts = filteredProducts.OrderByDescending(p => p.stock).ToList();
                    break;
            }
        }
        
        UpdateInventoryUI(filteredProducts);
        CalculateAndDisplayInventorySummary(filteredProducts);
    }

    private void CalculateAndDisplayInventorySummary(List<ProductData> products)
    {
        long totalValue = 0;
        long totalQuantity = 0;

        foreach (var product in products)
        {
            totalValue += product.price * product.stock;
            totalQuantity += product.stock;
        }

        if (totalInventoryValueText != null) totalInventoryValueText.text = $"Tổng giá trị: {totalValue:N0} VNĐ";
        if (totalInventoryQuantityText != null) totalInventoryQuantityText.text = $"Tổng số lượng: {totalQuantity}";
    }
    
    private void UpdateInventorySummary(long value, long quantity)
    {
        if (totalInventoryValueText != null) totalInventoryValueText.text = $"Tổng giá trị: {value:N0} VNĐ";
        if (totalInventoryQuantityText != null) totalInventoryQuantityText.text = $"Tổng số lượng: {quantity}";
    }

    private void SetButtonInteractable(Button button, bool interactable)
    {
        if (button != null)
        {
            button.interactable = interactable;
            CanvasGroup canvasGroup = button.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = button.gameObject.AddComponent<CanvasGroup>();
            }
            canvasGroup.alpha = interactable ? 1f : 0.5f;
        }
    }

    // Cập nhật giao diện danh sách sản phẩm hiển thị trên màn hình
    public void UpdateInventoryUI(List<ProductData> productsToDisplay)
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
                    // Đăng ký lắng nghe sự kiện từ ProductUIItem
                    uiItem.OnImportRequested.AddListener(HandleImportStockRequest); // <--- THÊM DÒNG NÀY
                }
                else
                {
                    Debug.LogWarning("Prefab productItemPrefab không có script ProductUIItem. Vui lòng kiểm tra lại!");
                }
            }
        }
        else
        {
            Debug.Log("Không có sản phẩm nào để hiển thị sau khi áp dụng bộ lọc/tìm kiếm.");
            if (inventoryStatusText != null) inventoryStatusText.text = "Không tìm thấy sản phẩm phù hợp với bộ lọc/tìm kiếm.";
        }
    }

    // Hàm xử lý yêu cầu nhập kho từ ProductUIItem
    private void HandleImportStockRequest(ProductData product)
    {
        Debug.Log($"Yêu cầu nhập kho cho sản phẩm: {product.productName} (ID: {product.productId})");
        if (importStockPanelManager != null)
        {
            // Hiển thị panel nhập kho và truyền dữ liệu sản phẩm, kèm theo một callback
            // Callback này sẽ được gọi khi ImportStockPanelManager hoàn tất cập nhật
            importStockPanelManager.ShowPanel(product, () => {
                // Khi callback được gọi, có nghĩa là tồn kho đã được cập nhật
                // Chúng ta cần tải lại toàn bộ dữ liệu để đảm bảo UI được đồng bộ
                LoadAllUserProductsFromFirestore(); 
            });
        }
        else
        {
            Debug.LogError("ImportStockPanelManager chưa được gán trong Inspector!");
        }
    }
}
