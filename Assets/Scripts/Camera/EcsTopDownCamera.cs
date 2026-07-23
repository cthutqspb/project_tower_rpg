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
    [SerializeField] private float rotateSpeed = 0.1f;    

    private EntityManager _entityManager;
    private EntityQuery _playerQuery;
    
    // Вместо new @InputSystem_Actions() используем ссылку на глобальный экшен дельты
    private InputAction _cameraRotateAction;
    private float _currentAngle = 0f;

    void Start()
    {
        _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        _playerQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<LocalTransform>(),
            ComponentType.ReadWrite<MovementComponent>(), // Нам нужен доступ на запись, чтобы обновлять угол
            ComponentType.ReadOnly<PlayerTag>()
        );

        // Подключаемся к общему запущенному экшену дельты из редактора.
        // Если мышь над UI, этот экшен автоматически вернет 0 в ReadValue!
        _cameraRotateAction = UnityEngine.InputSystem.InputSystem.actions.FindAction("Player/CameraRotate");
    }

    void LateUpdate()
    {
        if (!_playerQuery.IsEmpty)
        {
            Entity playerEntity = _playerQuery.GetSingletonEntity();
            LocalTransform playerTransform = _entityManager.GetComponentData<LocalTransform>(playerEntity);
            var movementComponent = _entityManager.GetComponentData<MovementComponent>(playerEntity);
            
            float3 playerPosition = playerTransform.Position;

            // =========================================================================
            // 1. ПОЛУЧЕНИЕ СОСТОЯНИЙ ИЗ ECS-КОМПОНЕНТА И СЧИТЫВАНИЕ ДЕЛЬТЫ
            // =========================================================================
            // Кнопки мыши проверила ECS-система ввода. Если клик был по окну интерфейса, 
            // обе переменные ниже автоматически прилетят сюда как false.
            bool isLookingOrOrbiting = movementComponent.isLookAroundMode || movementComponent.isRmbOrMmbPressed;

            //Debug.Log($"IS isLmbPressed{movementComponent.isLookAroundMode}{movementComponent.isRmbOrMmbPressed}{isLookingOrOrbiting}");

            if (isLookingOrOrbiting && _cameraRotateAction != null)
            {
                // Считываем дельту. Из-за настройки No input redirection на UI, 
                // если мышь над BaseWindow, ReadValue вернет строго 0.
                float mouseDeltaX = _cameraRotateAction.ReadValue<float>();
                _currentAngle += mouseDeltaX * rotateSpeed * Time.deltaTime;
            }

            // Актуализируем угол орбиты в компоненте перемещения игрока
            if (_entityManager.Exists(playerEntity))
            {
                movementComponent.cameraAngle = _currentAngle; 
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

