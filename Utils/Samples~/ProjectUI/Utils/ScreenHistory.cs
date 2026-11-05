using System;
using System.Collections.Generic;

namespace ProjectUI
{
    public sealed class ScreenHistory
    {
        private const int MaxHistorySize = 32;

        private readonly Stack<UIScreen> history = new();

        public int Count => history.Count;

        public void Clear()
        {
            history.Clear();
        }

        public void Add(UIScreen screen)
        {
            if (history.Count > 0 && history.Peek() == screen)
                return;

            if (history.Count >= MaxHistorySize)
                Trim();

            history.Push(screen);
        }

        public UIScreen Pop()
        {
            return history.Pop();
        }

        public UIScreen Peek()
        {
            return history.Peek();
        }

        public bool NoneOf(int count, params Type[] screenTypes)
        {
            if (history.Count < count) return false;

            int checkedCount = 0;
            foreach (var screen in history)
            {
                if (checkedCount >= count) break;
                if (Array.IndexOf(screenTypes, screen.GetType()) >= 0)
                    return false;

                checkedCount++;
            }

            return true;
        }

        public bool AnyOf(int count, params Type[] screenTypes)
        {
            if (history.Count < count) return false;

            int checkedCount = 0;
            foreach (var screen in history)
            {
                if (checkedCount >= count) break;
                if (Array.IndexOf(screenTypes, screen.GetType()) >= 0)
                    return true;

                checkedCount++;
            }

            return false;
        }

        public bool AllOf(int count, params Type[] screenTypes)
        {
            if (history.Count < count) return false;

            int checkedCount = 0;
            foreach (var screen in history)
            {
                if (checkedCount >= count) break;
                if (Array.IndexOf(screenTypes, screen.GetType()) < 0)
                    return false;

                checkedCount++;
            }

            return true;
        }

        private void Trim()
        {
            var itemsToKeep = new UIScreen[MaxHistorySize / 2];
            int count = 0;

            foreach (var screen in history)
            {
                if (count >= itemsToKeep.Length) break;
                itemsToKeep[count++] = screen;
            }

            history.Clear();

            for (int i = count - 1; i >= 0; i--)
                history.Push(itemsToKeep[i]);
        }
    }
}
