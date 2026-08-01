using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using Unity.Entities;
using ProjectTowerRpg.ECS.Systems;

namespace ProjectTowerRpg.Core.UI
{
    public class DragData
    {
        public object Source;
        public int SlotIndex;
        public string ItemId;
        public int Amount;
        public Sprite Icon;
        public string SourceId;
        public string GridType;
    }

    public class DragManager : MonoBehaviour
    {
        private static DragManager _instance;
        public static DragManager Instance => _instance;

        private PanelRenderer _panelRenderer;
        private VisualElement _root;
        private IPanel _panel;  // ← ДОБАВИТЬ
        private VisualElement _ghost;
        private Label _amountLabel;
        private DragData _activeDrag;
        private EntityManager _entityManager;

        private void Awake() => _instance = this;

        private void Start()
        {
            _panelRenderer = GetComponent<PanelRenderer>();
            if (_panelRenderer == null)
            {
                Debug.LogError("[DragManager]: PanelRenderer не найден!");
                return;
            }

            _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            _panelRenderer.RegisterUIReloadCallback(OnUIReloaded);
        }

        private void OnUIReloaded(PanelRenderer renderer, VisualElement root, int version)
        {
            _root = root;
            _panel = root?.panel;  // ← ДОБАВИТЬ
            if (_root == null) return;

            _ghost = new VisualElement();
            _ghost.name = "drag-ghost";
            _ghost.style.position = Position.Absolute;
            _ghost.style.width = 48;
            _ghost.style.height = 48;
            _ghost.style.backgroundColor = new Color(1, 1, 1, 0.9f);
            _ghost.style.borderTopWidth = 2;
            _ghost.style.borderBottomWidth = 2;
            _ghost.style.borderLeftWidth = 2;
            _ghost.style.borderRightWidth = 2;
            _ghost.style.borderTopColor = Color.white;
            _ghost.style.borderBottomColor = Color.white;
            _ghost.style.borderLeftColor = Color.white;
            _ghost.style.borderRightColor = Color.white;
            _ghost.style.display = DisplayStyle.None;
            _ghost.pickingMode = PickingMode.Ignore;

            _amountLabel = new Label();
            _amountLabel.style.position = Position.Absolute;
            _amountLabel.style.bottom = 2;
            _amountLabel.style.right = 4;
            _amountLabel.style.fontSize = 14;
            _amountLabel.style.color = Color.white;
            _amountLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            _ghost.Add(_amountLabel);

            _root.Add(_ghost);
        }

        private void Update()
        {
            if (_activeDrag == null || _ghost == null) return;

            var mouse = Mouse.current;
            if (mouse == null) return;

            Vector2 mousePos = mouse.position.ReadValue();
            _ghost.style.left = mousePos.x - 24;
            _ghost.style.top = Screen.height - mousePos.y - 24;

            if (!mouse.leftButton.isPressed)
            {
                // Проверяем, над чем отпустили
                var localPos = new Vector2(mousePos.x, Screen.height - mousePos.y);
                var picked = _panel?.Pick(localPos);
                
                if (picked == null || picked == _root)
                {
                    // Отпустили мимо UI → отмена
                    CancelDrag();
                }
                // Если над UI — Finish вызовется из OnSlotPointerUp
            }
        }

        public void StartDrag(object source, int slotIndex, string itemId, int amount, Sprite icon,
                              string sourceId, string gridType)
        {
            if (_activeDrag != null) return;

            _activeDrag = new DragData
            {
                Source = source,
                SlotIndex = slotIndex,
                ItemId = itemId,
                Amount = amount,
                Icon = icon,
                SourceId = sourceId,
                GridType = gridType
            };

            _ghost.style.display = DisplayStyle.Flex;
            if (icon != null)
            {
                _ghost.style.backgroundImage = new StyleBackground(icon);
            }

            _amountLabel.text = amount > 1 ? amount.ToString() : "";
            _amountLabel.style.display = amount > 1 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void Finish(object targetComponent, int targetSlot)
        {
            if (_activeDrag == null) return;

            _ghost.style.display = DisplayStyle.None;
            _ghost.style.backgroundImage = StyleKeyword.Null;

            if (targetComponent != null && targetSlot >= 0)
            {
                string targetId = GetDataSourceId(targetComponent);
                if (!string.IsNullOrEmpty(targetId))
                {
                    Entity sourceEntity = EntityRegistry.Get(_activeDrag.SourceId);
                    Entity targetEntity = EntityRegistry.Get(targetId);

                    if (sourceEntity != Entity.Null && targetEntity != Entity.Null)
                    {
                        var actionEntity = _entityManager.CreateEntity();
                        _entityManager.AddComponentData(actionEntity, new ActionCommand
                        {
                            Type = "item_transfer",
                            SourceEntity = sourceEntity,
                            SourceSlot = _activeDrag.SlotIndex,
                            TargetEntity = targetEntity,
                            TargetSlot = targetSlot,
                            ItemId = _activeDrag.ItemId,
                            Amount = _activeDrag.Amount
                        });

                        Debug.Log($"[DragManager]: Команда создана {_activeDrag.ItemId} -> {targetSlot}");
                    }
                }
            }

            _activeDrag = null;
        }

        private void CancelDrag()
        {
            if (_activeDrag == null) return;
            
            _ghost.style.display = DisplayStyle.None;
            _ghost.style.backgroundImage = StyleKeyword.Null;
            _activeDrag = null;
            
            Debug.Log("[DragManager] Драг отменён (отпущен мимо UI)");
        }

        private string GetDataSourceId(object component)
        {
            if (component == null) return null;

            if (component is IDataSourceProvider provider)
            {
                return provider.DataSourceId;
            }

            return null;
        }

        public bool IsDragging => _activeDrag != null;
        public DragData GetActiveDrag() => _activeDrag;

        private void OnDestroy()
        {
            if (_panelRenderer != null)
            {
                _panelRenderer.UnregisterUIReloadCallback(OnUIReloaded);
            }
            _instance = null;
        }
    }
}
