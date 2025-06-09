// File: ProductData.cs
using System;
// Không cần using UnityEngine; vì ProductData là một class dữ liệu thuần túy

[System.Serializable]
public class ProductData
{
    // Đảm bảo tên biến khớp với key trong Firestore/JSON của bạn
    // Ví dụ: "productName" trong JSON -> public string productName;
    public string productName;
    public string unit;
    public long price;       // Sử dụng long cho giá để khớp với kiểu Number trong Firestore
    public long importPrice; // Tương tự cho giá nhập
    public string barcode;
    public string imageUrl;
    public long stock;       // Stock cũng có thể là long
    public string category;
    public string manufacturer;

    // Bạn có thể thêm một constructor nếu muốn khởi tạo dễ dàng hơn
    public ProductData() { }

    public ProductData(string productName, string unit, long price, long importPrice, string barcode, string imageUrl, long stock, string category, string manufacturer)
    {
        this.productName = productName;
        this.unit = unit;
        this.price = price;
        this.importPrice = importPrice;
        this.barcode = barcode;
        this.imageUrl = imageUrl;
        this.stock = stock;
        this.category = category;
        this.manufacturer = manufacturer;
    }
}
