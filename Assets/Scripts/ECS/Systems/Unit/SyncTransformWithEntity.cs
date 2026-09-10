using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using ProjectTowerRpg.ECS.Components;

public class SyncTransformWithEntity : MonoBehaviour
{
    private EntityManager _entityManager;
    private Entity _boundEntity = Entity.Null; // Наша жестко привязанная ECS-душа
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
        // Если связь еще не установлена или сущность стёрта — стоим в покое, не дергаем сцену.
        // Зачисткой 3D-тела со сцены теперь монопольно и безбажно рулит UnitViewDestroySystem!
        if (!_isInitialized || _boundEntity == Entity.Null || !_entityManager.Exists(_boundEntity)) return;

        // 🦾 ОПТИМИЗАЦИЯ ОЗУ: В один присест забираем трансформ из памяти чанка
        var localTransform = _entityManager.GetComponentData<LocalTransform>(_boundEntity);
        
        // Перемещаем и поворачиваем визуальное тело на Meadows-карте вслед за ECS
        transform.position = localTransform.Position;
        transform.rotation = localTransform.Rotation;

        // 🪦 WOW/BG3 КАНОН МAРИОНEТКИ: Проверяем тэг смерти прямо в ОЗУ чанка за 0 наносекунд нагрузки!
        if (_animator != null && _entityManager.HasComponent<IsDeadTag>(_boundEntity))
        {
            // 1. Принудительно выжигаем в Аниматоре все оси движения в ноль кадра,
            // чтобы лежащий или падающий труп не пытался ложно бежать или скользить!
            _animator.SetFloat("VelocityX", 0f);
            _animator.SetFloat("VelocityZ", 0f);
            _animator.SetFloat("VelocityY", 0f);
            
            // 2. 🎯 Включаем состояние смерти в твоем Аниматоре!
            // (Зайди в Unity Animator Controller и добавь булеан параметр "IsDead")
            _animator.SetBool("IsDead", true);
            
            return; // 🦾 Сущность мертва — пулей выходим, наглухо блокируя всю живую логику холмов и камер!
        }

        // Если юнит девственно жив — страхуем Аниматор от застревания в позе трупа
        if (_animator != null)
        {
            _animator.SetBool("IsDead", false);
        }

        // 🧬 СИНХРОНИЗАЦИЯ 8-СТОРОННЕГО BLEND TREE С УЧЕТОМ КАМЕРЫ И ИИ (Только для живых!):
        if (_entityManager.HasComponent<MovementComponent>(_boundEntity))
        {
            var moveData = _entityManager.GetComponentData<MovementComponent>(_boundEntity);
            
            if (_animator != null)
            {
                // Проверяем, движется ли юнит вообще (по квадрату длины вектора направления)
                bool isMoving = math.lengthsq(moveData.Direction) > 0.001f;

                if (isMoving)
                {
                    // Проверяем маркер: это Игрок или Монстр/NPC?
                    if (_entityManager.HasComponent<PlayerTag>(_boundEntity))
                    {
                        // 🧙‍♂️ ИГРОК (WoW/BG3 стрейф-канон без лунной походки):
                        float cameraAngle = moveData.CameraAngle;
                        float3 cameraForward = new float3(math.sin(cameraAngle), 0f, math.cos(cameraAngle));
                        float3 cameraRight = new float3(cameraForward.z, 0f, -cameraForward.x);
                        
                        float3 worldMoveVector = (cameraForward * moveData.Direction.z) + (cameraRight * moveData.Direction.x);
                        
                        // Переводим мировой вектор движения в локальное空间 "носа" персонажа
                        float3 localDir = math.mul(math.inverse(localTransform.Rotation), worldMoveVector);

                        _animator.SetFloat("VelocityX", localDir.x);
                        _animator.SetFloat("VelocityZ", localDir.z);
                    }
                    else
                    {
                        // 💀 МОНСТРЫ И NPC: Локальное направление относительно их собственного разворота
                        float3 localDir = math.mul(math.inverse(localTransform.Rotation), moveData.Direction);

                        _animator.SetFloat("VelocityX", localDir.x);
                        _animator.SetFloat("VelocityZ", localDir.z);
                    }
                }
                else
                {
                    // Юнит стоит в покое — сбрасываем бленд-дерево в Idle
                    _animator.SetFloat("VelocityX", 0f);
                    _animator.SetFloat("VelocityZ", 0f);
                }

                _animator.SetFloat("VelocityY", moveData.Direction.y);
                _animator.SetBool("IsGrounded", moveData.IsGrounded);
            }
        }        
    }
}

