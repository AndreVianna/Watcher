# Remote Data Server Documentation

## Overview

The Remote Data Server is designed to provide a unified, flexible, and efficient way to handle network communications in applications requiring both TCP (Transmission Control Protocol) and UDP (User Datagram Protocol) capabilities, built over WebSockets. It abstracts the complexities of network programming, allowing for easy integration and management of both protocols within a single interface. This document aims to clarify the intention behind this class and explain the purpose of each method for the Technical Advisor and Project Owner.

## Intention Behind the Interface

The primary intention behind the `IRemoteDataServer` interface is to create a versatile server component that can handle diverse network communication requirements while adhering to principles like SOLID (Single Responsibility, Open-Closed, Liskov Substitution, Interface Segregation, and Dependency Inversion), DRY (Don't Repeat Yourself), YAGNI (You Aren't Gonna Need It), and SoC (Separation of Concerns). Traditionally, TCP and UDP are handled separately due to their different characteristics and use cases. However, this separation often leads to duplicated efforts and a lack of consistency in handling network operations. `IRemoteDataServer` addresses these challenges by providing a unified approach, ensuring consistency, reducing complexity, and improving maintainability.

## Key Features

- **Unified TCP and UDP Handling Over WebSockets**: Seamlessly manage both TCP and UDP communications through a single interface implemented over WebSockets.
- **Flexibility**: Cater to various network communication patterns, including fire-and-forget, request-response, and streaming.
- **Simplicity**: Simplify the client code by abstracting the underlying protocol details.
- **Streaming Support**: Support both lossless (TCP) and lossy (UDP) streaming, suitable for different data transmission scenarios.
- **Event-Driven Architecture**: Provide comprehensive event handling to monitor and respond to network activities effectively.

## Method Descriptions

### `Start(CancellationToken ct)`

- **Purpose**: Initializes the server, starting both TCP and UDP listeners based on predefined configurations (like port numbers).
- **Use Case**: Used to begin server operations, setting up necessary resources for handling incoming connections and data.

### `Stop()`

- **Purpose**: Gracefully stops the server, releasing all resources, and shutting down both TCP and UDP listeners.
- **Use Case**: Used to terminate server operations, especially when the application is closing or when network operations need to be temporarily halted.

### `Send<TData>(Endpoint remoteAddress, TData data, CancellationToken ct)`

- **Purpose**: Sends data to a specified remote address. Primarily used for UDP communications, employing a fire-and-forget approach.
- **Use Case**: Ideal for scenarios where quick, non-guaranteed data transmission is required, like broadcasting status updates or real-time metrics.

### `Request<TRequest, TResponse>(Endpoint remoteAddress, TRequest request, CancellationToken ct)`

- **Purpose**: Sends a request and waits for a response. Specifically designed for TCP's reliable communication model.
- **Use Case**: Essential for interactions that require a response, such as querying a database or requesting configuration data from a server.

### `StartStreaming(Endpoint remoteAddress, StreamType streamType, Func<CancellationToken, Task<DataBlock>> getData, CancellationToken ct)`

- **Purpose**: Begins a streaming session to a remote address, with control over the streaming type (lossless or lossy).
- **Use Case**: Suitable for continuous data transmission scenarios like video streaming (lossy/UDP) or file transfers (lossless/TCP).

### `StopStreaming()`

- **Purpose**: Ends an ongoing streaming session, ensuring resources are released and the stream is properly terminated.
- **Use Case**: Used to halt streaming activities, either when the operation completes or when it needs to be interrupted due to external factors.

## Event Descriptions

The `IRemoteDataServer` interface includes a comprehensive set of events to monitor various aspects of server operations and network activities:

- `OnServerStarted`: Triggered when the server starts.
- `OnClientConnected`: Occurs when a new TCP client connects (TCP only).
- `OnStreamingStarted`: Indicates the start of a streaming session.
- `OnDataStreamed`: Fired during a streaming session, providing streamed data blocks.
- `OnDataReceived`: Occurs when data is received, for both TCP and UDP.
- `OnDataSent`: Fired when data is sent, particularly for UDP.
- `OnRequestSent` and `OnResponseReceived`: Specific to TCP, indicating request and response activities.
- `OnStreamingStopped`: Indicates the end of a streaming session.
- `OnServerStopped`: Triggered when the server stops.

## Conclusion

The `IRemoteDataServer` interface is an innovative solution to streamline network communication in applications that require both TCP and UDP protocols, implemented over WebSockets. It offers a cohesive, flexible, and efficient way to manage various network communication patterns while maintaining simplicity and robustness in design. This unified approach is expected to enhance productivity, reduce errors, and improve overall