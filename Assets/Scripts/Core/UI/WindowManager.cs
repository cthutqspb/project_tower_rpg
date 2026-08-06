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

            int lastIndex = _stack.Count - 1;
            var top = _stack[lastIndex];

            if (top == null)
            {
                _stack.RemoveAt(lastIndex);
                return;
            }

            // ИСПРАВЛЕНО: Сначала убираем окно из стека менеджера, чтобы top.Close() не смог удалить его повторно!
            _stack.RemoveAt(lastIndex);

            // Теперь безопасно закрываем. Если внутри .Close() вызовется WindowManager.Pop, 
            // метод Pop просто увидит, что окна в стеке уже нет, и ничего не сделает.
            top.Close();
        }

        public static WindowContext GetTop()
        {
            return _stack.Count > 0 ? _stack[_stack.Count - 1] : null;
        }

        public static bool IsAnyOpen => _stack.Count > 0;
    }
}
