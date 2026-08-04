using UnityEngine;
using Unity.Mathematics;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using Unity.Entities;
using ProjectTowerRpg.ECS.Systems;
using ProjectTowerRpg.Core.UI.Components;
using ProjectTowerRpg.ECS.Components;
using Unity.Transforms;
using ProjectTowerRpg.Core;

namespace ProjectTowerRpg.Core.UI
{
    public class DragManager : MonoBehaviour
    {
        private static DragManager _instance;
        public static DragManager Instance => _instance;

        private PanelRenderer _panelRenderer;
        private VisualElement _root;
        private IPanel _panel;
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
            _panel = root?.panel;
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
            Debug.Log("[DragManager]: Ghost создан");
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
                var localPos = new Vector2(mousePos.x, Screen.height - mousePos.y);
                var picked = _panel?.Pick(localPos);

                if (picked != null && picked != _root)
                {
                    var slot = picked.GetFirstAncestorOfType<IDragSource>();
                    if (slot != null)
                    {
                        int targetSlot = slot is SlotElement slotElement ? slotElement.SlotIndex : -1;
                        Finish(slot, targetSlot);
                        return;
                    }
                    
                    var provider = picked.GetFirstAncestorOfType<IDataSourceProvider>();
                    if (provider != null)
                    {
                        Finish(provider, -1);
                        return;
                    }
                }
                
                HandleWorldDrop(mousePos);
            }
        }

        private float3 GetDropPosition(float3 playerPosition)
        {
            var camera = Camera.main;
            if (camera == null) return playerPosition + new float3(1.5f, 0, 1.5f);
            
            var forward = camera.transform.forward;
            forward.y = 0;
            forward.Normalize();
            
            // Лёгкий рандом в сторону
            var random = new Unity.Mathematics.Random((uint)UnityEngine.Random.Range(1, 999999));
            float sideAngle = random.NextFloat(-0.3f, 0.3f);
            var direction = math.mul(quaternion.RotateY(sideAngle), forward);
            
            return playerPosition + direction * random.NextFloat(0.27f, 0.72f);
        }

        private void HandleWorldDrop(Vector2 mousePosition)
        {
            if (!PlayerUtils.TryGetPosition(out float3 playerPosition))
            {
                CancelDrag();
                return;
            }

            // Смещение на 2 метра вперёд (по оси Z) и чуть вправо (по X)
            float3 dropPosition = GetDropPosition(playerPosition);

            Entity sourceEntity = GetEntityFromComponent(_activeDrag.Source);

            if (sourceEntity != Entity.Null)
            {
                ClearGhost();

                var actionEntity = _entityManager.CreateEntity();
                _entityManager.AddComponentData(actionEntity, new ActionCommand
                {
                    Type = "item_drop",
                    SourceEntity = sourceEntity,
                    SourceSlot = _activeDrag.SlotIndex,
                    TargetEntity = Entity.Null,
                    TargetSlot = -1,
                    ItemId = _activeDrag.ItemId,
                    Amount = _activeDrag.Amount,
                    Position = dropPosition
                });

                Debug.Log($"[DragManager]: Дроп {_activeDrag.ItemId} рядом с игроком: {dropPosition}");
            }
            else
            {
                Debug.LogWarning("[DragManager] Исходная ECS-сущность не найдена");
                CancelDrag();
                return;
            }

            _activeDrag = null;
        }

        public void StartDrag(DragData data)
        {
            if (_activeDrag != null)
            {
                CancelDrag();
            }

            _activeDrag = data;

            ClearGhost();

            _ghost.style.display = DisplayStyle.Flex;
            if (data.Icon != null)
            {
                _ghost.style.backgroundImage = new StyleBackground(data.Icon);
            }

            _amountLabel.text = data.Amount > 1 ? data.Amount.ToString() : "";
            _amountLabel.style.display = data.Amount > 1 ? DisplayStyle.Flex : DisplayStyle.None;

            Debug.Log($"[DragManager]: Драг начат {data.ItemId} x{data.Amount}");
        }

        public void Finish(object targetComponent, int targetSlot)
        {
            if (_activeDrag == null) return;

            ClearGhost();

            if (targetComponent != null && targetSlot >= 0)
            {
                Entity sourceEntity = GetEntityFromComponent(_activeDrag.Source);
                Entity targetEntity = GetEntityFromComponent(targetComponent);

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

            _activeDrag = null;
        }

        private Entity GetEntityFromComponent(object component)
        {
            if (component == null) return Entity.Null;

            if (component is SlotElement slotElement)
            {
                return slotElement.InventoryEntity;
            }

            if (component is StaticGrid grid)
            {
                return grid.InventoryEntity;
            }

            if (component is IDataSourceProvider provider)
            {
                var entity = EntityRegistry.Get(provider.DataSourceId);
                if (entity != Entity.Null) return entity;
            }

            return Entity.Null;
        }

        public void CancelDrag()
        {
            if (_activeDrag == null) return;

            ClearGhost();
            _activeDrag = null;

            Debug.Log("[DragManager] Драг отменён");
        }

        private void ClearGhost()
        {
            if (_ghost == null) return;

            _ghost.style.display = DisplayStyle.None;
            _ghost.style.backgroundImage = StyleKeyword.Null;
            _amountLabel.text = "";
            _amountLabel.style.display = DisplayStyle.None;
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
