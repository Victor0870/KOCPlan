using Firebase.Firestore;
using System;

[FirestoreData] // Đảm bảo class này có thể được chuyển đổi bởi Firestore
[Serializable] // Đảm bảo class này có thể được serialize bởi Unity (nếu cần)
public class ProductData
{
    // Trường này dùng để lưu trữ Document ID từ Firestore.
    // KHÔNG gắn [FirestoreProperty] vì đây không phải là một trường dữ liệu trong document Firestore.
    // Document ID được quản lý bởi Firestore và được truy cập thông qua DocumentSnapshot.Id.
    public string productId { get; set; }

    [FirestoreProperty("productName")]
    public string productName { get; set; }

    [FirestoreProperty("unit")]
    public string unit { get; set; }

    [FirestoreProperty("price")]
    public long price { get; set; } // Nên dùng long cho tiền hoặc số lượng lớn

    [FirestoreProperty("importPrice")]
    public long importPrice { get; set; }

    [FirestoreProperty("barcode")]
    public string barcode { get; set; }

    [FirestoreProperty("imageUrl")]
    public string imageUrl { get; set; }

    [FirestoreProperty("stock")]
    public long stock { get; set; } // Nên dùng long cho số lượng tồn kho

    [FirestoreProperty("category")]
    public string category { get; set; }

    [FirestoreProperty("manufacturer")]
    public string manufacturer { get; set; }

    // Constructor mặc định cần thiết cho Firestore để deserialize dữ liệu
    public ProductData() { }
}
