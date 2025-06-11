// File: CartItemUI.cs
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.Events; // Cần thiết cho UnityEvent

public class CartItemUI : MonoBehaviour
{
    public TMP_Text productNameText;
    public TMP_Text quantityText;
    public TMP_Text priceText;
    public TMP_Text subtotalText; // Giá = Quantity * Price

    public Button increaseQuantityButton;
    public Button decreaseQuantityButton;
    public Button removeButton;

    // Events để thông báo cho SalesManager khi có thay đổi
    public UnityEvent<string, long> OnQuantityChanged = new UnityEvent<string, long>();
    public UnityEvent<string> OnRemovedFromCart = new UnityEvent<string>();

    private ProductData _cartItemData; // ProductData.stock sẽ lưu số lượng trong giỏ

    public void SetCartItemData(ProductData data)
    {
        _cartItemData = data;

        if (productNameText != null) productNameText.text = data.productName;
        if (quantityText != null) quantityText.text = data.stock.ToString(); // Stock is quantity in cart
        if (priceText != null) priceText.text = $"{data.price:N0} VNĐ";
        if (subtotalText != null) subtotalText.text = $"{data.price * data.stock:N0} VNĐ";

        // Gán sự kiện cho các nút
        if (increaseQuantityButton != null)
        {
            increaseQuantityButton.onClick.RemoveAllListeners();
            increaseQuantityButton.onClick.AddListener(OnIncreaseQuantity);
        }
        if (decreaseQuantityButton != null)
        {
            decreaseQuantityButton.onClick.RemoveAllListeners();
            decreaseQuantityButton.onClick.AddListener(OnDecreaseQuantity);
        }
        if (removeButton != null)
        {
            removeButton.onClick.RemoveAllListeners();
            removeButton.onClick.AddListener(OnRemoveItem);
        }
    }

    private void OnIncreaseQuantity()
    {
        _cartItemData.stock++;
        quantityText.text = _cartItemData.stock.ToString();
        subtotalText.text = $"{_cartItemData.price * _cartItemData.stock:N0} VNĐ";
        OnQuantityChanged.Invoke(_cartItemData.productId, _cartItemData.stock);
    }

    private void OnDecreaseQuantity()
    {
        _cartItemData.stock--;
        if (_cartItemData.stock <= 0)
        {
            OnRemovedFromCart.Invoke(_cartItemData.productId); // Tự động xóa nếu số lượng về 0
        }
        else
        {
            quantityText.text = _cartItemData.stock.ToString();
            subtotalText.text = $"{_cartItemData.price * _cartItemData.stock:N0} VNĐ";
            OnQuantityChanged.Invoke(_cartItemData.productId, _cartItemData.stock);
        }
    }

    private void OnRemoveItem()
    {
        OnRemovedFromCart.Invoke(_cartItemData.productId);
    }
}
