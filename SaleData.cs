// File: SaleData.cs
using Firebase.Firestore;
using System;
using System.Collections.Generic;

[FirestoreData]
[Serializable]
public class SaleData
{
    // ID của khách hàng (nếu có, từ collection customers)
    [FirestoreProperty("customerId")]
    public string customerId { get; set; }

    // Tên khách hàng tại thời điểm bán (để không bị ảnh hưởng nếu tên khách hàng thay đổi sau này)
    [FirestoreProperty("customerName")]
    public string customerName { get; set; }

    [FirestoreProperty("customerPhone")]
    public string customerPhone { get; set; }

    [FirestoreProperty("totalAmount")]
    public long totalAmount { get; set; } // Tổng cộng sau thuế

    [FirestoreProperty("taxAmount")]
    public long taxAmount { get; set; } // Tiền thuế

    [FirestoreProperty("subtotal")]
    public long subtotal { get; set; } // Tổng tiền hàng trước thuế

    [FirestoreProperty("saleDate")]
    public Timestamp saleDate { get; set; } // Thời gian tạo đơn hàng

    [FirestoreProperty("items")]
    public List<SaleItem> items { get; set; } // Danh sách các sản phẩm trong đơn hàng

    public SaleData() { }
}
