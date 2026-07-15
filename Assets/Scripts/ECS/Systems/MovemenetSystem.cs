using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using ProjectTowerRpg.ECS.Components;

namespace ProjectTowerRpg.ECS.Systems
{
    // 🚀 СВЕРХЗВУКОВАЯ СИСТЕМА ПЕРЕМЕЩЕНИЯ:
    // Она автоматически параллелится компилятором на все ядра твоего i9!
    public partial struct MovementSystem : ISystem
    {
        // Вызывается движком каждый божий кадр (апдейт)
        public void OnUpdate(ref SystemState state)
        {
            // 🎰 ВЕЛИКИЙ ПОТОК (Cache Locality):
            // SystemAPI.Query на Си-уровне вытаскивает плотные чанки памяти.
            // RefRW — ссылка на чтение/запись (мутируем позицию).
            // RefRO — ссылка только на чтение (смотрим скорость и направление).
            foreach (var (transform, movement) in SystemAPI.Query<RefRW<LocalTransform>, RefRO<MovementComponent>>())
            {
                // Если вектор направления не нулевой — шёлково сдвигаем сущность в 3D!
                if (math.lengthsq(movement.ValueRO.direction) > 0)
                {
                    // Формула веков: Позиция += Направление * Скорость * dt
                    // LocalTransform.Position — это нативный float3 встроенного пакета Transforms!
                    transform.ValueRW.Position += movement.ValueRO.direction * movement.ValueRO.speed * SystemAPI.Time.DeltaTime;
                }
            }
        }
    }
}

