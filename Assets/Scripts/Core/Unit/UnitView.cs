using UnityEngine;
using Unity.Entities;
using ProjectTowerRpg.Core.Presentation;

namespace ProjectTowerRpg.ECS.Systems
{
    public class UnitView : MonoBehaviour
    {
        [Header("Unit View Passport")]
        public string uid;
        public string unitId;

        [HideInInspector] public bool IsLinked;
        [System.NonSerialized] public Entity entity;

        public SyncTransformWithEntity Sync { get; private set; }

        private bool _unregistered;

        private void Awake()
        {
            Sync = GetComponent<SyncTransformWithEntity>();
        }

        /// <summary>
        /// Вызывается системой видимости при спавне 3D-тела.
        /// Регистрирует вьюху в реестре Entity → View.
        /// </summary>
        public void LinkToEntity(Entity boundEntity)
        {
            if (boundEntity == Entity.Null) return;

            // Переиспользование под другую сущность — сначала отвязываемся от старой.
            if (IsLinked && entity != Entity.Null && entity != boundEntity)
                Unregister();

            entity = boundEntity;

            // К моменту привязки компонент Sync уже точно навешан — обновим кэш, если не нашли в Awake.
            if (Sync == null)
                Sync = GetComponent<SyncTransformWithEntity>();

            EntityViewRegistry.Register(entity, this);
            IsLinked = true;
            _unregistered = false;
        }

        private void Unregister()
        {
            if (_unregistered) return;
            if (!IsLinked || entity == Entity.Null) return;

            EntityViewRegistry.Unregister(entity);
            _unregistered = true;
            IsLinked = false;
        }

        private void OnDisable() => Unregister();
        private void OnDestroy() => Unregister();
    }
}

