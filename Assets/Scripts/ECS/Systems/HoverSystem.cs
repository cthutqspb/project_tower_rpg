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
                if (_mainCamera == null) return; // Если камеры до сих пор нет — выходим
            }

            var currentMouse = UnityEngine.InputSystem.Mouse.current;
            Vector2 mousePosition = currentMouse != null 
                ? currentMouse.position.ReadValue() 
                : Vector2.zero;

            Ray ray = _mainCamera.ScreenPointToRay(mousePosition);
            
            if (Physics.SphereCast(ray, 0.3f, out RaycastHit hit, 100f))
            {
                Entity foundEntity = Entity.Null;
                bool isItem = false;
                bool isUnit = false;
                string currentUnitId = string.Empty;
                
                // 🦾 СИ-ФИКС ОБЛАСТИ ВИДИМОСТИ: Вынесли ссылку в общий скоуп SphereCast!
                UnitView targetUnitView = null;

                // 📦 1. ПРОВЕРЯЕМ ПРЕДМЕТ
                var itemView = hit.collider.GetComponentInParent<ItemView>();
                if (itemView != null)
                {
                    if (itemView.entity != Entity.Null)
                    {
                        foundEntity = itemView.entity;
                        isItem = true;
                    }
                }
                // 👥 2. ПРОВЕРЯЕМ ЮНИТА
                else
                {
                    var unitView = hit.collider.GetComponentInParent<UnitView>();
                    if (unitView != null)
                    {
                        if (unitView.entity != Entity.Null)
                        {
                            foundEntity = unitView.entity;
                            currentUnitId = unitView.unitId;
                            targetUnitView = unitView; // Шёлково сохраняем ссылку для нижнего скоупа
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
                            // 🦾 СВЕРХЗВУКОВОЙ ПАСПОРТ: Достаем сырой unmanaged NameKey из ECS-чанка
                            var unitComp = EntityManager.GetComponentData<UnitComponent>(foundEntity);
                            string rawNameKey = unitComp.NameKey.ToString();

                            // Шёлково швыряем сырой ключ в context! Никаких локализаций на бэкенде!
                            TooltipManager.Show(TooltipDomain.WORLD, TooltipKind.UNIT, dbUnitCfg, context: rawNameKey);
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

