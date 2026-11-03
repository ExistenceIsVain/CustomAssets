namespace Utils.Samples
{
    /// <summary>
    /// Компонент примера, у которого есть панель с кнопками в демо-сцене.
    /// DemoMenu находит все такие компоненты в сцене и рисует их панели по вкладкам.
    /// </summary>
    public interface IDemoPanel
    {
        /// <summary>Имя вкладки.</summary>
        string Title { get; }

        /// <summary>Содержимое панели: GUILayout-кнопки и подписи. Вызывается из OnGUI меню.</summary>
        void DrawPanel();
    }
}
