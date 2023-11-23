namespace Watcher.Common.ValueObjects;

public enum StreamType { 
    Assured,   // Represents TCP-based streaming, ensuring reliability and order
    Fast,      // Represents UDP-based streaming, prioritizing speed and efficiency
}
