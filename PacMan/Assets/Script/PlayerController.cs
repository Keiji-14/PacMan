using UnityEngine;

namespace Player
{
    public class PlayerController : MonoBehaviour
    {
        #region PrivateField
        /// <summary>速度ベクトル</summary>
        private Vector3 velocity;
        #endregion

        #region SerializeField
        /// <summary>移動速度</summary>
        [SerializeField] private float speed;
        [Header("Component")]
        /// <summary>アニメーター</summary>
        [SerializeField] private Animator animator;
        /// <summary>キャラクターコントローラー</summary>
        [SerializeField] private CharacterController characterController;
        #endregion

        #region UnityEvent
        private void Update()
        {
            Move();
        }
        #endregion

        /// <summary>
        /// 移動処理
        /// </summary>
        private void Move()
        {
            float moveX = Input.GetAxis("Horizontal");
            float moveZ = Input.GetAxis("Vertical");

            Vector3 move = transform.right * moveX + transform.forward * moveZ;
            characterController.Move(move * speed * Time.deltaTime);

            characterController.Move(velocity * Time.deltaTime);
        }
    }
}