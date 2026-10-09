using UnityEngine;
using Unity.Entities;
using ProjectTowerRpg.Core.Units; // Подключаем реестр только на стороне вьюхи

namespace ProjectTowerRpg.ECS.Systems
{
    public class UnitView : MonoBehaviour
    {
        [Header("Unit View Passport")]
        public string uid;      // Забивается для игрока ("player") или генерируется чанком
        public string unitId;   // Забивается в IDE Unity ("skeleton_warrior", "skeleton_mage")

        [HideInInspector] 
        public bool IsLinked = false; // Флаг-замок
        private bool _unregistered;
        [System.NonSerialized] public Entity entity;

        // 🦾 Кэш тяжёлых GetComponent — ищем один раз при инициализации
        public SyncTransformWithEntity Sync { get; private set; }

        private void Awake()
        {
            Sync = GetComponent<SyncTransformWithEntity>();
        }

        /// <summary>
        /// 🦾 СЕКА-ИНИЦИАЛИЗАТОР: Вызывается системой видимости при спавне 3D-тела.
        /// Вьюха сама бережно заносит себя в телефонную книгу реестра!
        /// </summary>
        public void LinkToEntity(Entity boundEntity)
        {
            entity = boundEntity;
            IsLinked = true;

            // Встаем на учет за O(1)
            UnitViewRegistry.Register(entity, this);
        }

        private void Unregister()
        {
            if (_unregistered) return;
            if (!IsLinked || entity == Entity.Null) return;

            UnitViewRegistry.Unregister(entity);
            _unregistered = true;
        }

        private void OnDisable() => Unregister();
        private void OnDestroy() => Unregister();
    }
}

