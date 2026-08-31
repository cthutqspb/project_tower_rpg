using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using ProjectTowerRpg.ECS.Components;

public class SyncTransformWithEntity : MonoBehaviour
{
    private EntityManager _entityManager;
    private Entity _boundEntity = Entity.Null; // Наша жестко привязаная ECS-душа
    private Animator _animator; 
    private bool _isInitialized = false;

    // Стерильный инициализатор. Вызывается извне универсальной системой связывания
    public void Initialize(Entity entity)
    {
        _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        _animator = GetComponent<Animator>();
        _boundEntity = entity;
        _isInitialized = true;
    }

    void Update()
    {
        // Если связь еще не установлена — стоим в покое, не дергаем сцену
        if (!_isInitialized || _boundEntity == Entity.Null) return;

        // 🪐 СИСТЕМA ЧAНКOВ (Душа улетела — тело исчезло):
        if (!_entityManager.Exists(_boundEntity))
        {
            Destroy(gameObject);
            return;
        }

        // Вытаскиваем координаты и вращение конкретно НАШЕЙ ECS-сущности из ОЗУ
        var localTransform = _entityManager.GetComponentData<LocalTransform>(_boundEntity);
        
        // Перемещаем и поворачиваем визуальное тело на Meadows-карте вслед за ECS
        transform.position = localTransform.Position;
        transform.rotation = localTransform.Rotation;

        
        // 🧬 СИНХРОНИЗАЦИЯ 8-СТОРОННЕГО BLEND TREE С УЧЕТОМ КАМЕРЫ И ИИ:
        if (_entityManager.HasComponent<MovementComponent>(_boundEntity))
        {
            var moveData = _entityManager.GetComponentData<MovementComponent>(_boundEntity);
            var transformData = _entityManager.GetComponentData<LocalTransform>(_boundEntity);
            
            if (_animator != null)
            {
                // Проверяем, движется ли юнит вообще (по квадрату длины)
                bool isMoving = math.lengthsq(moveData.Direction) > 0.001f;

                if (isMoving)
                {
                                        // Проверяем маркер: это Игрок или Монстр/NPC?
                    if (_entityManager.HasComponent<PlayerTag>(_boundEntity))
                    {
                        // 🧙‍♂️ ИГРОК (ИСПРАВЛЕНО ДЛЯ WOW/BG3 КАНОНА):
                        // Чтобы стрейфы и бег назад не превращались в лунную походку,
                        // нам нужно пересчитать направление движения относительно текущего разворота туловища!
                        
                        // Считаем вектор бега игрока относительно камеры (копируем логику из MovementSystem)
                        float cameraAngleInRadians = moveData.CameraAngle;
                        float3 cameraForward = new float3(math.sin(cameraAngleInRadians), 0f, math.cos(cameraAngleInRadians));
                        float3 cameraRight = new float3(cameraForward.z, 0f, -cameraForward.x);
                        
                        float3 worldMoveVector = (cameraForward * moveData.Direction.z) + (cameraRight * moveData.Direction.x);
                        
                        // Переводим этот мировой вектор движения в локальное пространство "носа" персонажа
                        float3 localDir = math.mul(math.inverse(transformData.Rotation), worldMoveVector);

                        // Передаем в Аниматор чистые локальные оси. 
                        // Теперь если перс пятится назад, Аниматор включит правильные шаги без скольжения!
                        _animator.SetFloat("VelocityX", localDir.x);
                        _animator.SetFloat("VelocityZ", localDir.z);
                    }
                    else
                    {
                        // 💀 МОНСТРЫ И NPC: Оставляем ваш рабочий вариант (он написан идеально)
                        float3 localDir = math.mul(math.inverse(transformData.Rotation), moveData.Direction);

                        _animator.SetFloat("VelocityX", localDir.x);
                        _animator.SetFloat("VelocityZ", localDir.z);
                    }
                }
                else
                {
                    // Юнит стоит на паузе раздумий — сбрасываем бленд в абсолютный центр (Покой/Idle)
                    _animator.SetFloat("VelocityX", 0f);
                    _animator.SetFloat("VelocityZ", 0f);
                }

                _animator.SetFloat("VelocityY", moveData.Direction.y);
                _animator.SetBool("IsGrounded", moveData.IsGrounded);
            }
        }        
    }
}

