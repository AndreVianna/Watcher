namespace Watcher.Common.ValueObjects;

public delegate Task AsyncEventHandler(CancellationTokenSource cts);
public delegate Task AsyncEventHandler<in TEventArgs>(TEventArgs args, CancellationTokenSource cts);
