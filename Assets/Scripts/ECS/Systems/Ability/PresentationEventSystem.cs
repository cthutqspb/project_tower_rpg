using Unity.Entities;
using UnityEngine;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.Units;

namespace ProjectTowerRpg.ECS.Systems
{
    // 🦾 КАНОН ПРЕЗЕНТАЦИИ: Система тикает строго в SimulationGroup,
    // выгребает события, дергает визуал и сама же безопасно чистит память в конце кадра!
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class PresentationEventSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var em = EntityManager;

            // 1. Ищем синглтон-сущность нашего буфера презентационных событий
            if (!SystemAPI.TryGetSingletonEntity< PresentationEventBufferTag >(out var bufferEntity)) return;

            // 2. Напрямую выгребаем буфер событий из ОЗУ чанка симуляции
            var eventBuffer = em.GetBuffer< PresentationEvent >(bufferEntity);

            // Если за этот кадр сервер не выплюнул ни одного события — шёлково выходим
            if (eventBuffer.Length == 0) return;

            // 3. Хладнокровно перебираем все накопившиеся за кадр визуальные сигналы
            for (int i = 0; i < eventBuffer.Length; i++)
            {
                var ev = eventBuffer[i];

                // Ищем 3D-модель (вьюху) моба в нашей телефонной книге по его Entity за O(1)
                var unitView = UnitViewRegistry.Get(ev.Source);
                if (unitView == null) continue;

                // Вытаскиваем Аниматор из найденной модели
                var animator = unitView.GetComponent< Animator >();
                if (animator == null) continue;

                // СТEЙТ-МАШИНА ОБРАБОТКИ ВИЗУАЛЬНЫХ СИГНАЛОВ:
                switch (ev.Kind)
                {
                    case PresentationEventKind.Attack:
                        animator.SetTrigger("Attack");
                        Debug.Log($"⚔️ [PresentationSystem]: Нажат триггер Attack для юнита {ev.Source.Index}");
                        break;

                    case PresentationEventKind.CastStart:
                        animator.ResetTrigger("CastEnd"); 
                        animator.SetBool("IsCasting", true);
                        animator.SetTrigger("CastStart");
                        break;

                    case PresentationEventKind.CastEnd:
                        animator.ResetTrigger("CastStart"); 
                        animator.SetBool("IsCasting", false);
                        animator.SetTrigger("CastEnd");
                        break;

                    case PresentationEventKind.Hit:
                        animator.SetTrigger("Hit");
                        
                        // 🚀 ДОБАВИЛИ ДЛЯ ТЕСТА: Спавним красивый префаб взрыва льда/удара прямо в ECS-системе!
                        var impactPrefab = Resources.Load< GameObject >($"VFX/impact_{ev.Param}");
                        if (impactPrefab != null)
                        {
                            Object.Instantiate(impactPrefab, unitView.transform.position + new Vector3(0f, 1f, 0f), Quaternion.identity);
                        }
                        break;

                    case PresentationEventKind.Death:
                        // Смерть застрахуем тут, когда уберем старый тег
                        break;
                }
            }

            // 🦾 ЕДИНСТВЕННЫЙ ИСТИННЫЙ КАНОН ОЧИСТКИ: 
            // Буфер очищается строго в самом конце метода OnUpdate этой же системы,
            // гарантируя, что память освобождается ровно после того, как все события отработали!
            eventBuffer.Clear();
        }
    }
}

