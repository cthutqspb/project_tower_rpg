using Unity.Entities;
using Unity.Mathematics;

namespace ProjectTowerRpg.ECS.Components
{
    /// <summary>
    /// Единый глобальный стейт ховера в ОЗУ.
    /// Полный аналог твоего self.current_hover_id из Defold.
    /// </summary>
    public struct HoverState : IComponentData
    {
        public Entity CurrentEntity; // Сущность под мышью (Entity.Null, если пусто)
        public float3 HitPosition;   // Точка пересечения в 3D мире
        
        public bool HasTarget => CurrentEntity != Entity.Null;
    }
}

