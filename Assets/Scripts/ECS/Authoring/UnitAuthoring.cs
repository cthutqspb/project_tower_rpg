using UnityEngine;
using Unity.Entities;
using ProjectTowerRpg.ECS.Components;

namespace ProjectTowerRpg.ECS.Authoring
{
    // 🏛️ ЕДИНЫЙ КОМПОНЕНТ-АВТОР ДЛЯ ВСЕХ СУЩЕСТВ В ИГРЕ (Игрок, Скелет, Моб):
    public class UnitAuthoring : MonoBehaviour
    {
        public float moveSpeed = 5.0f;
        public bool isPlayer = false; // Главный Си-тумблер разделения сущностей!
    }

    // 🔬 ВСЕЯДНЫЙ СИ-БЕЙКЕР: Нарезает компоненты в ОЗУ в зависимости от настроек
    public class UnitBaker : Baker<UnitAuthoring>
    {
        public override void Bake(UnitAuthoring authoring)
        {
            // Получаем Entity для нашего 3D-объекта
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // 1. Абсолютно всем юнитам пришиваем базовый компонент движения!
            AddComponent(entity, new MovementComponent
            {
                speed = authoring.moveSpeed,
                direction = Unity.Mathematics.float3.zero // Изначально все стоят смирно!
            });

            // 2. Если в инспекторе взведен флаг игрока — нагло доливаем в чанк Тег Игрока!
            if (authoring.isPlayer)
            {
                AddComponent<PlayerTag>(entity);
            }
        }
    }
}

