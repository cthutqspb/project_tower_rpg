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
                // Исправлено: Добавлен обязательный синтаксический флаг ref
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
                var itemAuthoring = hit.collider.GetComponent<ItemAuthoring>();

                if (itemAuthoring != null && itemAuthoring.Entity != Entity.Null)
                {
                    Entity foundEntity = itemAuthoring.Entity;

                    if (hoverState.ValueRO.CurrentEntity == foundEntity)
                    {
                        hoverState.ValueRW.HitPosition = hit.point;
                        return;
                    }

                    hoverState.ValueRW.CurrentEntity = foundEntity;
                    hoverState.ValueRW.HitPosition = hit.point;

                    if (EntityManager.HasComponent<ItemComponent>(foundEntity))
                    {
                        // Нативная замена CustomCursor на встроенную систему Unity
                        // Текстуры курсоров можно будет подключить позже, пока ставим дефолт
                        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto); 
                    }
                    
                    Debug.Log($"[HoverSystem] Мышь наведена на Entity ID: {foundEntity.Index}");
                    return;
                }
            }

            if (hoverState.ValueRO.HasTarget)
            {
                // Исправлено: Добавлен обязательный синтаксический флаг ref
                ResetHover(ref hoverState.ValueRW);
            }
        }

        private void ResetHover(ref HoverState hover)
        {
            hover.CurrentEntity = Entity.Null;
            hover.HitPosition = math.float3(0, 0, 0); // Ошибка CS0103 полностью пропала
            
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            //Debug.Log("[HoverSystem] Мышь ушла в пустоту. Ховер сброшен.");
        }
    }
}

