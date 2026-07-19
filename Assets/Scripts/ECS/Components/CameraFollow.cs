using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    // Ссылка на родительский объект твоей тянки (Amy) из основной сцены
    public Transform target;

    // Смещение камеры относительно игрока (высота и дальность под изометрию)
    public Vector3 offset = new Vector3(0f, 8f, -10f);

    // Скорость сглаживания (чем меньше, тем плавнее камера летит за игроком)
    public float smoothSpeed = 5f;

    void LateUpdate()
    {
        if (target == null) return;

        // Рассчитываем идеальную позицию, где должна быть камера
        Vector3 desiredPosition = target.position + offset;

        // Плавно перемещаем камеру из текущей точки в идеальную (аналог Lerp)
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
        
        // Присваиваем позицию камере
        transform.position = smoothedPosition;

        // Заставляем камеру всегда жестко смотреть в точку, где стоит игрок
        transform.LookAt(target.position);
    }
}

