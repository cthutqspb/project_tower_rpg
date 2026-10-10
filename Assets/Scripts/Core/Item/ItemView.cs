using UnityEngine;
using Unity.Entities;
using ProjectTowerRpg.Core.Presentation;

namespace ProjectTowerRpg.ECS.Systems
{
    public class ItemView : MonoBehaviour
    {
        [Header("Item View Passport")]
        public string uid;      // "i_123_456"
        public string itemId;   // "iron_sword"

        [HideInInspector] public bool IsLinked;
        [HideInInspector] [System.NonSerialized] public Entity entity = Entity.Null;

        private bool _unregistered;

        /// <summary>
        /// Вызывается при спавне или линковке куба на сцене.
        /// Регистрирует вьюху в реестре Entity → View.
        /// </summary>
        public void LinkToEntity(Entity boundEntity)
        {
            if (boundEntity == Entity.Null) return;

            // Переиспользование под другую сущность — сначала отвязываемся от старой.
            if (IsLinked && entity != Entity.Null && entity != boundEntity)
                Unregister();

            entity = boundEntity;

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
