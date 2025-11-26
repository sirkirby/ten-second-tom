using Spectre.Console;
using Spectre.Console.Rendering;

namespace TenSecondTom.Infrastructure.Cli;

/// <summary>
/// IAnsiConsole wrapper that provides escape-cancellable input.
/// Wraps a real console but intercepts input to detect Escape key.
/// </summary>
public sealed class EscapeCancellableConsole : IAnsiConsole
{
    private readonly IAnsiConsole _inner;
    private readonly EscapeCancellableInput _input;

    public EscapeCancellableConsole()
    {
        _inner = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Detect,
            ColorSystem = ColorSystemSupport.Detect,
            Interactive = InteractionSupport.Yes,
            Out = new AnsiConsoleOutput(Console.Out),
        });
        _input = new EscapeCancellableInput();
    }

    public Profile Profile => _inner.Profile;
    public IAnsiConsoleCursor Cursor => _inner.Cursor;
    public IAnsiConsoleInput Input => _input;
    public IExclusivityMode ExclusivityMode => _inner.ExclusivityMode;
    public RenderPipeline Pipeline => _inner.Pipeline;

    public void Clear(bool home) => _inner.Clear(home);
    public void Write(IRenderable renderable) => _inner.Write(renderable);
}
