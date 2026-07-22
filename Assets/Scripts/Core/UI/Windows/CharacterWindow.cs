using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectTowerRpg.Core.UI.Components;

namespace ProjectTowerRpg.Core.UI.Windows
{
    // 🎒 СТEРИЛЬНЫЙ КОНТРОЛЛЕР: 0 полей стилей в C#, всё зашито в разметку через UI Builder!
    public class CharacterWindow : BaseWindow
    {
        private StaticGrid _inventoryGrid;

        protected override void OnWindowTreeRebuilt(VisualElement panelRoot)
        {
            if (panelRoot == null) return;

            // Нам больше не нужно panelRoot.styleSheets.Add() — Unity сама всё прочитала из связки в UI Builder!
            
            // Ищем контейнер для сетки сумок внутри нашего окна
            var gridContainer = panelRoot.Q<VisualElement>("grid-container");

            if (gridContainer != null)
            {
                gridContainer.Clear();

                // КОМПОНЕНТНЫЙ ПУШ: Создаем нашу универсальную слепую сетку ячеек (6 колонок на 4 ряда)
                _inventoryGrid = new StaticGrid(columns: 6, rows: 4, gridType: "inventory");
                
                // Шёлково пихаем её внутрь UXML-якоря
                gridContainer.Add(_inventoryGrid);

                // Тестовый муляж предметов из твоей JSON базы данных
                List<string> fakeEcsInventory = new List<string>
                {
                    "iron_sword", "crystal_sword", "leather_helmet", "clown_hat", "lesser_mana_potion"
                };

                _inventoryGrid.SetDataSource(fakeEcsInventory);
                
                // Включаем видимость окна на экране через базовый класс
                Show();
                
                Debug.Log("🎯 [CharacterWindow]: Окно рюкзака успешно выведено на экран по ультимативному стандарту Unity 6!");
            }
            else
            {
                Debug.LogError("🚨 [CharacterWindow]: Не удалось найти узел 'grid-container' внутри CharacterWindow.uxml!");
            }
        }
    }
}


