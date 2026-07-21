using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectTowerRpg.Core.UI.Components;

namespace ProjectTowerRpg.Core.UI
{
    public class MainUiController : MonoBehaviour
    {
        [Header("UXML Шаблон одной ЯЧЕЙКИ")]
        public VisualTreeAsset slotTemplate; 

        [Header("USS Стили СЛОТА")]
        public StyleSheet slotStyles; // Сюда кидаем SlotElement.uss

        [Header("USS Стили ОКНА")]
        public StyleSheet windowStyles; // Сюда кидаем CharacterWindow.uss

        private PanelRenderer _panelRenderer;
        private int _uiVersion = -1;

        void Awake()
        {
            _panelRenderer = GetComponent<PanelRenderer>();
            if (_panelRenderer != null) _panelRenderer.RegisterUIReloadCallback(OnUIReload);
        }

        void OnDestroy()
        {
            if (_panelRenderer != null) _panelRenderer.UnregisterUIReloadCallback(OnUIReload);
        }

        private void OnUIReload(PanelRenderer panelRenderer, VisualElement root, int version)
        {
            if (_uiVersion == version) return;
            _uiVersion = version;

            if (slotTemplate == null || root == null) return;

            // 🎰 ИЗОЛИРОВАННО ПУШИМ СТИЛИ В КОРЕНЬ ЭКРАНА:
            if (slotStyles != null) root.styleSheets.Add(slotStyles);
            if (windowStyles != null) root.styleSheets.Add(windowStyles);

            var gridContainer = root.Q<VisualElement>("grid-container");
            
            if (gridContainer != null)
            {
                gridContainer.Clear();

                StaticGrid inventoryGrid = new StaticGrid(slotTemplate, columns: 6, rows: 4, gridType: "inventory");
                gridContainer.Add(inventoryGrid);

                List<string> fakeEcsInventory = new List<string>
                {
                    "iron_sword", "crystal_sword", "leather_helmet", "clown_hat", "lesser_mana_potion"
                };

                inventoryGrid.RefreshGrid(fakeEcsInventory);
            }
        }
    }
}


