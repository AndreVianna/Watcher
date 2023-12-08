using DotnetToolbox.Net.Contracts;

namespace Watcher.Daemon.Handlers;

[Handler]
public sealed class WorkstationHandler {
    [Route("COMMAND ping")]
    public static Response HandlePing() => Response.Ok("Pong!");

    [Route("COMMAND start")]
    public static Response HandleStart(Request request) {
        var name = request.GetContentOrDefault<string>();
        if (name is null) return Response.BadRequest("Invalid agent name.");
        var result = ExecuteProcess("net", $"start {name}");
        return result != "Success"
                   ? Response.InternalServerError(result)
                   : Response.Ok();
    }

    [Route("COMMAND run")]
    public static Response HandleRun(Request request) {
        var app = request.GetContentOrDefault<ApplicationDto>();
        if (app is null) return Response.BadRequest("Invalid request.");
        if (string.IsNullOrWhiteSpace(app.Name)) return Response.BadRequest("Application name is required.");
        var result = ExecuteProcess(app.Name, app.Arguments);
        return result != "Success"
                   ? Response.InternalServerError(result)
                   : Response.Ok();
    }

    [Route("COMMAND stop")]
    public static Response HandleStop(Request request) {
        var name = request.GetContentOrDefault<string>();
        if (name is null) return Response.BadRequest("Invalid agent name.");
        var result = ExecuteProcess("net", $"stop {name}");
        return result != "Success"
                   ? Response.InternalServerError(result)
                   : Response.Ok();
    }

    [Route("COMMAND kill")]
    public static Response HandleKill(Request request) {
        var id = request.GetContentOrDefault<int?>();
        var search = id is null ? null : $"/PID {id}";
        if (search is null) {
            var name = request.GetContentOrDefault<string>();
            search = name is null ? null : $"/IM {name}";
        }
        if (search is null) return Response.BadRequest("Invalid process id.");
        var result = ExecuteProcess("TaskKill", $"/F /T {search}");
        return result != "Success"
                   ? Response.InternalServerError(result)
                   : Response.Ok();
    }

    private static string ExecuteProcess(string processName, string? arguments = null) {
        var process = new Process {
            StartInfo = new ProcessStartInfo {
                Arguments = arguments ?? string.Empty,
                FileName = processName,
                CreateNoWindow = true,
            },
        };
        if (!process.Start()) return $"Failed to start process. Command line: {processName} {arguments}";
        process.WaitForExit();
        return process.ExitCode != 0
                   ? $"Process exited abnormally. Command line: {processName} {arguments}"
                   : "Success";
    }
}
