using Unity.Entities;
using UnityEngine;
using TMPro;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core;
using ProjectTowerRpg.Core.Colors;
using ProjectTowerRpg.Core.Items;
using ProjectTowerRpg.Core.Units;
using ProjectTowerRpg.Core.Abilities;
using ProjectTowerRpg.Core.Auras;
using ProjectTowerRpg.Core.Shared;
using ProjectTowerRpg.Core.Localization;

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

                
                switch (ev.Kind)
                {
                    case PresentationEventKind.AuraApplied:
                        HandleAuraApplied(ev);
                        continue;

                    case PresentationEventKind.ItemLooted:
                        HandleItemLooted(ev);
                        continue;

                    case PresentationEventKind.ItemDropped:
                        HandleItemDropped(ev);
                        continue;

                    default:
                        break;
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
                    case PresentationEventKind.CastStart:
                        HandleCastStart(ev, unitView, animator);
                        break;

                    case PresentationEventKind.CastEnd:
                        HandleCastEnd(ev, unitView, animator);
                        break;                  

                    case PresentationEventKind.Hit:
                        HandleHit(ev, unitView, animator);
                        break;

                    case PresentationEventKind.Death:
                    {
                        HandleDeath(in ev, unitView, animator);
                        break;
                    }
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
            string magicElement = auraCfg.identity?.element ?? "default";
            var colors = ColorUtils.GetColorByElement(magicElement);

            // =========================================================================
            // 🦾 ЗРЯЧИЙ СИ-КОНТРОЛЬ КЛИЕНТА: Сканируем все живые руны над головой моба
            // =========================================================================
            var activeRunes = unitView.GetComponentsInChildren<AuraVfxLifetime>();
            for (int i = 0; i < activeRunes.Length; i++)
            {
                var activeRune = activeRunes[i];
                var activeTmp = activeRune.GetComponent<TextMeshPro>(); 
                
                if (activeTmp != null)
                {
                    // Сценарий А: Это ТОТ ЖЕ САМЫЙ дебафф (например, повторный Фаербол в ignite)
                    if (activeTmp.text == targetIconChar)
                    {
                        // Просто сочно продлеваем жизнь существующему глифу в 4К-атласе!
                        activeRune.Initialize(activeTmp, colors.FaceColor, colors.GlowColor, 2.5f);
                        
                        Debug.Log($"🔄 [Presentation]: Аура '{auraIdStr}' ({magicElement}) продлена. Префаб сохранен.");
                        return; // Выходим, новый префаб не спавним!
                    }
                    else
                    {
                        // 🦾 Сценарий Б: ММО-ПРИОРИТЕТ! Прилетела СОВЕРШЕННО НОВАЯ аура (например, Хит от Фростбола)
                        // Мы ХЛАДНОКРОВНО и намертво гасим предыдущий глиф из 3D-мира прямо сейчас,
                        // полностью исключая наложение, мерцание и неоновую кашу в воздухе!
                        Object.Destroy(activeRune.gameObject);
                        
                        Debug.Log($"🧹 [Presentation]: Старый 3D-глиф '{activeTmp.text}' принудительно выжжен. Освобождаем место под '{auraIdStr}'.");
                    }
                }
            }

            // =========================================================================
            // 🔮 СПАВН НОВОЙ РУНЫ (Сработает, если на цели ничего не было, или мы только что стерли чужой глиф!)
            // =========================================================================
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

                var auraLife = auraObj.GetComponent<AuraVfxLifetime>();
                if (auraLife != null)
                {
                   auraLife.Initialize(tmpText, colors.FaceColor, colors.GlowColor, 2.5f);
                }
                
                tmpText.ForceMeshUpdate();
            }

            Debug.Log($"✨ [Presentation]: Впервые заспавнен 3D-глиф '{auraIdStr}' ({magicElement}) над юнитом {unitEntity.Index}.");
        }
        
        private void HandleItemLooted(in PresentationEvent ev)
        {
            string lootedId = ev.Param.ToString();
            var lootedCfg = ItemsDatabase.GetItem(lootedId);
            
            if (lootedCfg == null)
                return;
            
            var qualityColor = SolarizedOsakaNight.GetQualityColor(lootedCfg.identity?.quality);
            string hexColor = "#" + UnityEngine.ColorUtility.ToHtmlStringRGB(qualityColor);

            LogBroadcast.Send(
                LogChannel.World, 
                string.Format(
                    "Вы получили добычу: <color={0}>[{1}]</color>.",
                    hexColor,
                    LocalizationManager.Get(lootedCfg.identity?.name_key)
                )
            );            
        }

        private void HandleItemDropped(in PresentationEvent ev)
        {
            string droppedId = ev.Param.ToString();
            var lootedCfg = ItemsDatabase.GetItem(droppedId);
            
            if (lootedCfg == null)
                return;

            var qualityColor = SolarizedOsakaNight.GetQualityColor(lootedCfg.identity?.quality);
            string hexColor = "#" + UnityEngine.ColorUtility.ToHtmlStringRGB(qualityColor);

            LogBroadcast.Send(
                LogChannel.World, 
                string.Format(
                    "Предмет: <color={0}>[{1}]</color>. выброшен",
                    hexColor,
                    LocalizationManager.Get(lootedCfg.identity?.name_key)
                )
            );       
        }

        private static void HandleCastStart(in PresentationEvent ev, UnitView unitView, Animator animator)
        {
            string castStartId = ev.Param.ToString();
            var castStartCfg = AbilitiesDatabase.GetAbility(castStartId);

            if (castStartCfg?.identity == null || castStartCfg.parameters == null)
                return;

            var tags = castStartCfg.identity.tags;
            bool isMelee = tags != null && tags.Contains("melee_attack");

            // — Лог —
            if (isMelee)
            {
                LogBroadcast.Send(
                    LogChannel.Combat,
                    string.Format(
                        "⚔️ [Бой]: Юнит {0} совершил атаку ближнего боя.",
                        ev.Source.Index
                    )
                );
            }
            else
            {
                string logActionText = castStartCfg.parameters.cast_time <= 0f
                    ? "применил"
                    : "начинает чтение";
                
                LogBroadcast.Send(
                    LogChannel.Combat,
                    string.Format(
                        "🔮 [Каст]: Юнит {0} {1} {2}",
                        ev.Source.Index,
                        logActionText,
                        LocalizationManager.Get(castStartCfg.identity.name_key)
                    )
                );
            }

            // — Визуал —
            if (isMelee)
            {
                animator.SetTrigger("Attack");
                return;
            }
            string element = castStartCfg.identity.element ?? "default";
            var elementColors = ColorUtils.GetColorByElement(element);
            var sync = unitView.Sync;

            if (castStartCfg.parameters.cast_time <= 0f)
            {
                animator.SetTrigger("CastInstant");

                if (sync != null)
                    sync.PlayInstantCastVfx(elementColors);
            }
            else
            {
                animator.ResetTrigger("CastEnd");
                animator.SetBool("IsCasting", true);
                animator.SetTrigger("CastStart");

                if (sync != null)
                    sync.StartCastVfx(elementColors);
            }            
        }

         private static void HandleCastEnd(in PresentationEvent ev, UnitView unitView, Animator animator)
        {
            animator.ResetTrigger("CastStart"); 
            animator.SetBool("IsCasting", false);
            animator.SetTrigger("CastEnd");

            string castEndId = ev.Param.ToString();
            var castEndCfg = AbilitiesDatabase.GetAbility(castEndId);

            if (castEndCfg != null && castEndCfg.parameters != null)
            {
                var sync = unitView.Sync;

                if (sync != null)
                {
                    // 🦾 ВСЕЯДНЫЙ СИ-РАЗДЕЛИТЕЛЬ РАНТАЙМА:
                    // Если это завершение ДОЛГОГО каста — мы просто тушим постоянное свечение рук.
                    // А если это МГНОВЕННЫЙ каст (cast_time <= 0), то постоянное свечение и так не горело,
                    // и мы сочно бахаем мгновенный Burst-взрыв искр прямо в момент взмаха рук!
                    if (castEndCfg.parameters.cast_time <= 0f)
                    {
                        string element = castEndCfg.identity?.element ?? "default";
                        var elementColors = ColorUtils.GetColorByElement(element);
                        sync.PlayInstantCastVfx(elementColors);
                    }
                    else
                    {
                        sync.StopCastVfx();
                    }
                }
            }
        }

        private static void HandleHit(in PresentationEvent ev, UnitView unitView, Animator animator)
        {
            animator.SetTrigger("Hit");
                        
            // 🚀 ДОБАВИЛИ ДЛЯ ТЕСТА: Спавним красивый префаб взрыва льда/удара прямо в ECS-системе!
            var impactPrefab = Resources.Load<GameObject>($"VFX/impact_{ev.Param}");
            if (impactPrefab != null)
            {
                Object.Instantiate(
                    impactPrefab,
                    unitView.transform.position + new Vector3(0f, 1f, 0f),
                    Quaternion.identity
                );
            }

        }

        private static void HandleDeath(in PresentationEvent ev, UnitView unitView, Animator animator)
        {
            // 🦾 ТУШИМ ЭФФЕКТЫ: Через кэш unitView.Sync мгновенно гасим боевой неон в ладонях
            var sync = unitView.Sync;
            if (sync != null)
            {
                sync.StopCastVfx();
            }

            // 🦾 ММО БОЕВОЙ ЛОГ: Снайперски выстреливаем финальным Rich Text аккордом в чат
            LogBroadcast.Send(
                LogChannel.Combat,
                string.Format(
                    "💀 [Бой]: Сущность {0} официально пала в бою!",
                    ev.Source.Index
                )
            );

            // 🚀 Сюда завтра шёлково встанет: 
            // GameObject.Instantiate(bloodPrefab, unitView.transform.position, Quaternion.identity);
            // AudioSource.PlayClipAtPoint(deathVoiceClip, unitView.transform.position);
        }

    }
}

