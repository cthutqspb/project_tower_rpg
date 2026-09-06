using Unity.Entities;
using UnityEngine;
using System.Collections.Generic;


namespace ProjectTowerRpg.Core.UI
{   
    public struct IconData
    {
        public string Glyph;   // Nerd Font символ
        public Color Color;    // Цвет иконки
        public List<string> Classes;

        public IconData(string glyph, Color color, List<string> classes = null)
        {
            Glyph = glyph;
            Color = color;
            Classes = classes ?? new List<string>();
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
