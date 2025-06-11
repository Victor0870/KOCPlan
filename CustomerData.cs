using Firebase.Firestore;
using System;

[FirestoreData]
[Serializable]
public class CustomerData
{
    // Trường này dùng để lưu trữ Document ID của khách hàng từ Firestore.
    // KHÔNG gắn [FirestoreProperty] vì đây không phải là một trường dữ liệu trong document Firestore.
    // Document ID được quản lý bởi Firestore và được truy cập thông qua DocumentSnapshot.Id.
    public string customerId { get; set; }

    [FirestoreProperty("phone")]
    public string phone { get; set; }

    [FirestoreProperty("name")]
    public string name { get; set; }

    [FirestoreProperty("address")]
    public string address { get; set; }

    // Thêm trường mới cho Mã số thuế
    [FirestoreProperty("taxId")]
    public string taxId { get; set; }

    // Constructor mặc định cần thiết cho Firestore để deserialize dữ liệu
    public CustomerData() { }
}
