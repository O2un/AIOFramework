namespace O2un.Core.Events
{
    public interface IUIEvent
    {
    }

    public interface IUIEventPublisher : IEventPublisher<IUIEvent>
    {
    }

    public interface IUIEventSubscriber : IEventSubscriber<IUIEvent>
    {
    }

    public sealed class UIEventBus : EventBus<IUIEvent>, IUIEventPublisher, IUIEventSubscriber
    {
    }
}
