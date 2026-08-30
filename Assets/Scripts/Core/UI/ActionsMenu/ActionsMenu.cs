using UnityEngine;
using UnityEngine.UIElements;
using Unity.Entities;
using ProjectTowerRpg.ECS.Actions;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.Items;

namespace ProjectTowerRpg.Core.UI
{
    public static class ActionsMenu
    {
        private static ActionsMenuVisual _visual;
        private static EntityManager _entityManager;
        private static PanelRenderer _panelRenderer;
        private static bool _isInitialized = false;
        private static bool _subscribed = false;

        // ================================================================
        // ЛЕНИВАЯ ИНИЦИАЛИЗАЦИЯ (ВЫЗЫВАЕТСЯ ПРИ ПЕРВОМ SHOW)
        // ================================================================
        private static void EnsureInitialized()
        {
            if (_isInitialized) return;
            
            _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            if (_entityManager == null)
            {
                Debug.LogWarning("[ActionsMenu] EntityManager == null");
                return;
            }

            _panelRenderer = Object.FindAnyObjectByType<PanelRenderer>();
            if (_panelRenderer == null)
            {
                Debug.LogWarning("[ActionsMenu] PanelRenderer не найден!");
                return;
            }

            if (!_subscribed)
            {
                _panelRenderer.RegisterUIReloadCallback(OnUIReloaded);
                _subscribed = true;
            }

            _isInitialized = true;
            Debug.Log("[ActionsMenu] Инициализирован (лениво)");
        }

        private static void OnUIReloaded(PanelRenderer renderer, VisualElement globalUiRoot, int version)
        {
            if (globalUiRoot == null) return;
            
            if (_visual != null && globalUiRoot.Contains(_visual))
            {
                globalUiRoot.Remove(_visual);
            }

            _visual = new ActionsMenuVisual();
            globalUiRoot.Add(_visual);
            Debug.Log("[ActionsMenu] ActionsMenuVisual добавлен в корень UI");
        }

        // ================================================================
        // SHOW ДЛЯ СЛОТА / ФРЕЙМА (УНИВЕРСАЛЬНЫЙ UI КОНТЕКСТ)
        // ================================================================
        public static void Show(UiClickContext context)
        {
            EnsureInitialized();

            if (!_isInitialized)
            {
                Debug.LogWarning("[ActionsMenu] Не инициализирован!");
                return;
            }

            if (_visual == null)
            {
                Debug.LogWarning("[ActionsMenu] _visual == null, ждём OnUIReloaded");
                return;
            }

            if (context.ContextEntity == Entity.Null) return;
            if (_entityManager == null) return;

            var em = _entityManager;

            // Извлекаем ECS-теги прямо из сущности в контексте
            bool isPaperdoll = em.HasComponent<PaperdollTag>(context.ContextEntity);
            bool isInventory = em.HasComponent<InventoryTag>(context.ContextEntity);
            bool isContainer = em.HasComponent<ContainerTag>(context.ContextEntity);

            if (isContainer)
            {
                Debug.Log($"[ActionsMenu] ПКМ по сундуку: осмотр {context.TargetId}");
                return;
            }

            if (!isInventory && !isPaperdoll) return;

            var itemConfig = ItemsDatabase.GetItem(context.TargetId);
            if (itemConfig == null)
            {
                Debug.LogWarning($"[ActionsMenu] Предмет {context.TargetId} не найден!");
                return;
            }

            bool isEquipped = isPaperdoll;
            bool canSplit = context.Amount > 1 && itemConfig.properties.stackable;

            var actions = ActionsMenuDB.GetGuiActions(itemConfig, isEquipped, canSplit, "gui");
            if (actions == null || actions.Count == 0)
            {
                Debug.Log($"[ActionsMenu] Нет действий для {context.TargetId}");
                return;
            }

            // Маппим данные во внутреннюю структуру MenuActionData
            var data = new MenuActionData
            {
                SlotIndex = context.SlotIndex,
                ContainerEntity = context.ContextEntity,
                ItemId = context.TargetId,
                Amount = context.Amount,
                SourceType = isPaperdoll ? "paperdoll" : "inventory",
                TargetEntity = PlayerUtils.GetEntityByTag<PlayerTag>()
            };

            // Позиционируем меню по координатам мыши из контекста
            _visual.Show(context.MousePosition, actions, data);
        }

        // ================================================================
        // SHOW ДЛЯ МИРА
        // ================================================================
        public static void Show(Vector2 screenPosition, Entity targetEntity)
        {
            // TODO: для мира
        }

        public static void Hide()
        {
            _visual?.Hide();
        }

        public static bool IsVisible()
        {
            return _visual?.IsVisible() ?? false;
        }

        // ================================================================
        // ОЧИСТКА
        // ================================================================
        public static void Cleanup()
        {
            if (_panelRenderer != null && _subscribed)
            {
                _panelRenderer.UnregisterUIReloadCallback(OnUIReloaded);
                _subscribed = false;
            }
            
            _visual = null;
            _isInitialized = false;
            Debug.Log("[ActionsMenu] Очищен");
        }

        public static void HandleMenuActionSelected(MenuAction action, MenuActionData data)
        {
            if (_entityManager == null) return;
            var em = _entityManager;

            // 🔥 ОПРЕДЕЛЯЕМ ЦЕЛЕВУЮ СУЩНОСТЬ НА ОСНОВЕ action.Data
            Entity targetEntity = Entity.Null;

            if (action.Data != null && action.Data.TryGetValue("target_type", out var typeObj))
            {
                string targetType = typeObj.ToString();
                
                if (targetType == "paperdoll")
                {
                    // Ищем куклу игрока
                    var playerEntity = PlayerUtils.GetEntityByTag<PlayerTag>();
                    if (playerEntity != Entity.Null)
                    {
                        targetEntity = ContainerHelper.GetContainerForUnit<PaperdollTag>(playerEntity, em);
                    }
                }
                else if (targetType == "inventory")
                {
                    // Ищем инвентарь игрока
                    var playerEntity = PlayerUtils.GetEntityByTag<PlayerTag>();
                    if (playerEntity != Entity.Null)
                    {
                        targetEntity = ContainerHelper.GetContainerForUnit<InventoryTag>(playerEntity, em);
                    }
                }
                else if (targetType == "player_inventory")
                {
                    targetEntity = PlayerUtils.GetEntityByTag<InventoryTag>(em);
                }
            }
            
            // 🔥 Если targetEntity всё ещё Null — используем fallback (data.TargetEntity)
            if (targetEntity == Entity.Null)
            {
                targetEntity = data.TargetEntity;
            }

            // Создаём ActionCommand
            var actionEntity = em.CreateEntity();
            em.AddComponentData(actionEntity, new ActionCommand
            {
                Action = action.Action,
                SourceEntity = data.ContainerEntity,
                SourceSlot = data.SlotIndex,
                TargetEntity = targetEntity,
                TargetSlot = -1,
                ItemId = data.ItemId,
                Amount = data.Amount,
            });

            Debug.Log($"[ActionsMenu] Команда: {action.Action}, Source: {data.ContainerEntity.Index}, Target: {targetEntity.Index}");
        }

        // ================================================================
        // ДАННЫЕ
        // ================================================================
        public class MenuActionData
        {
            public int SlotIndex = -1;
            public Entity ContainerEntity = Entity.Null;
            public string ItemId;
            public int Amount;
            public string SourceType;
            public Entity TargetEntity = Entity.Null;
            public Vector3 WorldPosition;
        }
    }
}
