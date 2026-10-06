using Unity.Entities;
using UnityEngine;
using TMPro;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.Colors;
using ProjectTowerRpg.Core.Units;
using ProjectTowerRpg.Core.Auras;
using ProjectTowerRpg.Core.Shared;

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
            if (!SystemAPI.TryGetSingletonEntity<PresentationEventBufferTag>(out var bufferEntity)) return;

            // 2. Напрямую выгребаем буфер событий из ОЗУ чанка симуляции
            var eventBuffer = em.GetBuffer<PresentationEvent>(bufferEntity);

            // Если за этот кадр сервер не выплюнул ни одного события — шёлково выходим
            if (eventBuffer.Length == 0) return;

            // 3. Хладнокровно перебираем все накопившиеся за кадр визуальные сигналы
            for (int i = 0; i < eventBuffer.Length; i++)
            {
                var ev = eventBuffer[i];

                // 🦾 ДЕКОМПОЗИРОВАННЫЙ КЕЙС АУРЫ:
                // При наложении баффа целью является ev.Target, поэтому уводим логику в метод сразу,
                // не ломая общий цикл поиска аниматоров по ev.Source!
                if (ev.Kind == PresentationEventKind.AuraApplied)
                {
                    HandleAuraApplied(ev);
                    continue;
                }

                // Ищем 3D-модель (вьюху) моба в нашей телефонной книге по его Entity за O(1)
                var unitView = UnitViewRegistry.Get(ev.Source);
                if (unitView == null) continue;

                // Вытаскиваем Аниматор из найденной модели
                var animator = unitView.GetComponent<Animator>();
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
                        var impactPrefab = Resources.Load<GameObject>($"VFX/impact_{ev.Param}");
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

        // =========================================================================
        // 🔮 ИЗОЛИРОВАННЫЙ СИ-ШЛЮЗ УПРАВЛЕНИЯ НЕОНОВЫМИ РУНАМИ АУР
        // =========================================================================
        private void HandleAuraApplied(PresentationEvent ev)
        {
            Entity unitEntity = ev.Target; 
            string auraIdStr = ev.Param.ToString();

            var unitView = UnitViewRegistry.Get(unitEntity);
            if (unitView == null) return;

            GameObject targetGo = unitView.gameObject;
            if (targetGo == null) return;

            var auraCfg = AurasDatabase.GetAura(auraIdStr);
            if (auraCfg == null) return;
            
            string targetIconChar = auraCfg.visuals.icon_char;
            
            // 🦾 СИ-ШЛЮЗ РАСКРАСКИ: Вытаскиваем текстовую школу магии из JSON (например, "fire")
            string element = auraCfg.identity?.element ?? "default";
            
            // За долю наносекунды получаем пару LDR/HDR цветов под эту школу магии!
            var colors = ColorUtils.GetColorByElement(element);

            // ГВАРД ПОВТОРНОГО СПАВНА
            var activeRunes = unitView.GetComponentsInChildren<AuraVfxLifetime>();
            for (int i = 0; i < activeRunes.Length; i++)
            {
                var activeRune = activeRunes[i];
                var activeTmp = activeRune.GetComponent<TextMeshPro>(); 
                
                if (activeTmp != null && activeTmp.text == targetIconChar)
                {
                    // 🚀 ПРОДЛЕНИЕ ЖИЗНИ: Передаем СВЕЖИЕ, динамические цвета школы!
                    activeRune.Initialize(activeTmp, colors.FaceColor, colors.GlowColor, 2.5f);
                    
                    Debug.Log($"🔄 [Presentation]: Аура '{auraIdStr}' ({element}) продлена над головой юнита {unitEntity.Index}.");
                    return; 
                }
            }

            // СПАВН НОВОЙ РУНЫ
            var prefab = Resources.Load<GameObject>("Shared/AuraRune");
            if (prefab == null) return;

            GameObject auraObj = Object.Instantiate(prefab);
            auraObj.transform.position = targetGo.transform.position + Vector3.up * 2.2f;
            auraObj.transform.SetParent(unitView.transform, true);

            var tmpText = auraObj.GetComponent<TextMeshPro>();
            if (tmpText != null)
            {
                tmpText.text = targetIconChar;

                var mat = tmpText.fontMaterial;
                if (mat != null)
                {
                    mat.EnableKeyword("GLOW_ON");
                    
                    // 🚀 ДИНАМИЧЕСКИЙ НАКАТ ПАЛИТРЫ ШКОЛЫ В МАТЕРИАЛ:
                    mat.SetColor("_FaceColor", colors.FaceColor);
                    mat.SetColor("_GlowColor", colors.GlowColor);                     

                    mat.SetFloat("_FaceDilate", 0.0f);      
                    mat.SetFloat("_FaceSoftness", 0.0f);  
                    mat.SetFloat("_ScaleRatioB", 0.35f); 
                    mat.SetFloat("_GlowOffset", -0.05f); 
                    mat.SetFloat("_GlowInner", -0.01f);  
                    mat.SetFloat("_GlowOuter", 0.05f);   
                    mat.SetFloat("_GlowPower", 1.0f);
                }

                // Инициализируем автономный цикл жизни новой руны с правильным цветом
                var auraLife = auraObj.GetComponent<AuraVfxLifetime>();
                if (auraLife != null)
                {
                   auraLife.Initialize(tmpText, colors.FaceColor, colors.GlowColor, 2.5f);
                }
                
                tmpText.ForceMeshUpdate();
            }

            Debug.Log($"✨ [Presentation]: Впервые заспавнен 3D-глиф '{auraIdStr}' ({element}) над юнитом {unitEntity.Index}.");
        }
    }
}

