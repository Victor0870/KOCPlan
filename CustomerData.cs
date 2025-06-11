// File: CustomerData.cs
using Firebase.Firestore;
using System;

[FirestoreData]
[Serializable]
public class CustomerData
{
    // Firestore Document ID cho khách hàng
    [FirestoreProperty(ExcludeFromConversion = true)]
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

    public CustomerData() { }
}
