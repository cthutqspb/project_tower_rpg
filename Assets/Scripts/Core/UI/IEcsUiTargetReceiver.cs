using ProjectTowerRpg.ECS.Components;

namespace ProjectTowerRpg.Core.UI
{
    /// <summary>
    /// 🎯 СТEРИЛЬНЫЙ ИНТEРФEЙС ЦEЛИ: Позволяет UIPullSystem наливать статы 
    /// выделенного скелета внутрь TargetFrame без жесткой связности слоев.
    /// </summary>
    public interface IEcsUiTargetReceiver
    {
        // Принудительно передаем Си-структуры данных по ссылке (ref) для максимального FPS
        void UpdateTargetInfo(ref HealthComponent health, ref ResourceComponent resource);
        
        // Сигнал полной очистки и скрытия рамки с экрана
        void ClearTarget();
    }
}

