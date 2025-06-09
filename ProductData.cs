// File: ProductData.cs
using Firebase.Firestore; // Cần dùng để gán DocumentReference hoặc DocumentSnapshot ID

[FirestoreData] // Đảm bảo class này có thể được chuyển đổi bởi Firestore
[System.Serializable] // Đảm bảo class này có thể được serialize bởi Unity (nếu cần)
public class ProductData
{
    // Thêm trường này để lưu trữ Document ID từ Firestore
    // Quan trọng: Trường này sẽ KHÔNG được lưu trữ trong Firestore, mà chỉ dùng để tham chiếu cục bộ
    [FirestoreProperty(ExcludeFromConversion = true)] // Ngăn Firestore cố gắng ghi lại trường này
    public string productId { get; set; } // Firestore Document ID

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

    // Constructor mặc định cần thiết cho Firestore
    public ProductData() { }
}
