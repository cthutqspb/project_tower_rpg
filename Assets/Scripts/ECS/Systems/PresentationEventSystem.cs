using Unity.Entities;
using UnityEngine;
using TMPro;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core;
using ProjectTowerRpg.Core.Colors;
using ProjectTowerRpg.Core.Items;
using ProjectTowerRpg.Core.Presentation;
using ProjectTowerRpg.Core.Abilities;
using ProjectTowerRpg.Core.Auras;
using ProjectTowerRpg.Core.Shared;
using ProjectTowerRpg.Core.Localization;

namespace ProjectTowerRpg.ECS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class PresentationEventSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var em = EntityManager;

            if (!SystemAPI.TryGetSingletonEntity<PresentationEventBufferTag>(out var bufferEntity)) return;

            var eventBuffer = em.GetBuffer<PresentationEvent>(bufferEntity);
            if (eventBuffer.Length == 0) return;

            for (int i = 0; i < eventBuffer.Length; i++)
            {
                var ev = eventBuffer[i];

                // 🎯 ОБРАБОТКА СОБЫТИЙ БЕЗ ЮНИТА (ЛОГИ, ИНВЕНТАРЬ)
                switch (ev.Kind)
                {
                    case PresentationEventKind.ItemLooted:
                        HandleItemLooted(ev);
                        continue;

                    case PresentationEventKind.ItemDropped:
                        HandleItemDropped(ev);
                        continue;
                }

                // 🎯 ОБРАБОТКА СОБЫТИЙ АУР (Таргет — это юнит, на которого наложили ауру)
                switch (ev.Kind)
                {
                    case PresentationEventKind.AuraApplied:
                        HandleAuraApplied(ev);
                        continue;

                    case PresentationEventKind.AuraEnd:
                        HandleAuraEnd(ev);
                        continue;

                    case PresentationEventKind.AuraRemoved:
                        HandleAuraRemoved(ev);
                        continue;
                }
                
                // 🎯 ОБРАБОТКА СОБЫТИЙ БОЯ (Сорс — это юнит, который делает действие)
                var unitView = EntityViewRegistry.Get<UnitView>(ev.Source);
                if (unitView == null) continue;

                var animator = unitView.GetComponent<Animator>();
                if (animator == null) continue;

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
                        HandleDeath(in ev, unitView, animator);
                        break;
                }
            }

            eventBuffer.Clear();
        }

        // =========================================================================
        // 🔮 ОБРАБОТКА АУР (Иконки и VFX)
        // =========================================================================
        private void HandleAuraApplied(PresentationEvent ev)
        {
            Entity unitEntity = ev.Target; 
            string auraIdStr = ev.Param.ToString();

            var unitView = EntityViewRegistry.Get<UnitView>(unitEntity);
            if (unitView == null) return;

            var auraCfg = AurasDatabase.GetAura(auraIdStr);
            if (auraCfg == null) return;

            // 🎯 1. ПРОВЕРКА НА VFX (щиты, эллипсы)
            if (!string.IsNullOrEmpty(auraCfg.visuals.vfx_type))
            {
                HandleAuraBodyVfx(unitView, auraCfg.visuals.vfx_type, auraIdStr);
                return; // Если это VFX, то иконку не спавним
            }

            // 🎯 2. ИНАЧЕ — СПАВНИМ ИКОНКУ (твой старый код)
            GameObject targetGo = unitView.gameObject;
            string targetIconChar = auraCfg.visuals.icon_char;
            string magicElement = auraCfg.identity?.element ?? "default";
            var colors = ColorUtils.GetColorByElement(magicElement);

            var activeRunes = unitView.GetComponentsInChildren<AuraVfxLifetime>();
            for (int i = 0; i < activeRunes.Length; i++)
            {
                var activeRune = activeRunes[i];
                var activeTmp = activeRune.GetComponent<TextMeshPro>(); 
                
                if (activeTmp != null)
                {
                    if (activeTmp.text == targetIconChar)
                    {
                        activeRune.Initialize(activeTmp, colors.FaceColor, colors.GlowColor, 2.5f);
                        Debug.Log($"🔄 [Presentation]: Аура '{auraIdStr}' ({magicElement}) продлена. Префаб сохранен.");
                        return;
                    }
                    else
                    {
                        Object.Destroy(activeRune.gameObject);
                        Debug.Log($"🧹 [Presentation]: Старый 3D-глиф '{activeTmp.text}' принудительно выжжен.");
                    }
                }
            }

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

        // =========================================================================
        // 🛡️ СПАВН ЭЛЛИПСА (Body VFX)
        // =========================================================================
        private void HandleAuraBodyVfx(UnitView unitView, string vfxType, string auraId)
        {
            // 1. Проверяем, нет ли уже такого VFX (чтобы не дублировать при повторном касте)
            var existingVfx = unitView.GetComponentsInChildren<AuraBodyVfx>();
            foreach (var vfx in existingVfx)
            {
                if (vfx.VfxType == vfxType)
                {
                    vfx.RefreshLifetime();
                    return;
                }
            }

            // 2. Определяем путь к префабу
            string prefabPath = vfxType switch
            {
                "magic_shield" => "Shared/MagicShield",
                _ => null
            };

            if (string.IsNullOrEmpty(prefabPath)) return;

            // 3. Загружаем и спавним
            var prefab = Resources.Load<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[Presentation] Не найден префаб VFX: {prefabPath}");
                return;
            }

            GameObject vfxObj = Object.Instantiate(prefab);
            
            // Привязываем к персонажу (в центр тела)
            vfxObj.transform.SetParent(unitView.transform, false);
            vfxObj.transform.localPosition = new Vector3(0f, 1.0f, 0f);
            vfxObj.transform.localScale = new Vector3(1.2f, 1.5f, 1.2f); // Вытянутый эллипс

            var marker = vfxObj.AddComponent<AuraBodyVfx>();
            marker.Setup(auraId, vfxType, new Vector3(1.2f, 1.5f, 1.2f));

            Debug.Log($"🛡️ [Presentation]: Спавн VFX '{vfxType}' для ауры '{auraId}'");
        }

        // =========================================================================
        // 🧹 УДАЛЕНИЕ VFX (AuraEnd — просто исчезновение, AuraRemoved — "разбитие")
        // =========================================================================
        private void HandleAuraEnd(PresentationEvent ev)
        {
            Entity unitEntity = ev.Target;
            string auraIdStr = ev.Param.ToString();

            var unitView = EntityViewRegistry.Get(unitEntity);
            if (unitView == null) return;

            var vfxList = unitView.GetComponentsInChildren<AuraBodyVfx>();
            foreach (var vfx in vfxList)
            {
                if (vfx.AuraId == auraIdStr)
                {
                    vfx.PlayDeathAnimation();
                    Debug.Log($"🧹 [Presentation]: VFX '{vfx.VfxType}' истек (аура '{auraIdStr}' закончилась).");
                }
            }
        }

        private void HandleAuraRemoved(PresentationEvent ev)
        {
            Entity unitEntity = ev.Target;
            string auraIdStr = ev.Param.ToString();

            var unitView = EntityViewRegistry.Get(unitEntity);
            if (unitView == null) return;

            var vfxList = unitView.GetComponentsInChildren<AuraBodyVfx>();
            foreach (var vfx in vfxList)
            {
                if (vfx.AuraId == auraIdStr)
{
    // TODO: Здесь можно спавнить префаб взрыва (разбития) щита
    vfx.PlayDeathAnimation(); // 🎯 Вместо Object.Destroy
    Debug.Log($"💥 [Presentation]: VFX '{vfx.VfxType}' принудительно снят (аура '{auraIdStr}' разбита).");
}
            }
        }
        
        private void HandleItemLooted(in PresentationEvent ev)
        {
            string lootedId = ev.Param.ToString();
            var lootedCfg = ItemsDatabase.GetItem(lootedId);
            
            if (lootedCfg == null) return;
            
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
            
            if (lootedCfg == null) return;

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

            if (castStartCfg?.identity == null || castStartCfg.parameters == null) return;

            var tags = castStartCfg.identity.tags;
            bool isMelee = tags != null && tags.Contains("melee_attack");

            if (isMelee)
            {
                LogBroadcast.Send(LogChannel.Combat, string.Format("⚔️ [Бой]: Юнит {0} совершил атаку ближнего боя.", ev.Source.Index));
            }
            else
            {
                string logActionText = castStartCfg.parameters.cast_time <= 0f ? "применил" : "начинает чтение";
                LogBroadcast.Send(LogChannel.Combat, string.Format("🔮 [Каст]: Юнит {0} {1} {2}", ev.Source.Index, logActionText, LocalizationManager.Get(castStartCfg.identity.name_key)));
            }

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
                if (sync != null) sync.PlayInstantCastVfx(elementColors);
            }
            else
            {
                animator.ResetTrigger("CastEnd");
                animator.SetBool("IsCasting", true);
                animator.SetTrigger("CastStart");
                if (sync != null) sync.StartCastVfx(elementColors);
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
            
            // 1. Спавн эффекта удара (impact) — как было
            var impactPrefab = Resources.Load<GameObject>($"VFX/impact_{ev.Param}");
            if (impactPrefab != null)
            {
                Object.Instantiate(impactPrefab, unitView.transform.position + new Vector3(0f, 1f, 0f), Quaternion.identity);
            }

            // 2. 🎯 ТРЯСКА ВСЕХ АУРА-ЭФФЕКТОВ (щитов), которые висят на юните
            var auraVfxList = unitView.GetComponentsInChildren<AuraBodyVfx>();
            foreach (var vfx in auraVfxList)
            {
                vfx.TriggerHitRipple();
            }
        }

        private static void HandleDeath(in PresentationEvent ev, UnitView unitView, Animator animator)
        {
            var sync = unitView.Sync;
            if (sync != null) sync.StopCastVfx();

            // 🎯 ТУШИМ ВСЕ ИСТОЧНИКИ СВЕТА
            var lights = unitView.GetComponentsInChildren<Light>(true); // true = включая выключенные
            foreach (var l in lights)
            {
                l.enabled = false;
            }

            // 🎯 ТУШИМ ВСЕ СФЕРЫ (глаза)
            var renderers = unitView.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                // Если это сфера (или имя содержит Sphere/Eye) — выключаем
                if (r.gameObject.name.Contains("Sphere") || r.gameObject.name.Contains("Eye"))
                {
                    r.enabled = false;
                }
            }

            LogBroadcast.Send(LogChannel.Combat, string.Format("💀 [Бой]: Сущность {0} официально пала в бою!", ev.Source.Index));
        }
    }
}
