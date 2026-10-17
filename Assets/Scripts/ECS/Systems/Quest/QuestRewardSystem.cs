using Unity.Collections;
using Unity.Entities;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.ECS.Actions;
using ProjectTowerRpg.Core.Quest;
using Debug = UnityEngine.Debug;

namespace ProjectTowerRpg.ECS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(QuestSystem))]
    public partial class QuestRewardSystem : SystemBase
    {
        private EntityCommandBufferSystem _ecbSystem;

        protected override void OnCreate()
        {
            _ecbSystem = World.GetExistingSystemManaged<EndSimulationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate()
        {
            var em = EntityManager;
            var ecb = _ecbSystem.CreateCommandBuffer();

            var claimList = new NativeList<Entity>(Allocator.Temp);

            foreach (var (req, reqEntity) in SystemAPI.Query<RefRO<QuestRewardClaimRequest>>().WithEntityAccess())
                claimList.Add(reqEntity);

            foreach (var reqEntity in claimList)
            {
                var req = em.GetComponentData<QuestRewardClaimRequest>(reqEntity);
                HandleRewardClaim(req.Unit, req.QuestId, req.ChosenRewardIndex, em, ecb);
                ecb.DestroyEntity(reqEntity);
            }

            claimList.Dispose();
            _ecbSystem.AddJobHandleForProducer(Dependency);
        }

        private void HandleRewardClaim(Entity unit, FixedString64Bytes questId, int chosenIndex, EntityManager em, EntityCommandBuffer ecb)
        {
            if (unit == Entity.Null || !em.Exists(unit)) return;
            if (!em.HasBuffer<QuestComponent>(unit)) return;

            var qBuffer = em.GetBuffer<QuestComponent>(unit);
            for (int i = 0; i < qBuffer.Length; i++)
            {
                if (qBuffer[i].QuestId == questId)
                {
                    if (qBuffer[i].State != QuestState.Completed)
                    {
                        Debug.LogWarning($"[QuestRewardSystem] '{questId}' не в Completed (State={qBuffer[i].State}).");
                        return;
                    }

                    if (qBuffer[i].RewardClaimed)
                    {
                        Debug.LogWarning($"[QuestRewardSystem] '{questId}' уже получен.");
                        return;
                    }

                    var q = qBuffer[i];
                    q.State = QuestState.TurnedIn;
                    q.RewardClaimed = true;
                    qBuffer[i] = q;

                    GrantRewards(unit, questId, chosenIndex, em, ecb);

                    Debug.Log($"[QuestRewardSystem] Квест '{questId}' полностью завершён, награды выданы.");
                    return;
                }
            }

            Debug.LogWarning($"[QuestRewardSystem] '{questId}' не найден.");
        }

        private void GrantRewards(Entity unit, FixedString64Bytes questId, int chosenIndex, EntityManager em, EntityCommandBuffer ecb)
        {
            var config = QuestsDatabase.GetQuest(questId.ToString());
            if (config?.rewards == null || config.rewards.Count == 0) return;

            // 1. Гарантированные
            foreach (var r in config.rewards)
            {
                if (!r.is_choice)
                    GrantSingleReward(unit, r, em, ecb);
            }

            // 2. Выбранная choice
            if (chosenIndex >= 0 && chosenIndex < config.rewards.Count)
            {
                var chosen = config.rewards[chosenIndex];
                if (chosen.is_choice)
                    GrantSingleReward(unit, chosen, em, ecb);
            }
        }

        private void GrantSingleReward(Entity unit, QuestReward reward, EntityManager em, EntityCommandBuffer ecb)
        {
            switch (reward.kind)
            {
                case "item":
                {
                    var cmdEntity = ecb.CreateEntity();
                    ecb.AddComponent(cmdEntity, new ActionCommand
                    {
                        Action = ItemActions.Create,
                        TargetEntity = unit,
                        ItemId = reward.id ?? "",
                        Amount = reward.amount
                    });
                    break;
                }

                case "experience":
                    Debug.Log($"[QuestRewardSystem] XP: +{reward.amount} юниту {unit.Index}");
                    break;

                case "currency":
                    Debug.Log($"[QuestRewardSystem] Currency: {reward.id} x{reward.amount} юниту {unit.Index}");
                    break;

                case "ability":
                    Debug.Log($"[QuestRewardSystem] Ability: {reward.id} юниту {unit.Index}");
                    break;

                default:
                    Debug.LogWarning($"[QuestRewardSystem] Неизвестный тип награды: '{reward.kind}'");
                    break;
            }
        }
    }
}
