using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics; // ← Добавлен для math.float3
using UnityEngine;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.Items;
using ProjectTowerRpg.Core.UI;

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

            Vector2 mousePos = UnityEngine.InputSystem.Mouse.current != null 
                ? UnityEngine.InputSystem.Mouse.current.position.ReadValue() 
                : Vector2.zero;

            Ray ray = mainCamera.ScreenPointToRay(mousePos);
            
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
                    if (itemView.Entity != Entity.Null)
                    {
                        foundEntity = itemView.Entity;
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
                        hoverState.ValueRW.HitPosition = hit.point;
                        return;
                    }

                    hoverState.ValueRW.CurrentEntity = foundEntity;
                    hoverState.ValueRW.HitPosition = hit.point;

                    // ОБРАБОТКА КОНТЕНТА ПРЕДМЕТА
                    if (isItem && EntityManager.HasComponent<ItemComponent>(foundEntity))
                    {
                        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto); 
                        
                        var itemComponentData = EntityManager.GetComponentData<ItemComponent>(foundEntity);
                        string currentItemIdStr = itemComponentData.ItemId.ToString();

                        var dbItemCfg = ProjectTowerRpg.Core.Items.ItemsDatabase.GetItem(currentItemIdStr);
                        if (dbItemCfg != null)
                        {
                            TooltipManager.Show(TooltipDomain.WORLD, TooltipKind.ITEM, dbItemCfg);
                        }
                    }
                    // ОБРАБОТКА КОНТЕНТА ЮНИТА
                    else if (isUnit && EntityManager.HasComponent<UnitComponent>(foundEntity))
                    {
                        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto); 

                        var dbUnitCfg = ProjectTowerRpg.Core.Units.UnitsDatabase.GetUnit(currentUnitId);
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
            hover.HitPosition = math.float3(0, 0, 0); // Ошибка CS0103 полностью пропала
            
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            
            // 🌟 СБРОС ТУЛТИПА: Мышь ушла с объекта — убираем плашку с экрана
            TooltipManager.HideWorldTooltips();
        }
    }
}
