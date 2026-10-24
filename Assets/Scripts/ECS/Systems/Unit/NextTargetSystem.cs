using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.Units;
using Camera = UnityEngine.Camera;
using Debug = UnityEngine.Debug;

namespace ProjectTowerRpg.ECS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class NextTargetSystem : SystemBase
    {
        private const float MAX_DISTANCE = 40f;
        private const float CONE_DOT_THRESHOLD = 0.5f;   // ±60°

        protected override void OnUpdate()
        {
            var em = EntityManager;

            var requests = new NativeList<Entity>(Allocator.Temp);
            foreach (var (req, reqEntity) in SystemAPI.Query<RefRO<NextTargetRequest>>().WithEntityAccess())
                requests.Add(reqEntity);

            foreach (var reqEntity in requests)
            {
                var req = em.GetComponentData<NextTargetRequest>(reqEntity);
                HandleNextTarget(req.Unit, em);
                em.DestroyEntity(reqEntity);
            }

            requests.Dispose();
        }

        private void HandleNextTarget(Entity playerEntity, EntityManager em)
        {
            if (playerEntity == Entity.Null || !em.Exists(playerEntity)) return;
            if (!em.HasComponent<CombatStateComponent>(playerEntity)) return;
            if (!em.HasComponent<UnitComponent>(playerEntity)) return;
            if (!em.HasComponent<LocalTransform>(playerEntity)) return;

            var combat = em.GetComponentData<CombatStateComponent>(playerEntity);
            Entity currentTarget = combat.CurrentTarget;

            var playerUnit = em.GetComponentData<UnitComponent>(playerEntity);
            var playerTransform = em.GetComponentData<LocalTransform>(playerEntity);
            string playerFaction = playerUnit.Faction.ToString();

            float3 playerPos = playerTransform.Position;
            float3 cameraForward = GetCameraForwardXZ();

            // 1. Собираем всех враждебных живых в радиусе и в конусе
            var candidates = new List<(Entity entity, float dist)>();
            
            var allUnits = em.CreateEntityQuery(
                ComponentType.ReadOnly<UnitComponent>(),
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.Exclude<IsDeadTag>()
            ).ToEntityArray(Allocator.Temp);

            for (int i = 0; i < allUnits.Length; i++)
            {
                var entity = allUnits[i];
                if (entity == playerEntity) continue;

                var otherUnit = em.GetComponentData<UnitComponent>(entity);

                if (!FactionsDatabase.IsHostile(playerFaction, otherUnit.Faction.ToString()))
                    continue;

                var otherTransform = em.GetComponentData<LocalTransform>(entity);
                float3 toEnemy = otherTransform.Position - playerPos;
                toEnemy.y = 0f;

                float dist = math.length(toEnemy);
                if (dist > MAX_DISTANCE || dist < 0.01f) continue;

                // 2. Конус: dot между направлением камеры и направлением на врага
                float3 dirToEnemy = toEnemy / dist;
                float dot = math.dot(cameraForward, dirToEnemy);
                if (dot < CONE_DOT_THRESHOLD) continue;

                candidates.Add((entity, dist));
            }

            allUnits.Dispose();

            if (candidates.Count == 0)
            {
                Debug.Log("[NextTargetSystem] Враждебных целей в конусе нет");
                return;
            }

            // 3. Сортируем по дистанции
            candidates.Sort((a, b) => a.dist.CompareTo(b.dist));

            // 4. Находим текущую цель в списке
            int currentIndex = -1;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].entity == currentTarget)
                {
                    currentIndex = i;
                    break;
                }
            }

            // 5. Берём следующего по кругу
            int nextIndex = (currentIndex + 1) % candidates.Count;
            Entity nextTarget = candidates[nextIndex].entity;

            combat.CurrentTarget = nextTarget;
            em.SetComponentData(playerEntity, combat);

            Debug.Log($"[NextTargetSystem] Новая цель: {nextTarget.Index} (дистанция {candidates[nextIndex].dist:F1})");
        }

        private static float3 GetCameraForwardXZ()
        {
            if (Camera.main == null) return new float3(0f, 0f, 1f);

            float3 forward = Camera.main.transform.forward;
            forward.y = 0f;

            float lenSq = math.lengthsq(forward);
            if (lenSq < 0.0001f) return new float3(0f, 0f, 1f);

            return forward / math.sqrt(lenSq);
        }
    }
}
