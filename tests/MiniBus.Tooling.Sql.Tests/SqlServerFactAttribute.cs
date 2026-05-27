using System.IO.Pipes;
using System.Net.Sockets;
using Xunit;

namespace MiniBus.Tooling.Sql.Tests;

internal sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        Timeout = 180_000;

        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(SqlServerTestSettings.ConnectionStringEnvironmentVariable))
            && !DockerSocketIsReachable())
        {
            Skip = "Docker is not reachable, and MINIBUS_SQLSERVER_TEST_CONNECTION_STRING is not set. " +
                   "Start Docker Desktop with linux/amd64 container support or configure an external SQL Server/Azure SQL test connection string.";
        }
    }

    private static bool DockerSocketIsReachable()
    {
        var dockerHost = Environment.GetEnvironmentVariable("DOCKER_HOST");
        if (!string.IsNullOrWhiteSpace(dockerHost))
        {
            return DockerHostIsReachable(dockerHost);
        }

        return UnixSocketIsReachable("/var/run/docker.sock")
               || UnixSocketIsReachable(Path.Combine(
                   Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                   ".docker",
                   "run",
                   "docker.sock"))
               || NamedPipeIsReachable("npipe:////./pipe/docker_engine");
    }

    private static bool DockerHostIsReachable(string dockerHost)
    {
        if (!Uri.TryCreate(dockerHost, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.Scheme switch
        {
            "unix" => UnixSocketIsReachable(uri.LocalPath),
            "npipe" => NamedPipeIsReachable(dockerHost),
            "tcp" or "http" or "https" => TcpEndpointIsReachable(uri),
            _ => false
        };
    }

    private static bool NamedPipeIsReachable(string dockerHost)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        var pipeName = GetNamedPipeName(dockerHost);
        if (string.IsNullOrWhiteSpace(pipeName))
        {
            return false;
        }

        try
        {
            using var pipe = new NamedPipeClientStream(
                ".",
                pipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);
            pipe.Connect(250);
            return pipe.IsConnected;
        }
        catch
        {
            return false;
        }
    }

    private static string? GetNamedPipeName(string dockerHost)
    {
        var normalized = dockerHost.Replace('\\', '/');
        const string dockerDesktopPrefix = "npipe:////./pipe/";
        if (normalized.StartsWith(dockerDesktopPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return normalized[dockerDesktopPrefix.Length..];
        }

        const string alternatePrefix = "npipe://./pipe/";
        if (normalized.StartsWith(alternatePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return normalized[alternatePrefix.Length..];
        }

        return null;
    }

    private static bool UnixSocketIsReachable(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified)
            {
                Blocking = false
            };

            try
            {
                socket.Connect(new UnixDomainSocketEndPoint(path));
            }
            catch (SocketException exception) when (exception.SocketErrorCode is SocketError.WouldBlock or SocketError.InProgress)
            {
                // Expected for nonblocking connect; Poll below waits for completion.
            }

            return SocketConnectCompleted(socket);
        }
        catch
        {
            return false;
        }
    }

    private static bool TcpEndpointIsReachable(Uri uri)
    {
        if (string.IsNullOrWhiteSpace(uri.Host) || uri.Port <= 0)
        {
            return false;
        }

        try
        {
            using var socket = new Socket(SocketType.Stream, ProtocolType.Tcp)
            {
                Blocking = false
            };

            try
            {
                socket.Connect(uri.Host, uri.Port);
            }
            catch (SocketException exception) when (exception.SocketErrorCode is SocketError.WouldBlock or SocketError.InProgress)
            {
                // Expected for nonblocking connect; Poll below waits for completion.
            }

            return SocketConnectCompleted(socket);
        }
        catch
        {
            return false;
        }
    }

    private static bool SocketConnectCompleted(Socket socket)
    {
        if (!socket.Poll(250_000, SelectMode.SelectWrite))
        {
            return false;
        }

        var option = socket.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Error);
        return option is 0 or SocketError.Success;
    }
}
