using System.IO.Ports;

namespace RfLab.Core.Mcnf;

/// <summary>
/// An MCNF byte stream over a serial port: 8N1, no handshake, DTR and RTS
/// asserted as the RF-Lab node expects.
/// </summary>
/// <remarks>
/// Opening the port resets an Arduino Mega (DTR pulses its reset line), so the
/// caller waits for the node to boot before the first exchange.
///
/// Reads are blocking reads with a short timeout on a pool thread. The port's
/// own asynchronous stream aborts a pending read when the thread that started
/// it ends ("The I/O operation has been aborted because of either a thread
/// exit or an application request"), which an await-based caller cannot avoid.
/// </remarks>
public sealed class SerialPortByteStream : IMcnfByteStream
{
    private const int ReadSliceMilliseconds = 50;

    private readonly SerialPort port;

    private SerialPortByteStream(SerialPort port)
    {
        this.port = port;
    }

    /// <summary>The serial ports present, in COM-number order.</summary>
    public static IReadOnlyList<string> PortNames() =>
        [.. SerialPort.GetPortNames()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(PortNumber)
            .ThenBy(name => name, StringComparer.OrdinalIgnoreCase)];

    public static SerialPortByteStream Open(string portName, int baudRate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(portName);

        var port = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
        {
            Handshake = Handshake.None,
            ReadTimeout = ReadSliceMilliseconds,
            WriteTimeout = 2000,
        };

        try
        {
            port.Open();
            port.DtrEnable = true;
            port.RtsEnable = true;
            port.DiscardInBuffer();
            return new SerialPortByteStream(port);
        }
        catch
        {
            port.Dispose();
            throw;
        }
    }

    /// <summary>Discards bytes the node sent before the first request (its boot output).</summary>
    public void DiscardInput() => port.DiscardInBuffer();

    public ValueTask WriteAsync(
        ReadOnlyMemory<byte> bytes,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        byte[] data = bytes.ToArray();
        port.Write(data, 0, data.Length);
        return ValueTask.CompletedTask;
    }

    public async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        var data = new byte[buffer.Length];
        int count = await Task.Run(
            () =>
            {
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        return port.Read(data, 0, data.Length);
                    }
                    catch (TimeoutException)
                    {
                        // nothing yet: look at the cancellation again
                    }
                }
            },
            cancellationToken).ConfigureAwait(false);
        data.AsMemory(0, count).CopyTo(buffer);
        return count;
    }

    public ValueTask DisposeAsync()
    {
        try
        {
            port.Close();
        }
        catch (IOException)
        {
            // A removed USB adapter cannot be closed cleanly; it is gone anyway.
        }

        port.Dispose();
        return ValueTask.CompletedTask;
    }

    private static int PortNumber(string name) =>
        name.StartsWith("COM", StringComparison.OrdinalIgnoreCase)
        && int.TryParse(name.AsSpan(3), out int number)
            ? number
            : int.MaxValue;
}
