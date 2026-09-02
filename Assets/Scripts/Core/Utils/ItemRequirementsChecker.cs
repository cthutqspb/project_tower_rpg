using Unity.Entities;
using ProjectTowerRpg.ECS.Components;
using System.Collections.Generic;

namespace ProjectTowerRpg.Core.Items
{
    public struct RequirementResult
    {
        public bool IsOk;
        public string Reason;          // Ключ причины (для локализации)
        public List<string> Errors;    // Подробные ошибки (для тултипов)
    }

    public static class ItemRequirementsChecker
    {
        public static RequirementResult CheckRequirements(ItemConfig config, Entity unitEntity, EntityManager em)
        {
            var result = new RequirementResult
            {
                IsOk = true,
                Reason = "",
                Errors = new List<string>()
            };

            if (config == null || unitEntity == Entity.Null || !em.Exists(unitEntity))
            {
                result.IsOk = false;
                result.Reason = "invalid_item";
                result.Errors.Add("Предмет не найден");
                return result;
            }

            if (config.requirements == null)
                return result;

            // 🦾 СИ-СТРАХОВКА ЧАНКОВ: Вытягиваем данные безопасно, защищая рантайм от ArgumentException!
            int unitLevel = 1;
            if (em.HasComponent<UnitComponent>(unitEntity))
            {
                unitLevel = em.GetComponentData<UnitComponent>(unitEntity).Level;
            }

            // Проверяем уровень существа
            if (config.requirements.level > unitLevel)
            {
                result.IsOk = false;
                result.Reason = "low_level";
                result.Errors.Add($"Требуется уровень {config.requirements.level}, у вас {unitLevel}");
            }

            // Проверяем ресурс (если он жестко прописан в требованиях шмотки)
            if (!string.IsNullOrEmpty(config.requirements.resource))
            {
                string requiredResource = config.requirements.resource.ToLower();
                
                if (!em.HasComponent<ResourceComponent>(unitEntity))
                {
                    result.IsOk = false;
                    result.Reason = $"requires_{requiredResource}";
                    result.Errors.Add($"Требуется система ресурсов: {requiredResource}");
                }
                else
                {
                    var resource = em.GetComponentData<ResourceComponent>(unitEntity);
                    string currentResource = resource.Type.ToString().ToLower();

                    if (currentResource != requiredResource)
                    {
                        result.IsOk = false;
                        result.Reason = $"requires_{requiredResource}";
                        result.Errors.Add($"Требуется {requiredResource}");
                    }
                }
            }

            // Проверяем РПГ-атрибуты существа (если у объекта их нет — фоллбэк в 0 спасет от краша)
            var attrs = em.HasComponent<UnitCurrentAttributesComponent>(unitEntity) 
                ? em.GetComponentData<UnitCurrentAttributesComponent>(unitEntity) 
                : default;

            if (config.requirements.strength > attrs.strength)
            {
                result.IsOk = false;
                result.Reason = "low_stats";
                result.Errors.Add($"Требуется сила {config.requirements.strength}, у вас {attrs.strength}");
            }

            if (config.requirements.agility > attrs.agility)
            {
                result.IsOk = false;
                result.Reason = "low_stats";
                result.Errors.Add($"Требуется ловкость {config.requirements.agility}, у вас {attrs.agility}");
            }

            if (config.requirements.intellect > attrs.intellect)
            {
                result.IsOk = false;
                result.Reason = "low_stats";
                result.Errors.Add($"Требуется интеллект {config.requirements.intellect}, у вас {attrs.intellect}");
            }

            if (config.requirements.stamina > attrs.stamina)
            {
                result.IsOk = false;
                result.Reason = "low_stats";
                result.Errors.Add($"Требуется выносливость {config.requirements.stamina}, у вас {attrs.stamina}");
            }

            if (config.requirements.wisdom > attrs.wisdom)
            {
                result.IsOk = false;
                result.Reason = "low_stats";
                result.Errors.Add($"Требуется мудрость {config.requirements.wisdom}, у вас {attrs.wisdom}");
            }

            return result;
        }
    }
}

