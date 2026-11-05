using UnityEngine;
using Unity.Entities;
using ProjectTowerRpg.Core.Presentation;
using ProjectTowerRpg.ECS.Components;

public class HighlightManager : MonoBehaviour
{
    private EntityQuery _hoverQuery;
    private Entity _lastHoveredEntity = Entity.Null;

    void Start()
    {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        _hoverQuery = em.CreateEntityQuery(typeof(HoverState));
    }

    void Update()
    {
        // Если HoverState еще не создан (ECS не запущен) — выходим
        if (_hoverQuery.IsEmpty) return;
        
        var hover = _hoverQuery.GetSingleton<HoverState>();

        // Если сущность под мышкой изменилась
        if (hover.CurrentEntity != _lastHoveredEntity)
        {
            // Очищаем список рендереров в Render Feature
            HighlightRendererFeature.HighlightPass.RenderersToHighlight.Clear();
            
            if (hover.CurrentEntity != Entity.Null)
            {
                // 🎯 Достаем ЛЮБОЙ компонент (MonoBehaviour) из реестра по сущности
                // Неважно, UnitView это, InteractableView или ItemView
                var viewComponent = EntityViewRegistry.Get(hover.CurrentEntity);
                
                if (viewComponent != null)
                {
                    // Берем ВСЕ рендереры (включая дочерние створки ворот, скелета и т.д.)
                    var renderers = viewComponent.GetComponentsInChildren<Renderer>();
                    HighlightRendererFeature.HighlightPass.RenderersToHighlight.AddRange(renderers);
                }
            }

            _lastHoveredEntity = hover.CurrentEntity;
        }
    }
}
