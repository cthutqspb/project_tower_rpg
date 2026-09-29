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
        [System.NonSerialized] public Entity entity;

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

        private void OnDisable()
        {
            // Страхуем ОЗУ: если вьюху выключили — выписываем её из реестра событий каста
            if (IsLinked && entity != Entity.Null)
            {
                UnitViewRegistry.Unregister(entity);
            }
        }

        private void OnDestroy()
        {
            // Железная зачистка при уничтожении (куллинге или смерти трупа)
            if (IsLinked && entity != Entity.Null)
            {
                UnitViewRegistry.Unregister(entity);
            }
        }
    }
}

