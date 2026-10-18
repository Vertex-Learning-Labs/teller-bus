namespace Teller.Worker;

public sealed class WorkerOptions
{
    public const string SectionName = "Worker";

    public string Lanes { get; set; } = "All";

    public int ReadPrefetch { get; set; } = 16;

    public int ReadConcurrency { get; set; } = 16;

    public int PostPrefetch { get; set; } = 4;

    public int PostConcurrency { get; set; } = 2;

    public int PostsPerSecond { get; set; } = 10;

    public bool RunsReads => Is("All") || Is("Reads");

    public bool RunsPosts => Is("All") || Is("Posts");

    public void EnsureValid()
    {
        if (!RunsReads && !RunsPosts)
            throw new InvalidOperationException("Worker:Lanes must be All, Reads, or Posts.");

        if (ReadPrefetch < 1 || ReadConcurrency < 1 || PostPrefetch < 1 || PostConcurrency < 1 || PostsPerSecond < 1)
            throw new InvalidOperationException("Worker prefetch, concurrency, and rate must be at least 1.");
    }

    private bool Is(string value) => string.Equals(Lanes, value, StringComparison.OrdinalIgnoreCase);
}
