using UnityEngine.UIElements;

namespace ProjectTowerRpg.Core.UI
{
    public class WindowContext
    {
        public VisualElement Root { get; private set; }
        public System.Action OnClose { get; private set; }
        public System.Action OnShow { get; private set; }
        public bool IsVisible => Root != null && Root.style.display == DisplayStyle.Flex;

        public WindowContext(VisualElement root, System.Action onClose = null, System.Action onShow = null)
        {
            Root = root;
            OnClose = onClose;
            OnShow = onShow;
        }

        public void Show()
        {
            if (Root == null) return;
            Root.style.display = DisplayStyle.Flex;
            WindowManager.Push(this);
            OnShow?.Invoke();
        }

        public void Close()
        {
            if (Root == null) return;
            Root.style.display = DisplayStyle.None;
            WindowManager.Pop(this);
            OnClose?.Invoke();
        }

        public void Toggle()
        {
            if (IsVisible)
                Close();
            else
                Show();
        }
    }
}
