using Unity.Collections;
using Unity.Entities;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.Dialogue;
using ProjectTowerRpg.Core.Quest;
using System;
using Debug = UnityEngine.Debug;

namespace ProjectTowerRpg.ECS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(ActionDispatcherSystem))]
    public partial class DialogueSystem : SystemBase
    {
        private const int MAX_HISTORY = 512;

        private EntityCommandBufferSystem _ecbSystem;

        protected override void OnCreate()
        {
            _ecbSystem = World.GetExistingSystemManaged<EndSimulationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate()
        {
            var stateQuery = SystemAPI.QueryBuilder().WithAll<DialogueStateComponent>().Build();
            var startQuery = SystemAPI.QueryBuilder().WithAll<DialogueStartRequest>().Build();
            var chooseQuery = SystemAPI.QueryBuilder().WithAll<DialogueChooseOptionRequest>().Build();

            if (stateQuery.IsEmpty && startQuery.IsEmpty && chooseQuery.IsEmpty)
                return;

            var em = EntityManager;
            var ecb = _ecbSystem.CreateCommandBuffer();

            // 0. АВТО-ЗАКРЫТИЕ
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
                if (!stillInRange) autoCloseList.Add(playerEntity);
            }

            foreach (var playerEntity in autoCloseList)
                DialogueClose(playerEntity, em);
            
            autoCloseList.Dispose();

            // 1. СОБИРАЕМ ЗАПРОСЫ
            var startRequests = new NativeList<Entity>(Allocator.Temp);
            var chooseRequests = new NativeList<Entity>(Allocator.Temp);

            foreach (var (req, reqEntity) in SystemAPI.Query<RefRO<DialogueStartRequest>>().WithEntityAccess())
                startRequests.Add(reqEntity);

            foreach (var (req, reqEntity) in SystemAPI.Query<RefRO<DialogueChooseOptionRequest>>().WithEntityAccess())
                chooseRequests.Add(reqEntity);

            // 2. ОБРАБАТЫВАЕМ
            foreach (var reqEntity in startRequests)
            {
                var req = em.GetComponentData<DialogueStartRequest>(reqEntity);
                HandleDialogueStart(req.Unit, req.Npc, em);
                ecb.DestroyEntity(reqEntity);
            }

            foreach (var reqEntity in chooseRequests)
            {
                var req = em.GetComponentData<DialogueChooseOptionRequest>(reqEntity);
                HandleChooseOption(req.Unit, req.OptionIndex, em, ecb);
                ecb.DestroyEntity(reqEntity);
            }

            startRequests.Dispose();
            chooseRequests.Dispose();
        }

        private void HandleDialogueStart(Entity player, Entity npc, EntityManager em)
        {
            if (player == Entity.Null || !em.Exists(player)) return;
            if (npc == Entity.Null || !em.Exists(npc)) return;
            if (!em.HasComponent<UnitComponent>(npc)) return;

            var unitComp = em.GetComponentData<UnitComponent>(npc);
            string dialogueKey = unitComp.NameKey.ToString();

            var graph = DialoguesDatabase.GetDialogue(dialogueKey);
            if (graph == null)
            {
                Debug.LogWarning($"[DialogueSystem] Нет графа диалога для '{dialogueKey}'");
                return;
            }

            string startNode = ResolveStartNode(graph, player, em);
            if (string.IsNullOrEmpty(startNode))
            {
                Debug.LogWarning($"[DialogueSystem] Не удалось найти стартовую ноду для '{dialogueKey}'");
                return;
            }

            string startTextKey = "";
            if (graph.nodes.TryGetValue(startNode, out var startNodeData))
                startTextKey = startNodeData.text_key ?? "";

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

            RefreshOptionsBuffer(player, state, graph, em);
        }

        private void HandleChooseOption(Entity player, int optionIndex, EntityManager em, EntityCommandBuffer ecb)
        {
            if (player == Entity.Null || !em.Exists(player)) return;
            if (!em.HasComponent<DialogueStateComponent>(player)) return;

            var state = em.GetComponentData<DialogueStateComponent>(player);
            if (!state.IsActive) return;

            if (state.Speaker == Entity.Null || !em.Exists(state.Speaker)
                || !em.HasComponent<UnitComponent>(state.Speaker))
            {
                DialogueClose(player, em);
                return;
            }

            var speakerUnit = em.GetComponentData<UnitComponent>(state.Speaker);
            string dialogueKey = speakerUnit.NameKey.ToString();
            var graph = DialoguesDatabase.GetDialogue(dialogueKey);
            if (graph == null) return;

            if (!graph.nodes.TryGetValue(state.CurrentNode.ToString(), out var currentNode))
            {
                DialogueClose(player, em);
                return;
            }
            if (currentNode.type != "dialogue" || currentNode.options == null) return;
            if (optionIndex < 0 || optionIndex >= currentNode.options.Count) return;

            var chosenOption = currentNode.options[optionIndex];
            RecordChoice(player, speakerUnit.NameKey, state.CurrentNode, optionIndex, em);

            bool closeRequested = false;
            bool backRequested = false;

            if (chosenOption.actions != null)
            {
                foreach (var act in chosenOption.actions)
                {
                    if (act.type == "dialogue_close") { closeRequested = true; continue; }
                    if (act.type == "back") { backRequested = true; continue; }
                    ExecuteAction(act, player, em, ecb);
                }
            }

            if (closeRequested)
            {
                DialogueClose(player, em);
                return;
            }

            // 🎯 ВОЗВРАТ К СТАРТОВОЙ НОДЕ (если есть экшен back)
            if (backRequested)
            {
                state.CurrentNode = state.EntryNode;
                if (graph.nodes.TryGetValue(state.EntryNode.ToString(), out var entryNodeData))
                    state.TextKey = entryNodeData.text_key ?? "";
                em.SetComponentData(player, state);
                RefreshOptionsBuffer(player, state, graph, em);
                return;
            }

            // Переход к следующей ноде
            string nextNode = chosenOption.next_node;

            if (string.IsNullOrEmpty(nextNode) || nextNode == "end" || nextNode == "none")
            {
                DialogueClose(player, em);
                return;
            }

            nextNode = ResolveNodeChain(nextNode, graph, player, em);
            if (string.IsNullOrEmpty(nextNode))
            {
                DialogueClose(player, em);
                return;
            }

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

            while (history.Length >= MAX_HISTORY)
                history.RemoveAt(0);

            history.Add(new DialogueHistoryElement
            {
                SpeakerKey = speakerKey,
                NodeKey = nodeKey,
                OptionIndex = optionIndex
            });
        }

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
        // ОЦЕНКА УСЛОВИЙ (теперь с поддержкой invert)
        // ================================================================
        private bool EvaluateBranch(DialogueConditionBranch branch, Entity player, EntityManager em)
        {
            bool result = false;
            switch (branch.type)
            {
                case "quest":
                    result = EvaluateQuestCondition(branch.quest_id, branch.required_state, player, em);
                    break;
                case "flag":
                case "skill":
                default:
                    result = false;
                    break;
            }
            // 🎯 Инверсия для ветвления
            return branch.invert ? !result : result;
        }

        private bool EvaluateCondition(DialogueCondition cond, Entity player, EntityManager em)
        {
            bool result = true;
            switch (cond.type)
            {
                case "quest":
                    result = EvaluateQuestCondition(cond.quest_id, cond.required_state, player, em);
                    break;
                case "flag":
                case "skill":
                    result = false;
                    break;
                default:
                    result = true;
                    break;
            }
            // 🎯 Инверсия для опций
            return cond.invert ? !result : result;
        }

        private bool EvaluateQuestCondition(string questId, string requiredState, Entity player, EntityManager em)
        {
            if (player == Entity.Null || string.IsNullOrEmpty(questId) || string.IsNullOrEmpty(requiredState))
                return false;

            if (!em.HasBuffer<QuestComponent>(player)) return false;
            if (!Enum.TryParse<QuestState>(requiredState, true, out var required)) return false;

            var buffer = em.GetBuffer<QuestComponent>(player);
            QuestState current = QuestState.Locked; // Если квеста нет, то он Locked

            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].QuestId.ToString() == questId)
                {
                    current = buffer[i].State;
                    break;
                }
            }

            return current == required;
        }

        private void RefreshOptionsBuffer(Entity player, DialogueStateComponent state, DialogueGraph graph, EntityManager em)
        {
            if (!em.HasBuffer<DialogueOptionElement>(player))
                em.AddBuffer<DialogueOptionElement>(player);

            var buffer = em.GetBuffer<DialogueOptionElement>(player);
            buffer.Clear();

            if (!graph.nodes.TryGetValue(state.CurrentNode.ToString(), out var node))
                return;

            if (node.type != "dialogue" || node.options == null)
                return;

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
                            case "quest_accept":
                            case "quest_complete":
                                suffix = "ui_dialogue_action_quest";
                                break;
                            case "dialogue_close":
                                suffix = "ui_dialogue_action_close";
                                break;
                            case "back":
                                suffix = "ui_dialogue_action_back";
                                break;
                            case "vendor_open":
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

        private void DialogueClose(Entity player, EntityManager em)
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

        private void ExecuteAction(DialogueAction act, Entity player, EntityManager em, EntityCommandBuffer ecb)
        {
            switch (act.type)
            {
                case "quest_accept":
                {
                    var reqEntity = ecb.CreateEntity();
                    ecb.AddComponent(reqEntity, new QuestAcceptRequest { Unit = player, QuestId = act.quest_id });
                    break;
                }
                case "quest_complete":
                {
                    var reqEntity = ecb.CreateEntity();
                    ecb.AddComponent(reqEntity, new QuestCompleteRequest { Unit = player, QuestId = act.quest_id });
                    break;
                }
                case "dialogue_close":
                case "back":
                    break; // Обрабатываются снаружи
                case "set_flag":
                case "clear_flag":
                    break;
                case "vendor_open":
                    break; // TODO
            }
        }
    }
}
