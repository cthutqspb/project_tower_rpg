using UnityEngine;
using Unity.Mathematics;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using Unity.Entities;
using ProjectTowerRpg.ECS.Actions;      // ← ДОБАВЛЕНО
using ProjectTowerRpg.ECS.Systems;
using ProjectTowerRpg.Core.UI.Components;
using ProjectTowerRpg.ECS.Components;

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
            if (_ghost == null) return;
            if (_activeDrag == null) return;

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
                    IDragSource slot = null;
                    if (picked is IDragSource ds)
                        slot = ds;
                    else
                        slot = picked.GetFirstAncestorOfType<IDragSource>();

                    if (slot != null && slot is SlotElement slotElement)
                    {
                        Finish(slotElement, slotElement.SlotIndex);
                        return;
                    }

                    if (picked is IEntityContainer container)
                    {
                        Finish(container, -1);
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

            float3 dropPosition = GetDropPosition(playerPosition);
            Entity sourceEntity = GetEntityFromComponent(_activeDrag.Source);

            if (sourceEntity != Entity.Null)
            {
                ClearGhost();

                var actionEntity = _entityManager.CreateEntity();
                _entityManager.AddComponentData(actionEntity, new ActionCommand
                {
                    ActionType = "item_drop",
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
            if (_activeDrag != null) CancelDrag();

            _activeDrag = data;
            ClearGhost();

            _ghost.style.display = DisplayStyle.Flex;
            if (data.Icon != null)
                _ghost.style.backgroundImage = new StyleBackground(data.Icon);

            _amountLabel.text = data.Amount > 1 ? data.Amount.ToString() : "";
            _amountLabel.style.display = data.Amount > 1 ? DisplayStyle.Flex : DisplayStyle.None;

            Debug.Log($"[DragManager]: Драг начат {data.ItemId} x{data.Amount}");
        }

        public void Finish(object targetComponent, int targetSlot)
        {
            if (_activeDrag == null) return;

            Debug.Log($"[DragManager] Finish: target={targetComponent?.GetType().Name ?? "null"}, targetSlot={targetSlot}");
            ClearGhost();

            if (targetComponent != null && targetSlot != -1)
            {
                Entity sourceEntity = GetEntityFromComponent(_activeDrag.Source);
                Entity targetEntity = GetEntityFromComponent(targetComponent);

                Debug.Log($"[DragManager] sourceEntity={sourceEntity}, targetEntity={targetEntity}");

                if (sourceEntity != Entity.Null && targetEntity != Entity.Null)
                {
                    var actionEntity = _entityManager.CreateEntity();
                    _entityManager.AddComponentData(actionEntity, new ActionCommand
                    {
                        ActionType = "item_transfer",
                        SourceEntity = sourceEntity,
                        SourceSlot = _activeDrag.SlotIndex,
                        TargetEntity = targetEntity,
                        TargetSlot = targetSlot,
                        ItemId = _activeDrag.ItemId,
                        Amount = _activeDrag.Amount
                    });

                    Debug.Log($"[DragManager]: Команда создана {_activeDrag.ItemId} -> Слот #{targetSlot}");
                }
                else
                {
                    Debug.LogWarning($"[DragManager] sourceEntity или targetEntity == Entity.Null");
                }
            }

            _activeDrag = null;
        }

        private Entity GetEntityFromComponent(object component)
        {
            if (component == null) return Entity.Null;

            if (component is SlotElement slotElement)
                return slotElement.ContainerEntity;

            if (component is StaticGrid grid)
                return grid.BoundEntity;

            if (component is Paperdoll paperdoll)
                return paperdoll.BoundEntity;

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
                _panelRenderer.UnregisterUIReloadCallback(OnUIReloaded);

            _instance = null;
        }
    }
}
