using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine.InputSystem;
using ProjectTowerRpg.ECS.Components;

public class EcsTopDownCamera : MonoBehaviour
{
    [Header("Настройки орбиты")]
    [SerializeField] private float distanceToPlayer = 10f; 
    [SerializeField] private float cameraHeight = 8f;     
    [SerializeField] private float rotateSpeed = 0.1f;    // Подкручиваем под плавность дельты
    [SerializeField] private float autoFollowSmooth = 3f;  // Скорость авто-доворота за спину

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

            // 1. ПЕРЕХВАТ УПРАВЛЕНИЯ: МЫШЬ ИЛИ АВТО-СЛЕДОВАНИЕ?
            if (Mouse.current != null && (Mouse.current.middleButton.isPressed || Mouse.current.rightButton.isPressed))
            {
                // Если сеньор зажал мышь — крутим камеру свободно руками по нашему Linux-засову!
                float mouseDeltaX = _inputActions.Player.CameraRotate.ReadValue<float>();
                _currentAngle += mouseDeltaX * rotateSpeed * Time.deltaTime;
            }
            else
            {
                // 🎰 ФИКС СЛEПOТЫ .NET 10 (КАНОН UNITY.MATHEMATICS):
                // Передаем Си-кватернион в метод math.Euler(), который нативно
                // разложит его на три угла в радианах прямо в L1-кэше процессора!
                float3 eulerAngles = math.Euler(playerTransform.Rotation);
                
                // Нам нужна строго ось Y (вращение вокруг вертикали).
                // ВНИМАНИЕ: math.Euler возвращает радианы! Нам нужно прибавить math.PI (180 градусов),
                // чтобы камера шёлково встала строго СЗАДИ куба, а не внутри его пупа!
                float playerTargetAngle = eulerAngles.y + math.PI;

                // Плавно интерполируем угол камеры к углу персонажа через Mathf.LerpAngle.
                // Так как LerpAngle требует ГРАДУСЫ, а не радианы, мы нагло умножаем угол
                // на Mathf.Rad2Deg (Радианы в Градусы) перед интерполяцией!
                float currentAngleDegrees = _currentAngle * Mathf.Rad2Deg;
                float targetAngleDegrees = playerTargetAngle * Mathf.Rad2Deg;
                
                float resultAngleDegrees = Mathf.LerpAngle(currentAngleDegrees, targetAngleDegrees, autoFollowSmooth * Time.deltaTime);
                
                // Переводим результат обратно в радианы для нашей тригонометрии орбиты!
                _currentAngle = resultAngleDegrees * Mathf.Deg2Rad;
            }

            // 2. ТРИГОНОМЕТРИЧЕСКИЙ РАСЧЕТ ОРБИТЫ СВЕРХУ-ВНИЗ
            float offsetX = Mathf.Sin(_currentAngle) * distanceToPlayer;
            float offsetZ = Mathf.Cos(_currentAngle) * distanceToPlayer;

            Vector3 targetPosition = new Vector3(
                playerPosition.x + offsetX,
                playerPosition.y + cameraHeight,
                playerPosition.z + offsetZ
            );

            // 3. ПЛАВНОЕ ПЕРЕМЕЩЕНИЕ И ПОВОРОТ КАМЕРЫ НА СЦЕНЕ
            transform.position = Vector3.Lerp(transform.position, targetPosition, 10f * Time.deltaTime);
            transform.LookAt(new Vector3(playerPosition.x, playerPosition.y, playerPosition.z));
            if (_entityManager.Exists(playerEntity))
{
    var movementComponent = _entityManager.GetComponentData<MovementComponent>(playerEntity);
    
    // Записываем текущий угол орбиты камеры прямо в ОЗУ игрока
    movementComponent.cameraAngle = _currentAngle; 
    
    _entityManager.SetComponentData(playerEntity, movementComponent);
}
        }
    }
}


