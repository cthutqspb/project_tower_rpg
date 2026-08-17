using Unity.Entities;
using Unity.Mathematics;

namespace ProjectTowerRpg.ECS.Components
{
    public struct UnitSpawnMarkerComponent : IComponentData
    {
        public Entity PrefabEntity;     // 🏗️ Ссылка на готовый ECS-префаб, запекаемый Unity!
        public Unity.Collections.FixedString32Bytes UnitId; // Строка типа "amy" или "skeleton_warrior"
        public float3 SpawnPosition;
        public bool IsPlayer;           // Флаг: управлять им как игроком или отдать ИИ
        public int Level;
        public Unity.Collections.FixedString32Bytes Rank;
    }
}


