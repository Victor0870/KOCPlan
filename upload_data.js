// upload_data.js

const admin = require('firebase-admin');
const serviceAccount = require('./serviceAccountKey.json'); // Thay thế bằng tên file khóa của bạn

// Khởi tạo Firebase Admin SDK
admin.initializeApp({
  credential: admin.credential.cert(serviceAccount)
});

const db = admin.firestore();

// Đường dẫn tới file JSON chứa dữ liệu mẫu
const jsonDataPath = './sample_data.json'; // Thay thế bằng tên file JSON của bạn
const sampleData = require(jsonDataPath);

// Tên collection chính để lưu trữ dữ liệu mẫu (như đã thảo luận: 'sample_inventories')
const COLLECTION_NAME = 'sample_inventories';

async function uploadSampleData() {
  console.log('Bắt đầu tải dữ liệu mẫu lên Firestore...');

  try {
    for (const industryKey in sampleData) {
      if (sampleData.hasOwnProperty(industryKey)) {
        const products = sampleData[industryKey];
        const industryDocRef = db.collection(COLLECTION_NAME).doc(industryKey); // 'pharma' là ID document
        const productsCollectionRef = industryDocRef.collection('products'); // sub-collection 'products'

        console.log(`Đang xử lý ngành hàng: ${industryKey}`);

        // Sử dụng batch write để ghi nhiều document hiệu quả hơn
        const batch = db.batch();
        let productCount = 0;

        for (const productId in products) {
          if (products.hasOwnProperty(productId)) {
            const productData = products[productId];
            // Không cần đặt ID tự động, dùng ID từ JSON (SP001, SP002...)
            // Nếu bạn muốn Firestore tự tạo ID, hãy bỏ .doc(productId) và dùng .add(productData)
            const productDocRef = productsCollectionRef.doc(productId);
            batch.set(productDocRef, productData);
            productCount++;
          }
        }
        
        await batch.commit();
        console.log(`  Đã tải thành công ${productCount} sản phẩm cho ngành ${industryKey}.`);
      }
    }
    console.log('Tất cả dữ liệu mẫu đã được tải lên Firestore thành công!');
  } catch (error) {
    console.error('Lỗi khi tải dữ liệu mẫu:', error);
  } finally {
    // Đảm bảo ứng dụng thoát sau khi hoàn tất
    process.exit();
  }
}

// Chạy hàm tải dữ liệu
uploadSampleData();
