using System;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Entities;
using ProjectTowerRpg.ECS.Components; // Убедись, что подключен юзинг для ContainerType

namespace ProjectTowerRpg.Core.UI.Components
{
    [Serializable]
    public class ActionBar
    {
        [Header("Позиционирование по умолчанию (в % от экрана 0..100)")]
        [Range(0, 100)] public float DefaultLeft = 35f;  // 35% слева
        [Range(0, 100)] public float DefaultTop = 90f;   // 🌟 90% сверху — гарантированный адаптивный низ!

        private VisualElement _panelRoot;
        private Entity _boundPanelEntity = Entity.Null;
        private StaticGrid _slotsGrid; 

        // 🦾 1.ОТКРЫВАЕМ ГЕТТЕР ДЛЯ HUDMANAGER:
        // Теперь HUDManager шёлково увидит сетку и не будет ругаться ошибкой CS1061!
        public StaticGrid SlotsGrid => _slotsGrid;

        // 🦾 2. ПРИНИМАЕМ barIndex НА ВХОД:
        // Это позволит нам называть слоты уникально и вычислять ID панели (1, 2, 3...)
        public void BuildPanel(VisualElement hudRoot, VisualTreeAsset panelUxml, int barIndex)
        {
            if (hudRoot == null || panelUxml == null) return;

            // 1. Создаем контейнер-слот для панели с уникальным именем
            var dynamicSlot = new VisualElement();
            dynamicSlot.name = $"DynamicActionBarSlot_{barIndex}";
            dynamicSlot.pickingMode = PickingMode.Ignore;
            dynamicSlot.style.position = Position.Absolute;

            // Возвращаем твою рабочую схему, которая на 100% выводит элементы на экран
            dynamicSlot.style.left = Length.Percent(DefaultLeft);
            dynamicSlot.style.top = Length.Percent(DefaultTop);

            // 2. Клонируем вёрстку рамки панели из ActionBar.uxml
            _panelRoot = panelUxml.CloneTree();
            dynamicSlot.Add(_panelRoot);

            // 3. Создаем твой StaticGrid и вшиваем в каркас
            var container = _panelRoot.Q<VisualElement>("slots-container");
            if (container != null)
            {
                // 🦾 3. ЗАРЯЖАЕМ РЕЛЬСЫ ФИЛЬТРАЦИИ БУФЕРА:
                // Вычисляем panelId (например, для первой панели i=0 будет panelId=1)
                int panelId = barIndex + 1; 
                
                // Передаем в твой новый конструктор StaticGrid тип и номер панели хоткеев!
                _slotsGrid = new StaticGrid(12, 1, ContainerType.ACTION_BAR.ToString(), panelId);
                container.Add(_slotsGrid);
                
                Debug.Log($"[ActionBar]: StaticGrid успешно развернут для экшен-бара #{panelId} в ECS-режиме.");
            }
            else
            {
                Debug.LogError("🚨 [ActionBar]: В UXML шаблоне панели не найдена нода с name='slots-container'!");
            }

            hudRoot.Add(dynamicSlot);
        }

        public void BindToEntity(Entity panelEntity)
        {
            if (_boundPanelEntity != Entity.Null) UIRegistry.Unregister(_boundPanelEntity, this);
            _boundPanelEntity = panelEntity;
            
            if (_boundPanelEntity != Entity.Null)
            {
                _slotsGrid?.BindToEntity(panelEntity);
            }
        }
    }
}

