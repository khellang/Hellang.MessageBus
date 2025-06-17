# Usage #

## Creating the MessageBus ##
When creating a MessageBus, you need to specify a delegate to use for marshalling messages to the UI thread.
This can be done in two ways:

1. Directly using `System.Windows.Threading.Dispatcher` in WPF, Silverlight and WP7 or the `Windows.UI.Core.CoreDispatcher` in Windows Store apps.
2. Using `SynchronizationContext`

### Example ###
```csharp
// Using Dispatcher
Action<Action> uiThreadMarshaller = action => Dispatcher.Invoke(DispatcherPriority.Normal, action);

// Using SynchronizationContext (also works for WinForms)
Action<Action> uiThreadMarshaller = action => SynchronizationContext.Current.Send(action, null);

var messageBus = new MessageBus(uiThreadMarshaller);
```

## Subscribing ##
1. Mark your class with the `IHandle<T>` interface where `T` is the type of message you want to handle.
2. Call `Subscribe(object target)`

### Example ###
```csharp
public class MyClass : IHandle<CreatedMessage>
{
    public MyClass(IMessageBus messageBus)
    {
        messageBus.Subscribe(this);
    }

    public void Handle(CreatedMessage message)
    {
        // Handle the message here
    }
}
```

By default, messages are handled on the thread they are published on. This is not always desirable, i.e. if a message is published on a worker thread and you want to update some UI in the `Handle` method.
To make the MessageBus marshal the call to the UI thread, mark the `Handle` method with the `HandleOnUIThreadAttribute`.
### Example ###
```csharp
[HandleOnUIThread]
public void Handle(FileDownloadedMessage message)
{
    // Update UI
}
```
	
Messages are handled in a polymorphic fashion.
### Example ###
```csharp
public class Message { }
public class CreatedMessage : Message { }
public class CustomerCreatedMessage : CreatedMessage { }

public class MyClass : IHandle<Message>, IHandle<CreatedMessage>, IHandle<CustomerCreatedMessage>
{
    public void Handle(Message message)
    {
        // Handles Message, CreatedMessage and CustomerCreatedMessage
    }

    public void Handle(CreatedMessage message)
    {
        // Handles CreatedMessage and CustomerCreatedMessage
    }

    public void Handle(CustomerCreatedMessage message)
    {
        // Handles CustomerCreatedMessage
    }
}
```
	
## Publishing ##
To publish a message to subscribers of a given message type, either call `Publish<T>()` or `Publish<T>(T message)` where `T` is the type of message.

## Unsubscribing ##
To unsubscribe, just call `Unsubscribe(object target)`

## Sponsors

[Entity Framework Extensions](https://entityframework-extensions.net/?utm_source=khellang&utm_medium=Hellang.MessageBus) and [Dapper Plus](https://dapper-plus.net/?utm_source=khellang&utm_medium=Hellang.MessageBus) are major sponsors and proud to contribute to the development of Hellang.MessageBus.

[![Entity Framework Extensions](https://raw.githubusercontent.com/khellang/khellang/refs/heads/master/.github/entity-framework-extensions-sponsor.png)](https://entityframework-extensions.net/bulk-insert?utm_source=khellang&utm_medium=Hellang.MessageBus)

[![Dapper Plus](https://raw.githubusercontent.com/khellang/khellang/refs/heads/master/.github/dapper-plus-sponsor.png)](https://dapper-plus.net/bulk-insert?utm_source=khellang&utm_medium=Hellang.MessageBus)
