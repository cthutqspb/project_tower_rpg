using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace ProjectTowerRpg.ECS.Components
{
    public struct ProjectileTag : IComponentData { }

    // 🦾 СИ-ФИКС №1: Добавляем путь префаба прямо в приказ
    public struct ProjectileSpawnRequest : IComponentData
    {
        public Entity CasterEntity;   
        public Entity TargetEntity;   
        public FixedString32Bytes AbilityId; 
        public FixedString64Bytes PrefabPath; // <-- СЮДА ВШИВАЕМ ПУТЬ ДО КУБА/VFX
    }

    // 🦾 СИ-ФИКС №2: Добавляем путь префаба в сам летящий снаряд
    public struct ProjectileMovement : IComponentData
    {
        public float3 Direction;      
        public float Speed;           
        public Entity TargetEntity;   
        public Entity CasterEntity;   
        public FixedString32Bytes AbilityId; 
        public FixedString64Bytes PrefabPath; // <-- ЧТОБЫ КЛИЕНТ ПРОЧИТАЛ ИЗ ЧАНКА
    }
}


