using System.Collections.Generic;
using UnityEngine.UIElements;
using ProjectTowerRpg.Core.Items;

namespace ProjectTowerRpg.Core.UI.Components
{
    public class StaticGrid : VisualElement
    {
        private string _gridType;
        private List<SlotElement> _slots = new();

        // Наш изначальный, чистый конструктор!
        public StaticGrid(VisualTreeAsset slotTemplate, int columns, int rows, string gridType)
        {
            _gridType = gridType;

            style.flexDirection = FlexDirection.Row;
            style.flexWrap = Wrap.Wrap; 
            style.width = columns * 48; 

            int totalSlots = columns * rows;
            for (int i = 0; i < totalSlots; i++)
            {
                SlotElement slotInstance = new SlotElement(slotTemplate, i);
                _slots.Add(slotInstance);
                Add(slotInstance);
            }
        }

        public void RefreshGrid(List<string> itemIdsFromEcs)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (itemIdsFromEcs != null && i < itemIdsFromEcs.Count && !string.IsNullOrEmpty(itemIdsFromEcs[i]))
                {
                    var itemCfg = ItemsDatabase.GetItem(itemIdsFromEcs[i]);
                    _slots[i].DrawSlot(itemCfg, _gridType);
                }
                else
                {
                    _slots[i].ClearVisual();
                }
            }
        }
    }
}

