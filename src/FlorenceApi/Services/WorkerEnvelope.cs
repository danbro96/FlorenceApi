namespace FlorenceApi.Services;

internal sealed class WorkerEnvelope<TResult>
{
    public required TResult Result { get; set; }
}
