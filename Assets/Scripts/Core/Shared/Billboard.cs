using UnityEngine;

namespace ProjectTowerRpg.Core.UI.Components
{
    public class Billboard : MonoBehaviour
    {
        private Transform _cameraTransform;

        private void Start()
        {
            // Кэшируем главную Cinemachine-камеру один раз при старте
            if (Camera.main != null)
            {
                _cameraTransform = Camera.main.transform;
            }
        }

        private void LateUpdate()
        {
            if (_cameraTransform == null) return;

            // Намертво разворачиваем плоский текст лицом к объективу камеры
            transform.LookAt(transform.position + _cameraTransform.rotation * Vector3.forward, _cameraTransform.rotation * Vector3.up);
        }
    }
}

