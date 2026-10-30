namespace ProjectTowerRpg.Core.UI
{
    public interface IDragSource
    {
        bool CanDrag();
        DragData GetDragData();
    }
}
