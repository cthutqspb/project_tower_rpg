namespace ProjectTowerRpg.UI
{
    public interface IDragSource
    {
        bool CanDrag();
        DragData GetDragData();
    }
}
