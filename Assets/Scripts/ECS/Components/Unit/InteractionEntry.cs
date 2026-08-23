using Unity.Entities;

namespace ProjectTowerRpg.ECS.Components
{
    // Буферный элемент для хранения списка активных взаимодействий
    public struct InteractionEntry: IBufferElementData
    {
        public Entity TargetEntity;   // С кем/чем взаимодействуем
        public float MaxDistance;     // Максимальная дистанция
    }
}
