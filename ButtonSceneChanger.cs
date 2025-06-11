using UnityEngine;
using UnityEngine.SceneManagement; // Thêm thư viện này để quản lý Scene
using UnityEngine.UI; // Thêm thư viện này để truy cập các thành phần UI

public class ButtonSceneChanger : MonoBehaviour
{
    // Đặt tên scene bạn muốn chuyển đến ở đây
    public string sceneToLoad = "Inventory"; 

    void Start()
    {
        // Gán hàm OnButtonClick vào sự kiện click của Button
        // Đảm bảo script này được đính kèm vào GameObject có component Button
        Button button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(OnButtonClick);
        }
        else
        {
            Debug.LogError("Không tìm thấy Button component trên GameObject này. Vui lòng đính kèm script vào GameObject có Button.");
        }
    }

    void OnButtonClick()
    {
        // Tải scene theo tên đã chỉ định
        SceneManager.LoadScene(sceneToLoad);
        Debug.Log("Chuyển sang scene: " + sceneToLoad);
    }
}
