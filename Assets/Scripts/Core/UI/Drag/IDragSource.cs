using System;
using Unity.Entities;

namespace ProjectTowerRpg.Core.UI
{
    public interface IDragSource
    {
        bool CanDrag();
        DragData GetDragData();
    }
}
