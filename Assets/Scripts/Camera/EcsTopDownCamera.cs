using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine.InputSystem;
using ProjectTowerRpg.ECS.Components; // Убедитесь, что тут лежит ваш PlayerTag

public class EcsTopDownCamera : MonoBehaviour
{
    [Header("Настройки орбиты")]
    [SerializeField] private float distanceToPlayer = 10f; 
    [SerializeField] private float cameraHeight = 8f;     
    [SerializeField] private float rotateSpeed = 2f;      

    private EntityManager _entityManager;
    private EntityQuery _playerQuery;
    private @InputSystem_Actions _inputActions;
    private float _currentAngle = 0f;

    void Awake()
    {
        // Инициализируем наш сгенерированный AAA-ввод
        _inputActions = new @InputSystem_Actions();
    }

    void OnEnable() => _inputActions.Enable();
    void OnDisable() => _inputActions.Disable();

    void Start()
    {
        // Каноничный доступ к менеджеру сущностей ECS
        _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

        // Создаем эффективный быстрый кэш-запрос для поиска игрока
        _playerQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<LocalTransform>(),
            ComponentType.ReadOnly<PlayerTag>()
        );
    }

    // Использование LateUpdate — закон для камер, чтобы избежать рывков рендера
    void LateUpdate()
    {
        // 1. ИЩЕМ СУЩНОСТЬ ИГРОКА В ECS МИРЕ
        if (!_playerQuery.IsEmpty)
        {
            // Получаем сущность и вытаскиваем ее компонент LocalTransform
            Entity playerEntity = _playerQuery.GetSingletonEntity();
            LocalTransform playerTransform = _entityManager.GetComponentData<LocalTransform>(playerEntity);
            float3 playerPosition = playerTransform.Position;

            // 2. СЧИТЫВАЕМ ВРАЩЕНИЕ МЫШИ (BG3-стиль)
            if (Mouse.current != null && (Mouse.current.middleButton.isPressed || Mouse.current.rightButton.isPressed))
            {
                // Читаем ось из вашего Input Actions
                float mouseDeltaX = _inputActions.Player.CameraRotate.ReadValue<float>();
                _currentAngle += mouseDeltaX * rotateSpeed * Time.deltaTime;
            }

            // 3. ТРИГОНОМЕТРИЧЕСКИЙ РАСЧЕТ ОРБИТЫ СВЕРХУ-ВНИЗ
            float offsetX = Mathf.Sin(_currentAngle) * distanceToPlayer;
            float offsetZ = Mathf.Cos(_currentAngle) * distanceToPlayer;

            Vector3 targetPosition = new Vector3(
                playerPosition.x + offsetX,
                playerPosition.y + cameraHeight,
                playerPosition.z + offsetZ
            );

            // 4. ПЛАВНОЕ ПЕРЕМЕЩЕНИЕ И ПОВОРОТ
            transform.position = Vector3.Lerp(transform.position, targetPosition, 10f * Time.deltaTime);
            transform.LookAt(new Vector3(playerPosition.x, playerPosition.y, playerPosition.z));
        }
    }
}

