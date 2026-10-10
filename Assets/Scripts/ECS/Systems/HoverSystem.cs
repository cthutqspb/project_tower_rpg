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
        protected override void OnCreate()
        {
            EntityManager.CreateSingleton<HoverState>();
        }

        protected override void OnUpdate()
        {
            if (SystemAPI.TryGetSingletonRW<HoverState>(out var hoverState) == false) return;

            if (UIManager.IsBlocked)
            {
                ResetHover(ref hoverState.ValueRW);
                return;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera == null) return;

            // Вычистили уродливый инлайн-префикс UnityEngine.InputSystem.Mouse
            var currentMouse = UnityEngine.InputSystem.Mouse.current;
            Vector2 mousePosition = currentMouse != null 
                ? currentMouse.position.ReadValue() 
                : Vector2.zero;

            Ray ray = mainCamera.ScreenPointToRay(mousePosition);
            
            if (Physics.SphereCast(ray, 0.3f, out RaycastHit hit, 100f))
            {
                Entity foundEntity = Entity.Null;
                bool isItem = false;
                bool isUnit = false;
                string currentUnitId = string.Empty;

                // 📦 1. ПРОВЕРЯЕМ ПРЕДМЕТ: Ищем паспорт в родителях на случай сложных коллайдеров
                var itemView = hit.collider.GetComponentInParent<ItemView>();
                if (itemView != null)
                {
                    if (itemView.entity != Entity.Null)
                    {
                        foundEntity = itemView.entity;
                        isItem = true;
                    }
                }
                // 👥 2. ПРОВЕРЯЕМ ЮНИТА: Бронебойно ищем UnitView, если луч врезался в кость скелета
                else
                {
                    var unitView = hit.collider.GetComponentInParent<UnitView>();
                    if (unitView != null)
                    {
                        if (unitView.entity != Entity.Null)
                        {
                            foundEntity = unitView.entity;
                            currentUnitId = unitView.unitId;
                            isUnit = true;
                        }
                    }
                }

                // ЕСЛИ КОЛЛАЙДЕР ВЕРНУЛ ВАЛИДНУЮ ECS-СУЩНОСТЬ
                if (foundEntity != Entity.Null)
                {
                    if (hoverState.ValueRO.CurrentEntity == foundEntity)
                    {
                        // Старая физика PhysX нагло и шёлково скармливает Vector3 (hit.point)
                        // в твой unmanaged float3 (HitPosition) без лишней лапши конвертаций!
                        hoverState.ValueRW.HitPosition = hit.point;
                        return;
                    }

                    hoverState.ValueRW.CurrentEntity = foundEntity;
                    hoverState.ValueRW.HitPosition = hit.point;

                    // ОБРАБОТКА КОНТЕНТА ПРЕДМЕТА
                    if (isItem && EntityManager.HasComponent<ItemComponent>(foundEntity))
                    {
                        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto); 
                        
                        var itemComponent = EntityManager.GetComponentData<ItemComponent>(foundEntity);
                        string currentItemIdStr = itemComponent.ItemId.ToString();

                        var dbItemCfg = ItemsDatabase.GetItem(currentItemIdStr);
                        if (dbItemCfg != null)
                        {
                            TooltipManager.Show(TooltipDomain.WORLD, TooltipKind.ITEM, dbItemCfg);
                        }
                    }
                    // ОБРАБОТКА КОНТЕНТА ЮНИТА
                    else if (isUnit && EntityManager.HasComponent<UnitComponent>(foundEntity))
                    {
                        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto); 

                        var dbUnitCfg = UnitsDatabase.GetUnit(currentUnitId);
                        if (dbUnitCfg != null)
                        {
                            TooltipManager.Show(TooltipDomain.WORLD, TooltipKind.UNIT, dbUnitCfg);
                        }
                    }
                    
                    return;
                }
            }

            if (hoverState.ValueRO.HasTarget)
            {
                ResetHover(ref hoverState.ValueRW);
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

