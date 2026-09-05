using Unity.Entities;
using UnityEngine;

namespace ProjectTowerRpg.Core.UI
{   
    public struct IconData
    {
        public string Glyph;   // Nerd Font символ
        public Color Color;    // Цвет иконки
        public float FontSize; // Размер шрифта (опционально)

        public IconData(string glyph, Color color, float fontSize = 39f)
        {
            Glyph = glyph;
            Color = color;
            FontSize = fontSize;
        }
    }

    public class DragData
    {
        public object Source;
        public int SlotIndex;
        public string ItemId;
        public int Amount;
        public IconData Icon; //можно потом на Sprite заменить
        public Entity SourceEntity;
        public Entity TargetEntity;
    }
}
