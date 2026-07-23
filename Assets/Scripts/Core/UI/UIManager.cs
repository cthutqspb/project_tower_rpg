using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;

namespace ProjectTowerRpg.Core.UI
{
    public class UIManager : MonoBehaviour
    {     
        private static bool _isBlocked = false;
        public static bool IsBlocked => _isBlocked;
        
        // UIManager сам управляет флагом
        private void SetBlocked(bool blocked)
        {
            _isBlocked = blocked;
        }

        private PanelRenderer _panelRenderer;
        private VisualElement _root;
        private IPanel _panel;
        
        private void Start()
        {
            _panelRenderer = GetComponent<PanelRenderer>();
            if (_panelRenderer == null)
            {
                Debug.LogError("[UIManager]: PanelRenderer не найден!");
                return;
            }
            
            _panelRenderer.RegisterUIReloadCallback(OnUIReloaded);
        }
        
        private void OnUIReloaded(PanelRenderer renderer, VisualElement root, int version)
        {
            _root = root;
            _panel = root?.panel;
            Debug.Log("[UIManager]: UI инициализирован");
        }
        
        private void Update()
        {
            if (_root == null || _panel == null) return;
            
            var mouse = Mouse.current;
            if (mouse == null) return;
            
            Vector2 mousePos = mouse.position.ReadValue();
            Vector2 localPos = new Vector2(mousePos.x, Screen.height - mousePos.y);
            
            VisualElement picked = _panel.Pick(localPos);
            bool isOverUI = picked != null && picked != _root;
            
            if (isOverUI)
            {
                if (!IsBlocked)
                {
                    Debug.Log($"[UIManager]: 🟢 Мышь НАД UI - {picked.name}");
                    SetBlocked(true);
                }
            }
            else
            {
                if (IsBlocked)
                {
                    Debug.Log("[UIManager]: 🔴 Мышь НЕ НАД UI");
                    SetBlocked(false);
                }
            }
        }

        private void OnDestroy()
        {
            if (_panelRenderer != null)
            {
                _panelRenderer.UnregisterUIReloadCallback(OnUIReloaded);
            }
            SetBlocked(false);  // <-- изменили
    }    
    }
}
