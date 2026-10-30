namespace ProjectTowerRpg.Core.UI
{
    public interface IDropTarget
    {
        bool CanAcceptDrop(DragData data);
    }
}
