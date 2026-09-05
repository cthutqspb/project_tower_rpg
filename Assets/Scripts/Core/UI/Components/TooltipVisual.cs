using UnityEngine;
using UnityEngine.UIElements;
using ProjectTowerRpg.Core.Items;
using ProjectTowerRpg.Core.Abilities;
using ProjectTowerRpg.Core.Units;
using ProjectTowerRpg.Core.Localization;
using Unity.Entities;
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
            _background.style.width = 320;
            _background.style.backgroundColor = new Color(0.06f, 0.06f, 0.06f, 0.95f);
            _background.style.borderTopWidth = 1;
            _background.style.borderBottomWidth = 1;
            _background.style.borderLeftWidth = 1;
            _background.style.borderRightWidth = 1;
            var borderColor = new Color(0.25f, 0.25f, 0.25f, 0.4f);
            _background.style.borderTopColor = borderColor;
            _background.style.borderBottomColor = borderColor;
            _background.style.borderLeftColor = borderColor;
            _background.style.borderRightColor = borderColor;
            _background.style.paddingTop = 10;
            _background.style.paddingBottom = 10;
            _background.style.paddingLeft = 12;
            _background.style.paddingRight = 12;
            Add(_background);
        }

        private void AddLine(string leftText, string rightText = "", Color? color = null, bool isHeader = false)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;

            var textColor = color ?? Color.white;

            var labelLeft = new Label(leftText);
            labelLeft.style.color = textColor;
            labelLeft.style.whiteSpace = WhiteSpace.Normal;
            labelLeft.style.flexGrow = 1;
            labelLeft.style.flexShrink = 1;

            if (isHeader)
            {
                row.style.justifyContent = Justify.Center;
                labelLeft.style.fontSize = 17;
                labelLeft.style.unityFontStyleAndWeight = FontStyle.Bold;
                labelLeft.style.unityTextAlign = TextAnchor.UpperCenter;
                row.style.marginBottom = 8;
            }
            else
            {
                labelLeft.style.fontSize = 13;
                row.style.marginBottom = 4;
            }

            row.Add(labelLeft);

            if (!string.IsNullOrEmpty(rightText) && !isHeader)
            {
                var labelRight = new Label(rightText);
                labelRight.style.color = textColor;
                labelRight.style.fontSize = 13;
                labelRight.style.flexGrow = 0;
                labelRight.style.flexShrink = 0;
                labelRight.style.marginLeft = 15;
                labelRight.style.unityTextAlign = TextAnchor.UpperRight;
                row.Add(labelRight);
            }

            _background.Add(row);
        }

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
            Color nameColor = SolarizedOsakaNight.GetQualityColor(cfg.identity.quality);

            if (cfg.properties.stackable && amount > 1)
            {
                nameText += $" (x{amount})";
            }
            AddLine(nameText, color: nameColor, isHeader: true);

            if (!isInInventory)
            {
                return;
            }

            string typeKey = !string.IsNullOrEmpty(cfg.properties.weapon_type) ? cfg.properties.weapon_type : cfg.properties.armor_type;
            if (string.IsNullOrEmpty(typeKey)) typeKey = cfg.identity.type;

            string localizedType = LocalizationManager.Get(typeKey);
            string localizedSlot = LocalizationManager.Get(cfg.properties.equip_slot);

            string subHeader = $"{localizedType.ToUpper()} ({localizedSlot})";
            AddLine(subHeader, color: new Color(0.5f, 0.5f, 0.5f, 1f));

            // ================================================================
            // 🔥 БЛОК ТРЕБОВАНИЙ С ПРОВЕРКОЙ
            // ================================================================
            if (cfg.requirements != null)
            {
                string reqHeader = LocalizationManager.Get("required") + ":";
                AddLine(reqHeader.ToUpper(), color: Color.white);

                // Получаем сущность игрока
                var playerEntity = PlayerUtils.GetEntityByTag<PlayerTag>();
                if (playerEntity != Entity.Null)
                {
                    var em = World.DefaultGameObjectInjectionWorld.EntityManager;
                    var result = ItemRequirementsChecker.CheckRequirements(cfg, playerEntity, em);

                    // Выводим каждое требование с цветом (красный если не выполнено)
                    if (cfg.requirements.level > 0)
                    {
                        bool isOk = cfg.requirements.level <= GetUnitLevel(playerEntity, em);
                        Color reqColor = isOk ? Color.white : new Color(1f, 0.3f, 0.3f, 1f);
                        AddLine($"  {LocalizationManager.Get("level")}: {cfg.requirements.level}", color: reqColor);
                    }

                    if (cfg.requirements.strength > 0)
                    {
                        var attrs = em.GetComponentData<UnitCurrentAttributesComponent>(playerEntity);
                        bool isOk = cfg.requirements.strength <= attrs.strength;
                        Color reqColor = isOk ? Color.white : new Color(1f, 0.3f, 0.3f, 1f);
                        AddLine($"  {LocalizationManager.Get("stat_strength")}: {cfg.requirements.strength}", color: reqColor);
                    }

                    if (cfg.requirements.agility > 0)
                    {
                        var attrs = em.GetComponentData<UnitCurrentAttributesComponent>(playerEntity);
                        bool isOk = cfg.requirements.agility <= attrs.agility;
                        Color reqColor = isOk ? Color.white : new Color(1f, 0.3f, 0.3f, 1f);
                        AddLine($"  {LocalizationManager.Get("stat_agility")}: {cfg.requirements.agility}", color: reqColor);
                    }

                    if (cfg.requirements.intellect > 0)
                    {
                        var attrs = em.GetComponentData<UnitCurrentAttributesComponent>(playerEntity);
                        bool isOk = cfg.requirements.intellect <= attrs.intellect;
                        Color reqColor = isOk ? Color.white : new Color(1f, 0.3f, 0.3f, 1f);
                        AddLine($"  {LocalizationManager.Get("stat_intellect")}: {cfg.requirements.intellect}", color: reqColor);
                    }

                    if (cfg.requirements.stamina > 0)
                    {
                        var attrs = em.GetComponentData<UnitCurrentAttributesComponent>(playerEntity);
                        bool isOk = cfg.requirements.stamina <= attrs.stamina;
                        Color reqColor = isOk ? Color.white : new Color(1f, 0.3f, 0.3f, 1f);
                        AddLine($"  {LocalizationManager.Get("stat_stamina")}: {cfg.requirements.stamina}", color: reqColor);
                    }

                    if (cfg.requirements.wisdom > 0)
                    {
                        var attrs = em.GetComponentData<UnitCurrentAttributesComponent>(playerEntity);
                        bool isOk = cfg.requirements.wisdom <= attrs.wisdom;
                        Color reqColor = isOk ? Color.white : new Color(1f, 0.3f, 0.3f, 1f);
                        AddLine($"  {LocalizationManager.Get("stat_wisdom")}: {cfg.requirements.wisdom}", color: reqColor);
                    }

                    if (!string.IsNullOrEmpty(cfg.requirements.resource))
                    {
                        var resource = em.GetComponentData<ResourceComponent>(playerEntity);
                        bool isOk = resource.Type.ToString().ToLower() == cfg.requirements.resource.ToLower();
                        Color reqColor = isOk ? Color.white : new Color(1f, 0.3f, 0.3f, 1f);
                        string resName = LocalizationManager.Get(cfg.requirements.resource);
                        AddLine($"  {LocalizationManager.Get("resource")}: {resName}", color: reqColor);
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
                    AddLine(dmgText, color: new Color(1f, 0.85f, 0.2f, 1f));
                }

                if (cfg.combat_stats.armor_rating > 0)
                {
                    string armorName = LocalizationManager.Get("armor_rating");
                    string armorText = $"{cfg.combat_stats.armor_rating} {armorName.ToUpper()}";
                    AddLine(armorText, color: new Color(0.7f, 0.8f, 1f, 1f));
                }

                if (cfg.combat_stats.attributes != null)
                {
                    var attrs = cfg.combat_stats.attributes;
                    if (attrs.strength != 0) AddLine($"{(attrs.strength > 0 ? "+" : "")}{attrs.strength} {LocalizationManager.Get("stat_strength")}", color: new Color(0.4f, 0.6f, 1f, 1f));
                    if (attrs.agility != 0) AddLine($"{(attrs.agility > 0 ? "+" : "")}{attrs.agility} {LocalizationManager.Get("stat_agility")}", color: new Color(0.4f, 0.6f, 1f, 1f));
                    if (attrs.intellect != 0) AddLine($"{(attrs.intellect > 0 ? "+" : "")}{attrs.intellect} {LocalizationManager.Get("stat_intellect")}", color: new Color(0.4f, 0.6f, 1f, 1f));
                    if (attrs.stamina != 0) AddLine($"{(attrs.stamina > 0 ? "+" : "")}{attrs.stamina} {LocalizationManager.Get("stat_stamina")}", color: new Color(0.4f, 0.6f, 1f, 1f));
                    if (attrs.wisdom != 0) AddLine($"{(attrs.wisdom > 0 ? "+" : "")}{attrs.wisdom} {LocalizationManager.Get("stat_wisdom")}", color: new Color(0.4f, 0.6f, 1f, 1f));
                }
            }

            if (!string.IsNullOrEmpty(cfg.identity.desc_key))
            {
                string localizedDesc = LocalizationManager.Get(cfg.identity.desc_key);
                AddLine(localizedDesc, color: new Color(0.8f, 0.7f, 0.5f, 1f));
            }

            string weightName = LocalizationManager.Get("weight");
            string priceName = LocalizationManager.Get("price");

            string weightLabel = $"{weightName}: {cfg.identity.weight:F1}";
            string priceLabel = $"{priceName}: {cfg.identity.price}";
            AddLine(weightLabel, priceLabel, color: new Color(0.5f, 0.5f, 0.5f, 1f));
        }

        private void RenderAbility(AbilityConfig cfg) {
            string nameText = LocalizationManager.Get(cfg.identity.name_key).ToUpper();
            AddLine(nameText, color: Color.white, isHeader: true);
        }

        private void RenderUnit(UnitConfig cfg)
        {
            string nameText = LocalizationManager.Get(cfg.identity.name_key).ToUpper();
            AddLine(nameText, color: Color.white, isHeader: true);
        }

        private int GetUnitLevel(Entity unitEntity, EntityManager em)
        {
            return em.HasComponent<UnitComponent>(unitEntity)
                ? em.GetComponentData<UnitComponent>(unitEntity).Level
                : 1;
        }
    }
}
