using UnityEngine;

namespace Player
{
    public class PlayerCamera : MonoBehaviour
    {
        #region SerializeField
        /// <summary>マウス感度<summary>
        [SerializeField] private float mouseSensitivity = 100.0f;
        /// <summary>プレイヤーのTransform</summary>
        [SerializeField] private Transform playerBody;
        #endregion

        #region UnityEvent
        private void Update()
        {
            LookAround();
        }
        #endregion

        #region PrivateMethod
        /// <summary>
        /// マウス入力に基づいてカメラを回転させる処理
        /// </summary>
        private void LookAround()
        {
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;

            // プレイヤーをY軸周りに回転させる
            playerBody.Rotate(Vector3.up * mouseX);
        }
        #endregion
    }
}