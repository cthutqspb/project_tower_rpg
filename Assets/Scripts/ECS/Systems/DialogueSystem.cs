using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using ProjectTowerRpg.ECS.Actions;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.ECS.Reducers;
using ProjectTowerRpg.Core.Dialogue;
using ProjectTowerRpg.Core.Quest;
using Debug = UnityEngine.Debug;

namespace ProjectTowerRpg.ECS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(ActionDispatcherSystem))]
    public partial class DialogueSystem : SystemBase
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

            // ================================================================
            // 0. АВТО-ЗАКРЫТИЕ: если игрок вышел из радиуса взаимодействия
            // ================================================================
            var autoCloseList = new NativeList<Entity>(Allocator.Temp);

            foreach (var (stateRO, playerEntity) in SystemAPI.Query<RefRO<DialogueStateComponent>>().WithEntityAccess())
            {
                if (!stateRO.ValueRO.IsActive) continue;

                bool stillInRange = false;
                if (SystemAPI.HasBuffer<InteractionEntry>(playerEntity))
                {
                    var entries = SystemAPI.GetBuffer<InteractionEntry>(playerEntity);
                    for (int i = 0; i < entries.Length; i++)
                    {
                        if (entries[i].TargetEntity == stateRO.ValueRO.Speaker)
                        {
                            stillInRange = true;
                            break;
                        }
                    }
                }

                if (!stillInRange)
                    autoCloseList.Add(playerEntity);
            }

            foreach (var playerEntity in autoCloseList)
            {
                if (em.HasComponent<DialogueStateComponent>(playerEntity))
                {
                    var state = em.GetComponentData<DialogueStateComponent>(playerEntity);
                    state.IsActive = false;
                    em.SetComponentData(playerEntity, state);
                }

                if (em.HasBuffer<DialogueOptionElement>(playerEntity))
                    em.GetBuffer<DialogueOptionElement>(playerEntity).Clear();
            }

            autoCloseList.Dispose();

            // ================================================================
            // 1. СОБИРАЕМ ЗАПРОСЫ (только чтение, без структурных изменений)
            // ================================================================
            var startRequests = new NativeList<Entity>(Allocator.Temp);
            var chooseRequests = new NativeList<Entity>(Allocator.Temp);

            foreach (var (req, reqEntity) in SystemAPI.Query<RefRO<StartDialogueRequest>>().WithEntityAccess())
                startRequests.Add(reqEntity);

            foreach (var (req, reqEntity) in SystemAPI.Query<RefRO<DialogueChooseOptionRequest>>().WithEntityAccess())
                chooseRequests.Add(reqEntity);

            // ================================================================
            // 2. ОБРАБАТЫВАЕМ ВНЕ ИТЕРАЦИИ
            // ================================================================
            foreach (var reqEntity in startRequests)
            {
                var req = em.GetComponentData<StartDialogueRequest>(reqEntity);
                HandleStartDialogue(req.Player, req.Npc, em);
                ecb.DestroyEntity(reqEntity);
            }

            foreach (var reqEntity in chooseRequests)
            {
                var req = em.GetComponentData<DialogueChooseOptionRequest>(reqEntity);
                HandleChooseOption(req.Player, req.OptionIndex, em, ecb);
                ecb.DestroyEntity(reqEntity);
            }

            startRequests.Dispose();
            chooseRequests.Dispose();
            _ecbSystem.AddJobHandleForProducer(Dependency);
        }

        // ================================================================
        // СТАРТ ДИАЛОГА
        // ================================================================
        private void HandleStartDialogue(Entity player, Entity npc, EntityManager em)
        {
            if (player == Entity.Null || !em.Exists(player)) return;
            if (npc == Entity.Null || !em.Exists(npc)) return;
            if (!em.HasComponent<UnitComponent>(npc)) return;

            // Ключ диалога = NameKey спикера
            var unitComp = em.GetComponentData<UnitComponent>(npc);
            string dialogueKey = unitComp.NameKey.ToString();

            var graph = DialoguesDatabase.GetDialogue(dialogueKey);
            if (graph == null)
            {
                Debug.LogWarning($"[DialogueSystem] Нет графа диалога для '{dialogueKey}'");
                return;
            }

            // Ищем стартовую ноду
            string startNode = ResolveStartNode(graph, player, em);
            if (string.IsNullOrEmpty(startNode))
            {
                Debug.LogWarning($"[DialogueSystem] Не удалось найти стартовую ноду для '{dialogueKey}'");
                return;
            }

            // Текст стартовой ноды
            string startTextKey = "";
            if (graph.nodes.TryGetValue(startNode, out var startNodeData))
                startTextKey = startNodeData.text_key ?? "";

            // Состояние диалога
            var state = new DialogueStateComponent
            {
                Speaker = npc,
                CurrentNode = startNode,
                EntryNode = startNode,
                SpeakerNameKey = dialogueKey,
                TextKey = startTextKey,
                IsActive = true
            };

            if (em.HasComponent<DialogueStateComponent>(player))
                em.SetComponentData(player, state);
            else
                em.AddComponentData(player, state);

            // Буфер опций
            RefreshOptionsBuffer(player, state, graph, em);
        }

        // ================================================================
        // ВЫБОР ОПЦИИ
        // ================================================================
        private void HandleChooseOption(
            Entity player,
            int optionIndex,
            EntityManager em,
            EntityCommandBuffer ecb
        )
        {
            if (player == Entity.Null || !em.Exists(player)) return;
            if (!em.HasComponent<DialogueStateComponent>(player)) return;

            var state = em.GetComponentData<DialogueStateComponent>(player);
            if (!state.IsActive) return;

            // Граф по спикеру
            var speakerUnit = em.GetComponentData<UnitComponent>(state.Speaker);
            FixedString64Bytes dialogueKeyFS = speakerUnit.NameKey;
            string dialogueKey = dialogueKeyFS.ToString();   // для DialoguesDatabase
            var graph = DialoguesDatabase.GetDialogue(dialogueKey);
            if (graph == null) return;

            // Текущая нода
            if (!graph.nodes.TryGetValue(state.CurrentNode.ToString(), out var currentNode))
            {
                CloseDialogue(player, em);
                return;
            }
            if (currentNode.type != "dialogue" || currentNode.options == null) return;
            if (optionIndex < 0 || optionIndex >= currentNode.options.Count) return;

            var chosenOption = currentNode.options[optionIndex];
            RecordChoice(player, dialogueKey, state.CurrentNode, optionIndex, em);

            // Экшены
            if (chosenOption.actions != null)
            {
                foreach (var act in chosenOption.actions)
                {
                    ExecuteAction(act, player, em, ecb);
                }
            }

            // Переход
            string nextNode = chosenOption.next_node;

            if (string.IsNullOrEmpty(nextNode) || nextNode == "end" || nextNode == "none")
            {
                CloseDialogue(player, em);
                return;
            }

            nextNode = ResolveNodeChain(nextNode, graph, player, em);
            if (string.IsNullOrEmpty(nextNode))
            {
                CloseDialogue(player, em);
                return;
            }

            // Обновляем стейт (нода + текст)
            state.CurrentNode = nextNode;
            if (graph.nodes.TryGetValue(nextNode, out var nextNodeData))
                state.TextKey = nextNodeData.text_key ?? "";

            em.SetComponentData(player, state);

            RefreshOptionsBuffer(player, state, graph, em);
        }

        private void RecordChoice(Entity player, FixedString64Bytes speakerKey, FixedString64Bytes nodeKey, int optionIndex, EntityManager em)
        {
            if (!em.HasBuffer<DialogueHistoryElement>(player))
                em.AddBuffer<DialogueHistoryElement>(player);

            var history = em.GetBuffer<DialogueHistoryElement>(player);

            for (int i = 0; i < history.Length; i++)
            {
                if (history[i].SpeakerKey == speakerKey
                    && history[i].NodeKey == nodeKey
                    && history[i].OptionIndex == optionIndex)
                {
                    return;
                }
            }

            history.Add(new DialogueHistoryElement
            {
                SpeakerKey = speakerKey,
                NodeKey = nodeKey,
                OptionIndex = optionIndex
            });
        }

        // ================================================================
        // РАЗРЕШЕНИЕ ЦЕПОЧКИ НОД (роутеры → dialogue)
        // ================================================================
        private string ResolveNodeChain(string nodeKey, DialogueGraph graph, Entity player, EntityManager em)
        {
            int safety = 32;
            while (safety-- > 0)
            {
                if (string.IsNullOrEmpty(nodeKey) || nodeKey == "end" || nodeKey == "none")
                    return null;

                if (!graph.nodes.TryGetValue(nodeKey, out var node))
                    return null;

                if (node.type == "dialogue")
                    return nodeKey;

                // type == "condition"
                string next = ResolveConditionNode(node, player, em);
                if (string.IsNullOrEmpty(next))
                    return null;

                nodeKey = next;
            }
            Debug.LogError($"[DialogueSystem] Цикл в нодах-роутерах возле '{nodeKey}'");
            return null;
        }

        private string ResolveConditionNode(DialogueNode node, Entity player, EntityManager em)
        {
            if (node.branches != null)
            {
                foreach (var branch in node.branches)
                {
                    if (EvaluateBranch(branch, player, em))
                        return branch.next_node;
                }
            }
            return node.default_next;
        }

        private string ResolveStartNode(DialogueGraph graph, Entity player, EntityManager em)
        {
            return ResolveNodeChain(graph.start_node, graph, player, em);
        }

        // ================================================================
        // ОЦЕНКА УСЛОВИЙ
        // ================================================================
        private bool EvaluateBranch(DialogueConditionBranch branch, Entity player, EntityManager em)
        {
            switch (branch.type)
            {
                case "quest":
                    return EvaluateQuestCondition(branch.quest_id, branch.required_state, player, em);

                case "flag":
                    return false;

                case "skill":
                    return false;

                default:
                    return false;
            }
        }

        private bool EvaluateCondition(DialogueCondition cond, Entity player, EntityManager em)
        {
            switch (cond.type)
            {
                case "quest":
                    return EvaluateQuestCondition(cond.quest_id, cond.required_state, player, em);

                case "flag":
                    return false;

                case "skill":
                    return false;

                default:
                    return true;
            }
        }

        private bool EvaluateQuestCondition(string questId, string requiredState, Entity player, EntityManager em)
        {
            if (player == Entity.Null || !em.HasBuffer<QuestComponent>(player)) return false;

            var buffer = em.GetBuffer<QuestComponent>(player);
            QuestState current = QuestState.Locked;

            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].QuestId.ToString() == questId)
                {
                    current = buffer[i].State;
                    break;
                }
            }

            return current.ToString().ToLower().Trim() == requiredState.ToLower().Trim();
        }

        // ================================================================
        // ЗАПОЛНЕНИЕ БУФЕРА ВИДИМЫХ ОПЦИЙ
        // ================================================================
        private void RefreshOptionsBuffer(Entity player, DialogueStateComponent state, DialogueGraph graph, EntityManager em)
        {
            if (!graph.nodes.TryGetValue(state.CurrentNode.ToString(), out var node))
                return;

            if (node.type != "dialogue" || node.options == null)
                return;

            if (!em.HasBuffer<DialogueOptionElement>(player))
                em.AddBuffer<DialogueOptionElement>(player);

            var buffer = em.GetBuffer<DialogueOptionElement>(player);
            buffer.Clear();

            for (int i = 0; i < node.options.Count; i++)
            {
                var opt = node.options[i];
                if (!IsOptionVisible(opt, player, em)) continue;

                FixedString32Bytes suffix = "";

                if (opt.actions != null)
                {
                    for (int a = 0; a < opt.actions.Count; a++)
                    {
                        switch (opt.actions[a].type)
                        {
                            case "accept_quest":
                                suffix = "ui_dialogue_action_quest";
                                break;
                            case "complete_quest":
                                suffix = "ui_dialogue_action_quest";
                                break;
                            case "close_dialogue":
                                suffix = "ui_dialogue_action_close";
                                break;
                            case "back":
                                suffix = "ui_dialogue_action_back";
                                break;
                            case "open_vendor":
                                suffix = "ui_dialogue_action_vendor";
                                break;
                        }

                        if (!suffix.IsEmpty) break;
                    }
                }

                bool isVisited = IsOptionVisited(player, state.SpeakerNameKey, state.CurrentNode, i, em);
                buffer.Add(new DialogueOptionElement
                {
                    TextKey = opt.text_key,
                    SuffixKey = suffix,
                    OptionIndex = i,
                    NextNode = opt.next_node ?? "",
                    IsVisited = isVisited
                });
            }
        }

        private bool IsOptionVisible(DialogueOption opt, Entity player, EntityManager em)
        {
            if (opt.conditions == null || opt.conditions.Count == 0) return true;

            foreach (var cond in opt.conditions)
            {
                if (!EvaluateCondition(cond, player, em)) return false;
            }
            return true;
        }

        private bool IsOptionVisited(Entity player, FixedString64Bytes speakerKey, FixedString64Bytes nodeKey, int optionIndex, EntityManager em)
        {
            if (!em.HasBuffer<DialogueHistoryElement>(player)) return false;

            var history = em.GetBuffer<DialogueHistoryElement>(player);
            for (int i = 0; i < history.Length; i++)
            {
                if (history[i].SpeakerKey == speakerKey
                    && history[i].NodeKey == nodeKey
                    && history[i].OptionIndex == optionIndex)
                {
                    return true;
                }
            }
            return false;
        }

        // ================================================================
        // ЗАКРЫТИЕ ДИАЛОГА
        // ================================================================
        private void CloseDialogue(Entity player, EntityManager em)
        {
            if (em.HasComponent<DialogueStateComponent>(player))
            {
                var state = em.GetComponentData<DialogueStateComponent>(player);
                state.IsActive = false;
                em.SetComponentData(player, state);
            }

            if (em.HasBuffer<DialogueOptionElement>(player))
                em.GetBuffer<DialogueOptionElement>(player).Clear();
        }

        // ================================================================
        // ЭКШЕНЫ
        // ================================================================
        private void ExecuteAction(DialogueAction act, Entity player, EntityManager em, EntityCommandBuffer ecb)
        {
            switch (act.type)
            {
                case "accept_quest":
                    PlayerReducer.QuestAccept(em, ecb, player, act.quest_id);
                    break;

                case "complete_quest":
                    Debug.Log($"[DialogueSystem] complete_quest '{act.quest_id}' (не реализовано)");
                    break;

                case "close_dialogue":
                    break;

                case "set_flag":
                case "clear_flag":
                    break;
            }
        }
    }
}
