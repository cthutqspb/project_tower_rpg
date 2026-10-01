using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using ProjectTowerRpg.ECS.Components;

public class SyncTransformWithEntity : MonoBehaviour
{
    public enum ViewType
    {
        Unit,
        Projectile,
        Item,
        Object
    }

    [Header("Настройка роли визуального тела")]
    public ViewType Type = ViewType.Unit;

    private EntityManager _entityManager;
    private Entity _boundEntity = Entity.Null; 
    private bool _isInitialized = false;

    // Кэш-компоненты для юнитов
    private Animator _animator; 
    private Collider _hitbox;
    private Collider _clickbox;
    
    public Entity BoundEntity => _boundEntity;

    public void Initialize(Entity entity)
    {
        _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        _boundEntity = entity;
        _isInitialized = true;

        // Инициализируем тяжелые компоненты строго по роли
        switch (Type)
        {
            case ViewType.Unit:
                _animator = GetComponent<Animator>();
                var colliders = GetComponents<Collider>();
                foreach (var c in colliders)
                {
                    if (c.isTrigger) _clickbox = c;
                    else _hitbox = c;
                }
                break;

            case ViewType.Projectile:
            case ViewType.Item:
            case ViewType.Object:
                // Снарядам, луту и дверям физические хитбоксы юнитов и аниматоры не нужны
                break;
        }
    }

    void Update()
    {
        if (!_isInitialized || _boundEntity == Entity.Null || !_entityManager.Exists(_boundEntity)) return;

        // 🦾 ОБЩАЯ МАТЕМАТИКА: Абсолютно все типы сущностей покадрово двигаются за ECS
        var localTransform = _entityManager.GetComponentData<LocalTransform>(_boundEntity);
        transform.position = localTransform.Position;
        transform.rotation = localTransform.Rotation;

        // 🎯 СИТУАЦИОННЫЙ СВИТЧ ДЛЯ РАЗДЕЛЕНИЯ ЛОГИКИ ПРЕЗЕНТАЦИИ
        switch (Type)
        {
            case ViewType.Unit:
                UpdateUnitVisuals(localTransform);
                break;

            case ViewType.Projectile:
                // Здесь в будущем можно крутить локальный скейл или покадровый жир самого снаряда
                break;

            case ViewType.Item:
                // Логика вращения шмотки на земле или мерцания подсветки лута
                break;

            case ViewType.Object:
                // Логика анимации открытия дверей, сундуков при изменении ECS-компонента стейта
                break;
        }
    }

    // 💀 ИЗОЛИРОВАННАЯ ЛОГИКА ЮНИТОВ (Анимации, Хитбоксы, Blend Tree)
    private void UpdateUnitVisuals(LocalTransform localTransform)
    {
        if (_animator != null && _entityManager.HasComponent<IsDeadTag>(_boundEntity))
        {
            _animator.SetFloat("VelocityX", 0f);
            _animator.SetFloat("VelocityZ", 0f);
            _animator.SetFloat("VelocityY", 0f);
            _animator.SetBool("IsDead", true);

            if (_hitbox != null && _hitbox.enabled) _hitbox.enabled = false;
            return; 
        }

        if (_animator != null) _animator.SetBool("IsDead", false);
        if (_hitbox != null && !_hitbox.enabled) _hitbox.enabled = true;

        if (_entityManager.HasComponent<MovementComponent>(_boundEntity))
        {
            var moveData = _entityManager.GetComponentData<MovementComponent>(_boundEntity);
            
            if (_animator != null)
            {
                bool isMoving = math.lengthsq(moveData.Direction) > 0.001f;

                if (isMoving)
                {
                    if (_entityManager.HasComponent<PlayerTag>(_boundEntity))
                    {
                        // 🧙‍♂️ ИГРОК (WoW/BG3 стрейф-канон без лунной походки)
                        float cameraAngle = moveData.CameraAngle;
                        float3 cameraForward = new float3(math.sin(cameraAngle), 0f, math.cos(cameraAngle));
                        float3 cameraRight = new float3(cameraForward.z, 0f, -cameraForward.x);
                        float3 worldMoveVector = (cameraForward * moveData.Direction.z) + (cameraRight * moveData.Direction.x);
                        float3 localDir = math.mul(math.inverse(localTransform.Rotation), worldMoveVector);

                        _animator.SetFloat("VelocityX", localDir.x);
                        _animator.SetFloat("VelocityZ", localDir.z);
                    }
                    else
                    {
                        // 💀 МОНСТРЫ И NPC
                        float3 localDir = math.mul(math.inverse(localTransform.Rotation), moveData.Direction);
                        _animator.SetFloat("VelocityX", localDir.x);
                        _animator.SetFloat("VelocityZ", localDir.z);
                    }
                }
                else
                {
                    _animator.SetFloat("VelocityX", 0f);
                    _animator.SetFloat("VelocityZ", 0f);
                }

                _animator.SetFloat("VelocityY", moveData.Direction.y);
                _animator.SetBool("IsGrounded", moveData.IsGrounded);
            }
        }
    }
}

