// File: SalesManager.cs
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Firebase.Firestore;
using Firebase.Extensions;
using Firebase.Auth;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;
using System.Linq;

public class SalesManager : MonoBehaviour
{
    [Header("Customer Info UI")]
    public TMP_InputField customerPhoneInputField;
    public Button lookupCustomerButton; // Nút tra cứu, nếu bạn muốn có
    public TMP_InputField customerNameInputField; // Sẽ toggle interactable
    public TMP_InputField customerAddressInputField; // Sẽ toggle interactable
    public TMP_InputField customerTaxIdInputField;   // NEW: Mã số thuế
    public TMP_Text customerLookupStatusText;
    public Button clearCustomerInfoButton; // NEW: Nút để xóa thông tin KH hiện tại
    private CustomerData currentCustomer; // Lưu trữ dữ liệu khách hàng cho đơn hàng hiện tại

    [Header("Product Selection UI")]
    public TMP_InputField productSearchInputField;
    public Button searchProductButton;
    public Button scanBarcodeButton; // Placeholder
    public GameObject searchResultDisplayPanel; // Panel để hiển thị chi tiết sản phẩm được chọn từ tìm kiếm
    public TMP_Text selectedProductNameText;
    public TMP_Text selectedProductStockText;
    public TMP_Text selectedProductPriceText;
    public TMP_InputField quantityToCartInputField;
    public Button addToCartButton;
    public Button clearSelectionButton;

    [Header("Cart UI")]
    public GameObject cartItemsParent; // GameObject cha cho các prefab CartItemUI
    public GameObject cartItemPrefab; // Prefab cho từng sản phẩm trong giỏ hàng
    public GameObject cartEmptyPromptPanel; // Panel hiển thị khi giỏ hàng trống

    [Header("Payment Summary UI")]
    public TMP_Text subtotalText;
    public TMP_Text taxText;
    public TMP_Text grandTotalText;
    public Button completeSaleButton;
    public Button cancelSaleButton;
    public Button exportInvoiceButton; // NEW: Nút Xuất hóa đơn

    [Header("Navigation")]
    public Button backToInventoryButton;

    // Firebase
    private FirebaseFirestore db;
    private FirebaseUser currentUser;
    private CollectionReference userProductsCollection;
    private CollectionReference userCustomersCollection; // NEW: Collection khách hàng
    private CollectionReference userSalesCollection; // NEW: Collection đơn hàng

    private List<ProductData> allUserProducts = new List<ProductData>(); // Cache các sản phẩm trong kho
    private Dictionary<string, ProductData> productsInCart = new Dictionary<string, ProductData>(); // ProductID -> ProductData (trong đó stock là số lượng trong giỏ)
    private Dictionary<string, GameObject> cartItemUIObjects = new Dictionary<string, GameObject>(); // ProductID -> GameObject của CartItemUI
    private const double TAX_RATE = 0.10; // 10% thuế

    void Awake()
    {
        db = FirebaseFirestore.DefaultInstance;
        FirebaseAuth auth = FirebaseAuth.DefaultInstance;
        auth.StateChanged += AuthStateChanged;

        // Khởi tạo trạng thái UI
        if (searchResultDisplayPanel != null) searchResultDisplayPanel.SetActive(false);
        if (cartEmptyPromptPanel != null) cartEmptyPromptPanel.SetActive(true); // Ban đầu giỏ hàng trống
        
        SetCustomerInputFieldsInteractable(true); // Ban đầu cho phép nhập thông tin khách hàng mới
        customerLookupStatusText.text = "";
    }

    void Start()
    {
        // Gán sự kiện cho các nút
        if (customerPhoneInputField != null) customerPhoneInputField.onEndEdit.AddListener(OnCustomerPhoneEndEdit);
        if (lookupCustomerButton != null) lookupCustomerButton.onClick.AddListener(OnLookupCustomerButtonClicked);
        if (clearCustomerInfoButton != null) clearCustomerInfoButton.onClick.AddListener(ClearCustomerInfo);

        if (productSearchInputField != null) productSearchInputField.onEndEdit.AddListener(delegate { OnProductSearchRequested(productSearchInputField.text); });
        if (searchProductButton != null) searchProductButton.onClick.AddListener(delegate { OnProductSearchRequested(productSearchInputField.text); });
        if (addToCartButton != null) addToCartButton.onClick.AddListener(OnAddToCartButtonClicked);
        if (clearSelectionButton != null) clearSelectionButton.onClick.AddListener(ClearProductSelection);

        if (completeSaleButton != null) completeSaleButton.onClick.AddListener(OnCompleteSaleButtonClicked);
        if (cancelSaleButton != null) cancelSaleButton.onClick.AddListener(OnCancelSaleButtonClicked);
        if (exportInvoiceButton != null) exportInvoiceButton.onClick.AddListener(OnExportInvoiceButtonClicked);
        if (backToInventoryButton != null) backToInventoryButton.onClick.AddListener(OnBackToInventoryButtonClicked);

        // Đảm bảo UnityMainThreadDispatcher đã được khởi tạo
        UnityMainThreadDispatcher.Instance();
        UpdateCartSummaryUI(); // Cập nhật tổng tiền ban đầu
    }

    void OnDestroy()
    {
        FirebaseAuth auth = FirebaseAuth.DefaultInstance;
        if (auth != null)
        {
            auth.StateChanged -= AuthStateChanged;
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
                userCustomersCollection = db.Collection("shops").Document(currentUser.UserId).Collection("customers");
                userSalesCollection = db.Collection("shops").Document(currentUser.UserId).Collection("sales");
                Debug.Log($"SalesManager: User collections initialized for UID: {currentUser.UserId}");
                LoadAllInventoryProducts(); // Tải kho hàng để tìm kiếm
            }
            else
            {
                userProductsCollection = null;
                userCustomersCollection = null;
                userSalesCollection = null;
                Debug.Log("SalesManager: User logged out.");
                // Chuyển về màn hình đăng nhập nếu cần
            }
        }
    }

    private async void LoadAllInventoryProducts()
    {
        if (userProductsCollection == null) return;
        allUserProducts.Clear();
        try
        {
            QuerySnapshot snapshot = await userProductsCollection.GetSnapshotAsync();
            foreach (DocumentSnapshot document in snapshot.Documents)
            {
                ProductData product = document.ConvertTo<ProductData>();
                product.productId = document.Id;
                allUserProducts.Add(product);
            }
            Debug.Log($"SalesManager: Loaded {allUserProducts.Count} products from inventory for search.");
        }
        catch (Exception e)
        {
            Debug.LogError($"SalesManager: Error loading inventory products: {e.Message}");
        }
    }

    // --- Customer Management ---

    private void OnCustomerPhoneEndEdit(string phone)
    {
        // Chỉ tra cứu khi người dùng ngừng nhập (ấn Enter hoặc click ra ngoài)
        OnLookupCustomerButtonClicked();
    }

    private async void OnLookupCustomerButtonClicked()
    {
        string phone = customerPhoneInputField.text.Trim();
        if (string.IsNullOrEmpty(phone))
        {
            ClearCustomerInfo(); // Nếu số điện thoại trống, reset
            customerLookupStatusText.text = "Vui lòng nhập số điện thoại khách hàng.";
            return;
        }

        customerLookupStatusText.text = "Đang tra cứu khách hàng...";
        // Tạm thời disable các trường khác trong khi tra cứu
        SetCustomerInputFieldsInteractable(false);

        try
        {
            // Tra cứu khách hàng theo số điện thoại
            // Firestore không có khả năng query trực tiếp bằng Document ID trừ khi bạn đặt ID là số điện thoại
            // Nếu Document ID của khách hàng không phải là số điện thoại, bạn phải query theo trường "phone"
            QuerySnapshot querySnapshot = await userCustomersCollection.WhereEqualTo("phone", phone).Limit(1).GetSnapshotAsync();

            if (querySnapshot.Count > 0)
            {
                DocumentSnapshot doc = querySnapshot.Documents[0];
                currentCustomer = doc.ConvertTo<CustomerData>();
                currentCustomer.customerId = doc.Id; // Gán ID của document
                
                customerNameInputField.text = currentCustomer.name;
                customerAddressInputField.text = currentCustomer.address;
                customerTaxIdInputField.text = currentCustomer.taxId; // Điền mã số thuế
                
                // Sau khi điền, đặt các trường này thành không tương tác (chỉ đọc)
                SetCustomerInputFieldsInteractable(false);
                customerLookupStatusText.text = $"Đã tìm thấy khách hàng: {currentCustomer.name}.";
                Debug.Log($"Tìm thấy khách hàng: {currentCustomer.name}");
            }
            else
            {
                // Nếu không tìm thấy, chuẩn bị cho khách hàng mới
                currentCustomer = new CustomerData { phone = phone }; // Pre-fill số điện thoại
                customerNameInputField.text = "";
                customerAddressInputField.text = "";
                customerTaxIdInputField.text = ""; // Xóa mã số thuế

                // Cho phép nhập thông tin khách hàng mới
                SetCustomerInputFieldsInteractable(true);
                customerLookupStatusText.text = "Không tìm thấy khách hàng. Vui lòng nhập thông tin mới.";
                Debug.Log("Không tìm thấy khách hàng. Cho phép nhập mới.");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Lỗi khi tra cứu khách hàng: {e.Message}");
            customerLookupStatusText.text = $"Lỗi: {e.Message}";
            SetCustomerInputFieldsInteractable(true); // Re-enable in case of error
        }
        finally
        {
            // Số điện thoại input field luôn cho phép tương tác
            customerPhoneInputField.interactable = true; 
        }
    }

    private void SetCustomerInputFieldsInteractable(bool interactable)
    {
        // Số điện thoại luôn cho phép nhập
        // customerPhoneInputField.interactable = true; 

        // Các trường còn lại sẽ được bật/tắt tương tác
        if (customerNameInputField != null) customerNameInputField.interactable = interactable;
        if (customerAddressInputField != null) customerAddressInputField.interactable = interactable;
        if (customerTaxIdInputField != null) customerTaxIdInputField.interactable = interactable;

        // Tùy chọn: Thay đổi màu nền hoặc màu chữ để biểu thị trạng thái chỉ đọc
        Color readOnlyColor = new Color(0.9f, 0.9f, 0.9f, 0.7f); // Màu xám nhạt và hơi trong suốt
        Color editableColor = Color.white;

        if (customerNameInputField != null && customerNameInputField.targetGraphic != null)
            customerNameInputField.targetGraphic.color = interactable ? editableColor : readOnlyColor;
        if (customerAddressInputField != null && customerAddressInputField.targetGraphic != null)
            customerAddressInputField.targetGraphic.color = interactable ? editableColor : readOnlyColor;
        if (customerTaxIdInputField != null && customerTaxIdInputField.targetGraphic != null)
            customerTaxIdInputField.targetGraphic.color = interactable ? editableColor : readOnlyColor;
    }

    public void ClearCustomerInfo()
    {
        currentCustomer = null;
        customerPhoneInputField.text = "";
        customerNameInputField.text = "";
        customerAddressInputField.text = "";
        customerTaxIdInputField.text = "";
        SetCustomerInputFieldsInteractable(true); // Bật lại để nhập khách hàng mới
        customerLookupStatusText.text = "";
        Debug.Log("Đã xóa thông tin khách hàng hiện tại.");
    }

    // --- Product Search & Add to Cart ---
    private void OnProductSearchRequested(string searchText)
    {
        if (string.IsNullOrEmpty(searchText.Trim()))
        {
            ClearProductSelection();
            return;
        }

        // Tìm sản phẩm theo barcode hoặc tên
        ProductData foundProduct = allUserProducts.FirstOrDefault(p =>
            p.barcode.Equals(searchText.Trim(), StringComparison.OrdinalIgnoreCase) ||
            p.productName.IndexOf(searchText.Trim(), StringComparison.OrdinalIgnoreCase) >= 0);

        if (foundProduct != null)
        {
            ShowProductSelection(foundProduct);
        }
        else
        {
            ClearProductSelection();
            selectedProductNameText.text = "Không tìm thấy sản phẩm.";
            selectedProductStockText.text = "";
            selectedProductPriceText.text = "";
        }
    }

    private ProductData selectedProductForCart; // Sản phẩm đang được chọn để thêm vào giỏ
    private void ShowProductSelection(ProductData product)
    {
        selectedProductForCart = product;
        if (searchResultDisplayPanel != null) searchResultDisplayPanel.SetActive(true);

        if (selectedProductNameText != null) selectedProductNameText.text = product.productName;
        if (selectedProductStockText != null) selectedProductStockText.text = $"Tồn kho: {product.stock}";
        if (selectedProductPriceText != null) selectedProductPriceText.text = $"Giá: {product.price:N0} VNĐ";
        if (quantityToCartInputField != null) quantityToCartInputField.text = "1"; // Mặc định số lượng là 1
        
        // Nút "Thêm vào giỏ" chỉ tương tác được nếu tồn kho > 0
        if (addToCartButton != null) addToCartButton.interactable = product.stock > 0;
    }

    private void ClearProductSelection()
    {
        selectedProductForCart = null;
        if (searchResultDisplayPanel != null) searchResultDisplayPanel.SetActive(false);
        if (productSearchInputField != null) productSearchInputField.text = "";
    }

    private void OnAddToCartButtonClicked()
    {
        if (selectedProductForCart == null) return;

        long quantityToAdd;
        if (!long.TryParse(quantityToCartInputField.text, out quantityToAdd) || quantityToAdd <= 0)
        {
            Debug.LogWarning("Số lượng thêm vào giỏ không hợp lệ.");
            // Có thể hiển thị thông báo lỗi trên UI
            return;
        }

        // Kiểm tra tồn kho trước khi thêm vào giỏ
        ProductData actualInventoryProduct = allUserProducts.FirstOrDefault(p => p.productId == selectedProductForCart.productId);
        if (actualInventoryProduct == null)
        {
            Debug.LogError($"Sản phẩm {selectedProductForCart.productName} không còn trong kho cache.");
            return;
        }

        long currentQuantityInCart = productsInCart.ContainsKey(selectedProductForCart.productId) ? productsInCart[selectedProductForCart.productId].stock : 0;
        long totalRequestedQuantity = currentQuantityInCart + quantityToAdd;

        if (totalRequestedQuantity > actualInventoryProduct.stock)
        {
            Debug.LogWarning($"Không đủ hàng trong kho cho {selectedProductForCart.productName}. Tồn kho: {actualInventoryProduct.stock}. Giỏ đã có: {currentQuantityInCart}. Yêu cầu thêm: {quantityToAdd}.");
            // Hiển thị thông báo lỗi trên UI
            return;
        }

        // Thêm/Cập nhật sản phẩm trong giỏ hàng
        if (productsInCart.ContainsKey(selectedProductForCart.productId))
        {
            productsInCart[selectedProductForCart.productId].stock += quantityToAdd; // 'stock' của ProductData dùng để lưu số lượng trong giỏ
        }
        else
        {
            // Tạo một bản sao mới của ProductData để lưu vào giỏ hàng, tránh ảnh hưởng đến dữ liệu kho gốc
            ProductData newCartItem = new ProductData
            {
                productId = selectedProductForCart.productId,
                productName = selectedProductForCart.productName,
                unit = selectedProductForCart.unit,
                price = selectedProductForCart.price,
                stock = quantityToAdd // Số lượng trong giỏ
            };
            productsInCart.Add(newCartItem.productId, newCartItem);
        }

        UpdateCartUI();
        ClearProductSelection();
    }

    // --- Cart Management ---
    private void UpdateCartUI()
    {
        // Xóa tất cả các item giỏ hàng cũ
        foreach (Transform child in cartItemsParent.transform)
        {
            Destroy(child.gameObject);
        }
        cartItemUIObjects.Clear();

        // Instantiate các item giỏ hàng mới
        if (productsInCart.Count > 0)
        {
            if (cartEmptyPromptPanel != null) cartEmptyPromptPanel.SetActive(false); // Ẩn thông báo giỏ trống
            foreach (var kvp in productsInCart)
            {
                ProductData cartItemData = kvp.Value;
                GameObject cartItemGO = Instantiate(cartItemPrefab, cartItemsParent.transform);
                CartItemUI uiItem = cartItemGO.GetComponent<CartItemUI>();
                if (uiItem != null)
                {
                    uiItem.SetCartItemData(cartItemData);
                    // Đăng ký các sự kiện để SalesManager có thể lắng nghe thay đổi từ CartItemUI
                    uiItem.OnQuantityChanged.AddListener(HandleCartItemQuantityChanged);
                    uiItem.OnRemovedFromCart.AddListener(HandleRemoveCartItem);
                    cartItemUIObjects.Add(cartItemData.productId, cartItemGO);
                }
                else
                {
                    Debug.LogWarning("Prefab cartItemPrefab không có script CartItemUI. Vui lòng kiểm tra lại!");
                }
            }
        }
        else
        {
            if (cartEmptyPromptPanel != null) cartEmptyPromptPanel.SetActive(true); // Hiển thị thông báo giỏ trống
        }

        UpdateCartSummaryUI();
    }

    private void HandleCartItemQuantityChanged(string productId, long newQuantity)
    {
        if (productsInCart.ContainsKey(productId))
        {
            ProductData cartItem = productsInCart[productId];
            ProductData inventoryProduct = allUserProducts.FirstOrDefault(p => p.productId == productId);

            // Kiểm tra lại tồn kho khi số lượng trong giỏ thay đổi
            if (inventoryProduct != null && newQuantity > inventoryProduct.stock)
            {
                Debug.LogWarning($"Số lượng trong giỏ của {cartItem.productName} ({newQuantity}) không thể vượt quá tồn kho hiện có ({inventoryProduct.stock}).");
                // Reset UI của item đó về số lượng tối đa cho phép hoặc số lượng trước đó
                if (cartItemUIObjects.ContainsKey(productId))
                {
                    cartItemUIObjects[productId].GetComponent<CartItemUI>().SetCartItemData(cartItem); // Revert UI
                }
                return;
            }
            
            cartItem.stock = newQuantity; // Cập nhật số lượng trong giỏ
            if (newQuantity <= 0) // Nếu số lượng về 0, xóa khỏi giỏ
            {
                HandleRemoveCartItem(productId);
            }
            else
            {
                UpdateCartSummaryUI(); // Tính toán lại tổng tiền
            }
        }
    }

    private void HandleRemoveCartItem(string productId)
    {
        if (productsInCart.ContainsKey(productId))
        {
            productsInCart.Remove(productId);
            if (cartItemUIObjects.ContainsKey(productId))
            {
                Destroy(cartItemUIObjects[productId]);
                cartItemUIObjects.Remove(productId);
            }
            UpdateCartUI(); // Cập nhật lại UI giỏ hàng
        }
    }

    private void UpdateCartSummaryUI()
    {
        long currentSubtotal = 0;
        foreach (var product in productsInCart.Values)
        {
            currentSubtotal += product.price * product.stock;
        }

        long currentTax = (long)(currentSubtotal * TAX_RATE);
        long currentGrandTotal = currentSubtotal + currentTax;

        if (subtotalText != null) subtotalText.text = $"Tổng tiền hàng: {currentSubtotal:N0} VNĐ";
        if (taxText != null) taxText.text = $"Thuế ({TAX_RATE * 100}%): {currentTax:N0} VNĐ";
        if (grandTotalText != null) grandTotalText.text = $"Tổng cộng: {currentGrandTotal:N0} VNĐ";

        // Enable/disable complete sale button
        if (completeSaleButton != null)
        {
            completeSaleButton.interactable = productsInCart.Count > 0;
        }
    }

    // --- Sale Completion ---
    private async void OnCompleteSaleButtonClicked()
    {
        if (productsInCart.Count == 0)
        {
            customerLookupStatusText.text = "Giỏ hàng trống. Không thể hoàn tất đơn hàng.";
            return;
        }
        if (currentUser == null)
        {
            customerLookupStatusText.text = "Lỗi: Người dùng chưa đăng nhập. Vui lòng đăng nhập lại.";
            return;
        }

        // --- Bước 1: Xác nhận và lưu thông tin khách hàng ---
        string finalCustomerPhone = customerPhoneInputField.text.Trim();
        string finalCustomerName = customerNameInputField.text.Trim();
        string finalCustomerAddress = customerAddressInputField.text.Trim();
        string finalCustomerTaxId = customerTaxIdInputField.text.Trim();

        if (string.IsNullOrEmpty(finalCustomerPhone))
        {
            customerLookupStatusText.text = "Vui lòng nhập số điện thoại khách hàng.";
            return;
        }
        if (string.IsNullOrEmpty(finalCustomerName) && customerNameInputField.interactable) // Nếu đang ở chế độ nhập mới, tên không được trống
        {
            customerLookupStatusText.text = "Vui lòng nhập tên khách hàng.";
            return;
        }

        // Nếu đây là khách hàng mới (customerId là null hoặc rỗng)
        if (currentCustomer == null || string.IsNullOrEmpty(currentCustomer.customerId))
        {
            currentCustomer = new CustomerData
            {
                phone = finalCustomerPhone,
                name = finalCustomerName,
                address = finalCustomerAddress,
                taxId = finalCustomerTaxId
            };

            try
            {
                // Thêm khách hàng mới vào Firestore và lấy ID
                DocumentReference newCustomerDocRef = await userCustomersCollection.AddAsync(currentCustomer);
                currentCustomer.customerId = newCustomerDocRef.Id;
                Debug.Log($"Đã lưu khách hàng mới: {currentCustomer.name} (ID: {currentCustomer.customerId})");
            }
            catch (Exception e)
            {
                Debug.LogError($"Lỗi khi lưu khách hàng mới: {e.Message}");
                customerLookupStatusText.text = $"Lỗi khi lưu khách hàng: {e.Message}";
                // Quyết định có nên dừng giao dịch nếu không lưu được khách hàng hay không. Tùy thuộc vào yêu cầu.
                // Ở đây, ta vẫn tiếp tục để hoàn tất đơn hàng, nhưng khách hàng có thể không được lưu trữ.
            }
        }
        else // Đây là khách hàng hiện có, có thể cập nhật nếu có thay đổi (chức năng edit customer sau này)
        {
            // Hiện tại, chúng ta không cho phép sửa thông tin khách hàng cũ qua giao diện này
            // Nếu bạn muốn cho phép sửa, bạn sẽ cần thêm logic update ở đây
        }


        // --- Bước 2: Chuẩn bị và lưu bản ghi đơn hàng ---
        List<SaleItem> saleItems = new List<SaleItem>();
        foreach (var productInCart in productsInCart.Values)
        {
            saleItems.Add(new SaleItem
            {
                productId = productInCart.productId,
                productName = productInCart.productName,
                unit = productInCart.unit,
                quantity = productInCart.stock, // stock ở đây là số lượng bán
                priceAtSale = productInCart.price // Giá tại thời điểm bán
            });
        }

        // Tính toán lại tổng tiền cuối cùng trước khi lưu
        long finalSubtotal = productsInCart.Values.Sum(p => p.price * p.stock);
        long finalTax = (long)(finalSubtotal * TAX_RATE);
        long finalGrandTotal = finalSubtotal + finalTax;

        SaleData newSale = new SaleData
        {
            customerId = currentCustomer?.customerId, // Có thể là null nếu không lưu được khách hàng
            customerName = currentCustomer?.name,
            customerPhone = currentCustomer?.phone,
            totalAmount = finalGrandTotal,
            taxAmount = finalTax,
            subtotal = finalSubtotal,
            saleDate = Timestamp.FromDateTime(DateTime.UtcNow),
            items = saleItems
        };

        // Tạm thời vô hiệu hóa các nút
        completeSaleButton.interactable = false;
        cancelSaleButton.interactable = false;
        customerLookupStatusText.text = "Đang hoàn tất đơn hàng...";

        try
        {
            // Lưu bản ghi đơn hàng vào Firestore
            await userSalesCollection.AddAsync(newSale);
            Debug.Log("Đã lưu đơn hàng thành công.");

            // --- Bước 3: Cập nhật tồn kho (Giảm số lượng sản phẩm đã bán) ---
            WriteBatch batch = db.StartBatch();
            foreach (var cartProduct in productsInCart.Values)
            {
                DocumentReference productDocRef = userProductsCollection.Document(cartProduct.productId);
                
                // Firestore Transaction/Batch Update
                // Lấy snapshot để kiểm tra tồn kho hiện tại và đảm bảo không bị lỗi concurrency nếu có nhiều giao dịch
                DocumentSnapshot productSnap = await productDocRef.GetSnapshotAsync();
                if (productSnap.Exists)
                {
                    ProductData currentInventoryProduct = productSnap.ConvertTo<ProductData>();
                    long newStock = currentInventoryProduct.stock - cartProduct.stock; // cartProduct.stock là số lượng bán

                    if (newStock < 0)
                    {
                        Debug.LogError($"Cảnh báo: Tồn kho của {currentInventoryProduct.productName} bị âm sau giao dịch. Đã bán {cartProduct.stock}, Tồn kho ban đầu {currentInventoryProduct.stock}. Sẽ đặt về 0.");
                        newStock = 0; // Đảm bảo tồn kho không bị âm trong DB
                    }

                    batch.Update(productDocRef, "stock", newStock);
                }
                else
                {
                    Debug.LogWarning($"Sản phẩm '{cartProduct.productName}' (ID: {cartProduct.productId}) không tồn tại trong kho để cập nhật.");
                }
            }
            await batch.CommitAsync();
            Debug.Log("Đã cập nhật tồn kho thành công.");

            // --- Hoàn tất giao dịch ---
            customerLookupStatusText.text = "Đơn hàng đã hoàn tất!";
            Debug.Log("Đơn hàng đã hoàn tất thành công!");
            OnCancelSaleButtonClicked(); // Reset UI cho đơn hàng mới
            LoadAllInventoryProducts(); // Tải lại kho hàng để cập nhật tồn kho hiển thị (đảm bảo dữ liệu mới nhất)

        }
        catch (Exception e)
        {
            Debug.LogError($"Lỗi khi hoàn tất đơn hàng: {e.Message}");
            customerLookupStatusText.text = $"Lỗi: {e.Message}";
        }
        finally
        {
            completeSaleButton.interactable = true;
            cancelSaleButton.interactable = true;
        }
    }

    private void OnCancelSaleButtonClicked()
    {
        productsInCart.Clear();
        cartItemUIObjects.Clear();
        UpdateCartUI(); // Xóa UI giỏ hàng và hiển thị thông báo trống

        ClearCustomerInfo(); // Xóa thông tin khách hàng

        ClearProductSelection(); // Xóa sản phẩm đang chọn để thêm vào giỏ
        productSearchInputField.text = ""; // Xóa nội dung tìm kiếm
        Debug.Log("Đã hủy đơn hàng và reset.");
    }

    private void OnExportInvoiceButtonClicked()
    {
        Debug.Log("Nút 'Xuất hóa đơn' được nhấn. Chức năng sẽ được phát triển sau.");
        // TODO: Triển khai chức năng xuất hóa đơn (ví dụ: tạo PDF)
    }

    private void OnBackToInventoryButtonClicked()
    {
        SceneManager.LoadScene("Inventory"); // Đảm bảo Scene "Inventory" đã được thêm vào Build Settings
    }
}




