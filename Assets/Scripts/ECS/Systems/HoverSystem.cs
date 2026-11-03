using Unity.Entities;
using Unity.Mathematics; // ← Добавлен для math.float3
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.UI;
using ProjectTowerRpg.Core.Units;
using ProjectTowerRpg.Core.Items;
// 🚀 ХИРУРГИЧЕСКИЙ РАЗВОД ИМПОРТОВ: Заголовки чисты, клинч векторов и физики ликвидирован!
using Camera = UnityEngine.Camera;
using Vector2 = UnityEngine.Vector2;
using Ray = UnityEngine.Ray;
using Physics = UnityEngine.Physics;
using RaycastHit = UnityEngine.RaycastHit;
using Cursor = UnityEngine.Cursor;
using CursorMode = UnityEngine.CursorMode;

namespace ProjectTowerRpg.ECS.Systems
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation)]
    public partial class HoverSystem : SystemBase
    {
        private Camera _mainCamera;
        protected override void OnStartRunning() => _mainCamera = Camera.main;

        protected override void OnCreate()
        {
            EntityManager.CreateSingleton<HoverState>();
        }

        // 🎯 Буфер для SphereCastNonAlloc (создается один раз, не аллоцирует в Update)
        private readonly RaycastHit[] _hitBuffer = new RaycastHit[16];

        // 🎯 Компаратор для сортировки по дистанции (без лямбды, без мусора)
        private class HitDistanceComparer : System.Collections.Generic.IComparer<RaycastHit>
        {
            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
        }
        private static readonly HitDistanceComparer _hitComparer = new HitDistanceComparer();

        protected override void OnUpdate()
        {
            if (SystemAPI.TryGetSingletonRW<HoverState>(out var hoverState) == false) return;

            if (UIManager.IsBlocked)
            {
                ResetHover(ref hoverState.ValueRW);
                return;
            }
            
            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
                if (_mainCamera == null) return;
            }

            var currentMouse = UnityEngine.InputSystem.Mouse.current;
            Vector2 mousePosition = currentMouse != null 
                ? currentMouse.position.ReadValue() 
                : Vector2.zero;

            Ray ray = _mainCamera.ScreenPointToRay(mousePosition);

            // 🎯 SphereCastNonAlloc: пишет в _hitBuffer, не создает мусор
            int hitCount = Physics.SphereCastNonAlloc(ray, 0.3f, _hitBuffer, 100f);

            if (hitCount == 0)
            {
                if (hoverState.ValueRO.HasTarget)
                    ResetHover(ref hoverState.ValueRW);
                return;
            }

            // Сортируем только валидные хиты (0..hitCount) по дистанции
            System.Array.Sort(_hitBuffer, 0, hitCount, _hitComparer);

            Entity foundEntity = Entity.Null;
            bool isItem = false;
            bool isUnit = false;
            string currentUnitId = string.Empty;
            RaycastHit validHit = default;

            for (int i = 0; i < hitCount; i++)
            {
                var hit = _hitBuffer[i];

                // 📦 Предмет
                var itemView = hit.collider.GetComponentInParent<ItemView>();
                if (itemView != null && itemView.entity != Entity.Null)
                {
                    foundEntity = itemView.entity;
                    isItem = true;
                    validHit = hit;
                    break;
                }

                // 👥 Юнит
                var unitView = hit.collider.GetComponentInParent<UnitView>();
                if (unitView != null && unitView.entity != Entity.Null)
                {
                    // 🎯 Пропускаем игрока (и тех, кем управляет игрок, например майнд-контроль)
                    if (EntityManager.HasComponent<PlayerTag>(unitView.entity) || 
                        EntityManager.HasComponent<ControlledByPlayerTag>(unitView.entity))
                        continue;

                    foundEntity = unitView.entity;
                    currentUnitId = unitView.unitId;
                    isUnit = true;
                    validHit = hit;
                    break;
                }
            }

            if (foundEntity == Entity.Null)
            {
                if (hoverState.ValueRO.HasTarget)
                    ResetHover(ref hoverState.ValueRW);
                return;
            }

            if (hoverState.ValueRO.CurrentEntity == foundEntity)
            {
                hoverState.ValueRW.HitPosition = validHit.point;
                return;
            }

            hoverState.ValueRW.CurrentEntity = foundEntity;
            hoverState.ValueRW.HitPosition = validHit.point;

            // Обработка предмета
            if (isItem && EntityManager.HasComponent<ItemComponent>(foundEntity))
            {
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);

                var itemComponent = EntityManager.GetComponentData<ItemComponent>(foundEntity);
                string itemId = itemComponent.ItemId.ToString();

                var config = ItemsDatabase.GetItem(itemId);
                if (config != null)
                {
                    TooltipManager.Show(TooltipDomain.WORLD, TooltipKind.ITEM, config);
                }
            }
            // Обработка юнита
            else if (isUnit && EntityManager.HasComponent<UnitComponent>(foundEntity))
            {
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);

                var unitConfig = UnitsDatabase.GetUnit(currentUnitId);
                if (unitConfig != null)
                {
                    var unitComponent = EntityManager.GetComponentData<UnitComponent>(foundEntity);
                    string nameKey = unitComponent.NameKey.ToString();

                    TooltipManager.Show(TooltipDomain.WORLD, TooltipKind.UNIT, unitConfig, context: nameKey);
                }
            }
        }

        private void ResetHover(ref HoverState hover)
        {
            hover.CurrentEntity = Entity.Null;
            hover.HitPosition = math.float3(0, 0, 0); // Твой чистый Си-сброс вектора
            
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            
            // 🌟 СБРОС ТУЛТИПА: Мышь ушла с объекта — убираем плашку с экрана
            TooltipManager.HideWorldTooltips();
        }
    }
}

