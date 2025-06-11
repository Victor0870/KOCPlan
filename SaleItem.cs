// File: SaleItem.cs
using Firebase.Firestore;
using System;

[FirestoreData]
[Serializable]
public class SaleItem
{
    [FirestoreProperty("productId")]
    public string productId { get; set; }

    [FirestoreProperty("productName")]
    public string productName { get; set; }

    [FirestoreProperty("unit")]
    public string unit { get; set; }

    [FirestoreProperty("quantity")]
    public long quantity { get; set; } // Số lượng sản phẩm bán trong đơn hàng này

    [FirestoreProperty("priceAtSale")]
    public long priceAtSale { get; set; } // Giá sản phẩm tại thời điểm bán

    public SaleItem() { }
}
