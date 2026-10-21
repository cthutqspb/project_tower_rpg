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

    // Внутренний кэш живых эффектов каста в ОЗУ вьюхи
    private GameObject _leftHandVfx;
    private GameObject _rightHandVfx;

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
        if (!_isInitialized || _boundEntity == Entity.Null) return;

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

            // 1. Физический хитбокс стен выключаем наглухо, чтобы тян могла бегать сквозь кости!
            if (_hitbox != null && _hitbox.enabled) _hitbox.enabled = false;

            // 2. 🦾 СИ-ФИКС ДЛЯ КЛИКБОКСА (Ужимаем в плоский блин на земле):
            if (_clickbox != null)
            {
                if (_clickbox is CapsuleCollider capsule)
                {
                    capsule.height = 0.2f;              // Схлопываем высоту до 20 сантиметров
                    capsule.center = new Vector3(0f, 0.1f, 0f); // Опускаем центр вплотную к полу
                }
                else if (_clickbox is BoxCollider box)
                {
                    box.size = new Vector3(box.size.x, 0.2f, box.size.z); // Ужимаем коробку по Y
                    box.center = new Vector3(0f, 0.1f, 0f);
                }
            }
            
            return; 
        }

        // =========================================================================
        // 🦾 ОБРАТНЫЙ СБРОС ПРИ ВОСКРЕШЕНИИ / СПАВНЕ (Возвращаем исходный ААА-рост):
        // =========================================================================
        if (_animator != null) _animator.SetBool("IsDead", false);
        
        if (_hitbox != null && !_hitbox.enabled) _hitbox.enabled = true;
        
        if (_clickbox != null)
        {
            if (_clickbox is CapsuleCollider capsule && capsule.height < 0.5f)
            {
                capsule.height = 2.0f;              // Возвращаем стандартный дефолтный рост 2 метра
                capsule.center = new Vector3(0f, 1.0f, 0f); // Центр на высоте 1 метр
            }
            else if (_clickbox is BoxCollider box && box.size.y < 0.5f)
            {
                box.size = new Vector3(box.size.x, 2.0f, box.size.z);
                box.center = new Vector3(0f, 1.0f, 0f);
            }
        }

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

    /// <summary>
    /// 🦾 МЕКАНИМ-КАНOН БЕЗ GUI: Автоматически качает общий префаб и красит его по костям аватара
    /// </summary>
    public void StartCastVfx(ProjectTowerRpg.Core.Colors.ColorUtils.ElementColors colors)
    {
        StopCastVfx();

        if (_animator == null) return;

        // Аппаратно качаем ОДИН общий префаб для всех 40+ существ в игре!
        var handGlowPrefab = Resources.Load<GameObject>("Shared/HandGlowVfx");
        if (handGlowPrefab == null) return;

        // Меканим за 0 наносекунд выдает кости, запеченные в Humanoid-конфигураторе
        Transform leftHand = _animator.GetBoneTransform(HumanBodyBones.LeftHand);
        Transform rightHand = _animator.GetBoneTransform(HumanBodyBones.RightHand);

        if (leftHand != null)
        {
            _leftHandVfx = Object.Instantiate(handGlowPrefab, leftHand.position, Quaternion.identity, leftHand);
            ApplyColorToVfx(_leftHandVfx, colors.GlowColor); 
        }

        if (rightHand != null)
        {
            _rightHandVfx = Object.Instantiate(handGlowPrefab, rightHand.position, Quaternion.identity, rightHand);
            ApplyColorToVfx(_rightHandVfx, colors.GlowColor);
        }
    }

        /// <summary>
    /// 🦾 МГНОВЕННЫЙ БУРСТ БЕЗ GUI: Теперь префаб привязывается к костям рук, убирая лаги в прыжке!
    /// </summary>
    public void PlayInstantCastVfx(ProjectTowerRpg.Core.Colors.ColorUtils.ElementColors colors)
    {
        if (_animator == null) return;

        var instantMuzzlePrefab = Resources.Load<GameObject>("Shared/InstantMuzzleVfx");
        if (instantMuzzlePrefab == null) return;

        Transform leftHand = _animator.GetBoneTransform(HumanBodyBones.LeftHand);
        Transform rightHand = _animator.GetBoneTransform(HumanBodyBones.RightHand);

        // 👑 СИ-ФИКС: Передаем leftHand / rightHand как четвертый аргумент (Parent)!
        // Теперь префаб взрыва намертво привязан к кистям тян и будет лететь вместе с ней в прыжке!
        if (leftHand != null)
        {
            GameObject leftVfx = Object.Instantiate(instantMuzzlePrefab, leftHand.position, Quaternion.identity, leftHand);
            ApplyColorToVfx(leftVfx, colors.GlowColor);
        }

        if (rightHand != null)
        {
            GameObject rightVfx = Object.Instantiate(instantMuzzlePrefab, rightHand.position, Quaternion.identity, rightHand);
            ApplyColorToVfx(rightVfx, colors.GlowColor);
        }
    }

    public void StopCastVfx()
    {
        if (_leftHandVfx != null) { Object.Destroy(_leftHandVfx); _leftHandVfx = null; }
        if (_rightHandVfx != null) { Object.Destroy(_rightHandVfx); _rightHandVfx = null; }
    }

    private void ApplyColorToVfx(GameObject vfxObj, Color hdrColor)
    {
        if (vfxObj == null) return;

        var allParticleSystems = vfxObj.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in allParticleSystems)
        {
            var main = ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(hdrColor);
        }

        var allRenderers = vfxObj.GetComponentsInChildren<Renderer>(true);
        foreach (var renderer in allRenderers)
        {
            if (renderer.material != null)
            {
                if (renderer.material.HasProperty("_BaseColor")) 
                    renderer.material.SetColor("_BaseColor", hdrColor);
                else if (renderer.material.HasProperty("_Color")) 
                    renderer.material.SetColor("_Color", hdrColor);
                else if (renderer.material.HasProperty("_TintColor")) 
                    renderer.material.SetColor("_TintColor", hdrColor);

                if (renderer.material.HasProperty("_EmissionColor"))
                {
                    renderer.material.SetColor("_EmissionColor", hdrColor);
                    renderer.material.EnableKeyword("_EMISSION"); 
                }
            }
        }
    }
}

