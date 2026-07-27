namespace O2un.Core.Events
{
    public readonly struct OpenSceneUI<T> : IUIEvent
    {
        public T Type { get; }

        public OpenSceneUI(T type)
        {
            Type = type;
        }
    }

    public readonly struct CloseSceneUI<T> : IUIEvent
    {
        public T Type { get; }

        public CloseSceneUI(T type)
        {
            Type = type;
        }
    }
}
