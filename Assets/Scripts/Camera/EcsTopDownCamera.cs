using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using ProjectTowerRpg.ECS.Components;

public class EcsTopDownCamera : MonoBehaviour
{
    [Header("Настройки орбиты")]
    [SerializeField] private float distanceToPlayer = 10f; 
    [SerializeField] private float cameraHeight = 8f;     
    [SerializeField] private float rotateSpeed = 0.1f;    // Подкручиваем под плавность дельты

    private EntityManager _entityManager;
    private EntityQuery _playerQuery;
    private @InputSystem_Actions _inputActions;
    private float _currentAngle = 0f;

    void Awake()
    {
        _inputActions = new @InputSystem_Actions();
    }

    void OnEnable() => _inputActions.Enable();
    void OnDisable() => _inputActions.Disable();

    void Start()
    {
        _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        _playerQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<LocalTransform>(),
            ComponentType.ReadOnly<PlayerTag>()
        );
    }

    void LateUpdate()
    {
        if (!_playerQuery.IsEmpty)
        {
            Entity playerEntity = _playerQuery.GetSingletonEntity();
            LocalTransform playerTransform = _entityManager.GetComponentData<LocalTransform>(playerEntity);
            float3 playerPosition = playerTransform.Position;

            // =========================================================================
            // 1. ПЕРЕХВАТ УПРАВЛЕНИЯ: РАЗДЕЛЕНИЕ ЛКМ (ОСМОТР) И ПКМ/КОЛЕСО (БОЙ)
            // =========================================================================
            bool isLmbPressed = Mouse.current != null && Mouse.current.leftButton.isPressed;
            bool isRmbPressed = Mouse.current != null && Mouse.current.rightButton.isPressed;
            bool isMmbPressed = Mouse.current != null && Mouse.current.middleButton.isPressed;

            // Крутим камеру по орбите, если зажата ВООБЩЕ ЛЮБАЯ кнопка мыши
            if (isLmbPressed || isRmbPressed || isMmbPressed)
            {
                float mouseDeltaX = _inputActions.Player.CameraRotate.ReadValue<float>();
                _currentAngle += mouseDeltaX * rotateSpeed * Time.deltaTime;
            }

            // Шлём данные и переключатель режимов в ОЗУ игрока
            if (_entityManager.Exists(playerEntity))
            {
                var movementComponent = _entityManager.GetComponentData<MovementComponent>(playerEntity);
                
                // Записываем текущий угол орбиты
                movementComponent.cameraAngle = _currentAngle; 
                
                // Включаем режим осмотра ТОЛЬКО если зажат ЛКМ, но не боевые кнопки!
                movementComponent.isLookAroundMode = isLmbPressed && !isRmbPressed && !isMmbPressed;

                movementComponent.isRmbOrMmbPressed = isRmbPressed || isMmbPressed;
                
                _entityManager.SetComponentData(playerEntity, movementComponent);
            }

            // =========================================================================
            // 2. ТРИГОНОМЕТРИЧЕСКИЙ РАСЧЕТ ОРБИТЫ СВЕРХУ-ВНИЗ
            // =========================================================================
            float offsetX = Mathf.Sin(_currentAngle) * distanceToPlayer;
            float offsetZ = Mathf.Cos(_currentAngle) * distanceToPlayer;

            Vector3 targetPosition = new Vector3(
                playerPosition.x + offsetX,
                playerPosition.y + cameraHeight,
                playerPosition.z + offsetZ
            );

            // =========================================================================
            // 3. ПЛАВНОЕ ПЕРЕМЕЩЕНИЕ И ПОВОРОТ КАМЕРЫ НА СЦЕНЕ
            // =========================================================================
            transform.position = Vector3.Lerp(transform.position, targetPosition, 10f * Time.deltaTime);
            transform.LookAt(new Vector3(playerPosition.x, playerPosition.y, playerPosition.z));
        }
    }
}


