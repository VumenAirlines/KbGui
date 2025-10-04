using System;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using KbGui.Models;

namespace KbGui.Services;

public class AvaloniaConsole:IDisposable
{
    private Channel<string> StdInChannel { get; } = Channel.CreateUnbounded<string>();
    private Channel<string> StdOutChannel { get; } = Channel.CreateUnbounded<string>();
    private readonly CancellationTokenSource _tokenSource = new();
    private readonly Task _inputLoopTask;
  
    public ChannelWriter<string> StdIn => StdInChannel.Writer;
    public ChannelReader<string> StdOut => StdOutChannel.Reader;

    public AvaloniaConsole()
    {
        _inputLoopTask = Task.Run(() => ReadInputLoop(_tokenSource.Token));
    }


    private async Task ReadInputLoop(CancellationToken cancellationToken = default)
    {
        var buffer = new StringBuilder();
        try
        {
            await foreach (var text in StdInChannel.Reader.ReadAllAsync(cancellationToken))
            {
                buffer.Append(text);
                int newlineIndex;
                while ((newlineIndex = buffer.ToString().IndexOf('\n')) != -1)
                {
                    string line = buffer.ToString(0, newlineIndex);
                    if (line.EndsWith('\r'))
                        line = line.TrimEnd('\r');
                   
                    await StdOutChannel.Writer.WriteAsync(line, cancellationToken);
                    
                    buffer.Remove(0, newlineIndex + 1);
                }
            }
            
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (buffer.Length > 0)
                await StdOutChannel.Writer.WriteAsync(buffer.ToString(), cancellationToken);
            
            StdOutChannel.Writer.Complete();
        }
    }


    public void Dispose()
    {
        _tokenSource.Dispose();
        _inputLoopTask.Dispose();
    }
}