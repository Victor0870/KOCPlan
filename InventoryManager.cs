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
    private CollectionReference userProductsCollection;
    private ListenerRegistration productsListenerRegistration; // Biến để lưu trữ đăng ký listener

    private List<ProductData> allUserProducts = new List<ProductData>(); // Cache các sản phẩm trong kho

    // Các biến để theo dõi bộ lọc và tìm kiếm hiện tại
    private string currentSearchText = "";
    private string currentCategoryFilter = "Tất cả";
    private string currentManufacturerFilter = "Tất cả";
    private string currentSortOption = "productNameAsc"; // Mặc định sắp xếp theo tên A-Z

    [Header("Product Item UI")]
    public Transform productListContentParent; // Kéo GameObject Content của ScrollView vào đây
    public GameObject productItemPrefab; // Kéo Prefab ProductUIItem vào đây

    [Header("Product Edit/Add Panel UI")]
    public GameObject editProductPanel; // Panel để chỉnh sửa/thêm sản phẩm
    public TMP_InputField productNameInputField;
    public TMP_InputField unitInputField;
    public TMP_InputField priceInputField;
    public TMP_InputField importPriceInputField;
    public TMP_InputField barcodeInputField;
    public TMP_InputField imageUrlInputField;
    public TMP_InputField stockInputField;
    public TMP_Dropdown categoryDropdown; // Dropdown cho Category
    public TMP_Dropdown manufacturerDropdown; // Dropdown cho Manufacturer
    public Button saveProductButton;
    public Button cancelEditButton;
    public Button addProductButton;
    public Button deleteProductButton; // Nút xóa sản phẩm

    private ProductData productToEdit; // Lưu trữ sản phẩm đang được chỉnh sửa

    void Awake()
    {
        db = FirebaseFirestore.DefaultInstance;
        FirebaseAuth auth = FirebaseAuth.DefaultInstance;
        auth.StateChanged += AuthStateChanged;

        // Đảm bảo UnityMainThreadDispatcher đã được khởi tạo
        UnityMainThreadDispatcher.Instance();

        // Khởi tạo trạng thái UI
        if (inventoryContentPanel != null) inventoryContentPanel.SetActive(false);
        if (sampleInventorySelectionPanel != null) sampleInventorySelectionPanel.SetActive(false);
        if (editProductPanel != null) editProductPanel.SetActive(false);
        if (sampleLoadingPanel != null) sampleLoadingPanel.SetActive(false);

        // Khởi tạo Dropdown category và manufacturer
        InitializeFilterDropdowns();
    }

    void Start()
    {
        // Gán sự kiện cho các nút và input fields
        if (createFromSampleButton != null) createFromSampleButton.onClick.AddListener(OnCreateFromSampleButtonClicked);
        if (skipSampleButton != null) skipSampleButton.onClick.AddListener(OnSkipSampleButtonClicked);

        // Gán sự kiện cho các Dropdown lọc và tìm kiếm
        if (categoryFilterDropdown != null) categoryFilterDropdown.onValueChanged.AddListener(delegate { OnFilterChanged(); });
        if (manufacturerFilterDropdown != null) manufacturerFilterDropdown.onValueChanged.AddListener(delegate { OnFilterChanged(); });
        if (searchInputField != null) searchInputField.onEndEdit.AddListener(delegate { OnSearchOrSortChanged(); });
        if (sortDropdown != null) sortDropdown.onValueChanged.AddListener(delegate { OnSearchOrSortChanged(); });

        // Gán sự kiện cho nút thêm sản phẩm mới
        if (addProductButton != null) addProductButton.onClick.AddListener(OnAddProductButtonClicked);
    }

    void OnDestroy()
    {
        FirebaseAuth auth = FirebaseAuth.DefaultInstance;
        if (auth != null)
        {
            auth.StateChanged -= AuthStateChanged;
        }
        // Hủy đăng ký listener khi đối tượng bị hủy
        if (productsListenerRegistration != null)
        {
            productsListenerRegistration.Dispose();
        }
    }

    private void AuthStateChanged(object sender, EventArgs e)
    {
        FirebaseUser newUser = FirebaseAuth.DefaultInstance.CurrentUser;
        if (newUser != currentUser)
        {
            currentUser = newUser;
            if (currentUser != null)
            {
                userProductsCollection = db.Collection("shops").Document(currentUser.UserId).Collection("products");
                Debug.Log($"InventoryManager: User products collection initialized for UID: {currentUser.UserId}");
                CheckAndLoadUserInventory();
            }
            else
            {
                userProductsCollection = null;
                Debug.Log("InventoryManager: User logged out.");
                // Hủy đăng ký listener khi người dùng đăng xuất
                if (productsListenerRegistration != null)
                {
                    productsListenerRegistration.Dispose();
                    productsListenerRegistration = null;
                }
                // Reset UI hoặc chuyển về màn hình đăng nhập nếu cần
            }
        }
    }

    private async void CheckAndLoadUserInventory()
    {
        if (userProductsCollection == null) return;

        try
        {
            // Kiểm tra xem người dùng đã có sản phẩm trong kho hay chưa
            QuerySnapshot snapshot = await userProductsCollection.Limit(1).GetSnapshotAsync();

            if (snapshot.Count > 0)
            {
                Debug.Log("Người dùng đã có dữ liệu tồn kho. Tải dữ liệu.");
                // Nếu đã có dữ liệu, chuyển sang chế độ hiển thị tồn kho và thiết lập listener
                if (inventoryContentPanel != null) inventoryContentPanel.SetActive(true);
                if (sampleInventorySelectionPanel != null) sampleInventorySelectionPanel.SetActive(false);
                if (inventoryStatusText != null) inventoryStatusText.text = "Đang tải dữ liệu tồn kho...";
                
                // --- THAY ĐỔI LỚN TẠI ĐÂY: Sử dụng SnapshotListener ---
                SetupProductsListener();
            }
            else
            {
                Debug.Log("Người dùng chưa có dữ liệu tồn kho. Hiển thị tùy chọn kho mẫu.");
                // Nếu chưa có, hiển thị panel chọn kho mẫu
                if (inventoryContentPanel != null) inventoryContentPanel.SetActive(false);
                if (sampleInventorySelectionPanel != null) sampleInventorySelectionPanel.SetActive(true);
                if (inventoryStatusText != null) inventoryStatusText.text = "Bạn chưa có sản phẩm nào. Vui lòng tạo từ kho mẫu hoặc thêm mới.";
                LoadIndustryDropdown(); // Tải các lựa chọn ngành hàng cho kho mẫu
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Lỗi khi kiểm tra tồn kho: {e.Message}");
            if (inventoryStatusText != null) inventoryStatusText.text = $"Lỗi: {e.Message}";
        }
    }

    private void SetupProductsListener()
    {
        if (userProductsCollection == null) return;

        // Hủy đăng ký listener cũ nếu có
        if (productsListenerRegistration != null)
        {
            productsListenerRegistration.Dispose();
        }

        // Thiết lập listener mới
        productsListenerRegistration = userProductsCollection.Listen(snapshot =>
        {
            // Đảm bảo cập nhật UI trên Main Thread
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                Debug.Log($"Nhận được cập nhật từ Firestore. Số lượng thay đổi: {snapshot.Changes.Count}");
                foreach (DocumentChange change in snapshot.Changes)
                {
                    ProductData product = change.Document.ConvertTo<ProductData>();
                    product.productId = change.Document.Id; // Gán ID của document

                    switch (change.ChangeType)
                    {
                        case DocumentChange.Type.Added:
                            // Nếu sản phẩm mới, thêm vào danh sách
                            if (!allUserProducts.Any(p => p.productId == product.productId))
                            {
                                allUserProducts.Add(product);
                            }
                            break;
                        case DocumentChange.Type.Modified:
                            // Nếu sản phẩm đã thay đổi, cập nhật trong danh sách
                            int index = allUserProducts.FindIndex(p => p.productId == product.productId);
                            if (index != -1)
                            {
                                allUserProducts[index] = product;
                            }
                            break;
                        case DocumentChange.Type.Removed:
                            // Nếu sản phẩm bị xóa, loại bỏ khỏi danh sách
                            allUserProducts.RemoveAll(p => p.productId == product.productId);
                            break;
                    }
                }
                // Sau khi cập nhật allUserProducts, hiển thị lại UI
                ApplyFiltersAndSortAndDisplay();
                UpdateInventorySummary();
                if (inventoryStatusText != null) inventoryStatusText.text = "Tồn kho đã được cập nhật.";
            });
        });
    }

    // --- Các hàm đã có từ trước, giữ nguyên hoặc điều chỉnh nhẹ ---

    private void LoadIndustryDropdown()
    {
        // Implement logic to load industries from sample_data.json or directly from Firestore if you have a separate industries collection
        // For now, hardcode some options or load from sample_data.json locally
        if (industryDropdown != null)
        {
            industryDropdown.ClearOptions();
            List<string> options = new List<string> { "Dược phẩm" /*, "Bán lẻ", "Thời trang" */ }; // Ví dụ
            industryDropdown.AddOptions(options);
            industryDropdown.value = 0;
            industryDropdown.RefreshShownValue();
        }
    }

    public async void OnCreateFromSampleButtonClicked()
    {
        if (currentUser == null || userProductsCollection == null)
        {
            Debug.LogError("Người dùng chưa đăng nhập hoặc collection sản phẩm chưa được khởi tạo.");
            if (inventoryStatusText != null) inventoryStatusText.text = "Lỗi: Vui lòng đăng nhập lại.";
            return;
        }

        string selectedIndustry = industryDropdown.options[industryDropdown.value].text;
        Debug.Log($"Đang tạo kho từ mẫu cho ngành: {selectedIndustry}");

        if (sampleLoadingPanel != null) sampleLoadingPanel.SetActive(true);
        if (sampleStatusText != null) sampleStatusText.text = "Đang tải dữ liệu mẫu...";

        try
        {
            // Tải dữ liệu mẫu từ Firestore
            // Thay vì tải toàn bộ sample_data.json từ local, chúng ta sẽ đọc từ Firestore
            // Giả định cấu trúc Firestore: sample_inventories/{industryKey}/products/{productId}
            CollectionReference sampleProductsCollection = db.Collection("sample_inventories").Document(selectedIndustry.ToLower()).Collection("products");
            QuerySnapshot sampleSnapshot = await sampleProductsCollection.GetSnapshotAsync();

            List<ProductData> productsToAdd = new List<ProductData>();
            foreach (DocumentSnapshot doc in sampleSnapshot.Documents)
            {
                ProductData product = doc.ConvertTo<ProductData>();
                product.productId = doc.Id; // Giữ nguyên ID từ mẫu
                product.stock = 0; // Đặt tồn kho về 0 khi thêm từ mẫu
                productsToAdd.Add(product);
            }

            if (sampleStatusText != null) sampleStatusText.text = $"Đã tải {productsToAdd.Count} sản phẩm mẫu. Đang ghi vào kho của bạn...";

            // Sử dụng WriteBatch để ghi nhiều sản phẩm vào kho của người dùng
            WriteBatch batch = db.StartBatch();
            foreach (ProductData product in productsToAdd)
            {
                // Sử dụng ID từ dữ liệu mẫu làm ID document trong kho của người dùng
                DocumentReference productDocRef = userProductsCollection.Document(product.productId);
                batch.Set(productDocRef, product);
            }
            await batch.CommitAsync();

            // Cập nhật trạng thái firstLoginCompleted của người dùng
            DocumentReference userDocRef = db.Collection("users").Document(currentUser.UserId);
            await userDocRef.UpdateAsync("firstLoginCompleted", true);

            Debug.Log($"Đã tạo kho từ mẫu {selectedIndustry} thành công.");
            if (sampleStatusText != null) sampleStatusText.text = "Tạo kho thành công!";
            if (sampleLoadingPanel != null) sampleLoadingPanel.SetActive(false);

            // Sau khi tạo thành công, ẩn panel chọn mẫu và hiển thị kho chính
            if (sampleInventorySelectionPanel != null) sampleInventorySelectionPanel.SetActive(false);
            if (inventoryContentPanel != null) inventoryContentPanel.SetActive(true);

            // Tải lại dữ liệu tồn kho bằng listener mới
            SetupProductsListener();

        }
        catch (Exception e)
        {
            Debug.LogError($"Lỗi khi tạo kho từ mẫu: {e.Message}");
            if (sampleStatusText != null) sampleStatusText.text = $"Lỗi: {e.Message}";
            if (sampleLoadingPanel != null) sampleLoadingPanel.SetActive(false);
        }
    }

    public async void OnSkipSampleButtonClicked()
    {
        if (currentUser == null) return;

        // Cập nhật trạng thái firstLoginCompleted của người dùng
        DocumentReference userDocRef = db.Collection("users").Document(currentUser.UserId);
        try
        {
            await userDocRef.UpdateAsync("firstLoginCompleted", true);
            Debug.Log("Người dùng đã bỏ qua tạo kho mẫu.");

            // Ẩn panel chọn mẫu và hiển thị kho chính (lúc này sẽ trống)
            if (sampleInventorySelectionPanel != null) sampleInventorySelectionPanel.SetActive(false);
            if (inventoryContentPanel != null) inventoryContentPanel.SetActive(true);
            if (inventoryStatusText != null) inventoryStatusText.text = "Kho hàng của bạn trống. Vui lòng thêm sản phẩm mới.";

            // Thiết lập listener để lắng nghe các sản phẩm (sẽ là danh sách rỗng ban đầu)
            SetupProductsListener();
        }
        catch (Exception e)
        {
            Debug.LogError($"Lỗi khi bỏ qua tạo kho mẫu: {e.Message}");
        }
    }

    private void InitializeFilterDropdowns()
    {
        // Cần tải động các danh mục và nhà sản xuất từ dữ liệu sản phẩm
        // Nhưng ban đầu có thể có các tùy chọn "Tất cả"
        if (categoryFilterDropdown != null)
        {
            categoryFilterDropdown.ClearOptions();
            categoryFilterDropdown.options.Add(new TMP_Dropdown.OptionData("Tất cả"));
            // Sẽ thêm các category thực tế sau khi tải sản phẩm
        }
        if (manufacturerFilterDropdown != null)
        {
            manufacturerFilterDropdown.ClearOptions();
            manufacturerFilterDropdown.options.Add(new TMP_Dropdown.OptionData("Tất cả"));
            // Sẽ thêm các manufacturer thực tế sau khi tải sản phẩm
        }

        // Setup Sort Dropdown options
        if (sortDropdown != null)
        {
            sortDropdown.ClearOptions();
            sortDropdown.options.Add(new TMP_Dropdown.OptionData("Tên (A-Z)"));
            sortDropdown.options.Add(new TMP_Dropdown.OptionData("Tên (Z-A)"));
            sortDropdown.options.Add(new TMP_Dropdown.OptionData("Giá (Thấp - Cao)"));
            sortDropdown.options.Add(new TMP_Dropdown.OptionData("Giá (Cao - Thấp)"));
            sortDropdown.options.Add(new TMP_Dropdown.OptionData("Tồn kho (Thấp - Cao)"));
            sortDropdown.options.Add(new TMP_Dropdown.OptionData("Tồn kho (Cao - Thấp)"));
            sortDropdown.value = 0; // Mặc định là Tên (A-Z)
            sortDropdown.RefreshShownValue();
        }
    }

    private void UpdateFilterDropdownOptions()
    {
        // Lấy tất cả các category và manufacturer duy nhất từ `allUserProducts`
        HashSet<string> categories = new HashSet<string>(allUserProducts.Select(p => p.category).Where(c => !string.IsNullOrEmpty(c)).Distinct());
        HashSet<string> manufacturers = new HashSet<string>(allUserProducts.Select(p => p.manufacturer).Where(m => !string.IsNullOrEmpty(m)).Distinct());

        // Cập nhật Dropdown Category
        if (categoryFilterDropdown != null)
        {
            string currentSelectedCategory = categoryFilterDropdown.options[categoryFilterDropdown.value].text;
            categoryFilterDropdown.ClearOptions();
            List<string> categoryOptions = new List<string> { "Tất cả" };
            categoryOptions.AddRange(categories.OrderBy(c => c));
            categoryFilterDropdown.AddOptions(categoryOptions);
            // Cố gắng chọn lại giá trị đã chọn trước đó nếu vẫn còn
            int index = categoryOptions.IndexOf(currentSelectedCategory);
            categoryFilterDropdown.value = (index != -1) ? index : 0;
            categoryFilterDropdown.RefreshShownValue();
        }

        // Cập nhật Dropdown Manufacturer
        if (manufacturerFilterDropdown != null)
        {
            string currentSelectedManufacturer = manufacturerFilterDropdown.options[manufacturerFilterDropdown.value].text;
            manufacturerFilterDropdown.ClearOptions();
            List<string> manufacturerOptions = new List<string> { "Tất cả" };
            manufacturerOptions.AddRange(manufacturers.OrderBy(m => m));
            manufacturerFilterDropdown.AddOptions(manufacturerOptions);
            // Cố gắng chọn lại giá trị đã chọn trước đó nếu vẫn còn
            int index = manufacturerOptions.IndexOf(currentSelectedManufacturer);
            manufacturerFilterDropdown.value = (index != -1) ? index : 0;
            manufacturerFilterDropdown.RefreshShownValue();
        }
    }

    public void OnFilterChanged()
    {
        currentCategoryFilter = categoryFilterDropdown.options[categoryFilterDropdown.value].text;
        currentManufacturerFilter = manufacturerFilterDropdown.options[manufacturerFilterDropdown.value].text;
        ApplyFiltersAndSortAndDisplay();
    }

    public void OnSearchOrSortChanged()
    {
        currentSearchText = searchInputField.text.Trim();
        currentSortOption = sortDropdown.options[sortDropdown.value].text;
        ApplyFiltersAndSortAndDisplay();
    }


    private void ApplyFiltersAndSortAndDisplay()
    {
        IEnumerable<ProductData> filteredProducts = allUserProducts;

        // Apply Search Filter
        if (!string.IsNullOrEmpty(currentSearchText))
        {
            filteredProducts = filteredProducts.Where(p =>
                p.productName.IndexOf(currentSearchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                p.barcode.IndexOf(currentSearchText, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        // Apply Category Filter
        if (currentCategoryFilter != "Tất cả")
        {
            filteredProducts = filteredProducts.Where(p => p.category == currentCategoryFilter);
        }

        // Apply Manufacturer Filter
        if (currentManufacturerFilter != "Tất cả")
        {
            filteredProducts = filteredProducts.Where(p => p.manufacturer == currentManufacturerFilter);
        }

        // Apply Sorting
        switch (currentSortOption)
        {
            case "Tên (A-Z)":
                filteredProducts = filteredProducts.OrderBy(p => p.productName);
                break;
            case "Tên (Z-A)":
                filteredProducts = filteredProducts.OrderByDescending(p => p.productName);
                break;
            case "Giá (Thấp - Cao)":
                filteredProducts = filteredProducts.OrderBy(p => p.price);
                break;
            case "Giá (Cao - Thấp)":
                filteredProducts = filteredProducts.OrderByDescending(p => p.price);
                break;
            case "Tồn kho (Thấp - Cao)":
                filteredProducts = filteredProducts.OrderBy(p => p.stock);
                break;
            case "Tồn kho (Cao - Thấp)":
                filteredProducts = filteredProducts.OrderByDescending(p => p.stock);
                break;
        }

        // Cập nhật lại các dropdown sau khi có dữ liệu mới
        UpdateFilterDropdownOptions();
        UpdateInventoryUI(filteredProducts.ToList());
    }

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
                    // Đăng ký sự kiện chỉnh sửa/xóa cho từng item
                    uiItem.OnEditClicked.RemoveAllListeners(); // Xóa listener cũ trước khi thêm mới để tránh trùng lặp
                    uiItem.OnEditClicked.AddListener(EditProduct);
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

    private void UpdateInventorySummary()
    {
        long totalValue = allUserProducts.Sum(p => p.stock * p.price);
        long totalQuantity = allUserProducts.Sum(p => p.stock);

        if (totalInventoryValueText != null) totalInventoryValueText.text = $"Tổng giá trị tồn kho: {totalValue:N0} VNĐ";
        if (totalInventoryQuantityText != null) totalInventoryQuantityText.text = $"Tổng số lượng tồn kho: {totalQuantity:N0}";
    }

    public void OnAddProductButtonClicked()
    {
        productToEdit = null; // Đánh dấu là đang thêm mới
        PopulateEditPanel(null); // Đặt các trường về trống
        SetEditPanelInteractable(true, true); // Cho phép nhập và hiển thị nút Add
        if (editProductPanel != null) editProductPanel.SetActive(true);
        if (deleteProductButton != null) deleteProductButton.gameObject.SetActive(false); // Ẩn nút xóa khi thêm mới
    }

    public void EditProduct(ProductData product)
    {
        productToEdit = product; // Lưu sản phẩm cần chỉnh sửa
        PopulateEditPanel(product); // Điền thông tin vào panel chỉnh sửa
        SetEditPanelInteractable(true, false); // Cho phép nhập và hiển thị nút Save
        if (editProductPanel != null) editProductPanel.SetActive(true);
        if (deleteProductButton != null) deleteProductButton.gameObject.SetActive(true); // Hiển thị nút xóa khi chỉnh sửa
    }

    private void PopulateEditPanel(ProductData product)
    {
        if (product == null)
        {
            // Reset fields for new product
            if (productNameInputField != null) productNameInputField.text = "";
            if (unitInputField != null) unitInputField.text = "";
            if (priceInputField != null) priceInputField.text = "0";
            if (importPriceInputField != null) importPriceInputField.text = "0";
            if (barcodeInputField != null) barcodeInputField.text = "";
            if (imageUrlInputField != null) imageUrlInputField.text = "";
            if (stockInputField != null) stockInputField.text = "0";
            // Set dropdowns to default (first option)
            if (categoryDropdown != null) categoryDropdown.value = 0;
            if (manufacturerDropdown != null) manufacturerDropdown.value = 0;
        }
        else
        {
            // Populate fields with existing product data
            if (productNameInputField != null) productNameInputField.text = product.productName;
            if (unitInputField != null) unitInputField.text = product.unit;
            if (priceInputField != null) priceInputField.text = product.price.ToString();
            if (importPriceInputField != null) importPriceInputField.text = product.importPrice.ToString();
            if (barcodeInputField != null) barcodeInputField.text = product.barcode;
            if (imageUrlInputField != null) imageUrlInputField.text = product.imageUrl;
            if (stockInputField != null) stockInputField.text = product.stock.ToString();

            // Select correct options in dropdowns
            if (categoryDropdown != null)
            {
                int index = categoryDropdown.options.FindIndex(option => option.text == product.category);
                categoryDropdown.value = (index != -1) ? index : 0;
            }
            if (manufacturerDropdown != null)
            {
                int index = manufacturerDropdown.options.FindIndex(option => option.text == product.manufacturer);
                manufacturerDropdown.value = (index != -1) ? index : 0;
            }
        }
        PopulateCategoryAndManufacturerDropdownsInEditPanel(); // Cập nhật options cho dropdown trong edit panel
    }

    private void PopulateCategoryAndManufacturerDropdownsInEditPanel()
    {
        // Lấy tất cả các category và manufacturer duy nhất từ `allUserProducts`
        HashSet<string> categories = new HashSet<string>(allUserProducts.Select(p => p.category).Where(c => !string.IsNullOrEmpty(c)).Distinct());
        HashSet<string> manufacturers = new HashSet<string>(allUserProducts.Select(p => p.manufacturer).Where(m => !string.IsNullOrEmpty(m)).Distinct());

        if (categoryDropdown != null)
        {
            string currentSelectedCategory = categoryDropdown.options.Count > 0 ? categoryDropdown.options[categoryDropdown.value].text : "";
            categoryDropdown.ClearOptions();
            List<string> categoryOptions = new List<string>();
            categoryOptions.AddRange(categories.OrderBy(c => c)); // Thêm các category hiện có
            categoryDropdown.AddOptions(categoryOptions);
            // Chọn lại giá trị nếu có, nếu không thì chọn mặc định 0 hoặc để trống
            if (!string.IsNullOrEmpty(currentSelectedCategory) && categoryOptions.Contains(currentSelectedCategory))
            {
                categoryDropdown.value = categoryOptions.IndexOf(currentSelectedCategory);
            }
            else if (categoryOptions.Count > 0)
            {
                categoryDropdown.value = 0;
            }
            categoryDropdown.RefreshShownValue();
        }

        if (manufacturerDropdown != null)
        {
            string currentSelectedManufacturer = manufacturerDropdown.options.Count > 0 ? manufacturerDropdown.options[manufacturerDropdown.value].text : "";
            manufacturerDropdown.ClearOptions();
            List<string> manufacturerOptions = new List<string>();
            manufacturerOptions.AddRange(manufacturers.OrderBy(m => m)); // Thêm các manufacturer hiện có
            manufacturerDropdown.AddOptions(manufacturerOptions);
            // Chọn lại giá trị nếu có, nếu không thì chọn mặc định 0 hoặc để trống
            if (!string.IsNullOrEmpty(currentSelectedManufacturer) && manufacturerOptions.Contains(currentSelectedManufacturer))
            {
                manufacturerDropdown.value = manufacturerOptions.IndexOf(currentSelectedManufacturer);
            }
            else if (manufacturerOptions.Count > 0)
            {
                manufacturerDropdown.value = 0;
            }
            manufacturerDropdown.RefreshShownValue();
        }
    }


    private void SetEditPanelInteractable(bool interactable, bool isAddingNew)
    {
        if (productNameInputField != null) productNameInputField.interactable = interactable;
        if (unitInputField != null) unitInputField.interactable = interactable;
        if (priceInputField != null) priceInputField.interactable = interactable;
        if (importPriceInputField != null) importPriceInputField.interactable = interactable;
        if (barcodeInputField != null) barcodeInputField.interactable = interactable;
        if (imageUrlInputField != null) imageUrlInputField.interactable = interactable;
        // Stock có thể được chỉnh sửa khi thêm mới, nhưng khi chỉnh sửa sản phẩm đã có thì không nên
        // vì stock được cập nhật qua các giao dịch bán hàng/nhập hàng.
        // Tùy theo yêu cầu nghiệp vụ mà ta cho phép hoặc không. Ở đây ta sẽ cho phép chỉnh sửa.
        if (stockInputField != null) stockInputField.interactable = interactable;
        if (categoryDropdown != null) categoryDropdown.interactable = interactable;
        if (manufacturerDropdown != null) manufacturerDropdown.interactable = interactable;

        if (saveProductButton != null) saveProductButton.gameObject.SetActive(interactable && !isAddingNew);
        if (addProductButton != null) addProductButton.gameObject.SetActive(interactable && isAddingNew);

        // Tùy chọn: Thay đổi màu nền hoặc màu chữ để biểu thị trạng thái
        Color readOnlyColor = new Color(0.9f, 0.9f, 0.9f, 0.7f);
        Color editableColor = Color.white;

        if (productNameInputField != null && productNameInputField.targetGraphic != null)
            productNameInputField.targetGraphic.color = interactable ? editableColor : readOnlyColor;
        // Áp dụng cho các trường khác tương tự...
    }


    public async void OnSaveEditButtonClicked()
    {
        if (currentUser == null || userProductsCollection == null) return;

        // Lấy dữ liệu từ UI
        string productName = productNameInputField.text.Trim();
        string unit = unitInputField.text.Trim();
        long price = 0;
        long importPrice = 0;
        string barcode = barcodeInputField.text.Trim();
        string imageUrl = imageUrlInputField.text.Trim();
        long stock = 0;
        string category = categoryDropdown.options[categoryDropdown.value].text;
        string manufacturer = manufacturerDropdown.options[manufacturerDropdown.value].text;

        // Kiểm tra dữ liệu hợp lệ
        if (string.IsNullOrEmpty(productName))
        {
            if (inventoryStatusText != null) inventoryStatusText.text = "Tên sản phẩm không được để trống.";
            return;
        }
        if (!long.TryParse(priceInputField.text, out price) || price < 0)
        {
            if (inventoryStatusText != null) inventoryStatusText.text = "Giá bán không hợp lệ.";
            return;
        }
        if (!long.TryParse(importPriceInputField.text, out importPrice) || importPrice < 0)
        {
            if (inventoryStatusText != null) inventoryStatusText.text = "Giá nhập không hợp lệ.";
            return;
        }
        if (!long.TryParse(stockInputField.text, out stock) || stock < 0)
        {
            if (inventoryStatusText != null) inventoryStatusText.text = "Tồn kho không hợp lệ.";
            return;
        }

        ProductData newProductData = new ProductData
        {
            productName = productName,
            unit = unit,
            price = price,
            importPrice = importPrice,
            barcode = barcode,
            imageUrl = imageUrl,
            stock = stock,
            category = category,
            manufacturer = manufacturer
        };

        try
        {
            if (productToEdit == null) // Thêm mới sản phẩm
            {
                DocumentReference docRef = await userProductsCollection.AddAsync(newProductData);
                Debug.Log($"Đã thêm sản phẩm mới với ID: {docRef.Id}");
                if (inventoryStatusText != null) inventoryStatusText.text = $"Đã thêm sản phẩm: {productName}";
            }
            else // Cập nhật sản phẩm hiện có
            {
                await userProductsCollection.Document(productToEdit.productId).SetAsync(newProductData, SetOptions.MergeAll);
                Debug.Log($"Đã cập nhật sản phẩm ID: {productToEdit.productId}");
                if (inventoryStatusText != null) inventoryStatusText.text = $"Đã cập nhật sản phẩm: {productName}";
            }
            OnCancelEditButtonClicked(); // Đóng panel sau khi lưu
            // Dữ liệu sẽ tự động được cập nhật thông qua listener
        }
        catch (Exception e)
        {
            Debug.LogError($"Lỗi khi lưu sản phẩm: {e.Message}");
            if (inventoryStatusText != null) inventoryStatusText.text = $"Lỗi khi lưu sản phẩm: {e.Message}";
        }
    }

    public async void OnDeleteProductButtonClicked()
    {
        if (productToEdit == null || currentUser == null || userProductsCollection == null) return;

        // Xác nhận xóa
        bool confirmDelete = await ConfirmDeletion("Bạn có chắc chắn muốn xóa sản phẩm này không?");
        if (!confirmDelete)
        {
            return;
        }

        try
        {
            await userProductsCollection.Document(productToEdit.productId).DeleteAsync();
            Debug.Log($"Đã xóa sản phẩm ID: {productToEdit.productId}");
            if (inventoryStatusText != null) inventoryStatusText.text = $"Đã xóa sản phẩm: {productToEdit.productName}";
            OnCancelEditButtonClicked(); // Đóng panel sau khi xóa
            // Dữ liệu sẽ tự động được cập nhật thông qua listener
        }
        catch (Exception e)
        {
            Debug.LogError($"Lỗi khi xóa sản phẩm: {e.Message}");
            if (inventoryStatusText != null) inventoryStatusText.text = $"Lỗi khi xóa sản phẩm: {e.Message}";
        }
    }

    // Hàm giả lập hộp thoại xác nhận (bạn cần triển khai UI thực tế)
    private Task<bool> ConfirmDeletion(string message)
    {
        // Trong một ứng dụng Unity thực tế, bạn sẽ hiển thị một AlertDialog hoặc Confirmation Panel
        // và trả về Task<bool> khi người dùng nhấn OK/Cancel.
        // Ví dụ đơn giản:
        Debug.Log($"CONFIRMATION: {message} (Tự động xác nhận TRUE trong code demo)");
        return Task.FromResult(true); // Tạm thời luôn trả về true để demo
    }

    public void OnCancelEditButtonClicked()
    {
        productToEdit = null;
        if (editProductPanel != null) editProductPanel.SetActive(false);
    }
}
