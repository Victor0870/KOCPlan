using UnityEngine;
using System.Collections;
using Firebase.Auth;
using Firebase.Firestore;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine.UI;
using System;
using System.Linq; // Quan trọng để sử dụng LINQ (Where, OrderBy, ToList)

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

    [Header("Filter Dropdowns")]
    public TMP_Dropdown categoryFilterDropdown;
    public TMP_Dropdown manufacturerFilterDropdown;

    [Header("Inventory Summary UI")]
    public TMP_Text totalInventoryValueText; // Text để hiển thị tổng giá trị tồn kho
    public TMP_Text totalInventoryQuantityText; // Text để hiển thị tổng số lượng tồn kho

    // Thêm các biến UI mới cho tìm kiếm và sắp xếp
    [Header("Search & Sort UI")]
    public TMP_InputField searchInputField; // InputField để nhập chuỗi tìm kiếm
    public TMP_Dropdown sortDropdown; // Dropdown để chọn tùy chọn sắp xếp

    private FirebaseUser currentUser;
    private FirebaseFirestore db;
    private CollectionReference userProductsCollection; // Collection sản phẩm của người dùng
    private CollectionReference sampleInventoriesCollection; // Collection kho mẫu

    [Header("Inventory Display UI")]
    public Transform productListContentParent;
    public GameObject productItemPrefab;

    // Biến để lưu trữ TẤT CẢ sản phẩm đã tải về từ Firestore (để lọc/tìm kiếm/sắp xếp offline)
    private List<ProductData> allUserProducts = new List<ProductData>();

    // Biến để lưu trữ giá trị lọc/tìm kiếm/sắp xếp hiện tại
    private string currentCategoryFilter = "Tất cả"; // Giá trị mặc định
    private string currentManufacturerFilter = "Tất cả"; // Giá trị mặc định
    private string currentSearchTerm = ""; // Giá trị mặc định
    private string currentSortOption = "Tên (A-Z)"; // Giá trị mặc định cho sắp xếp

    // Danh sách các ngành hàng mẫu và ID Firestore tương ứng
    private Dictionary<string, string> sampleIndustries = new Dictionary<string, string>
    {
        { "Dược phẩm", "pharma" },
        { "Dầu bôi trơn", "dau_boi_tron" },
        { "Tạp hóa", "retail" }
    };

    void Awake()
    {
        // Kiểm tra người dùng đã đăng nhập
        if (AuthManager.Instance == null || AuthManager.Instance.GetCurrentUser() == null)
        {
            Debug.LogError("InventoryManager: Không tìm thấy AuthManager hoặc người dùng chưa đăng nhập. Vui lòng đăng nhập trước.");
            // Bạn có thể thêm code để chuyển cảnh về màn hình đăng nhập ở đây
            return;
        }

        currentUser = AuthManager.Instance.GetCurrentUser();
        db = FirebaseFirestore.DefaultInstance;

        // Khởi tạo các CollectionReference cho Firestore
        userProductsCollection = db.Collection("shops").Document(currentUser.UserId).Collection("products");
        sampleInventoriesCollection = db.Collection("sample_inventories");

        // Gắn sự kiện cho các nút và dropdown liên quan đến kho mẫu
        if (createFromSampleButton != null) createFromSampleButton.onClick.AddListener(OnCreateFromSampleButtonClicked);
        if (skipSampleButton != null) skipSampleButton.onClick.AddListener(OnSkipSampleButtonClicked);
        if (industryDropdown != null) PopulateIndustryDropdown();

        // Gắn sự kiện cho các Dropdown lọc
        if (categoryFilterDropdown != null)
        {
            categoryFilterDropdown.onValueChanged.AddListener(OnCategoryFilterChanged);
        }
        if (manufacturerFilterDropdown != null)
        {
            manufacturerFilterDropdown.onValueChanged.AddListener(OnManufacturerFilterChanged);
        }

        // Gắn sự kiện cho InputField tìm kiếm và Dropdown sắp xếp
        if (searchInputField != null)
        {
            searchInputField.onValueChanged.AddListener(OnSearchTermChanged);
        }
        if (sortDropdown != null)
        {
            sortDropdown.onValueChanged.AddListener(OnSortOptionChanged);
        }

        // Ẩn các panel liên quan đến kho mẫu khi khởi tạo
        if (sampleInventorySelectionPanel != null) sampleInventorySelectionPanel.SetActive(false);
        if (sampleLoadingPanel != null) sampleLoadingPanel.SetActive(false);
    }

    void Start()
    {
        // Khi ứng dụng khởi động, kiểm tra kho và tải toàn bộ dữ liệu (nếu có)
        CheckUserInventory();
        // Điền các tùy chọn cho dropdown sắp xếp
        PopulateSortDropdown();
    }

    void OnDestroy()
    {
        // Hủy đăng ký sự kiện để tránh lỗi khi GameObject bị hủy
        if (createFromSampleButton != null) createFromSampleButton.onClick.RemoveListener(OnCreateFromSampleButtonClicked);
        if (skipSampleButton != null) skipSampleButton.onClick.RemoveListener(OnSkipSampleButtonClicked);
        if (categoryFilterDropdown != null) categoryFilterDropdown.onValueChanged.RemoveListener(OnCategoryFilterChanged);
        if (manufacturerFilterDropdown != null) manufacturerFilterDropdown.onValueChanged.RemoveListener(OnManufacturerFilterChanged);
        if (searchInputField != null) searchInputField.onValueChanged.RemoveListener(OnSearchTermChanged);
        if (sortDropdown != null) sortDropdown.onValueChanged.RemoveListener(OnSortOptionChanged);
    }

    // Điền dữ liệu cho Dropdown chọn ngành hàng mẫu
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

    // Điền dữ liệu cho Dropdown sắp xếp
    private void PopulateSortDropdown()
    {
        if (sortDropdown == null) return;
        sortDropdown.ClearOptions();
        List<string> sortOptions = new List<string>
        {
            "Tên (A-Z)",
            "Tên (Z-A)",
            "Số lượng (Thấp-Cao)",
            "Số lượng (Cao-Thấp)",
            "Giá bán (Thấp-Cao)",
            "Giá bán (Cao-Thấp)"
        };
        sortDropdown.AddOptions(sortOptions);
        // Đảm bảo giá trị mặc định được chọn đúng sau khi điền
        sortDropdown.value = sortOptions.IndexOf(currentSortOption);
    }

    // Kiểm tra xem kho hàng của người dùng đã có dữ liệu chưa
    public async void CheckUserInventory()
    {
        if (inventoryStatusText != null) inventoryStatusText.text = "Đang kiểm tra kho hàng của bạn...";
        if (sampleLoadingPanel != null) sampleLoadingPanel.SetActive(true);

        try
        {
            // Chỉ đọc 1 document để kiểm tra xem collection có rỗng không
            QuerySnapshot snapshot = await userProductsCollection.Limit(1).GetSnapshotAsync();

            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                if (sampleLoadingPanel != null) sampleLoadingPanel.SetActive(false);

                if (snapshot.Count == 0)
                {
                    // Kho trống, hiển thị panel lựa chọn kho mẫu
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
                    // Kho có dữ liệu, ẩn panel lựa chọn kho mẫu
                    if (sampleInventorySelectionPanel != null) sampleInventorySelectionPanel.SetActive(false);
                    if (inventoryStatusText != null) inventoryStatusText.text = "Kho hàng của bạn có dữ liệu.";
                    
                    // Tải TẤT CẢ sản phẩm về bộ nhớ cục bộ để làm việc offline
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

    // Tải tất cả sản phẩm từ Firestore chỉ MỘT LẦN vào allUserProducts
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

            // Thêm tùy chọn mặc định cho filter dropdowns
            uniqueCategories.Add("Tất cả"); 
            uniqueManufacturers.Add("Tất cả"); 

            foreach (DocumentSnapshot document in snapshot.Documents)
            {
                ProductData product = document.ConvertTo<ProductData>();
                product.productId = document.Id; // Gán Firestore Document ID vào đối tượng ProductData
                allUserProducts.Add(product); // Lưu vào danh sách cục bộ

                // Thu thập các giá trị duy nhất cho filter dropdowns
                if (!string.IsNullOrEmpty(product.category)) uniqueCategories.Add(product.category);
                if (!string.IsNullOrEmpty(product.manufacturer)) uniqueManufacturers.Add(product.manufacturer);
            }

            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                // Điền dữ liệu cho các filter dropdowns sau khi tải xong sản phẩm
                PopulateCategoryFilterDropdown(new List<string>(uniqueCategories));
                PopulateManufacturerFilterDropdown(new List<string>(uniqueManufacturers));

                // Áp dụng bộ lọc/tìm kiếm/sắp xếp ban đầu và hiển thị danh sách
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

    // Điền tùy chọn cho Dropdown Nhóm hàng
    private void PopulateCategoryFilterDropdown(List<string> categories)
    {
        if (categoryFilterDropdown == null) return;
        categoryFilterDropdown.ClearOptions();
        categories.Sort(); // Sắp xếp theo thứ tự chữ cái
        categoryFilterDropdown.AddOptions(categories);
        // Thiết lập giá trị mặc định hoặc giá trị hiện tại
        categoryFilterDropdown.value = categories.IndexOf(currentCategoryFilter);
    }

    // Điền tùy chọn cho Dropdown Nhà sản xuất
    private void PopulateManufacturerFilterDropdown(List<string> manufacturers)
    {
        if (manufacturerFilterDropdown == null) return;
        manufacturerFilterDropdown.ClearOptions();
        manufacturers.Sort(); // Sắp xếp theo thứ tự chữ cái
        manufacturerFilterDropdown.AddOptions(manufacturers);
        // Thiết lập giá trị mặc định hoặc giá trị hiện tại
        manufacturerFilterDropdown.value = manufacturers.IndexOf(currentManufacturerFilter);
    }

    // Xử lý khi giá trị Dropdown Nhóm hàng thay đổi
    private void OnCategoryFilterChanged(int index)
    {
        currentCategoryFilter = categoryFilterDropdown.options[index].text;
        Debug.Log($"Lọc theo Nhóm hàng: {currentCategoryFilter}");
        ApplyFiltersAndDisplayProducts(); // Lọc trên dữ liệu cục bộ và cập nhật UI
    }

    // Xử lý khi giá trị Dropdown Nhà sản xuất thay đổi
    private void OnManufacturerFilterChanged(int index)
    {
        currentManufacturerFilter = manufacturerFilterDropdown.options[index].text;
        Debug.Log($"Lọc theo Nhà sản xuất: {currentManufacturerFilter}");
        ApplyFiltersAndDisplayProducts(); // Lọc trên dữ liệu cục bộ và cập nhật UI
    }

    // Xử lý khi chuỗi tìm kiếm trong InputField thay đổi
    private void OnSearchTermChanged(string newTerm)
    {
        currentSearchTerm = newTerm;
        Debug.Log($"Tìm kiếm: {currentSearchTerm}");
        ApplyFiltersAndDisplayProducts(); // Tìm kiếm trên dữ liệu cục bộ và cập nhật UI
    }

    // Xử lý khi tùy chọn sắp xếp trong Dropdown thay đổi
    private void OnSortOptionChanged(int index)
    {
        currentSortOption = sortDropdown.options[index].text;
        Debug.Log($"Sắp xếp theo: {currentSortOption}");
        ApplyFiltersAndDisplayProducts(); // Sắp xếp trên dữ liệu cục bộ và cập nhật UI
    }

    // Hàm chính: Áp dụng tất cả bộ lọc, tìm kiếm và sắp xếp trên dữ liệu cục bộ, sau đó cập nhật UI
    private void ApplyFiltersAndDisplayProducts()
    {
        // Bắt đầu với toàn bộ danh sách sản phẩm đã tải
        IEnumerable<ProductData> processedProducts = allUserProducts.AsEnumerable();

        // 1. Áp dụng bộ lọc Nhóm hàng (Category)
        if (currentCategoryFilter != "Tất cả")
        {
            processedProducts = processedProducts.Where(p => p.category == currentCategoryFilter);
        }

        // 2. Áp dụng bộ lọc Nhà sản xuất (Manufacturer)
        if (currentManufacturerFilter != "Tất cả")
        {
            processedProducts = processedProducts.Where(p => p.manufacturer == currentManufacturerFilter);
        }

        // 3. Áp dụng tìm kiếm theo chuỗi (nếu có)
        if (!string.IsNullOrEmpty(currentSearchTerm))
        {
            string lowerSearchTerm = currentSearchTerm.ToLower(); // Chuyển về chữ thường để tìm kiếm không phân biệt hoa/thường
            processedProducts = processedProducts.Where(p => 
                (p.productName != null && p.productName.ToLower().Contains(lowerSearchTerm)) || // Tìm theo tên sản phẩm
                (p.barcode != null && p.barcode.ToLower().Contains(lowerSearchTerm)) // Tìm theo barcode
            );
        }

        // 4. Áp dụng sắp xếp
        switch (currentSortOption)
        {
            case "Tên (A-Z)":
                processedProducts = processedProducts.OrderBy(p => p.productName);
                break;
            case "Tên (Z-A)":
                processedProducts = processedProducts.OrderByDescending(p => p.productName);
                break;
            case "Số lượng (Thấp-Cao)":
                processedProducts = processedProducts.OrderBy(p => p.stock);
                break;
            case "Số lượng (Cao-Thấp)":
                processedProducts = processedProducts.OrderByDescending(p => p.stock);
                break;
            case "Giá bán (Thấp-Cao)":
                processedProducts = processedProducts.OrderBy(p => p.price);
                break;
            case "Giá bán (Cao-Thấp)":
                processedProducts = processedProducts.OrderByDescending(p => p.price);
                break;
            default:
                processedProducts = processedProducts.OrderBy(p => p.productName); // Mặc định nếu không khớp
                break;
        }
        
        // Chuyển kết quả đã xử lý (lọc, tìm kiếm, sắp xếp) thành List để hiển thị
        List<ProductData> productsToDisplay = processedProducts.ToList();

        // --- Tính toán Tổng Giá trị và Tổng Số lượng tồn kho trên danh sách ĐÃ LỌC/TÌM KIẾM ---
        long totalValue = 0;
        long totalQuantity = 0;

        foreach (ProductData product in productsToDisplay)
        {
            totalQuantity += product.stock;
            totalValue += product.stock * product.price; // Giá trị tồn kho = số lượng * giá bán (giả sử giá bán là giá trị để tính)
        }

        // Cập nhật UI Text hiển thị tổng
        if (totalInventoryValueText != null)
        {
            totalInventoryValueText.text = $"Tổng giá trị: {totalValue:N0} VND"; // Định dạng số có dấu phẩy
        }
        if (totalInventoryQuantityText != null)
        {
            totalInventoryQuantityText.text = $"Tổng số lượng: {totalQuantity:N0} sản phẩm";
        }
        // --- Kết thúc tính toán tổng ---

        // Cập nhật giao diện người dùng hiển thị danh sách sản phẩm
        UpdateInventoryUI(productsToDisplay);
        // Cập nhật trạng thái hiển thị số lượng sản phẩm
        if (inventoryStatusText != null) inventoryStatusText.text = $"Đã hiển thị {productsToDisplay.Count} / {allUserProducts.Count} sản phẩm.";
    }

    // Xử lý khi nhấn nút "Tạo từ mẫu"
    private async void OnCreateFromSampleButtonClicked()
    {
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
        SetPanelInteractable(sampleInventorySelectionPanel, false); // Vô hiệu hóa panel để ngăn tương tác trong khi sao chép

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
            WriteBatch batch = db.StartBatch(); // Sử dụng WriteBatch để ghi nhiều document hiệu quả hơn

            foreach (DocumentSnapshot document in sampleProductsSnapshot.Documents)
            {
                ProductData product = document.ConvertTo<ProductData>();
                // Tạo một Document ID mới cho mỗi sản phẩm khi sao chép vào kho của người dùng
                DocumentReference newProductRef = userProductsCollection.Document(); 
                batch.Set(newProductRef, product); 
                productsCopied++;
            }

            await batch.CommitAsync(); // Gửi tất cả các thao tác ghi trong batch

            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                if (sampleStatusText != null) sampleStatusText.text = $"Đã sao chép thành công {productsCopied} sản phẩm từ kho mẫu '{selectedIndustryName}'.";
                if (sampleLoadingPanel != null) sampleLoadingPanel.SetActive(false);
                SetPanelInteractable(sampleInventorySelectionPanel, true);
                
                sampleInventorySelectionPanel.SetActive(false); // Ẩn panel chọn mẫu
                LoadAllUserProductsFromFirestore(); // Tải lại TOÀN BỘ kho hàng sau khi sao chép để cập nhật dữ liệu offline
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

    // Xử lý khi nhấn nút "Bỏ qua" (tạo kho mẫu)
    private void OnSkipSampleButtonClicked()
    {
        Debug.Log("Người dùng đã bỏ qua việc tạo kho mẫu.");
        if (sampleInventorySelectionPanel != null) sampleInventorySelectionPanel.SetActive(false);
        if (inventoryStatusText != null) inventoryStatusText.text = "Kho hàng của bạn đang trống. Bạn có thể thêm sản phẩm thủ công.";
    }

    // Hàm tiện ích để bật/tắt khả năng tương tác của một Panel UI
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
            canvasGroup.alpha = interactable ? 1f : 0.5f; // Giảm opacity khi không tương tác
        }
    }

    // Cập nhật giao diện danh sách sản phẩm hiển thị trên màn hình
    public void UpdateInventoryUI(List<ProductData> productsToDisplay)
    {
        // Xóa tất cả các item sản phẩm cũ trong danh sách
        foreach (Transform child in productListContentParent)
        {
            Destroy(child.gameObject);
        }

        // Nếu có sản phẩm để hiển thị, tạo các item mới
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
                    Debug.LogWarning("Prefab productItemPrefab không có script ProductUIItem. Vui lòng kiểm tra lại!");
                }
            }
        }
        else
        {
            Debug.Log("Không có sản phẩm nào để hiển thị sau khi áp dụng bộ lọc/tìm kiếm.");
            if (inventoryStatusText != null) inventoryStatusText.text = "Không tìm thấy sản phẩm phù hợp với bộ lọc/tìm kiếm.";
            // Không hiển thị panel mẫu khi không có sản phẩm sau khi lọc, chỉ khi kho trống hoàn toàn
        }
    }
}
