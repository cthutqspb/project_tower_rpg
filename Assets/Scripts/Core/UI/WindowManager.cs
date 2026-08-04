using System.Collections.Generic;
using UnityEngine.UIElements;

namespace ProjectTowerRpg.Core.UI
{
    public static class WindowManager
    {
        private static List<WindowContext> _stack = new List<WindowContext>();

        public static void Push(WindowContext context)
        {
            if (context == null) return;
            if (_stack.Contains(context)) return;

            _stack.Add(context);
            context.Root?.BringToFront();
        }

        public static void Pop(WindowContext context)
        {
            if (context == null) return;
            if (!_stack.Contains(context)) return;  // ← проверить, что есть

            _stack.Remove(context);
        }

        public static void CloseTop()
        {
            if (_stack.Count == 0) return;

            // ✅ Безопасно берём последний элемент
            var top = _stack[_stack.Count - 1];
            if (top == null)
            {
                _stack.RemoveAt(_stack.Count - 1);
                return;
            }

            // ✅ Закрываем и удаляем
            top.Close();
            _stack.RemoveAt(_stack.Count - 1);
        }

        public static WindowContext GetTop()
        {
            return _stack.Count > 0 ? _stack[_stack.Count - 1] : null;
        }

        public static bool IsAnyOpen => _stack.Count > 0;
    }
}
