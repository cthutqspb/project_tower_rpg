using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectTowerRpg.Core.UI
{
    public class UIInputHandler : MonoBehaviour
    {
        private InputAction _toggleCharacterAction;
        private InputAction _closeWindowAction;

        private void Start()
        {
            var actions = UnityEngine.InputSystem.InputSystem.actions;
            _toggleCharacterAction = actions.FindAction("UI/ToggleCharacterWindow");
            _closeWindowAction = actions.FindAction("UI/CloseWindow");
        }

        private void Update()
        {
            if (_toggleCharacterAction.triggered)
            {
                UIEvents.TriggerToggleCharacterWindow();
            }

            if (_closeWindowAction.triggered)
            {
                WindowManager.CloseTop();
            }
        }
    }
}
