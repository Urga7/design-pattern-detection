using JetBrains.Annotations;

namespace DesignPatternDetection.DesignPatternExamples.Behavioral;

// Observer: the abstract notification every subscriber reacts to.
[UsedImplicitly]
public abstract class Subscriber
{
    public abstract string Update(string news);
}

// Concrete observers: each reacts to the notification in its own way.
[UsedImplicitly]
public sealed class EmailSubscriber : Subscriber
{
    public override string Update(string news) => $"email: {news}";
}

[UsedImplicitly]
public sealed class SmsSubscriber : Subscriber
{
    public override string Update(string news) => $"sms: {news}";
}

// Subject: keeps the subscribers registered from outside and notifies each of them when something happens.
[UsedImplicitly]
public sealed class NewsAgency
{
    private readonly List<Subscriber> _subscribers = [];

    public void Subscribe(Subscriber subscriber) => _subscribers.Add(subscriber);

    public void Unsubscribe(Subscriber subscriber) => _subscribers.Remove(subscriber);

    public string Publish(string news) =>
        string.Join(" | ", _subscribers.Select(subscriber => subscriber.Update(news)));
}
