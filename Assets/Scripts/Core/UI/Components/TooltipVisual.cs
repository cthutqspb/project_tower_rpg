using UnityEngine;
using UnityEngine.UIElements;
using ProjectTowerRpg.Core.Items;
using System.Collections.Generic;

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
            this.pickingMode = PickingMode.Ignore; // Пропускаем мышь насквозь

            // Базовый MMO-контейнер шириной 320 пикселей из Defold
            _background = new VisualElement();
            _background.style.width = 320;
            _background.style.backgroundColor = new Color(0.06f, 0.06f, 0.06f, 0.95f);
            
            // Благородная рамка
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

        // ================================================================
        // 🧱 АНАЛОГ ТВОЕГО МЕТОДА add_line ИЗ DEFOLD
        // ================================================================
        private void AddLine(string leftText, string rightText = "", Color? color = null, bool isHeader = false)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            
            // ИСПРАВЛЕНО: Вместо JustifyContent.SpaceBetween пишем Justify.SpaceBetween
            row.style.justifyContent = Justify.SpaceBetween; 

            var textColor = color ?? Color.white;

            // ЛЕВЫЙ ТЕКСТ
            var labelLeft = new Label(leftText);
            labelLeft.style.color = textColor;
            labelLeft.style.whiteSpace = WhiteSpace.Normal; // Автоперенос длинных строк
            labelLeft.style.flexGrow = 1;
            labelLeft.style.flexShrink = 1;

            if (isHeader)
            {
                // ИСПРАВЛЕНО: Вместо JustifyContent.Center пишем Justify.Center
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

            // ПРАВЫЙ ТЕКСТ (Если передан — улетает вправо)
            if (!string.IsNullOrEmpty(rightText) && !isHeader)
            {
                var labelRight = new Label(rightText);
                labelRight.style.color = textColor;
                labelRight.style.fontSize = 13;
                labelRight.style.flexGrow = 0;
                labelRight.style.flexShrink = 0;
                labelRight.style.marginLeft = 15; // Тот самый зазор для воздуха
                labelRight.style.unityTextAlign = TextAnchor.UpperRight;
                row.Add(labelRight);
            }

            _background.Add(row);
        }

        // ================================================================
        // 🎯 ТВОЙ РОДНОЙ МЕТОД update(self, dt) ИЗ DEFOLD ОДИН В ОДИН
        // ================================================================
        public void UpdateTick()
        {
            var payload = TooltipManager.GetCurrent();

            // Если сессии нет или мы зажали и тащим шмотку — гасим визуал
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

            // ПОЗИЦИОНИРОВАНИЕ (mouse_x + 20, mouse_y - 20)
            this.style.left = TooltipManager.MouseX + 20;
            this.style.top = Screen.height - TooltipManager.MouseY + 20;

            // Если данные ховера не менялись — выходим (0 тактов процессора)
            if (payload == _payloadRef)
            {
                return;
            }

            _payloadRef = payload;
            _background.Clear(); // Полностью очищаем старые строки
            this.style.display = DisplayStyle.Flex;

            this.BringToFront();

            // Разруливаем слои ховера
            switch (payload.Domain)
            {
                case TooltipDomain.INTERFACE:
                    if (payload.Kind == TooltipKind.ITEM && payload.Info is ItemConfig guiItem)
                    {
                        RenderItem(guiItem, isInInventory: true);
                    }
                    else if (payload.Kind == TooltipKind.ABILITY)
                    {
                        // RenderAbility(payload.Info);
                    }
                    break;

                case TooltipDomain.WORLD:
                    if (payload.Kind == TooltipKind.ITEM && payload.Info is ItemConfig worldItem)
                    {
                        RenderItem(worldItem, isInInventory: false);
                    }
                    break;
            }
        }

        // ================================================================
        // ⚔️ ЗРЯЧИЙ ААА-РЕНДЕР ТВОЕЙ ЛАПШИ ХАРАКТЕРИСТИК ИЗ DEFOLD
        // ================================================================
                private void RenderItem(ItemConfig cfg, bool isInInventory)
        {
            int amount = 1; // В будущем можно прокидывать реальный стак через payload.Context

            // 1. БЛОК ШАПКИ
            // ИСПРАВЛЕНО: Читаем через твой живой локализатор!
            string nameText = ProjectTowerRpg.Core.Localization.LocalizationManager.Get(cfg.identity.name_key).ToUpper(); 
            Color nameColor = GetQualityColor(cfg.identity.quality);
            
            if (cfg.properties.stackable && amount > 1)
            {
                nameText += $" (x{amount})";
            }
            AddLine(nameText, color: nameColor, isHeader: true);

            // 🛡️ ГВАРД ДИКОГО ЛУТА: Если вещь на земле — скрываем лапшу характеристик по твоему канону
            if (!isInInventory)
            {
                return;
            }

            string typeKey = !string.IsNullOrEmpty(cfg.properties.weapon_type) ? cfg.properties.weapon_type : cfg.properties.armor_type;
            if (string.IsNullOrEmpty(typeKey)) typeKey = cfg.identity.type;
            
            // ИСПРАВЛЕНО: Локализуем тип шмотки и слот экипировки из твоих JSON-файлов
            string localizedType = ProjectTowerRpg.Core.Localization.LocalizationManager.Get(typeKey);
            string localizedSlot = ProjectTowerRpg.Core.Localization.LocalizationManager.Get(cfg.properties.equip_slot);
            
            string subHeader = $"{localizedType.ToUpper()} ({localizedSlot})";
            AddLine(subHeader, color: new Color(0.5f, 0.5f, 0.5f, 1f));

            // 2. БЛОК ТРЕБОВАНИЙ
            if (cfg.requirements != null)
            {
                // ИСПРАВЛЕНО: Локализуем заголовки и статы
                string reqHeader = ProjectTowerRpg.Core.Localization.LocalizationManager.Get("required") + ":";
                AddLine(reqHeader.ToUpper(), color: Color.white);

                if (cfg.requirements.level > 0) 
                    AddLine($"  {ProjectTowerRpg.Core.Localization.LocalizationManager.Get("level")}: {cfg.requirements.level}", color: Color.white);
                if (cfg.requirements.strength > 0) 
                    AddLine($"  {ProjectTowerRpg.Core.Localization.LocalizationManager.Get("stat_strength")}: {cfg.requirements.strength}", color: Color.white);
                if (cfg.requirements.agility > 0) 
                    AddLine($"  {ProjectTowerRpg.Core.Localization.LocalizationManager.Get("stat_agility")}: {cfg.requirements.agility}", color: Color.white);
                if (cfg.requirements.intellect > 0) 
                    AddLine($"  {ProjectTowerRpg.Core.Localization.LocalizationManager.Get("stat_intellect")}: {cfg.requirements.intellect}", color: Color.white);
            }

            // 3. БОЕВЫЕ ХАРАКТЕРИСТИКИ (Крупно и ярко)
            if (cfg.combat_stats != null)
            {
                // Урон
                if (cfg.combat_stats.damage != null)
                {
                    string dmgTypeName = ProjectTowerRpg.Core.Localization.LocalizationManager.Get(cfg.combat_stats.damage.type);
                    string dmgText = $"{cfg.combat_stats.damage.min}-{cfg.combat_stats.damage.max} {dmgTypeName.ToUpper()} УРОН";
                    AddLine(dmgText, color: new Color(1f, 0.85f, 0.2f, 1f));
                }

                // Броня
                if (cfg.combat_stats.armor_rating > 0)
                {
                    string armorName = ProjectTowerRpg.Core.Localization.LocalizationManager.Get("armor_rating");
                    string armorText = $"{cfg.combat_stats.armor_rating} {armorName.ToUpper()}";
                    AddLine(armorText, color: new Color(0.7f, 0.8f, 1f, 1f));
                }

                // Параметры статов (+3 Сила, +5 Выносливость)
                if (cfg.combat_stats.attributes != null)
                {
                    var attrs = cfg.combat_stats.attributes;
                    if (attrs.strength != 0) AddLine($"{(attrs.strength > 0 ? "+" : "")}{attrs.strength} {ProjectTowerRpg.Core.Localization.LocalizationManager.Get("stat_strength")}", color: new Color(0.4f, 0.6f, 1f, 1f));
                    if (attrs.agility != 0) AddLine($"{(attrs.agility > 0 ? "+" : "")}{attrs.agility} {ProjectTowerRpg.Core.Localization.LocalizationManager.Get("stat_agility")}", color: new Color(0.4f, 0.6f, 1f, 1f));
                    if (attrs.intellect != 0) AddLine($"{(attrs.intellect > 0 ? "+" : "")}{attrs.intellect} {ProjectTowerRpg.Core.Localization.LocalizationManager.Get("stat_intellect")}", color: new Color(0.4f, 0.6f, 1f, 1f));
                    if (attrs.stamina != 0) AddLine($"{(attrs.stamina > 0 ? "+" : "")}{attrs.stamina} {ProjectTowerRpg.Core.Localization.LocalizationManager.Get("stat_stamina")}", color: new Color(0.4f, 0.6f, 1f, 1f));
                }
            }

            // 4. БЛОК ОПИСАНИЯ (Красивый золотисто-коричневый цвет)
            if (!string.IsNullOrEmpty(cfg.identity.desc_key))
            {
                string localizedDesc = ProjectTowerRpg.Core.Localization.LocalizationManager.Get(cfg.identity.desc_key);
                AddLine(localizedDesc, color: new Color(0.8f, 0.7f, 0.5f, 1f));
            }

            // 5. ТЕХНИЧЕСКАЯ ИНФО (Вес слева, Цена справа)
            string weightName = ProjectTowerRpg.Core.Localization.LocalizationManager.Get("weight");
            string priceName = ProjectTowerRpg.Core.Localization.LocalizationManager.Get("price");
            
            string weightLabel = $"{weightName}: {cfg.identity.weight:F1}";
            string priceLabel = $"{priceName}: {cfg.identity.price}";
            AddLine(weightLabel, priceLabel, color: new Color(0.5f, 0.5f, 0.5f, 1f));
        }

        private void ClearTooltip()
        {
            // Метод очищает бэкграунд перед каждым новым рендером, 
            // так как в UI Toolkit мы просто вызываем _background.Clear();
        }

        private Color GetQualityColor(string quality)
        {
            return quality switch
            {
                "rare" => new Color(0.2f, 0.5f, 0.9f, 1f),
                "uncommon" => new Color(0.12f, 0.75f, 0.23f, 1f),
                _ => Color.white
            };
        }
    }
}

