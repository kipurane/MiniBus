using System.Collections.ObjectModel;
using System.Globalization;
using Azure.Messaging.ServiceBus;

namespace MiniBus.AzureServiceBus.TransportMessageMapping;

public static class AzureServiceBusHeaderMapper
{
    public static void ApplyHeaders(ServiceBusMessage message, IReadOnlyDictionary<string, string> headers)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(headers);

        foreach (var header in headers)
        {
            message.ApplicationProperties[header.Key] = header.Value;
        }
    }

    public static IReadOnlyDictionary<string, string> ReadHeaders(ServiceBusReceivedMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return ReadHeaders(message.ApplicationProperties);
    }

    public static IReadOnlyDictionary<string, string> ReadHeaders(IReadOnlyDictionary<string, object> applicationProperties)
    {
        ArgumentNullException.ThrowIfNull(applicationProperties);

        var headers = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var property in applicationProperties)
        {
            if (TryConvertToHeaderValue(property.Value, out var value))
            {
                headers[property.Key] = value;
            }
        }

        return new ReadOnlyDictionary<string, string>(headers);
    }

    private static bool TryConvertToHeaderValue(object? value, out string headerValue)
    {
        switch (value)
        {
            case null:
                headerValue = string.Empty;
                return true;
            case string stringValue:
                headerValue = stringValue;
                return true;
            case IFormattable formattable:
                headerValue = formattable.ToString(null, CultureInfo.InvariantCulture);
                return true;
            case bool boolValue:
                headerValue = boolValue.ToString(CultureInfo.InvariantCulture);
                return true;
            default:
                headerValue = value.ToString() ?? string.Empty;
                return true;
        }
    }
}
