using Unity.Entities;
using UnityEngine;

namespace ProjectTowerRpg.Core.UI
{
    public class DragData
    {
        public object Source;
        public int SlotIndex;
        public string ItemId;
        public int Amount;
        public Sprite Icon;
        public Entity SourceEntity;
        public Entity TargetEntity;
    }
}
