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

    void LateUpdate()
    {
        // Если связь еще не установлена — стоим в покое, не дергаем сцену
        if (!_isInitialized || _boundEntity == Entity.Null) return;

        // 🪐 СИСТЕМA ЧAНКOВ (Душа улетела — тело исчезло):
        // Если ECS-сущность была уничтожена на бэкенде (выгрузился чанк),
        // 3D-марионетка мгновенно стирает себя со сцены, очищая оперативку!
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

        // 🧬 СИНХРОНИЗАЦИЯ BLEND TREE АНИМАЦИЙ С ECS:
        if (_entityManager.HasComponent<MovementComponent>(_boundEntity))
        {
            var moveData = _entityManager.GetComponentData<MovementComponent>(_boundEntity);
            
            if (_animator != null)
            {
                // Скармливаем компоненты направления (ввода или патруля ИИ) напрямую в твое Blend Tree!
                _animator.SetFloat("VelocityX", moveData.direction.x);
                _animator.SetFloat("VelocityZ", moveData.direction.z);
                _animator.SetFloat("VelocityY", moveData.direction.y);
                _animator.SetBool("IsGrounded", moveData.isGrounded);
            }
        }
    }
}

