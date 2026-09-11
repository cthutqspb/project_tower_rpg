using UnityEngine;
using UnityEngine.UIElements;
using Unity.Entities;
using ProjectTowerRpg.Core.Items;
using ProjectTowerRpg.Core.Abilities;
using ProjectTowerRpg.Core.Units;
using ProjectTowerRpg.Core.Localization;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.UI.Colors;

namespace ProjectTowerRpg.Core.UI.Components
{
    public class TooltipVisual : VisualElement
    {
        private VisualElement _background;
        private TooltipSessionData _payloadRef;

        public TooltipVisual()
        {
            this.name = "global-tooltip";
            this.style.position = Position.Absolute;
            this.style.display = DisplayStyle.None;
            this.pickingMode = PickingMode.Ignore;

            _background = new VisualElement();
            // 🦾 ЧИСТОТА: Все инлайн-стили фона уехали в твой USS-класс!
            _background.AddToClassList("global-tooltip-bg");
            Add(_background);
        }

        // 🦾 ВОЗВРАТ К ИСТОКАМ: Твой оригинальный рабочий метод генерации строк!
        private void AddLine(string leftText, string rightText = "", string styleClass = "text-normal", bool isHeader = false)
        {
            var row = new VisualElement();
            row.AddToClassList("tooltip-row");

            var labelLeft = new Label(leftText);
            labelLeft.style.whiteSpace = WhiteSpace.Normal;
            labelLeft.style.flexGrow = 1;
            labelLeft.style.flexShrink = 1;

            // Накатываем семантический цвет из твоей палитры
            if (!string.IsNullOrEmpty(styleClass))
            {
                labelLeft.AddToClassList(styleClass);
            }

            if (isHeader)
            {
                row.AddToClassList("tooltip-row-header");
                labelLeft.AddToClassList("tooltip-label-header");
            }
            else
            {
                labelLeft.AddToClassList("tooltip-label-normal");
            }

            row.Add(labelLeft);

            if (!string.IsNullOrEmpty(rightText) && !isHeader)
            {
                var labelRight = new Label(rightText);
                labelRight.style.flexGrow = 0;
                labelRight.style.flexShrink = 0;
                labelRight.style.marginLeft = 15;
                labelRight.style.unityTextAlign = TextAnchor.UpperRight;
                labelRight.AddToClassList("tooltip-label-normal");
                
                if (!string.IsNullOrEmpty(styleClass))
                {
                    labelRight.AddToClassList(styleClass);
                }
                
                row.Add(labelRight);
            }

            _background.Add(row);
        }

        // 🦾 ВОЗВРАТ К ИСТОКАМ: Твоя родная покадровая логика без залипаний!
        public void UpdateTick()
        {
            var payload = TooltipManager.GetCurrent();

            if (payload == null || DragManager.Instance.IsDragging)
            {
                if (this.style.display == DisplayStyle.Flex)
                {
                    _background.Clear();
                    this.style.display = DisplayStyle.None;
                    _payloadRef = null;
                }
                return;
            }

            this.style.left = TooltipManager.MouseX + 20;
            this.style.top = Screen.height - TooltipManager.MouseY + 20;

            if (payload == _payloadRef)
            {
                return;
            }

            _payloadRef = payload;
            _background.Clear();
            this.style.display = DisplayStyle.Flex;
            this.BringToFront();

            switch (payload.Domain)
            {
                case TooltipDomain.INTERFACE:
                    if (payload.Kind == TooltipKind.ITEM && payload.Info is ItemConfig guiItem)
                    {
                        RenderItem(guiItem, isInInventory: true);
                    }
                    if (payload.Kind == TooltipKind.ABILITY && payload.Info is AbilityConfig guiAbility)
                    {
                        RenderAbility(guiAbility);
                    }
                    break;

                case TooltipDomain.WORLD:
                    if (payload.Kind == TooltipKind.ITEM && payload.Info is ItemConfig worldItem)
                    {
                        RenderItem(worldItem, isInInventory: false);
                    }
                    else if (payload.Kind == TooltipKind.UNIT && payload.Info is UnitConfig worldUnit)
                    {
                        RenderUnit(worldUnit);
                    }
                    break;
            }
        }

        private void RenderItem(ItemConfig cfg, bool isInInventory)
        {
            int amount = 1;

            string nameText = LocalizationManager.Get(cfg.identity.name_key).ToUpper();
            // Накатываем семантический класс качества шмотки (text-quality-rare, text-quality-epic)
            string qualityClass = $"text-quality-{cfg.identity.quality.ToLower()}";

            if (cfg.properties.stackable && amount > 1)
            {
                nameText += $" (x{amount})";
            }
            AddLine(nameText, styleClass: qualityClass, isHeader: true);

            if (!isInInventory)
            {
                return;
            }

            string typeKey = !string.IsNullOrEmpty(cfg.properties.weapon_type) ? cfg.properties.weapon_type : cfg.properties.armor_type;
            if (string.IsNullOrEmpty(typeKey)) typeKey = cfg.identity.type;

            string localizedType = LocalizationManager.Get(typeKey);
            string localizedSlot = LocalizationManager.Get(cfg.properties.equip_slot);

            string subHeader = $"{localizedType.ToUpper()} ({localizedSlot})";
            AddLine(subHeader, styleClass: "text-muted");

            // ================================================================
            // 🔥 БЛОК ТРЕБОВАНИЙ С ПРОВЕРКОЙ
            // ================================================================
            if (cfg.requirements != null)
            {
                string reqHeader = LocalizationManager.Get("required") + ":";
                AddLine(reqHeader.ToUpper(), styleClass: "text-normal");
                
                var playerEntity = PlayerUtils.GetEntityByTag<PlayerTag>();
                if (playerEntity != Entity.Null)
                {
                    var em = World.DefaultGameObjectInjectionWorld.EntityManager;
                    var attrs = em.GetComponentData<UnitCurrentAttributesComponent>(playerEntity);
                    var resource = em.GetComponentData<ResourceComponent>(playerEntity);
                    int level = GetUnitLevel(playerEntity, em);

                    if (cfg.requirements.level > 0)
                    {
                        bool isOk = cfg.requirements.level <= GetUnitLevel(playerEntity, em);
                        string reqClass = isOk ? "text-normal" : "text-danger";
                        AddLine($"  {LocalizationManager.Get("level")}: {cfg.requirements.level}", styleClass: reqClass);
                    }

                    if (cfg.requirements.strength > 0)
                    {
                        bool isOk = cfg.requirements.strength <= attrs.strength;
                        string reqClass = isOk ? "text-normal" : "text-danger";
                        AddLine($"  {LocalizationManager.Get("stat_strength")}: {cfg.requirements.strength}", styleClass: reqClass);
                    }

                    if (cfg.requirements.agility > 0)
                    {
                        bool isOk = cfg.requirements.agility <= attrs.agility;
                        string reqClass = isOk ? "text-normal" : "text-danger";
                        AddLine($"  {LocalizationManager.Get("stat_agility")}: {cfg.requirements.agility}", styleClass: reqClass);
                    }

                    if (cfg.requirements.intellect > 0)
                    {
                        bool isOk = cfg.requirements.intellect <= attrs.intellect;
                        string reqClass = isOk ? "text-normal" : "text-danger";
                        AddLine($"  {LocalizationManager.Get("stat_intellect")}: {cfg.requirements.intellect}", styleClass: reqClass);
                    }

                    if (cfg.requirements.stamina > 0)
                    {
                        bool isOk = cfg.requirements.stamina <= attrs.stamina;
                        string reqClass = isOk ? "text-normal" : "text-danger";
                        AddLine($"  {LocalizationManager.Get("stat_stamina")}: {cfg.requirements.stamina}", styleClass: reqClass);
                    }

                    if (cfg.requirements.wisdom > 0)
                    {
                        bool isOk = cfg.requirements.wisdom <= attrs.wisdom;
                        string reqClass = isOk ? "text-normal" : "text-danger";
                        AddLine($"  {LocalizationManager.Get("stat_wisdom")}: {cfg.requirements.wisdom}", styleClass: reqClass);
                    }

                    if (!string.IsNullOrEmpty(cfg.requirements.resource))
                    {
                        bool isOk = resource.Type.ToString().ToLower() == cfg.requirements.resource.ToLower();
                        string reqClass = isOk ? "text-normal" : "text-danger";
                        string resName = LocalizationManager.Get(cfg.requirements.resource);
                        AddLine($"  {LocalizationManager.Get("resource")}: {resName}", styleClass: reqClass);
                    }
                }
            }

            // ================================================================
            // БОЕВЫЕ ХАРАКТЕРИСТИКИ
            // ================================================================
            if (cfg.combat_stats != null)
            {
                if (cfg.combat_stats.damage != null)
                {
                    string dmgTypeName = LocalizationManager.Get(cfg.combat_stats.damage.type);
                    string dmgText = $"{cfg.combat_stats.damage.min}-{cfg.combat_stats.damage.max} {dmgTypeName.ToUpper()} УРОН";
                    AddLine(dmgText, styleClass: "text-damage");
                }

                if (cfg.combat_stats.armor_rating > 0)
                {
                    string armorName = LocalizationManager.Get("armor_rating");
                    string armorText = $"{cfg.combat_stats.armor_rating} {armorName.ToUpper()}";
                    AddLine(armorText, styleClass: "text-armor");
                }

                if (cfg.combat_stats.attributes != null)
                {
                    var attrs = cfg.combat_stats.attributes;
                    if (attrs.strength != 0) AddLine($"{(attrs.strength > 0 ? "+" : "")}{attrs.strength} {LocalizationManager.Get("stat_strength")}", styleClass: "text-armor");
                    if (attrs.agility != 0) AddLine($"{(attrs.agility > 0 ? "+" : "")}{attrs.agility} {LocalizationManager.Get("stat_agility")}", styleClass: "text-armor");
                    if (attrs.intellect != 0) AddLine($"{(attrs.intellect > 0 ? "+" : "")}{attrs.intellect} {LocalizationManager.Get("stat_intellect")}", styleClass: "text-armor");
                    if (attrs.stamina != 0) AddLine($"{(attrs.stamina > 0 ? "+" : "")}{attrs.stamina} {LocalizationManager.Get("stat_stamina")}", styleClass: "text-armor");
                    if (attrs.wisdom != 0) AddLine($"{(attrs.wisdom > 0 ? "+" : "")}{attrs.wisdom} {LocalizationManager.Get("stat_wisdom")}", styleClass: "text-armor");
                }
            }

            if (!string.IsNullOrEmpty(cfg.identity.desc_key))
            {
                string localizedDesc = LocalizationManager.Get(cfg.identity.desc_key);
                AddLine(localizedDesc, styleClass: "text-normal");
            }

            string weightName = LocalizationManager.Get("weight");
            string priceName = LocalizationManager.Get("price");

            string weightLabel = $"{weightName}: {cfg.identity.weight:F1}";
            string priceLabel = $"{priceName}: {cfg.identity.price}";
            AddLine(weightLabel, priceLabel, styleClass: "text-muted");
        }

        private void RenderAbility(AbilityConfig cfg) 
        {
            string nameText = LocalizationManager.Get(cfg.identity.name_key).ToUpper();
            AddLine(nameText, styleClass: "text-normal", isHeader: true);
        }

        private void RenderUnit(UnitConfig cfg)
        {
            string nameText = LocalizationManager.Get(cfg.identity.name_key).ToUpper();
            AddLine(nameText, styleClass: "text-normal", isHeader: true);
        }

        private int GetUnitLevel(Entity unitEntity, EntityManager em)
        {
            return em.HasComponent<UnitComponent>(unitEntity)
                ? em.GetComponentData<UnitComponent>(unitEntity).Level
                : 1;
        }
    }
}



