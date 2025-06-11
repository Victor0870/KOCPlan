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
    private ListenerRegistration productsListenerRegistration; // Biến để lưu trữ đăng ký listener

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
                userCustomersCollection = db.Collection("shops").Document(currentUser.UserId).Collection("customers");
                userSalesCollection = db.Collection("shops").Document(currentUser.UserId).Collection("sales");
                Debug.Log($"SalesManager: User collections initialized for UID: {currentUser.UserId}");
                SetupProductsListener(); // Thiết lập listener để tải và cập nhật sản phẩm
            }
            else
            {
                userProductsCollection = null;
                userCustomersCollection = null;
                userSalesCollection = null;
                Debug.Log("SalesManager: User logged out.");
                // Hủy đăng ký listener khi người dùng đăng xuất
                if (productsListenerRegistration != null)
                {
                    productsListenerRegistration.Dispose();
                    productsListenerRegistration = null;
                }
                // Chuyển về màn hình đăng nhập nếu cần
            }
        }
    }

    // --- THAY ĐỔI LỚN TẠI ĐÂY: Sử dụng SnapshotListener cho sản phẩm ---
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
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                Debug.Log($"SalesManager: Nhận được cập nhật sản phẩm từ Firestore. Số lượng thay đổi: {snapshot.Changes.Count}");
                foreach (DocumentChange change in snapshot.Changes)
                {
                    ProductData product = change.Document.ConvertTo<ProductData>();
                    product.productId = change.Document.Id; // Gán ID của document

                    switch (change.ChangeType)
                    {
                        case DocumentChange.Type.Added:
                            if (!allUserProducts.Any(p => p.productId == product.productId))
                            {
                                allUserProducts.Add(product);
                            }
                            break;
                        case DocumentChange.Type.Modified:
                            int index = allUserProducts.FindIndex(p => p.productId == product.productId);
                            if (index != -1)
                            {
                                allUserProducts[index] = product;
                            }
                            break;
                        case DocumentChange.Type.Removed:
                            allUserProducts.RemoveAll(p => p.productId == product.productId);
                            break;
                    }
                }
                // Khi dữ liệu sản phẩm thay đổi, có thể cần cập nhật lại thông tin sản phẩm đang hiển thị
                // Ví dụ: Nếu một sản phẩm đang được chọn và tồn kho thay đổi, hiển thị cập nhật đó.
                // Đối với Sales Scene, chúng ta thường chỉ cần cache để tìm kiếm.
                Debug.Log($"SalesManager: allUserProducts hiện có {allUserProducts.Count} sản phẩm.");
            });
        });
    }

    // --- Customer Management ---

    private void OnCustomerPhoneEndEdit(string phone)
    {
        OnLookupCustomerButtonClicked();
    }

    private async void OnLookupCustomerButtonClicked()
    {
        string phone = customerPhoneInputField.text.Trim();
        if (string.IsNullOrEmpty(phone))
        {
            ClearCustomerInfo();
            customerLookupStatusText.text = "Vui lòng nhập số điện thoại khách hàng.";
            return;
        }

        customerLookupStatusText.text = "Đang tra cứu khách hàng...";
        SetCustomerInputFieldsInteractable(false); // Tạm thời disable các trường khác

        try
        {
            QuerySnapshot querySnapshot = await userCustomersCollection.WhereEqualTo("phone", phone).Limit(1).GetSnapshotAsync();

            if (querySnapshot.Count > 0)
            {
                DocumentSnapshot doc = querySnapshot.Documents[0];
                currentCustomer = doc.ConvertTo<CustomerData>();
                currentCustomer.customerId = doc.Id;

                customerNameInputField.text = currentCustomer.name;
                customerAddressInputField.text = currentCustomer.address;
                customerTaxIdInputField.text = currentCustomer.taxId;

                SetCustomerInputFieldsInteractable(false); // Đặt các trường này thành không tương tác (chỉ đọc)
                customerLookupStatusText.text = $"Đã tìm thấy khách hàng: {currentCustomer.name}.";
            }
            else
            {
                currentCustomer = new CustomerData { phone = phone };
                customerNameInputField.text = "";
                customerAddressInputField.text = "";
                customerTaxIdInputField.text = "";

                SetCustomerInputFieldsInteractable(true); // Cho phép nhập thông tin khách hàng mới
                customerLookupStatusText.text = "Không tìm thấy khách hàng. Vui lòng nhập thông tin mới.";
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
            customerPhoneInputField.interactable = true;
        }
    }

    private void SetCustomerInputFieldsInteractable(bool interactable)
    {
        if (customerNameInputField != null) customerNameInputField.interactable = interactable;
        if (customerAddressInputField != null) customerAddressInputField.interactable = interactable;
        if (customerTaxIdInputField != null) customerTaxIdInputField.interactable = interactable;

        Color readOnlyColor = new Color(0.9f, 0.9f, 0.9f, 0.7f);
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
        SetCustomerInputFieldsInteractable(true);
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

    private ProductData selectedProductForCart;
    private void ShowProductSelection(ProductData product)
    {
        selectedProductForCart = product;
        if (searchResultDisplayPanel != null) searchResultDisplayPanel.SetActive(true);

        if (selectedProductNameText != null) selectedProductNameText.text = product.productName;
        if (selectedProductStockText != null) selectedProductStockText.text = $"Tồn kho: {product.stock}";
        if (selectedProductPriceText != null) selectedProductPriceText.text = $"Giá: {product.price:N0} VNĐ";
        if (quantityToCartInputField != null) quantityToCartInputField.text = "1";

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
            return;
        }

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
            return;
        }

        if (productsInCart.ContainsKey(selectedProductForCart.productId))
        {
            productsInCart[selectedProductForCart.productId].stock += quantityToAdd;
        }
        else
        {
            ProductData newCartItem = new ProductData
            {
                productId = selectedProductForCart.productId,
                productName = selectedProductForCart.productName,
                unit = selectedProductForCart.unit,
                price = selectedProductForCart.price,
                stock = quantityToAdd
            };
            productsInCart.Add(newCartItem.productId, newCartItem);
        }

        UpdateCartUI();
        ClearProductSelection();
    }

    // --- Cart Management ---
    private void UpdateCartUI()
    {
        foreach (Transform child in cartItemsParent.transform)
        {
            Destroy(child.gameObject);
        }
        cartItemUIObjects.Clear();

        if (productsInCart.Count > 0)
        {
            if (cartEmptyPromptPanel != null) cartEmptyPromptPanel.SetActive(false);
            foreach (var kvp in productsInCart)
            {
                ProductData cartItemData = kvp.Value;
                GameObject cartItemGO = Instantiate(cartItemPrefab, cartItemsParent.transform);
                CartItemUI uiItem = cartItemGO.GetComponent<CartItemUI>();
                if (uiItem != null)
                {
                    uiItem.SetCartItemData(cartItemData);
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
            if (cartEmptyPromptPanel != null) cartEmptyPromptPanel.SetActive(true);
        }

        UpdateCartSummaryUI();
    }

    private void HandleCartItemQuantityChanged(string productId, long newQuantity)
    {
        if (productsInCart.ContainsKey(productId))
        {
            ProductData cartItem = productsInCart[productId];
            ProductData inventoryProduct = allUserProducts.FirstOrDefault(p => p.productId == productId);

            if (inventoryProduct != null && newQuantity > inventoryProduct.stock)
            {
                Debug.LogWarning($"Số lượng trong giỏ của {cartItem.productName} ({newQuantity}) không thể vượt quá tồn kho hiện có ({inventoryProduct.stock}).");
                if (cartItemUIObjects.ContainsKey(productId))
                {
                    cartItemUIObjects[productId].GetComponent<CartItemUI>().SetCartItemData(cartItem);
                }
                return;
            }

            cartItem.stock = newQuantity;
            if (newQuantity <= 0)
            {
                HandleRemoveCartItem(productId);
            }
            else
            {
                UpdateCartSummaryUI();
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
            UpdateCartUI();
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

        string finalCustomerPhone = customerPhoneInputField.text.Trim();
        string finalCustomerName = customerNameInputField.text.Trim();
        string finalCustomerAddress = customerAddressInputField.text.Trim();
        string finalCustomerTaxId = customerTaxIdInputField.text.Trim();

        if (string.IsNullOrEmpty(finalCustomerPhone))
        {
            customerLookupStatusText.text = "Vui lòng nhập số điện thoại khách hàng.";
            return;
        }
        if (string.IsNullOrEmpty(finalCustomerName) && customerNameInputField.interactable)
        {
            customerLookupStatusText.text = "Vui lòng nhập tên khách hàng.";
            return;
        }

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
                DocumentReference newCustomerDocRef = await userCustomersCollection.AddAsync(currentCustomer);
                currentCustomer.customerId = newCustomerDocRef.Id;
                Debug.Log($"Đã lưu khách hàng mới: {currentCustomer.name} (ID: {currentCustomer.customerId})");
            }
            catch (Exception e)
            {
                Debug.LogError($"Lỗi khi lưu khách hàng mới: {e.Message}");
                customerLookupStatusText.text = $"Lỗi khi lưu khách hàng: {e.Message}";
            }
        }


        List<SaleItem> saleItems = new List<SaleItem>();
        foreach (var productInCart in productsInCart.Values)
        {
            saleItems.Add(new SaleItem
            {
                productId = productInCart.productId,
                productName = productInCart.productName,
                unit = productInCart.unit,
                quantity = productInCart.stock,
                priceAtSale = productInCart.price
            });
        }

        long finalSubtotal = productsInCart.Values.Sum(p => p.price * p.stock);
        long finalTax = (long)(finalSubtotal * TAX_RATE);
        long finalGrandTotal = finalSubtotal + finalTax;

        SaleData newSale = new SaleData
        {
            customerId = currentCustomer?.customerId,
            customerName = currentCustomer?.name,
            customerPhone = currentCustomer?.phone,
            totalAmount = finalGrandTotal,
            taxAmount = finalTax,
            subtotal = finalSubtotal,
            saleDate = Timestamp.FromDateTime(DateTime.UtcNow),
            items = saleItems
        };

        completeSaleButton.interactable = false;
        cancelSaleButton.interactable = false;
        customerLookupStatusText.text = "Đang hoàn tất đơn hàng...";

        try
        {
            await userSalesCollection.AddAsync(newSale);
            Debug.Log("Đã lưu đơn hàng thành công.");

            WriteBatch batch = db.StartBatch();
            foreach (var cartProduct in productsInCart.Values)
            {
                DocumentReference productDocRef = userProductsCollection.Document(cartProduct.productId);

                // Quan trọng: Sử dụng `update` để chỉ sửa `stock` và tránh ghi đè toàn bộ document.
                // Firebase tự động sẽ kiểm tra tài liệu có tồn tại không.
                batch.Update(productDocRef, "stock", FieldValue.Increment(-cartProduct.stock));
            }
            await batch.CommitAsync();
            Debug.Log("Đã cập nhật tồn kho thành công.");

            customerLookupStatusText.text = "Đơn hàng đã hoàn tất!";
            Debug.Log("Đơn hàng đã hoàn tất thành công!");
            OnCancelSaleButtonClicked();
            // Không cần LoadAllInventoryProducts() nữa vì listener đã xử lý cập nhật local cache.
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
        UpdateCartUI();

        ClearCustomerInfo();

        ClearProductSelection();
        productSearchInputField.text = "";
        Debug.Log("Đã hủy đơn hàng và reset.");
    }

    private void OnExportInvoiceButtonClicked()
    {
        Debug.Log("Nút 'Xuất hóa đơn' được nhấn. Chức năng sẽ được phát triển sau.");
    }

    private void OnBackToInventoryButtonClicked()
    {
        SceneManager.LoadScene("Inventory");
    }
}
