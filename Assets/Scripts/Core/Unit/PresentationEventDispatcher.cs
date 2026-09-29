using UnityEngine;
using Unity.Entities;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.Units;

namespace ProjectTowerRpg.Core.UI
{
    public class PresentationEventDispatcher : MonoBehaviour
    {
        private EntityManager _entityManager;
        private EntityQuery _eventBufferQuery;
        private bool _isEcsReady;

        private void Start()
        {
            // Страхуем старт: подключаемся к ECS миру симуляции
            var world = World.DefaultGameObjectInjectionWorld;
            if (world != null)
            {
                _entityManager = world.EntityManager;
                
                // Ищем сущность-синглтон, на которой висит наш буфер событий PresentationEvent
                _eventBufferQuery = _entityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<PresentationEvent>(),
                    ComponentType.ReadOnly<PresentationEventBufferTag>()
                );
                
                _isEcsReady = true;
                Debug.Log("🔌 [EventDispatcher]: Успешно подключен к шине событий ECS!");
            }
        }

        private void Update()
        {
            if (!_isEcsReady || _eventBufferQuery.IsEmpty) return;

            // Вытаскиваем синглтон-сущность буфера событий за 0 наносекунд
            var bufferEntity = _eventBufferQuery.GetSingletonEntity();
            var eventBuffer = _entityManager.GetBuffer<PresentationEvent>(bufferEntity);

            // Если за этот кадр сервер не выплюнул ни одного события — шёлково выходим
            if (eventBuffer.Length == 0) return;

            // Хладнокровно перебираем все накопившиеся за кадр события симуляции
            for (int i = 0; i < eventBuffer.Length; i++)
            {
                var ev = eventBuffer[i];

                // Ищем 3D-модель (вьюху) моба в нашей "телефонной книге" по его Entity за O(1)!
                var unitView = UnitViewRegistry.Get(ev.Source);
                if (unitView == null) continue;

                // Вытаскиваем Аниматор из найденной модели
                var animator = unitView.GetComponent<Animator>();
                if (animator == null) continue;

                // СТEЙТ-МАШИНА ОБРАБОТКИ ВИЗУАЛЬНЫХ СИГНАЛОВ:
                switch (ev.Kind)
                {
                    case PresentationEventKind.Attack:
                        // Мгновенный фронт сигнала! Больше никаких застреваний во флагах _wasAttacking!
                        animator.SetTrigger("Attack");
                        Debug.Log($"⚔️ [Visual Event]: Нажат триггер Attack для юнита {ev.Source.Index}");
                        break;

                    case PresentationEventKind.CastStart:
                        animator.SetBool("IsCasting", true);
                        animator.SetTrigger("Cast");
                        break;

                    case PresentationEventKind.CastEnd:
                        animator.SetBool("IsCasting", false);
                        break;

                    case PresentationEventKind.Hit:
                        animator.SetTrigger("Hit");
                        break;

                    case PresentationEventKind.Death:
                        // Смерть мы пока страхуем через IsDeadTag в SyncTransform, 
                        // но в будущем её можно полностью перенести сюда!
                        break;
                }
            }

            // 🦾 КАНОН ОЧИСТКИ: После того как монобех прочитал лог событий кадра,
            // мы ПОЛНОСТЬЮ очищаем буфер в ECS, чтобы события не проигрывались по второму кругу на следующем кадре!
            eventBuffer.Clear();
        }
    }
}

